using Homeji.Domain.Entities;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.Marketplace;

public sealed class WalletAccountTests
{
    [Fact]
    public void CreditTopUp_WhenMinimumAmount_CreatesActivatedBalance()
    {
        var now = DateTimeOffset.UtcNow;
        var wallet = WalletAccount.Create(Guid.NewGuid(), now);

        wallet.CreditTopUp(WalletAccount.MinimumTopUp, now.AddMinutes(1));

        Assert.True(wallet.IsActivated);
        Assert.Equal(WalletAccount.MinimumTopUp, wallet.Balance);
        Assert.Equal(WalletAccount.MinimumTopUp, wallet.TotalDeposited);
        Assert.Equal(1, wallet.Version);
    }

    [Fact]
    public void DebitPurchase_WhenBalanceIsInsufficient_LeavesWalletUnchanged()
    {
        var now = DateTimeOffset.UtcNow;
        var wallet = WalletAccount.Create(Guid.NewGuid(), now);
        wallet.CreditTopUp(WalletAccount.MinimumTopUp, now.AddMinutes(1));

        Assert.Throws<DomainException>(() =>
            wallet.DebitPurchase(WalletAccount.MinimumTopUp + 1, now.AddMinutes(2)));
        Assert.Equal(WalletAccount.MinimumTopUp, wallet.Balance);
        Assert.Equal(0, wallet.TotalSpent);
        Assert.Equal(1, wallet.Version);
    }

    [Fact]
    public void CreditSaleProceeds_CreditsOnlyProvidedNetAmount()
    {
        var now = DateTimeOffset.UtcNow;
        var wallet = WalletAccount.Create(Guid.NewGuid(), now);

        wallet.CreditSaleProceeds(63_000, now.AddMinutes(1));

        Assert.Equal(63_000, wallet.Balance);
        Assert.Equal(63_000, wallet.TotalEarned);
    }

    [Fact]
    public void DebitWithdrawal_WhenReserveWouldRemain_DebitsBalance()
    {
        var now = DateTimeOffset.UtcNow;
        var wallet = WalletAccount.Create(Guid.NewGuid(), now);
        wallet.CreditTopUp(200_000, now.AddMinutes(1));

        wallet.DebitWithdrawal(180_000, now.AddMinutes(2));

        Assert.Equal(WalletAccount.MinimumWithdrawalReserve, wallet.Balance);
        Assert.Equal(2, wallet.Version);
    }

    [Fact]
    public void DebitWithdrawal_WhenReserveWouldBeViolated_LeavesWalletUnchanged()
    {
        var now = DateTimeOffset.UtcNow;
        var wallet = WalletAccount.Create(Guid.NewGuid(), now);
        wallet.CreditTopUp(200_000, now.AddMinutes(1));

        Assert.Throws<DomainException>(() => wallet.DebitWithdrawal(180_001, now.AddMinutes(2)));
        Assert.Equal(200_000, wallet.Balance);
        Assert.Equal(1, wallet.Version);
    }

    [Fact]
    public void CreditWithdrawalRefund_RestoresRejectedWithdrawal()
    {
        var now = DateTimeOffset.UtcNow;
        var wallet = WalletAccount.Create(Guid.NewGuid(), now);
        wallet.CreditTopUp(200_000, now.AddMinutes(1));
        wallet.DebitWithdrawal(100_000, now.AddMinutes(2));

        wallet.CreditWithdrawalRefund(100_000, now.AddMinutes(3));

        Assert.Equal(200_000, wallet.Balance);
        Assert.Equal(3, wallet.Version);
    }
}
