using Wallet.Infrastructure;
using Wallet.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks().AddDbContextCheck<WalletDbContext>("database");

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();

public partial class Program;
