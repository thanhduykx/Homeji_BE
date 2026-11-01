using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Admin;
using Homeji.Application.IRepositories.Admin;
using Homeji.Application.IServices.Admin;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Homeji.Application.Services.Admin;

public sealed class AdminReportOptions
{
    public string Recipient { get; set; } = "duongduy12314@gmail.com";
}

public sealed class AdminReportingService(UserContext users, IAdminReportingRepository repository,
    IAdminAnalyticsService analytics, IAdminReportWorkbookWriter writer, IOptions<AdminReportOptions> options,
    TimeProvider clock) : IAdminReportingService
{
    private async Task<Guid> RequireAdmin(CancellationToken cancellationToken)
    {
        var profile = await users.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureAdmin(profile);
        return profile.Id;
    }
    private static void ValidatePage(string? keyword, int page, int pageSize)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 100 || keyword?.Length > 100)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["query"] = ["Tìm kiếm tối đa 100 ký tự; mỗi trang từ 1 đến 100 dòng."] });
    }
    public async Task<AdminPageDto<AdminRegisteredAccountDto>> GetAccountsAsync(string? keyword, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        await RequireAdmin(cancellationToken); ValidatePage(keyword, page, pageSize);
        return await repository.GetAccountsAsync(keyword?.Trim(), page, pageSize, cancellationToken);
    }
    public async Task<AdminPageDto<AdminMemberDto>> GetMembersAsync(string? keyword, string? state, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        await RequireAdmin(cancellationToken); ValidatePage(keyword, page, pageSize);
        if (state is not (null or "" or "all" or "active" or "expired" or "scheduled" or "cancelled"))
            throw new RequestValidationException(new Dictionary<string, string[]> { ["state"] = ["Trạng thái gói không hợp lệ."] });
        return await repository.GetMembersAsync(keyword?.Trim(), state, page, pageSize, cancellationToken);
    }
    public async Task<AdminReportExportDto> CreateAsync(CreateAdminReportDto request, CancellationToken cancellationToken = default)
    {
        var userId = await RequireAdmin(cancellationToken);
        if (request.RequestId == Guid.Empty || request.Days is < 7 or > 90)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["request"] = ["Chọn khoảng báo cáo từ 7 đến 90 ngày và mã yêu cầu hợp lệ."] });
        var previous = await repository.FindRequestAsync(userId, request.RequestId, cancellationToken);
        if (previous is not null)
        {
            if (previous.PeriodDays != request.Days) throw new RequestValidationException(new Dictionary<string, string[]> { ["requestId"] = ["Mã yêu cầu đã dùng cho khoảng thời gian khác."] });
            return ToDto(previous);
        }
        var product = await analytics.GetProductAnalyticsAsync(request.Days, cancellationToken);
        var accounts = await repository.GetAllAccountsAsync(cancellationToken);
        var members = await repository.GetAllMembersAsync(cancellationToken);
        var file = writer.Write(new AdminReportingSnapshot(accounts, members, product));
        var export = new AdminReportExport(userId, request.RequestId, request.Days, options.Value.Recipient, file, clock.GetUtcNow());
        return ToDto(await repository.AddAsync(export, cancellationToken));
    }
    public async Task<IReadOnlyList<AdminReportExportDto>> GetExportsAsync(CancellationToken cancellationToken = default)
    {
        await RequireAdmin(cancellationToken); return await repository.GetExportsAsync(cancellationToken);
    }
    public async Task<AdminReportFileDto> DownloadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await RequireAdmin(cancellationToken);
        return await repository.DownloadAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(AdminReportExport), id);
    }
    public static AdminReportExportDto ToDto(AdminReportExport export) => new(export.Id, export.FileName,
        export.PeriodDays, export.CreatedAt, export.Recipient, export.EmailStatus, export.EmailAcceptedAt, export.Attempts, export.LastErrorCode);
}
