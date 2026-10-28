using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockPilot.Api.Data;
using StockPilot.Api.Models;

namespace StockPilot.Api.Tests;

// Same app as StockPilotApiFactory, but with a small page-size cap so the cap can be tested with few rows.
public class PagingApiFactory : StockPilotApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Paging:MaxPageSize"] = "5" }));
    }
}

// IClassFixture gives this class its own factory, hence its own empty database,
// so the data below is the only data and exact results can be asserted.
public class ProductPagingTests : IClassFixture<PagingApiFactory>
{
    private readonly PagingApiFactory _factory;

    public ProductPagingTests(PagingApiFactory factory) => _factory = factory;

    // 12 products S01..S12: quantity = n, price cycles 20, 30, 10 (so many rows share a price).
    private async Task<HttpClient> SeededClient()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!await db.Products.AnyAsync())   // xUnit builds a new instance per test: seed only once
            {
                var central = await db.Stores.FirstAsync(s => s.IsCentral);
                var supplier = new Supplier { Name = "Acme" };
                db.Suppliers.Add(supplier);
                for (var n = 1; n <= 12; n++)
                {
                    var product = new Product
                    {
                        Name = $"Product {n:00}",
                        Sku = $"S{n:00}",
                        UnitPrice = 10 * ((n % 3) + 1),
                        Supplier = supplier
                    };
                    product.ProductStocks.Add(new ProductStock { StoreId = central.Id, Quantity = n });
                    db.Products.Add(product);
                }
                await db.SaveChangesAsync();
            }
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TestAuth.LoginAs(_factory, "User"));
        return client;
    }

    private static async Task<(string[] Skus, string? Total)> Get(HttpClient client, string query)
    {
        var response = await client.GetAsync("/products" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        var skus = items!.Select(i => i.GetProperty("sku").GetString()!).ToArray();
        var total = response.Headers.TryGetValues("X-Total-Count", out var values) ? values.Single() : null;
        return (skus, total);
    }

    [Fact]
    public async Task Returns_the_requested_page_and_the_total_count()
    {
        var client = await SeededClient();

        var (skus, total) = await Get(client, "?page=1&pageSize=5");

        Assert.Equal(["S01", "S02", "S03", "S04", "S05"], skus);
        Assert.Equal("12", total);
    }

    [Fact]
    public async Task Last_page_contains_the_remaining_items()
    {
        var client = await SeededClient();

        var (skus, _) = await Get(client, "?page=3&pageSize=5");

        Assert.Equal(["S11", "S12"], skus);
    }

    [Fact]
    public async Task Page_size_above_the_configured_maximum_is_capped()
    {
        var client = await SeededClient();

        var (skus, total) = await Get(client, "?pageSize=50");

        Assert.Equal(5, skus.Length);
        Assert.Equal("12", total);   // the total is not affected by the cap
    }

    [Fact]
    public async Task Invalid_page_values_are_corrected()
    {
        var client = await SeededClient();

        var (skus, _) = await Get(client, "?page=-3&pageSize=0");

        Assert.Equal(["S01"], skus);   // page 1, page size 1
    }

    [Fact]
    public async Task Sorts_by_price_descending_with_id_as_tiebreaker()
    {
        var client = await SeededClient();

        var (skus, _) = await Get(client, "?sortBy=unitPrice&sortDir=desc&pageSize=5");

        // price 30: S02 S05 S08 S11 (by id), then the first price-20 row: S01
        Assert.Equal(["S02", "S05", "S08", "S11", "S01"], skus);
    }

    [Fact]
    public async Task Pages_never_repeat_or_skip_rows_when_sorting_by_a_tied_column()
    {
        var client = await SeededClient();

        var all = new List<string>();
        for (var page = 1; page <= 3; page++)
            all.AddRange((await Get(client, $"?sortBy=unitPrice&sortDir=desc&pageSize=5&page={page}")).Skus);

        Assert.Equal(
            ["S02", "S05", "S08", "S11", "S01", "S04", "S07", "S10", "S03", "S06", "S09", "S12"],
            all);
    }

    [Fact]
    public async Task Sorts_by_total_stock_quantity()
    {
        var client = await SeededClient();

        var (skus, _) = await Get(client, "?sortBy=quantityInStock&sortDir=desc&pageSize=3");

        Assert.Equal(["S12", "S11", "S10"], skus);
    }

    [Fact]
    public async Task Unknown_sort_column_falls_back_to_id_order()
    {
        var client = await SeededClient();

        var (skus, _) = await Get(client, "?sortBy=PasswordHash;DROP&pageSize=3");

        Assert.Equal(["S01", "S02", "S03"], skus);
    }

    [Fact]
    public async Task Total_count_reflects_the_filters()
    {
        var client = await SeededClient();

        var (skus, total) = await Get(client, "?minQuantity=10");

        Assert.Equal(["S10", "S11", "S12"], skus);
        Assert.Equal("3", total);
    }

    [Fact]
    public async Task Total_count_header_is_exposed_to_browsers_through_cors()
    {
        var client = await SeededClient();
        var origin = _factory.Services.GetRequiredService<IConfiguration>()
            .GetSection("Cors:AllowedOrigins").Get<string[]>()!.First();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/products");
        request.Headers.Add("Origin", origin);
        var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("Access-Control-Expose-Headers", out var exposed));
        Assert.Contains("X-Total-Count", string.Join(",", exposed!));
    }
}