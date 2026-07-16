using Homeji.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Homeji.Infrastructure.Context.Configurations;

public sealed class WalletWithdrawalRequestConfiguration : IEntityTypeConfiguration<WalletWithdrawalRequest>
{
    public void Configure(EntityTypeBuilder<WalletWithdrawalRequest> builder)
    {
        builder.ToTable("wallet_withdrawal_requests", "homeji");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(request => request.BankName).HasMaxLength(WalletWithdrawalRequest.MaxBankNameLength).IsRequired();
        builder.Property(request => request.AccountNumber).HasMaxLength(WalletWithdrawalRequest.MaxAccountNumberLength).IsRequired();
        builder.Property(request => request.AccountHolder).HasMaxLength(WalletWithdrawalRequest.MaxAccountHolderLength).IsRequired();
        builder.Property(request => request.Status).HasConversion<int>().IsConcurrencyToken().IsRequired();
        builder.Property(request => request.AdminNote).HasMaxLength(WalletWithdrawalRequest.MaxAdminNoteLength);
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(request => request.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(request => request.ProcessedBy)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(request => new { request.UserId, request.CreatedAt });
        builder.HasIndex(request => new { request.Status, request.CreatedAt });
        builder.ToTable(table => table.HasCheckConstraint("ck_wallet_withdrawal_requests_amount", "\"Amount\" > 0"));
    }
}
