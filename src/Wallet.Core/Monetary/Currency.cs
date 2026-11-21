using Wallet.Core.Errors;

namespace Wallet.Core.Monetary;

public readonly record struct Currency
{
    public static readonly Currency TRY = new("TRY");
    public static readonly Currency EUR = new("EUR");
    public static readonly Currency USD = new("USD");

    public static IReadOnlyList<Currency> Supported { get; } = [TRY, EUR, USD];

    private Currency(string code) => Code = code;

    public string Code { get; }

    public static Result<Currency> Parse(string? code)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        foreach (var currency in Supported)
        {
            if (currency.Code == normalized)
            {
                return currency;
            }
        }

        return DomainErrors.UnsupportedCurrency(code);
    }

    public override string ToString() => Code;
}
