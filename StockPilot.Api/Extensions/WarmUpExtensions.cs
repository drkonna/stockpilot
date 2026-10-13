using Microsoft.EntityFrameworkCore;
using StockPilot.Api.Data;

namespace StockPilot.Api.Extensions;

public static class WarmUpExtensions
{
    // Runs one trivial query at startup so the expensive first-time work
    // (opening the first DB connection, building the EF model, JIT) happens
    // before the first real user request instead of during it.
    public static async Task WarmUpDatabaseAsync(this WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Products.AsNoTracking().Select(p => p.Id).Take(1).ToListAsync();
        }
        catch (Exception ex)
        {
            // A failed warm-up must never stop the app from starting.
            app.Logger.LogWarning(ex, "Database warm-up failed; continuing startup.");
        }
    }
}