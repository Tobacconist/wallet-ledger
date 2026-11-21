using Wallet.Core.Monetary;

namespace Wallet.UnitTests;

public class MoneyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1500)]
    [InlineData(Money.MaxMinorUnits + 1)]
    public void Create_rejects_out_of_range_amounts(long minorUnits)
    {
        var result = Money.Create(minorUnits, Currency.TRY);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid-amount", result.Error.Code);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15050)]
    [InlineData(Money.MaxMinorUnits)]
    public void Create_accepts_amounts_in_range(long minorUnits)
    {
        var result = Money.Create(minorUnits, Currency.TRY);

        Assert.True(result.IsSuccess);
        Assert.Equal(minorUnits, result.Value.MinorUnits);
    }
}
