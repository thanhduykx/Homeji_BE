using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.External;

// Contains no OTP or password. Supabase remains the source of truth for verification.
internal sealed class SignupVerificationChallenge
{
    public string Email { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CodeExpiresAt { get; set; }
    public DateTimeOffset NextSendAt { get; set; }
    public int Attempts { get; set; }
}

internal sealed class SignupVerificationChallengeConfiguration : IEntityTypeConfiguration<SignupVerificationChallenge>
{
    public void Configure(EntityTypeBuilder<SignupVerificationChallenge> builder)
    {
        builder.ToTable("signup_verification_challenges", "homeji");
        builder.HasKey(x => x.Email);
        builder.Property(x => x.Email).HasMaxLength(254);
        builder.HasIndex(x => x.ExpiresAt);
    }
}
