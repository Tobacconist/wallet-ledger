using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Wallet.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public class MoneyMovementTests(WalletApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Deposit_returns_balanced_transaction_and_increases_balance()
    {
        var elif = await _client.CreateWalletAsync("Elif Yılmaz");

        var response = await _client.DepositAsync(elif, 250000);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Deposit", body.GetProperty("type").GetString());
        var entries = body.GetProperty("entries").EnumerateArray().ToList();
        Assert.Equal(2, entries.Count);
        Assert.Equal(0, entries.Sum(e => e.GetProperty("amount").GetInt64()));
        Assert.Equal(250000, entries.Single(e => e.GetProperty("accountId").GetGuid() == elif).GetProperty("balanceAfter").GetInt64());
        Assert.Equal(250000, await _client.BalanceAsync(elif));
    }

    [Fact]
    public async Task Created_transaction_can_be_fetched_by_location()
    {
        var elif = await _client.FundedWalletAsync("Elif Yılmaz", 50000);
        var kerem = await _client.CreateWalletAsync("Kerem Aydın");

        var response = await _client.TransferAsync(elif, kerem, 17550, description: "Kira payı - Kasım");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var fetched = await _client.GetFromJsonAsync<JsonElement>(response.Headers.Location);
        Assert.Equal("Transfer", fetched.GetProperty("type").GetString());
        Assert.Equal("Kira payı - Kasım", fetched.GetProperty("description").GetString());
        Assert.Equal(2, fetched.GetProperty("entries").GetArrayLength());
        Assert.Equal(32450, await _client.BalanceAsync(elif));
        Assert.Equal(17550, await _client.BalanceAsync(kerem));
    }

    [Fact]
    public async Task Unknown_transaction_returns_not_found()
    {
        var response = await _client.GetAsync($"/transfers/{Guid.CreateVersion7()}");

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "transaction-not-found");
    }

    [Fact]
    public async Task Deposit_to_unknown_account_returns_not_found()
    {
        var response = await _client.DepositAsync(Guid.CreateVersion7(), 1000);

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "account-not-found");
    }

    [Fact]
    public async Task Deposit_in_other_currency_is_rejected()
    {
        var elif = await _client.CreateWalletAsync("Elif Yılmaz");

        var response = await _client.DepositAsync(elif, 1000, "EUR");

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "currency-mismatch");
        Assert.Equal(0, await _client.BalanceAsync(elif));
    }

    [Fact]
    public async Task Deposit_to_frozen_wallet_is_rejected()
    {
        var elif = await _client.CreateWalletAsync("Elif Yılmaz");
        await _client.PostAsync($"/accounts/{elif}/freeze", null);

        var response = await _client.DepositAsync(elif, 1000);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "account-not-active");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public async Task Non_positive_amount_is_a_validation_error(long amountMinor)
    {
        var elif = await _client.CreateWalletAsync("Elif Yılmaz");

        var response = await _client.DepositAsync(elif, amountMinor);

        var body = await response.AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.True(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Withdrawal_beyond_balance_is_rejected()
    {
        var elif = await _client.FundedWalletAsync("Elif Yılmaz", 10000);

        var response = await _client.WithdrawAsync(elif, 10001);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "insufficient-funds");
        Assert.Equal(10000, await _client.BalanceAsync(elif));
    }

    [Fact]
    public async Task Withdrawal_of_full_balance_leaves_zero()
    {
        var elif = await _client.FundedWalletAsync("Elif Yılmaz", 10000);

        var response = await _client.WithdrawAsync(elif, 10000);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(0, await _client.BalanceAsync(elif));
    }

    [Fact]
    public async Task Transfer_to_same_account_is_rejected()
    {
        var elif = await _client.FundedWalletAsync("Elif Yılmaz", 10000);

        var response = await _client.TransferAsync(elif, elif, 100);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "same-account-transfer");
    }

    [Fact]
    public async Task Transfer_between_currencies_is_rejected()
    {
        var elif = await _client.FundedWalletAsync("Elif Yılmaz", 10000);
        var zeynep = await _client.CreateWalletAsync("Zeynep Kaya", "EUR");

        var response = await _client.TransferAsync(elif, zeynep, 100);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "currency-mismatch");
        Assert.Equal(10000, await _client.BalanceAsync(elif));
    }

    [Fact]
    public async Task Transfer_without_destination_is_a_validation_error()
    {
        var elif = await _client.FundedWalletAsync("Elif Yılmaz", 10000);

        var response = await _client.PostAsJsonAsync("/transfers", new { fromAccountId = elif, amountMinor = 100, currency = "TRY" });

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Wallet_with_balance_cannot_be_closed()
    {
        var elif = await _client.FundedWalletAsync("Elif Yılmaz", 2500);

        var response = await _client.PostAsync($"/accounts/{elif}/close", null);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "account-has-balance");
    }
}
