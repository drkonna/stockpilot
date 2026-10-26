using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockPilot.Api.Data;
using StockPilot.Api.Models;

namespace StockPilot.Api.Tests;

public class ProductSkuUniquenessTests : IClassFixture<StockPilotApiFactory>
{
    private const string Password = "Passw0rd!";
    private readonly StockPilotApiFactory _factory;

    public ProductSkuUniquenessTests(StockPilotApiFactory factory) => _factory = factory;

    // Logged-in client plus a supplier to attach products to
    private async Task<(HttpClient Client, int SupplierId)> Setup()
    {
        var email = $"{Guid.NewGuid():N}@test.com";
        int supplierId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var central = await db.Stores.FirstAsync(s => s.IsCentral);
            db.Users.Add(new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                Role = "User",
                StoreId = central.Id
            });
            var supplier = new Supplier { Name = "Acme" };
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync();
            supplierId = supplier.Id;
        }

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!["token"]);
        return (client, supplierId);
    }

    // Upper-case on purpose, so that sku.ToLower() really differs from sku
    private static string NewSku() => "U" + Guid.NewGuid().ToString("N")[..8].ToUpper();

    private static object Body(int supplierId, string sku) =>
        new { name = "Coffee", sku, quantityInStock = 1, unitPrice = 12.5m, supplierId };

    private static async Task<int> CreateAndGetId(HttpClient client, int supplierId, string sku)
    {
        var response = await client.PostAsJsonAsync("/products", Body(supplierId, sku));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Create_with_existing_sku_returns_409_ignoring_case_and_spaces()
    {
        var (client, supplierId) = await Setup();
        var sku = NewSku();
        await CreateAndGetId(client, supplierId, sku);

        foreach (var duplicate in new[] { sku, sku.ToLower(), $"  {sku}  " })
        {
            var response = await client.PostAsJsonAsync("/products", Body(supplierId, duplicate));
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
    }

    [Fact]
    public async Task Update_to_sku_of_another_product_returns_409_but_keeping_own_sku_is_fine()
    {
        var (client, supplierId) = await Setup();
        var skuA = NewSku();
        var skuB = NewSku();
        var idA = await CreateAndGetId(client, supplierId, skuA);
        await CreateAndGetId(client, supplierId, skuB);

        var clash = await client.PutAsJsonAsync($"/products/{idA}", Body(supplierId, skuB));
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);

        // Saving a product with its own SKU (even with different casing) must not clash with itself
        var own = await client.PutAsJsonAsync($"/products/{idA}", Body(supplierId, skuA.ToLower()));
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    }
}