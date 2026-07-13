using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class LandlordVerificationRequestConfiguration : IEntityTypeConfiguration<LandlordVerificationRequest>
{
    public void Configure(EntityTypeBuilder<LandlordVerificationRequest> builder)
    {
        builder.ToTable("landlord_verification_requests", "homeji");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.DocumentUrl).HasMaxLength(LandlordVerificationRequest.MaxDocumentUrlLength).IsRequired();
        builder.Property(request => request.ApplicantNote).HasMaxLength(LandlordVerificationRequest.MaxNoteLength);
        builder.Property(request => request.ReviewNote).HasMaxLength(LandlordVerificationRequest.MaxNoteLength);
        builder.Property(request => request.Status).HasConversion<int>();
        builder.HasIndex(request => new { request.Status, request.CreatedAt });
        builder.HasIndex(request => new { request.ApplicantId, request.CreatedAt });
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(request => request.ApplicantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
