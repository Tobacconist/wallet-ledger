using Wallet.Core.Accounts;
using Wallet.Core.Ledger;
using Wallet.Core.Monetary;
using static Wallet.UnitTests.Fixtures;

namespace Wallet.UnitTests;

public class LedgerPostingsTests
{
    private readonly Account _settlement = Settlement(Currency.TRY);

    [Fact]
    public void Deposit_credits_wallet_and_debits_settlement()
    {
        var wallet = NewWallet("Elif Yılmaz", Currency.TRY);

        var result = LedgerPostings.Deposit(_settlement, wallet, Amount(250000, Currency.TRY), "Maaş", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(TransactionType.Deposit, result.Value.Type);
        Assert.Equal(250000, wallet.Balance);
        Assert.Equal(-250000, _settlement.Balance);
    }

    [Fact]
    public void Deposit_in_wrong_currency_is_rejected()
    {
        var wallet = NewWallet("Elif Yılmaz", Currency.TRY);

        var result = LedgerPostings.Deposit(_settlement, wallet, Amount(1000, Currency.EUR), null, Now);

        Assert.Equal("currency-mismatch", result.Error?.Code);
    }

    [Fact]
    public void Withdraw_more_than_balance_is_rejected()
    {
        var wallet = FundedWallet("Elif Yılmaz", 10000, Currency.TRY, _settlement);

        var result = LedgerPostings.Withdraw(wallet, _settlement, Amount(10001, Currency.TRY), null, Now);

        Assert.Equal("insufficient-funds", result.Error?.Code);
        Assert.Equal(10000, wallet.Balance);
    }

    [Fact]
    public void Transfer_moves_money_between_wallets()
    {
        var elif = FundedWallet("Elif Yılmaz", 50000, Currency.TRY, _settlement);
        var kerem = NewWallet("Kerem Aydın", Currency.TRY);

        var result = LedgerPostings.Transfer(elif, kerem, Amount(17550, Currency.TRY), "Kira payı - Kasım", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(32450, elif.Balance);
        Assert.Equal(17550, kerem.Balance);
        Assert.Equal("Kira payı - Kasım", result.Value.Description);
        Assert.Equal(0, result.Value.Entries.Sum(e => e.Amount));
    }

    [Fact]
    public void Transfer_of_exact_balance_leaves_zero()
    {
        var elif = FundedWallet("Elif Yılmaz", 7500, Currency.TRY, _settlement);
        var kerem = NewWallet("Kerem Aydın", Currency.TRY);

        var result = LedgerPostings.Transfer(elif, kerem, Amount(7500, Currency.TRY), null, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, elif.Balance);
    }

    [Fact]
    public void Transfer_with_insufficient_funds_changes_nothing()
    {
        var elif = FundedWallet("Elif Yılmaz", 7500, Currency.TRY, _settlement);
        var kerem = NewWallet("Kerem Aydın", Currency.TRY);

        var result = LedgerPostings.Transfer(elif, kerem, Amount(7501, Currency.TRY), null, Now);

        Assert.Equal("insufficient-funds", result.Error?.Code);
        Assert.Equal(7500, elif.Balance);
        Assert.Equal(0, kerem.Balance);
    }

    [Fact]
    public void Transfer_to_same_account_is_rejected()
    {
        var elif = FundedWallet("Elif Yılmaz", 7500, Currency.TRY, _settlement);

        var result = LedgerPostings.Transfer(elif, elif, Amount(100, Currency.TRY), null, Now);

        Assert.Equal("same-account-transfer", result.Error?.Code);
    }

    [Fact]
    public void Transfer_between_different_currencies_is_rejected()
    {
        var elif = FundedWallet("Elif Yılmaz", 7500, Currency.TRY, _settlement);
        var zeynep = NewWallet("Zeynep Kaya", Currency.EUR);

        var result = LedgerPostings.Transfer(elif, zeynep, Amount(100, Currency.TRY), null, Now);

        Assert.Equal("currency-mismatch", result.Error?.Code);
        Assert.Equal(7500, elif.Balance);
    }

    [Fact]
    public void Transfer_from_frozen_wallet_is_rejected()
    {
        var elif = FundedWallet("Elif Yılmaz", 7500, Currency.TRY, _settlement);
        var kerem = NewWallet("Kerem Aydın", Currency.TRY);
        elif.Freeze();

        var result = LedgerPostings.Transfer(elif, kerem, Amount(100, Currency.TRY), null, Now);

        Assert.Equal("account-not-active", result.Error?.Code);
    }

    [Fact]
    public void Transfer_to_frozen_or_closed_wallet_is_rejected()
    {
        var elif = FundedWallet("Elif Yılmaz", 7500, Currency.TRY, _settlement);
        var frozen = NewWallet("Kerem Aydın", Currency.TRY);
        var closed = NewWallet("Zeynep Kaya", Currency.TRY);
        frozen.Freeze();
        closed.Close();

        Assert.Equal("account-not-active", LedgerPostings.Transfer(elif, frozen, Amount(100, Currency.TRY), null, Now).Error?.Code);
        Assert.Equal("account-not-active", LedgerPostings.Transfer(elif, closed, Amount(100, Currency.TRY), null, Now).Error?.Code);
        Assert.Equal(7500, elif.Balance);
    }

    [Fact]
    public void Transfer_from_system_account_is_rejected()
    {
        var kerem = NewWallet("Kerem Aydın", Currency.TRY);

        var result = LedgerPostings.Transfer(_settlement, kerem, Amount(100, Currency.TRY), null, Now);

        Assert.Equal("system-account-operation", result.Error?.Code);
    }

    [Fact]
    public void Reverse_posts_opposite_entries_and_restores_balances()
    {
        var elif = FundedWallet("Elif Yılmaz", 20000, Currency.TRY, _settlement);
        var kerem = NewWallet("Kerem Aydın", Currency.TRY);
        var transfer = LedgerPostings.Transfer(elif, kerem, Amount(5000, Currency.TRY), null, Now).Value!;

        var result = LedgerPostings.Reverse(transfer, Lookup(elif, kerem), Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(TransactionType.Reversal, result.Value.Type);
        Assert.Equal(transfer.Id, result.Value.ReversesTransactionId);
        Assert.Equal(20000, elif.Balance);
        Assert.Equal(0, kerem.Balance);
        foreach (var entry in transfer.Entries)
        {
            Assert.Equal(-entry.Amount, result.Value.Entries.Single(e => e.AccountId == entry.AccountId).Amount);
        }
    }

    [Fact]
    public void Reverse_fails_when_recipient_already_spent_the_money()
    {
        var elif = FundedWallet("Elif Yılmaz", 20000, Currency.TRY, _settlement);
        var kerem = NewWallet("Kerem Aydın", Currency.TRY);
        var transfer = LedgerPostings.Transfer(elif, kerem, Amount(5000, Currency.TRY), null, Now).Value!;
        LedgerPostings.Withdraw(kerem, _settlement, Amount(3000, Currency.TRY), null, Now);

        var result = LedgerPostings.Reverse(transfer, Lookup(elif, kerem), Now);

        Assert.Equal("insufficient-funds", result.Error?.Code);
        Assert.Equal(15000, elif.Balance);
        Assert.Equal(2000, kerem.Balance);
    }

    [Fact]
    public void Reversal_cannot_be_reversed()
    {
        var elif = FundedWallet("Elif Yılmaz", 20000, Currency.TRY, _settlement);
        var kerem = NewWallet("Kerem Aydın", Currency.TRY);
        var transfer = LedgerPostings.Transfer(elif, kerem, Amount(5000, Currency.TRY), null, Now).Value!;
        var reversal = LedgerPostings.Reverse(transfer, Lookup(elif, kerem), Now).Value!;

        var result = LedgerPostings.Reverse(reversal, Lookup(elif, kerem), Now);

        Assert.Equal("cannot-reverse-reversal", result.Error?.Code);
    }

    private static Dictionary<Guid, Account> Lookup(params Account[] accounts) => accounts.ToDictionary(a => a.Id);
}
