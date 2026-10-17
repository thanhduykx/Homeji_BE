using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class TransactionReceiptConfiguration : IEntityTypeConfiguration<TransactionReceipt>
{
    public void Configure(EntityTypeBuilder<TransactionReceipt> builder)
    {
        builder.ToTable("transaction_receipts", "homeji");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Event).HasConversion<int>();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.ReferenceCode).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.ReferenceId, x.Event }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.IssuedAt, x.Id });
    }
}

public sealed class ReceiptEmailDeliveryConfiguration : IEntityTypeConfiguration<ReceiptEmailDelivery>
{
    public void Configure(EntityTypeBuilder<ReceiptEmailDelivery> builder)
    {
        builder.ToTable("receipt_email_deliveries", "homeji");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.LastErrorCode).HasMaxLength(80);
        builder.HasOne<TransactionReceipt>().WithOne().HasForeignKey<ReceiptEmailDelivery>(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.ReceiptId).IsUnique();
        builder.HasIndex(x => new { x.SentAt, x.NextAttemptAt });
    }
}
