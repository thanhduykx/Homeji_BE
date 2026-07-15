using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class MarketplaceOrderConfiguration : IEntityTypeConfiguration<MarketplaceOrder>
{
    public void Configure(EntityTypeBuilder<MarketplaceOrder> builder)
    {
        builder.ToTable("marketplace_orders", "homeji");
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Status).HasConversion<int>().IsConcurrencyToken();
        builder.Property(order => order.AgreedPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(order => order.UnitPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(order => order.PlatformFeeRate).HasPrecision(5, 4).IsRequired();
        builder.Property(order => order.PlatformFeeAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(order => order.SellerNetAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(order => order.PickupAddress).HasMaxLength(MarketplaceOrder.MaxPickupAddressLength).IsRequired();
        builder.Property(order => order.Note).HasMaxLength(MarketplaceOrder.MaxNoteLength);
        builder.HasIndex(order => new { order.BuyerId, order.CreatedAt });
        builder.HasIndex(order => new { order.SellerId, order.CreatedAt });
        builder.HasIndex(order => new { order.MarketplacePostId, order.BuyerId, order.Status });
        builder.HasOne<MarketplacePost>()
            .WithMany()
            .HasForeignKey(order => order.MarketplacePostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
