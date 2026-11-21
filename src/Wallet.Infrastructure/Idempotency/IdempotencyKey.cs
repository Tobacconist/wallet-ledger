namespace Wallet.Infrastructure.Idempotency;

// Operation scopes the key (e.g. "deposit:{accountId}") so the same client key on two different endpoints does not collide.
public sealed record IdempotencyKey(string Key, string Operation, string RequestHash)
{
    public const int MaxKeyLength = 100;
}
