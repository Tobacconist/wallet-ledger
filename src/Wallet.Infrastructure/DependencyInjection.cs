using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallet.Infrastructure.Persistence;
using Wallet.Infrastructure.Services;

namespace Wallet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Wallet")
            ?? throw new InvalidOperationException("Connection string 'Wallet' is not configured.");

        services.AddDbContext<WalletDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AccountService>();

        return services;
    }
}
