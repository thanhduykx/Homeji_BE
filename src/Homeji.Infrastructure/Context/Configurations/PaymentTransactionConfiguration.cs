using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transactions", "homeji");
        builder.HasKey(payment => payment.Id).HasName("pk_payment_transactions");
        builder.Property(payment => payment.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(payment => payment.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(payment => payment.Method).HasColumnName("method").HasConversion<int>().IsRequired();
        builder.Property(payment => payment.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(payment => payment.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(payment => payment.Purpose).HasColumnName("purpose").HasConversion<int>().IsRequired();
        builder.Property(payment => payment.PackageCode).HasColumnName("package_code").HasMaxLength(PaymentTransaction.MaxPackageCodeLength);
        builder.Property(payment => payment.OrderCode).HasColumnName("order_code").HasMaxLength(PaymentTransaction.MaxOrderCodeLength).IsRequired();
        builder.Property(payment => payment.RequestId).HasColumnName("request_id").HasMaxLength(PaymentTransaction.MaxRequestIdLength);
        builder.Property(payment => payment.Description).HasColumnName("description").HasMaxLength(PaymentTransaction.MaxDescriptionLength).IsRequired();
        builder.Property(payment => payment.PaymentUrl).HasColumnName("payment_url").HasMaxLength(PaymentTransaction.MaxUrlLength);
        builder.Property(payment => payment.Deeplink).HasColumnName("deeplink").HasMaxLength(PaymentTransaction.MaxUrlLength);
        builder.Property(payment => payment.QrCodeUrl).HasColumnName("qr_code_url").HasMaxLength(PaymentTransaction.MaxUrlLength);
        builder.Property(payment => payment.QrCode).HasColumnName("qr_code");
        builder.Property(payment => payment.QrDataUrl).HasColumnName("qr_data_url");
        builder.Property(payment => payment.ExternalTransactionId).HasColumnName("external_transaction_id").HasMaxLength(PaymentTransaction.MaxExternalTransactionIdLength);
        builder.Property(payment => payment.ProviderMessage).HasColumnName("provider_message").HasMaxLength(PaymentTransaction.MaxProviderMessageLength);
        builder.Property(payment => payment.RawProviderPayload).HasColumnName("raw_provider_payload");
        builder.Property(payment => payment.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(payment => payment.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(payment => payment.PaidAt).HasColumnName("paid_at");
        builder.HasIndex(payment => payment.UserId).HasDatabaseName("ix_payment_transactions_user_id");
        builder.HasIndex(payment => payment.OrderCode).IsUnique().HasDatabaseName("ux_payment_transactions_order_code");
        builder.HasIndex(payment => payment.RequestId).HasDatabaseName("ix_payment_transactions_request_id");
    }
}
