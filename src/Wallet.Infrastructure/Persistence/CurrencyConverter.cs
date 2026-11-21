using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Wallet.Core.Monetary;

namespace Wallet.Infrastructure.Persistence;

internal sealed class CurrencyConverter() : ValueConverter<Currency, string>(c => c.Code, code => Currency.FromCode(code));
