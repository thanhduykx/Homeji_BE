using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.External;

// Codes and passwords stay out of persistent challenge state.
internal sealed class PasswordRecoveryChallenge
{
    public string Email { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset NextSendAt { get; set; }
    public int Attempts { get; set; }
}

internal sealed class PasswordRecoveryChallengeConfiguration : IEntityTypeConfiguration<PasswordRecoveryChallenge>
{
    public void Configure(EntityTypeBuilder<PasswordRecoveryChallenge> builder)
    {
        builder.ToTable("password_recovery_challenges", "homeji");
        builder.HasKey(x => x.Email);
        builder.Property(x => x.Email).HasMaxLength(254);
        builder.HasIndex(x => x.ExpiresAt);
    }
}
