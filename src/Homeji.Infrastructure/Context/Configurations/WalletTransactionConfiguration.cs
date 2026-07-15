using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("wallet_transactions", "homeji");
        builder.HasKey(transaction => transaction.Id);
        builder.Property(transaction => transaction.Kind).HasConversion<int>().IsRequired();
        builder.Property(transaction => transaction.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(transaction => transaction.BalanceAfter).HasPrecision(18, 2).IsRequired();
        builder.Property(transaction => transaction.Description).HasMaxLength(WalletTransaction.MaxDescriptionLength).IsRequired();
        builder.HasOne<WalletAccount>()
            .WithMany()
            .HasForeignKey(transaction => transaction.WalletUserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(transaction => new { transaction.WalletUserId, transaction.CreatedAt });
        builder.HasIndex(transaction => new { transaction.WalletUserId, transaction.Kind, transaction.ReferenceId }).IsUnique();
        builder.ToTable(table => table.HasCheckConstraint("ck_wallet_transactions_amount", "\"Amount\" <> 0"));
    }
}
