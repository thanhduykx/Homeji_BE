using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class MarketplaceSellerSubscriptionConfiguration : IEntityTypeConfiguration<MarketplaceSellerSubscription>
{
    public void Configure(EntityTypeBuilder<MarketplaceSellerSubscription> builder)
    {
        builder.ToTable("marketplace_seller_subscriptions", "homeji");
        builder.HasKey(subscription => subscription.Id);
        builder.Property(subscription => subscription.PackageCode).HasMaxLength(MarketplaceSellerSubscription.MaxCodeLength).IsRequired();
        builder.Property(subscription => subscription.PackageName).HasMaxLength(MarketplaceSellerSubscription.MaxNameLength).IsRequired();
        builder.Property(subscription => subscription.Price).HasPrecision(18, 2).IsRequired();
        builder.Property(subscription => subscription.CommissionRate).HasPrecision(5, 4).IsRequired();
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(subscription => subscription.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(subscription => new { subscription.UserId, subscription.ExpiresAt });
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_marketplace_seller_subscription_price", "\"Price\" > 0");
            table.HasCheckConstraint("ck_marketplace_seller_subscription_commission", "\"CommissionRate\" > 0 AND \"CommissionRate\" < 1");
        });
    }
}
