using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Wallet.Infrastructure.Persistence;

namespace Wallet.IntegrationTests;

// One real PostgreSQL container per test run. The in-memory provider is not an option here:
// row locks, check constraints and unique violations are exactly what these tests need to exercise.
public sealed class WalletApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("wallet")
        .WithUsername("wallet")
        .WithPassword("wallet_test")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Wallet", _postgres.GetConnectionString());
    }
}

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<WalletApiFactory>
{
    public const string Name = "api";
}
