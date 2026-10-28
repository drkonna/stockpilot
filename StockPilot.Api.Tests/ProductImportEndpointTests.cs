using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockPilot.Api.Data;
using StockPilot.Api.Models;

namespace StockPilot.Api.Tests;

public class ProductImportEndpointTests : IClassFixture<StockPilotApiFactory>
{
    private const string Password = "Passw0rd!";
    private const string Header = "Sku,Name,UnitPrice,Quantity,Category,Supplier,ProductFamily,Color,Size";

    private readonly StockPilotApiFactory _factory;

    public ProductImportEndpointTests(StockPilotApiFactory factory) => _factory = factory;

    // Creates a user with the given role straight in the database, then logs in for real.
    private async Task<string> LoginAs(string role)
    {
        var email = $"{Guid.NewGuid():N}@test.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var central = await db.Stores.FirstAsync(s => s.IsCentral);
            db.Users.Add(new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                Role = role,
                StoreId = central.Id
            });
            await db.SaveChangesAsync();
        }

        var response = await _factory.CreateClient().PostAsJsonAsync("/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return body!["token"];
    }

    private async Task<HttpResponseMessage> Upload(string? token, string fileName, string csv, string query = "")
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", fileName);   // "file" = the name of the endpoint parameter

        using var request = new HttpRequestMessage(HttpMethod.Post, "/products/import" + query) { Content = content };
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await _factory.CreateClient().SendAsync(request);
    }

    private static string NewSku() => "T" + Guid.NewGuid().ToString("N")[..8];

    private async Task<bool> ProductExists(string sku)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Products.AnyAsync(p => p.Sku == sku);
    }

    

    [Fact]
    public async Task File_over_the_size_limit_returns_400_with_the_limit_in_the_message()
    {
        var token = await LoginAs("Admin");
        var oversized = Header + "\n" + new string('x', 5 * 1024 * 1024 + 1);

        var response = await Upload(token, "MAIN_141026.csv", oversized);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("5 MB", json.GetProperty("message").GetString());
    }
    
    [Fact]
    public async Task Upload_without_token_returns_401()
    {
        var response = await Upload(null, "MAIN_141026.csv", $"{Header}\n{NewSku()},Coffee,12.50,1,,Acme,,,");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_as_non_admin_returns_403()
    {
        var token = await LoginAs("User");

        var response = await Upload(token, "MAIN_141026.csv", $"{Header}\n{NewSku()},Coffee,12.50,1,,Acme,,,");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Upload_as_admin_imports_the_file_and_returns_statistics()
    {
        var token = await LoginAs("Admin");
        var sku = NewSku();

        var response = await Upload(token, "MAIN_141026.csv",
            $"{Header}\n{sku},Coffee,12.50,10,Drinks,Acme,,,\n,NoSku,5,1,,Acme,,,");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, json.GetProperty("added").GetInt32());
        Assert.Equal(1, json.GetProperty("failed").GetInt32());
        Assert.Equal(3, json.GetProperty("errors")[0].GetProperty("row").GetInt32());
        Assert.True(await ProductExists(sku));
    }

    [Fact]
    public async Task Upload_with_invalid_file_name_returns_400_with_message()
    {
        var token = await LoginAs("Admin");

        var response = await Upload(token, "products.csv", $"{Header}\n{NewSku()},Coffee,12.50,1,,Acme,,,");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Dry_run_returns_statistics_but_saves_nothing()
    {
        var token = await LoginAs("Admin");
        var sku = NewSku();

        var response = await Upload(token, "MAIN_141026.csv",
            $"{Header}\n{sku},Coffee,12.50,10,Drinks,Acme,,,", "?dryRun=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("dryRun").GetBoolean());
        Assert.Equal(1, json.GetProperty("added").GetInt32());
        Assert.False(await ProductExists(sku));
    }
}