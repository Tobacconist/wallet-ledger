using Wallet.Core.Errors;

namespace Wallet.Core.Monetary;

public readonly record struct Money
{
    public const long MaxMinorUnits = 1_000_000_000_000;

    private Money(long minorUnits, Currency currency)
    {
        MinorUnits = minorUnits;
        Currency = currency;
    }

    public long MinorUnits { get; }

    public Currency Currency { get; }

    public static Result<Money> Create(long minorUnits, Currency currency)
    {
        if (minorUnits is <= 0 or > MaxMinorUnits)
        {
            return DomainErrors.InvalidAmount(minorUnits);
        }

        return new Money(minorUnits, currency);
    }

    public override string ToString() => $"{MinorUnits} {Currency}";
}
