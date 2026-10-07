using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using StockPilot.Api.Data;
using StockPilot.Api.Dtos;
using StockPilot.Api.Models;
using static StockPilot.Api.Helpers.ValidationHelper;

namespace StockPilot.Api.Endpoints;

public static class StoreEndpoints
{
    private static readonly Expression<Func<Store, StoreResponseDto>> StoreToDto = s => new StoreResponseDto
    {
        Id = s.Id,
        Name = s.Name,
        Code = s.Code,
        IsCentral = s.IsCentral
    };

    public static IEndpointRouteBuilder MapStoreEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/stores", async (AppDbContext db) =>
            await db.Stores
                .OrderByDescending(s => s.IsCentral)
                .ThenBy(s => s.Name)
                .Select(StoreToDto)
                .ToListAsync())
            .WithName("GetStores")
            .RequireAuthorization();

        app.MapGet("/stores/{id}", async (int id, AppDbContext db) =>
        {
            var store = await db.Stores
                .Where(s => s.Id == id)
                .Select(StoreToDto)
                .FirstOrDefaultAsync();

            return store is not null ? Results.Ok(store) : Results.NotFound();
        })
            .WithName("GetStoreById")
            .RequireAuthorization();

        app.MapPost("/stores", async (CreateStoreDto dto, AppDbContext db) =>
        {
            if (!TryValidate(dto, out var errors))
                return Results.ValidationProblem(errors);

            var name = dto.Name.Trim();
            var code = dto.Code.Trim().ToUpperInvariant();

            if (await db.Stores.AnyAsync(s => s.Code == code))
                return Results.Conflict(new { message = $"Υπάρχει ήδη κατάστημα με κωδικό '{code}'." });

            var store = new Store { Name = name, Code = code, IsCentral = false };
            db.Stores.Add(store);
            await db.SaveChangesAsync();

            var responseDto = await db.Stores
                .Where(s => s.Id == store.Id)
                .Select(StoreToDto)
                .FirstAsync();
            return Results.Created($"/stores/{store.Id}", responseDto);
        })
            .WithName("CreateStore")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        app.MapPut("/stores/{id}", async (int id, UpdateStoreDto dto, AppDbContext db) =>
        {
            if (!TryValidate(dto, out var errors))
                return Results.ValidationProblem(errors);

            var store = await db.Stores.FindAsync(id);
            if (store is null) return Results.NotFound();

            var name = dto.Name.Trim();
            var code = dto.Code.Trim().ToUpperInvariant();

            if (await db.Stores.AnyAsync(s => s.Code == code && s.Id != id))
                return Results.Conflict(new { message = $"Υπάρχει ήδη κατάστημα με κωδικό '{code}'." });

            store.Name = name;
            store.Code = code;
            await db.SaveChangesAsync();

            var responseDto = await db.Stores
                .Where(s => s.Id == store.Id)
                .Select(StoreToDto)
                .FirstAsync();
            return Results.Ok(responseDto);
        })
            .WithName("UpdateStore")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        app.MapDelete("/stores/{id}", async (int id, AppDbContext db) =>
        {
            var store = await db.Stores.FindAsync(id);
            if (store is null) return Results.NotFound();

            if (store.IsCentral)
                return Results.Conflict(new { message = "Το κεντρικό κατάστημα δεν μπορεί να διαγραφεί." });

            var inUse = await db.Users.AnyAsync(u => u.StoreId == id)
                     || await db.ProductStocks.AnyAsync(ps => ps.StoreId == id);
            if (inUse)
                return Results.Conflict(new { message = "Το κατάστημα έχει χρήστες ή απόθεμα και δεν μπορεί να διαγραφεί." });

            db.Stores.Remove(store);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
            .WithName("DeleteStore")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        return app;
    }
}