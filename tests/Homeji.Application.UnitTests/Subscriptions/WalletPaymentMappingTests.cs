using Homeji.Application.Mappers.Payments;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
namespace Homeji.Application.UnitTests.Subscriptions;

public sealed class WalletPaymentMappingTests
{
    [Fact]
    public void Immediate_wallet_payment_has_no_gateway_payment_deadline()
    {
        var now = DateTimeOffset.UtcNow;
        var payment = new PaymentTransaction(Guid.NewGuid(), PaymentMethod.Wallet, 99000, "wallet-test", "Premium", now, PaymentPurpose.PremiumSubscription, "PREMIUM_MONTHLY");
        payment.MarkPaid(null, null, null, now);
        Assert.Null(PaymentMapper.ToDto(payment).ExpiresAt);
    }
}
