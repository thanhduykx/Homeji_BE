namespace Homeji.Domain.Enums;

public enum ReceiptEvent
{
    DepositSucceeded = 1,
    PaymentSucceeded = 2,
    WithdrawalRequested = 3,
    WithdrawalCompleted = 4,
    WithdrawalRejected = 5,
    PurchasePaid = 6,
    PurchaseRefunded = 7,
}
