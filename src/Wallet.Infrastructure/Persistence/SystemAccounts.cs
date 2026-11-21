using Wallet.Core.Accounts;
using Wallet.Core.Monetary;

namespace Wallet.Infrastructure.Persistence;

public static class SystemAccounts
{
    private static readonly DateTimeOffset SeededAt = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<Currency, Guid> SettlementIds = new()
    {
        [Currency.TRY] = Guid.Parse("0196f1a0-0000-7000-8000-000000000001"),
        [Currency.EUR] = Guid.Parse("0196f1a0-0000-7000-8000-000000000002"),
        [Currency.USD] = Guid.Parse("0196f1a0-0000-7000-8000-000000000003"),
    };

    public static IReadOnlyList<Account> All { get; } =
        [.. SettlementIds.Select(p => Account.CreateSystem(p.Value, $"SETTLEMENT_{p.Key.Code}", p.Key, SeededAt))];

    public static Guid SettlementId(Currency currency) => SettlementIds[currency];
}
