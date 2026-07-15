using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class WalletAccountConfiguration : IEntityTypeConfiguration<WalletAccount>
{
    public void Configure(EntityTypeBuilder<WalletAccount> builder)
    {
        builder.ToTable("wallet_accounts", "homeji");
        builder.HasKey(wallet => wallet.UserId);
        builder.Property(wallet => wallet.Balance).HasPrecision(18, 2).IsRequired();
        builder.Property(wallet => wallet.TotalDeposited).HasPrecision(18, 2).IsRequired();
        builder.Property(wallet => wallet.TotalSpent).HasPrecision(18, 2).IsRequired();
        builder.Property(wallet => wallet.TotalEarned).HasPrecision(18, 2).IsRequired();
        builder.Property(wallet => wallet.Version).IsConcurrencyToken().IsRequired();
        builder.Ignore(wallet => wallet.IsActivated);
        builder.HasOne<UserProfile>()
            .WithOne()
            .HasForeignKey<WalletAccount>(wallet => wallet.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_wallet_accounts_balance", "\"Balance\" >= 0");
            table.HasCheckConstraint("ck_wallet_accounts_totals", "\"TotalDeposited\" >= 0 AND \"TotalSpent\" >= 0 AND \"TotalEarned\" >= 0");
        });
    }
}
