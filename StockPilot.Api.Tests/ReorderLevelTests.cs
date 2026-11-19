using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockPilot.Api.Data;
using StockPilot.Api.Models;

namespace StockPilot.Api.Tests;

public class ReorderLevelTests : IClassFixture<StockPilotApiFactory>
{
    private readonly StockPilotApiFactory _factory;

    public ReorderLevelTests(StockPilotApiFactory factory) => _factory = factory;

    // The database is shared by all tests of this class, so every test creates its own product
    // (unique SKU) and its own branch store, and asserts only about those rows.
    private async Task<(int ProductId, string Sku, int CentralId, int BranchId)> Seed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var central = await db.Stores.FirstAsync(s => s.IsCentral);
        var branch = new Store { Name = $"Branch {Guid.NewGuid():N}", Code = Guid.NewGuid().ToString("N")[..8] };
        var supplier = new Supplier { Name = "Acme" };
        var sku = $"RL-{Guid.NewGuid():N}";
        var product = new Product { Name = "Reorder test", Sku = sku, UnitPrice = 10, Supplier = supplier };
        db.Stores.Add(branch);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return (product.Id, sku, central.Id, branch.Id);
    }

    private async Task<HttpClient> ClientFor(string role, int? storeId = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TestAuth.LoginAs(_factory, role, storeId));
        return client;
    }

    private static JsonElement StoreRow(JsonElement view, int storeId) =>
        view.GetProperty("stores").EnumerateArray().First(r => r.GetProperty("storeId").GetInt32() == storeId);

    [Fact]
    public async Task Setting_a_reorder_level_flags_the_row_as_low_when_quantity_is_at_or_below_it()
    {
        var s = await Seed();
        var admin = await ClientFor("Admin");

        var put = await admin.PutAsJsonAsync($"/products/{s.ProductId}/stock/{s.CentralId}",
            new { quantity = 5, reorderLevel = 5 });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var view = await admin.GetFromJsonAsync<JsonElement>($"/products/{s.ProductId}/stock");
        var row = StoreRow(view, s.CentralId);
        Assert.Equal(5, row.GetProperty("reorderLevel").GetInt32());
        Assert.True(row.GetProperty("isLow").GetBoolean());   // 5 <= 5
    }

    [Fact]
    public async Task Negative_reorder_level_is_rejected()
    {
        var s = await Seed();
        var admin = await ClientFor("Admin");

        var put = await admin.PutAsJsonAsync($"/products/{s.ProductId}/stock/{s.CentralId}",
            new { quantity = 5, reorderLevel = -1 });

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task Omitting_the_reorder_level_clears_it()
    {
        var s = await Seed();
        var admin = await ClientFor("Admin");
        await admin.PutAsJsonAsync($"/products/{s.ProductId}/stock/{s.CentralId}", new { quantity = 3, reorderLevel = 5 });

        await admin.PutAsJsonAsync($"/products/{s.ProductId}/stock/{s.CentralId}", new { quantity = 3 });

        var view = await admin.GetFromJsonAsync<JsonElement>($"/products/{s.ProductId}/stock");
        var row = StoreRow(view, s.CentralId);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("reorderLevel").ValueKind);
        Assert.False(row.GetProperty("isLow").GetBoolean());
    }

    [Fact]
    public async Task Low_stock_list_contains_only_rows_at_or_below_their_level()
    {
        var s = await Seed();
        var admin = await ClientFor("Admin");
        await admin.PutAsJsonAsync($"/products/{s.ProductId}/stock/{s.CentralId}", new { quantity = 3, reorderLevel = 5 });   // low
        await admin.PutAsJsonAsync($"/products/{s.ProductId}/stock/{s.BranchId}", new { quantity = 50, reorderLevel = 10 });  // fine

        var rows = await admin.GetFromJsonAsync<JsonElement[]>("/stock/low");

        Assert.Contains(rows!, r => r.GetProperty("sku").GetString() == s.Sku && r.GetProperty("storeId").GetInt32() == s.CentralId);
        Assert.DoesNotContain(rows!, r => r.GetProperty("sku").GetString() == s.Sku && r.GetProperty("storeId").GetInt32() == s.BranchId);
    }

    [Fact]
    public async Task Regular_user_sees_only_their_own_store_even_when_asking_for_another()
    {
        var s = await Seed();
        var admin = await ClientFor("Admin");
        await admin.PutAsJsonAsync($"/products/{s.ProductId}/stock/{s.CentralId}", new { quantity = 1, reorderLevel = 5 });
        await admin.PutAsJsonAsync($"/products/{s.ProductId}/stock/{s.BranchId}", new { quantity = 1, reorderLevel = 5 });
        var branchUser = await ClientFor("User", s.BranchId);

        // The user asks for the central store, but the server scopes the answer to their own store.
        var rows = await branchUser.GetFromJsonAsync<JsonElement[]>($"/stock/low?storeId={s.CentralId}");

        Assert.All(rows!, r => Assert.Equal(s.BranchId, r.GetProperty("storeId").GetInt32()));
        Assert.Contains(rows!, r => r.GetProperty("sku").GetString() == s.Sku);
    }
}