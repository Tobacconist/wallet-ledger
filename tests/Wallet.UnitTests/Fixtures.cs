using Wallet.Core.Accounts;
using Wallet.Core.Ledger;
using Wallet.Core.Monetary;

namespace Wallet.UnitTests;

internal static class Fixtures
{
    public static readonly DateTimeOffset Now = new(2026, 11, 21, 9, 30, 0, TimeSpan.FromHours(3));

    public static Account Settlement(Currency currency) =>
        Account.CreateSystem(Guid.CreateVersion7(Now), $"SETTLEMENT_{currency.Code}", currency, Now);

    public static Account NewWallet(string owner, Currency currency) =>
        Account.OpenWallet(owner, currency, Now).Value!;

    public static Money Amount(long minorUnits, Currency currency) =>
        Money.Create(minorUnits, currency).Value;

    public static Account FundedWallet(string owner, long minorUnits, Currency currency, Account settlement)
    {
        var wallet = NewWallet(owner, currency);
        var deposit = LedgerPostings.Deposit(settlement, wallet, Amount(minorUnits, currency), "Maaş yatırma", Now);
        Assert.True(deposit.IsSuccess);
        return wallet;
    }
}
