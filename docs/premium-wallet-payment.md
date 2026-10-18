# Premium subscription purchase from wallet

Authenticated endpoint `POST /api/subscriptions/premium/{packageCode}/wallet` accepts a nonempty UUID requestId and the amount confirmed by the user. Price comes from the server catalog. PaymentMethod.Wallet=3 and WalletTransactionKind.SubscriptionPurchase=9 are additive integer enum values; existing storage requires no migration.

A PostgreSQL transaction and a per-user advisory lock serialize subscription wallet purchases across instances. The deterministic unique order code scopes request IDs to a user; replay returns the prior payment and rejects changed intent. Wallet.Version retains optimistic protection against marketplace purchases or withdrawals. The wallet debit, ledger, paid payment, entitlement and receipt/outbox commit atomically. EF execution strategy re-reads persisted state on a transient replay, including ambiguous commit failures. No payment provider is contacted.

Existing Premium is extended from its latest active expiry. Receipt mail uses the existing branded HTML/PDF pipeline; email delivery remains asynchronous. A failed email does not undo the purchase.

Validation: 462 unit + 188 integration tests pass with no skips on disposable local PostgreSQL (`output/quality/local-3e9ba290479c4a0bb7062b91537eb49a`). Five new database tests cover concurrent retries, insufficient funds under contention, changed price, rollback on receipt failure, renewal and replay after catalog changes. These tests enable the same Npgsql retry strategy used in production. No real customer wallet was debited for verification.
