using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    public void Configure(EntityTypeBuilder<UserSubscription> builder)
    {
        builder.ToTable("user_subscriptions", "homeji");

        builder.HasKey(subscription => subscription.Id)
            .HasName("pk_user_subscriptions");

        builder.Property(subscription => subscription.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(subscription => subscription.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(subscription => subscription.Tier)
            .HasColumnName("tier")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(subscription => subscription.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(subscription => subscription.PackageCode)
            .HasColumnName("package_code")
            .HasMaxLength(UserSubscription.MaxPackageCodeLength)
            .IsRequired();

        builder.Property(subscription => subscription.PackageName)
            .HasColumnName("package_name")
            .HasMaxLength(UserSubscription.MaxPackageNameLength)
            .IsRequired();

        builder.Property(subscription => subscription.PaymentTransactionId)
            .HasColumnName("payment_transaction_id");

        builder.Property(subscription => subscription.StartedAt)
            .HasColumnName("started_at")
            .IsRequired();

        builder.Property(subscription => subscription.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(subscription => subscription.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(subscription => subscription.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(subscription => subscription.UserId)
            .HasDatabaseName("ix_user_subscriptions_user_id");

        builder.HasIndex(subscription => new
            {
                subscription.UserId,
                subscription.Tier,
                subscription.Status,
                subscription.StartedAt,
                subscription.ExpiresAt,
            })
            .HasDatabaseName("ix_user_subscriptions_active_lookup");

        builder.HasIndex(subscription => subscription.PaymentTransactionId)
            .IsUnique()
            .HasDatabaseName("ux_user_subscriptions_payment_transaction_id")
            .HasFilter("payment_transaction_id IS NOT NULL");
    }
}
