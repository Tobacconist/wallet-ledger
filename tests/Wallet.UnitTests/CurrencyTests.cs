using Wallet.Core.Monetary;

namespace Wallet.UnitTests;

public class CurrencyTests
{
    [Theory]
    [InlineData("TRY")]
    [InlineData("try")]
    [InlineData(" eur ")]
    public void Parse_accepts_supported_codes_case_insensitively(string code)
    {
        var result = Currency.Parse(code);

        Assert.True(result.IsSuccess);
        Assert.Equal(code.Trim().ToUpperInvariant(), result.Value.Code);
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_rejects_unsupported_codes(string? code)
    {
        var result = Currency.Parse(code);

        Assert.False(result.IsSuccess);
        Assert.Equal("unsupported-currency", result.Error.Code);
    }
}
