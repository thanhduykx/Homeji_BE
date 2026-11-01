using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class AdminReportExportConfiguration : IEntityTypeConfiguration<AdminReportExport>
{
    public void Configure(EntityTypeBuilder<AdminReportExport> builder)
    {
        builder.ToTable("admin_report_exports", "homeji");
        builder.HasKey(row => row.Id);
        builder.HasIndex(row => new { row.RequestedById, row.RequestId }).IsUnique();
        builder.HasIndex(row => new { row.EmailStatus, row.NextAttemptAt });
        builder.HasIndex(row => row.CreatedAt);
        builder.Property(row => row.Recipient).HasMaxLength(254);
        builder.Property(row => row.FileName).HasMaxLength(200);
        builder.Property(row => row.EmailStatus).HasMaxLength(30);
        builder.Property(row => row.LastErrorCode).HasMaxLength(80);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(row => row.RequestedById).OnDelete(DeleteBehavior.Restrict);
    }
}
