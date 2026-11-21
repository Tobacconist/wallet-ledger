namespace Wallet.Infrastructure.Idempotency;

public sealed record IdempotencyKey(string Key, string Operation, string RequestHash)
{
    public const int MaxKeyLength = 100;
}
