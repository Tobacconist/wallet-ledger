using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Wallet.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public class StatementTests(WalletApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Paging_returns_every_entry_once_newest_first()
    {
        var elif = await _client.CreateWalletAsync("Elif Yılmaz");
        for (var i = 1; i <= 25; i++)
        {
            (await _client.DepositAsync(elif, i * 100)).EnsureSuccessStatusCode();
        }

        var lines = new List<JsonElement>();
        var pageSizes = new List<int>();
        long? cursor = null;
        do
        {
            var url = $"/accounts/{elif}/entries?limit=10" + (cursor is null ? "" : $"&cursor={cursor}");
            var page = await _client.GetFromJsonAsync<JsonElement>(url);
            var items = page.GetProperty("items").EnumerateArray().ToList();
            pageSizes.Add(items.Count);
            lines.AddRange(items);
            cursor = page.GetProperty("nextCursor").ValueKind == JsonValueKind.Null ? null : page.GetProperty("nextCursor").GetInt64();
        }
        while (cursor is not null);

        Assert.Equal([10, 10, 5], pageSizes);
        var ids = lines.Select(l => l.GetProperty("entryId").GetInt64()).ToList();
        Assert.Equal(25, ids.Distinct().Count());
        Assert.Equal(ids.OrderByDescending(id => id), ids);
        Assert.Equal(2500, lines[0].GetProperty("amount").GetInt64());
        Assert.Equal(32500, lines[0].GetProperty("balanceAfter").GetInt64());
        Assert.Equal("Deposit", lines[0].GetProperty("type").GetString());
        Assert.Equal("Maaş yatırma", lines[0].GetProperty("description").GetString());

        // Each line's running balance is the previous (older) line's balance plus its own amount.
        for (var i = 0; i < lines.Count - 1; i++)
        {
            Assert.Equal(
                lines[i + 1].GetProperty("balanceAfter").GetInt64() + lines[i].GetProperty("amount").GetInt64(),
                lines[i].GetProperty("balanceAfter").GetInt64());
        }
    }

    [Fact]
    public async Task Statement_shows_both_sides_of_a_transfer()
    {
        var elif = await _client.FundedWalletAsync("Elif Yılmaz", 40000);
        var kerem = await _client.CreateWalletAsync("Kerem Aydın");
        (await _client.TransferAsync(elif, kerem, 15000, description: "Tatil payı")).EnsureSuccessStatusCode();

        var elifLatest = (await _client.GetFromJsonAsync<JsonElement>($"/accounts/{elif}/entries")).GetProperty("items")[0];
        var keremLatest = (await _client.GetFromJsonAsync<JsonElement>($"/accounts/{kerem}/entries")).GetProperty("items")[0];

        Assert.Equal(-15000, elifLatest.GetProperty("amount").GetInt64());
        Assert.Equal(25000, elifLatest.GetProperty("balanceAfter").GetInt64());
        Assert.Equal(15000, keremLatest.GetProperty("amount").GetInt64());
        Assert.Equal("Tatil payı", keremLatest.GetProperty("description").GetString());
        Assert.Equal(elifLatest.GetProperty("transactionId").GetGuid(), keremLatest.GetProperty("transactionId").GetGuid());
    }

    [Fact]
    public async Task Empty_wallet_has_empty_statement()
    {
        var elif = await _client.CreateWalletAsync("Elif Yılmaz");

        var page = await _client.GetFromJsonAsync<JsonElement>($"/accounts/{elif}/entries");

        Assert.Equal(0, page.GetProperty("items").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("nextCursor").ValueKind);
    }

    [Theory]
    [InlineData("limit=0", "invalid-page-size")]
    [InlineData("limit=101", "invalid-page-size")]
    [InlineData("cursor=0", "invalid-cursor")]
    public async Task Invalid_paging_parameters_are_rejected(string query, string code)
    {
        var elif = await _client.CreateWalletAsync("Elif Yılmaz");

        var response = await _client.GetAsync($"/accounts/{elif}/entries?{query}");

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task Non_numeric_cursor_is_a_bad_request()
    {
        var elif = await _client.CreateWalletAsync("Elif Yılmaz");

        var response = await _client.GetAsync($"/accounts/{elif}/entries?cursor=abc");

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Statement_of_unknown_account_returns_not_found()
    {
        var response = await _client.GetAsync($"/accounts/{Guid.CreateVersion7()}/entries");

        await response.AssertProblemAsync(HttpStatusCode.NotFound, "account-not-found");
    }
}
