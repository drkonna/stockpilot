using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockPilot.Api.Data;
using StockPilot.Api.Models;

namespace StockPilot.Api.Tests;

public static class TestAuth
{
    public const string Password = "Passw0rd!";

    // Creates a user with the given role straight in the database, then logs in for real.
    public static async Task<string> LoginAs(StockPilotApiFactory factory, string role)
    {
        var email = $"{Guid.NewGuid():N}@test.com";
        using (var scope = factory.Services.CreateScope())
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

        var response = await factory.CreateClient().PostAsJsonAsync("/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return body!["token"];
    }
}