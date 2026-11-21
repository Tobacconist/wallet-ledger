using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Wallet.Core.Monetary;
using Wallet.Infrastructure.Persistence;

namespace Wallet.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public class AccountTests(WalletApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Created_wallet_can_be_fetched_by_location()
    {
        var create = await _client.PostAsJsonAsync("/accounts", new { ownerName = "Elif Yılmaz", currency = "try" });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(create.Headers.Location);

        var account = await _client.GetFromJsonAsync<JsonElement>(create.Headers.Location);
        Assert.Equal("Elif Yılmaz", account.GetProperty("ownerName").GetString());
        Assert.Equal("TRY", account.GetProperty("currency").GetString());
        Assert.Equal(0, account.GetProperty("balance").GetInt64());
        Assert.Equal("Active", account.GetProperty("status").GetString());
        Assert.Equal("Wallet", account.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Unknown_account_returns_not_found_problem()
    {
        var response = await _client.GetAsync($"/accounts/{Guid.CreateVersion7()}");

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "account-not-found");
    }

    [Fact]
    public async Task Missing_fields_return_validation_problem()
    {
        var response = await _client.PostAsJsonAsync("/accounts", new { ownerName = "", currency = "TR" });

        var body = await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("OwnerName", out _) || errors.TryGetProperty("ownerName", out _));
        Assert.True(errors.TryGetProperty("Currency", out _) || errors.TryGetProperty("currency", out _));
    }

    [Fact]
    public async Task Unsupported_currency_returns_bad_request()
    {
        var response = await _client.PostAsJsonAsync("/accounts", new { ownerName = "Kerem Aydın", currency = "GBP" });

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "unsupported-currency");
    }

    [Fact]
    public async Task Malformed_json_returns_bad_request_problem()
    {
        using var content = new StringContent("{\"ownerName\": \"Kerem", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/accounts", content);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Freeze_unfreeze_and_close_update_status()
    {
        var location = await CreateWalletAsync("Zeynep Kaya", "EUR");

        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync($"{location}/freeze", null)).StatusCode);
        Assert.Equal("Frozen", await StatusOfAsync(location));

        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync($"{location}/unfreeze", null)).StatusCode);
        Assert.Equal("Active", await StatusOfAsync(location));

        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync($"{location}/close", null)).StatusCode);
        Assert.Equal("Closed", await StatusOfAsync(location));
    }

    [Fact]
    public async Task Closed_wallet_cannot_be_frozen()
    {
        var location = await CreateWalletAsync("Zeynep Kaya", "EUR");
        await _client.PostAsync($"{location}/close", null);

        var response = await _client.PostAsync($"{location}/freeze", null);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "account-closed");
    }

    [Fact]
    public async Task System_account_cannot_be_frozen()
    {
        var response = await _client.PostAsync($"/accounts/{SystemAccounts.SettlementId(Currency.TRY)}/freeze", null);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "system-account-operation");
    }

    [Fact]
    public async Task Status_change_on_unknown_account_returns_not_found()
    {
        var response = await _client.PostAsync($"/accounts/{Guid.CreateVersion7()}/close", null);

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "account-not-found");
    }

    [Fact]
    public async Task Unknown_route_returns_problem()
    {
        var response = await _client.GetAsync("/accounts/not-a-guid");

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    private async Task<Uri> CreateWalletAsync(string ownerName, string currency)
    {
        var response = await _client.PostAsJsonAsync("/accounts", new { ownerName, currency });
        response.EnsureSuccessStatusCode();
        return response.Headers.Location!;
    }

    private async Task<string?> StatusOfAsync(Uri location)
    {
        var account = await _client.GetFromJsonAsync<JsonElement>(location);
        return account.GetProperty("status").GetString();
    }
}
