using Wallet.Core.Accounts;
using Wallet.Core.Ledger;
using Wallet.Core.Monetary;
using static Wallet.UnitTests.Fixtures;

namespace Wallet.UnitTests;

public class PostingTests
{
    private readonly Account _settlement = Settlement(Currency.TRY);
    private readonly Account _wallet = NewWallet("Elif Yılmaz", Currency.TRY);

    [Fact]
    public void Unbalanced_legs_throw()
    {
        PostingLeg[] legs = [new(_settlement, -1000), new(_wallet, 900)];

        Assert.Throws<InvalidOperationException>(() =>
            Posting.Post(TransactionType.Deposit, Currency.TRY, legs, null, null, Now));
    }

    [Fact]
    public void Single_leg_throws()
    {
        PostingLeg[] legs = [new(_wallet, 1000)];

        Assert.Throws<InvalidOperationException>(() =>
            Posting.Post(TransactionType.Deposit, Currency.TRY, legs, null, null, Now));
    }

    [Fact]
    public void Same_account_twice_throws()
    {
        PostingLeg[] legs = [new(_wallet, -1000), new(_wallet, 1000)];

        Assert.Throws<InvalidOperationException>(() =>
            Posting.Post(TransactionType.Transfer, Currency.TRY, legs, null, null, Now));
    }

    [Fact]
    public void Zero_amount_leg_throws()
    {
        var other = NewWallet("Kerem Aydın", Currency.TRY);
        PostingLeg[] legs = [new(_settlement, -1000), new(_wallet, 1000), new(other, 0)];

        Assert.Throws<InvalidOperationException>(() =>
            Posting.Post(TransactionType.Deposit, Currency.TRY, legs, null, null, Now));
    }

    [Fact]
    public void Successful_posting_produces_zero_sum_entries_with_running_balances()
    {
        PostingLeg[] legs = [new(_settlement, -12500), new(_wallet, 12500)];

        var result = Posting.Post(TransactionType.Deposit, Currency.TRY, legs, "Kira payı", null, Now);

        Assert.True(result.IsSuccess);
        var tx = result.Value;
        Assert.Equal(0, tx.Entries.Sum(e => e.Amount));
        Assert.All(tx.Entries, e => Assert.Equal(tx.Id, e.TransactionId));
        Assert.Equal(12500, tx.Entries.Single(e => e.AccountId == _wallet.Id).BalanceAfter);
        Assert.Equal(-12500, tx.Entries.Single(e => e.AccountId == _settlement.Id).BalanceAfter);
    }

    [Fact]
    public void Failed_posting_leaves_balances_untouched()
    {
        var eurWallet = NewWallet("Zeynep Kaya", Currency.EUR);
        PostingLeg[] legs = [new(_settlement, -5000), new(eurWallet, 5000)];

        var result = Posting.Post(TransactionType.Deposit, Currency.TRY, legs, null, null, Now);

        Assert.Equal("currency-mismatch", result.Error?.Code);
        Assert.Equal(0, _settlement.Balance);
        Assert.Equal(0, eurWallet.Balance);
    }
}
