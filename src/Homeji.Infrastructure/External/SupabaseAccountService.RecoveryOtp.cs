using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Accounts;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.External;

public sealed partial class SupabaseAccountService
{
    public async Task<AccountMessageDto> RequestPasswordOtpAsync(
        RequestPasswordOtpDto request, CancellationToken cancellationToken = default)
    {
        EnsureSignupOtpConfigured();
        ValidateEmail(request.Email);
        var email = request.Email.Trim().ToLowerInvariant();
        var now = _timeProvider.GetUtcNow();
        var nextSend = now.AddSeconds(60);
        var expires = now.AddMinutes(10);
        // Atomic per-account cooldown across API instances. Never create accounts for recovery.
        var claimed = await _database!.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO homeji.password_recovery_challenges ("Email", "UserId", "ExpiresAt", "NextSendAt", "Attempts")
            SELECT {email}, id, {expires}, {nextSend}, 0 FROM auth.users
            WHERE lower(email) = {email} AND email_confirmed_at IS NOT NULL
            ON CONFLICT ("Email") DO UPDATE SET "UserId" = EXCLUDED."UserId", "ExpiresAt" = EXCLUDED."ExpiresAt",
                "NextSendAt" = EXCLUDED."NextSendAt", "Attempts" = 0
            WHERE homeji.password_recovery_challenges."NextSendAt" <= {now}
            """, cancellationToken);
        if (claimed > 0)
        {
            using var generate = CreateAdminJsonRequest(HttpMethod.Post,
                new Uri("auth/v1/admin/generate_link", UriKind.Relative), new { type = "recovery", email });
            using var generated = await SendAsync(generate, cancellationToken);
            var expectedUser = await _database.Set<PasswordRecoveryChallenge>().AsNoTracking()
                .Where(x => x.Email == email).Select(x => x.UserId).SingleAsync(cancellationToken);
            if (ParseGeneratedAccountLink(generated).UserId != expectedUser)
                throw new ExternalServiceUnavailableException("Supabase Auth", "Tài khoản khôi phục không khớp.");
            var code = ReadSignupOtp(generated);
            var result = await _transactionalSender!.SendAsync(email, "Mã OTP đặt lại mật khẩu Homeji",
                HomejiEmailLayout.Otp(code, recovery: true),
                $"Mã OTP đặt lại mật khẩu Homeji: {code}. Có hiệu lực tối đa 10 phút. Không chia sẻ mã này. Nếu bạn không yêu cầu, hãy bỏ qua email.",
                Guid.NewGuid(), cancellationToken);
            if (!result.Sent)
                throw new ExternalServiceUnavailableException("Email", "Chưa gửi được mã. Vui lòng thử lại sau 60 giây.");
        }
        // Same public response for unknown/unconfirmed addresses and requests during cooldown.
        return new AccountMessageDto("Nếu email đã được xác thực, mã OTP sẽ được gửi. Vui lòng chờ 60 giây trước khi yêu cầu mã mới.");
    }

    public async Task<AccountMessageDto> ResetPasswordWithOtpAsync(
        ResetPasswordOtpDto request, CancellationToken cancellationToken = default)
    {
        EnsureSignupOtpConfigured();
        ValidateEmailAndPassword(request.Email, request.NewPassword);
        if (request.NewPassword.Length > 128)
            throw Validation("newPassword", "Mật khẩu không được quá 128 ký tự.");
        if (request.Code is null || request.Code.Length != 6 || request.Code.Any(c => c is < '0' or > '9'))
            throw Validation("code", "Mã OTP phải gồm đúng 6 chữ số.");
        var email = request.Email.Trim().ToLowerInvariant();
        var now = _timeProvider.GetUtcNow();
        var challenges = _database!.Set<PasswordRecoveryChallenge>();
        var claimed = await challenges.Where(x => x.Email == email && x.ExpiresAt > now && x.Attempts < 5)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Attempts, x => x.Attempts + 1), cancellationToken);
        if (claimed == 0)
            throw Validation("code", "Mã đã hết hạn hoặc vượt quá 5 lần thử. Vui lòng yêu cầu mã mới.");
        var expectedUser = await challenges.AsNoTracking().Where(x => x.Email == email)
            .Select(x => x.UserId).SingleAsync(cancellationToken);
        using var verify = CreateJsonRequest(HttpMethod.Post, new Uri("auth/v1/verify", UriKind.Relative),
            new { type = "recovery", email, token = request.Code });
        using var verified = await SendAsync(verify, cancellationToken);
        var session = ParseAuthSession(verified, "Mã khôi phục hợp lệ.");
        if (session.UserId != expectedUser || string.IsNullOrWhiteSpace(session.AccessToken))
            throw new ExternalServiceUnavailableException("Supabase Auth", "Phiên khôi phục không hợp lệ.");
        // The recovery session never leaves this server. An ordinary signup code cannot reset a password.
        var result = await ResetPasswordAsync(new ResetPasswordDto(session.AccessToken, request.NewPassword), cancellationToken);
        await challenges.Where(x => x.Email == email && x.UserId == expectedUser).ExecuteDeleteAsync(cancellationToken);
        return result;
    }
}
