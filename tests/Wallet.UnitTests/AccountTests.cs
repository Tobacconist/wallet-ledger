using Wallet.Core.Accounts;
using Wallet.Core.Monetary;
using static Wallet.UnitTests.Fixtures;

namespace Wallet.UnitTests;

public class AccountTests
{
    [Fact]
    public void OpenWallet_trims_owner_name_and_starts_active_with_zero_balance()
    {
        var result = Account.OpenWallet("  Elif Yılmaz ", Currency.TRY, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal("Elif Yılmaz", result.Value.OwnerName);
        Assert.Equal(AccountStatus.Active, result.Value.Status);
        Assert.Equal(0, result.Value.Balance);
        Assert.False(result.Value.AllowNegative);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenWallet_rejects_blank_owner_name(string ownerName)
    {
        var result = Account.OpenWallet(ownerName, Currency.TRY, Now);

        Assert.Equal("invalid-owner-name", result.Error?.Code);
    }

    [Fact]
    public void OpenWallet_rejects_too_long_owner_name()
    {
        var result = Account.OpenWallet(new string('a', Account.OwnerNameMaxLength + 1), Currency.TRY, Now);

        Assert.Equal("invalid-owner-name", result.Error?.Code);
    }

    [Fact]
    public void Close_requires_zero_balance()
    {
        var wallet = FundedWallet("Kerem Aydın", 2500, Currency.TRY, Settlement(Currency.TRY));

        var result = wallet.Close();

        Assert.Equal("account-has-balance", result.Error?.Code);
        Assert.Equal(AccountStatus.Active, wallet.Status);
    }

    [Fact]
    public void Close_empty_wallet_marks_it_closed()
    {
        var wallet = NewWallet("Kerem Aydın", Currency.TRY);

        Assert.True(wallet.Close().IsSuccess);
        Assert.Equal(AccountStatus.Closed, wallet.Status);
    }

    [Fact]
    public void Freeze_and_unfreeze_toggle_status()
    {
        var wallet = NewWallet("Zeynep Kaya", Currency.EUR);

        Assert.True(wallet.Freeze().IsSuccess);
        Assert.Equal(AccountStatus.Frozen, wallet.Status);

        Assert.True(wallet.Unfreeze().IsSuccess);
        Assert.Equal(AccountStatus.Active, wallet.Status);
    }

    [Fact]
    public void Closed_wallet_cannot_be_frozen_or_unfrozen()
    {
        var wallet = NewWallet("Zeynep Kaya", Currency.EUR);
        wallet.Close();

        Assert.Equal("account-closed", wallet.Freeze().Error?.Code);
        Assert.Equal("account-closed", wallet.Unfreeze().Error?.Code);
    }

    [Fact]
    public void System_account_cannot_be_frozen_or_closed()
    {
        var settlement = Settlement(Currency.TRY);

        Assert.Equal("system-account-operation", settlement.Freeze().Error?.Code);
        Assert.Equal("system-account-operation", settlement.Close().Error?.Code);
        Assert.True(settlement.AllowNegative);
    }
}
