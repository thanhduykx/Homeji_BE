using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("reports", "homeji");
        builder.HasKey(report => report.Id).HasName("pk_reports");
        builder.Property(report => report.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(report => report.ReporterId).HasColumnName("reporter_id").IsRequired();
        builder.Property(report => report.TargetType).HasColumnName("target_type").HasConversion<int>().IsRequired();
        builder.Property(report => report.TargetId).HasColumnName("target_id").IsRequired();
        builder.Property(report => report.Reason).HasColumnName("reason").HasMaxLength(Report.MaxReasonLength).IsRequired();
        builder.Property(report => report.Description).HasColumnName("description").HasMaxLength(Report.MaxDescriptionLength);
        builder.Property(report => report.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(report => report.ResolutionNote).HasColumnName("resolution_note").HasMaxLength(Report.MaxResolutionNoteLength);
        builder.Property(report => report.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(report => report.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(report => report.Status).HasDatabaseName("ix_reports_status");
        builder.HasIndex(report => new { report.TargetType, report.TargetId }).HasDatabaseName("ix_reports_target");
    }
}
