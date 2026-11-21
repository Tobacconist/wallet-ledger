using Microsoft.EntityFrameworkCore;
using Wallet.Core.Accounts;
using Wallet.Core.Ledger;
using Wallet.Core.Monetary;
using Wallet.Infrastructure.Idempotency;

namespace Wallet.Infrastructure.Persistence;

public sealed class WalletDbContext(DbContextOptions<WalletDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<LedgerTransaction> Transactions => Set<LedgerTransaction>();

    public DbSet<LedgerEntry> Entries => Set<LedgerEntry>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Currency>()
            .HaveConversion<CurrencyConverter>()
            .HaveColumnType("char(3)");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WalletDbContext).Assembly);
}
