using Microsoft.EntityFrameworkCore;
using StockPilot.Api.Data;
using System.Diagnostics;

namespace StockPilot.Api.Extensions;

public static class WarmUpExtensions
{
    // Runs one trivial query at startup so the expensive first-time work
    // (opening the first DB connection, building the EF model, JIT) happens
    // before the first real user request instead of during it.
    public static async Task WarmUpDatabaseAsync(this WebApplication app)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Products.AsNoTracking().AnyAsync();

            app.Logger.LogInformation("Database warm-up completed in {ElapsedMs} ms", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            // A failed warm-up must never stop the app from starting.
            app.Logger.LogWarning(ex, "Database warm-up failed after {ElapsedMs} ms; continuing startup.",
                sw.ElapsedMilliseconds);
        }
    }
}