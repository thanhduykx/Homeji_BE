using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Homeji.Infrastructure.Context.Configurations;

public sealed class PremiumPriceChangeConfiguration : IEntityTypeConfiguration<PremiumPriceChange>
{
    public void Configure(EntityTypeBuilder<PremiumPriceChange> builder)
    {
        builder.ToTable("premium_price_changes", "homeji");
        builder.HasKey(row => row.Id);
        builder.HasIndex(row => row.RequestId).IsUnique();
        builder.HasIndex(row => new { row.PackageCode, row.CreatedAt });
        builder.Property(row => row.PackageCode).HasMaxLength(50);
        builder.Property(row => row.PackageName).HasMaxLength(150);
        builder.Property(row => row.Reason).HasMaxLength(500);
        builder.Property(row => row.Price).HasPrecision(18, 0);
        builder.Property(row => row.PreviousPrice).HasPrecision(18, 0);
        builder.HasOne<UserProfile>().WithMany().HasForeignKey(row => row.AdminId).OnDelete(DeleteBehavior.Restrict);
    }
}
