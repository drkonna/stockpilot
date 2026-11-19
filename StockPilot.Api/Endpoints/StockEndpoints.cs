using Microsoft.EntityFrameworkCore;
using StockPilot.Api.Data;
using StockPilot.Api.Dtos;
using StockPilot.Api.Models;
using static StockPilot.Api.Helpers.ValidationHelper;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace StockPilot.Api.Endpoints;

public static class StockEndpoints
{
    public static IEndpointRouteBuilder MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/products/{id}/stock", async (int id, AppDbContext db) =>
        {
            var product = await db.Products
                .Where(p => p.Id == id)
                .Select(p => new { p.Id, p.Name, p.Sku })
                .FirstOrDefaultAsync();

            if (product is null) return Results.NotFound();

            // Ξεκινάμε από τα Stores, ώστε και τα καταστήματα χωρίς ProductStock row
            // να εμφανίζονται (με 0). Το Sum δίνει 0 όταν δεν υπάρχει row.
            var stores = await db.Stores
                .OrderByDescending(s => s.IsCentral)
                .ThenBy(s => s.Name)
                .Select(s => new StoreStockDto
                {
                    StoreId = s.Id,
                    StoreName = s.Name,
                    StoreCode = s.Code,
                    IsCentral = s.IsCentral,
                    Quantity = db.ProductStocks
                        .Where(ps => ps.ProductId == id && ps.StoreId == s.Id)
                        .Sum(ps => ps.Quantity),
                    ReorderLevel = db.ProductStocks
                        .Where(ps => ps.ProductId == id && ps.StoreId == s.Id)
                        .Max(ps => ps.ReorderLevel)
                })
                .ToListAsync();

            return Results.Ok(new ProductStockResponseDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Sku = product.Sku,
                TotalQuantity = stores.Sum(s => s.Quantity),
                Stores = stores
            });
        })
            .WithName("GetProductStock")
            .RequireAuthorization();

        app.MapPut("/products/{productId}/stock/{storeId}", async (
            int productId, int storeId, SetStockDto dto, AppDbContext db) =>
        {
            if (!TryValidate(dto, out var errors))
                return Results.ValidationProblem(errors);

            if (!await db.Products.AnyAsync(p => p.Id == productId))
                return Results.NotFound(new { message = "Το προϊόν δεν υπάρχει." });

            var store = await db.Stores.FindAsync(storeId);
            if (store is null)
                return Results.NotFound(new { message = "Το κατάστημα δεν υπάρχει." });

            var stock = await db.ProductStocks
                .FirstOrDefaultAsync(ps => ps.ProductId == productId && ps.StoreId == storeId);

            if (stock is null)
            {
                stock = new ProductStock { ProductId = productId, StoreId = storeId };
                db.ProductStocks.Add(stock);
            }

            stock.Quantity = dto.Quantity!.Value;
            stock.ReorderLevel = dto.ReorderLevel;
            await db.SaveChangesAsync();

            return Results.Ok(new StoreStockDto
            {
                StoreId = store.Id,
                StoreName = store.Name,
                StoreCode = store.Code,
                IsCentral = store.IsCentral,
                Quantity = stock.Quantity,
                ReorderLevel = stock.ReorderLevel
            });
        })
            .WithName("SetProductStock")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        app.MapGet("/stock/low", async (AppDbContext db, ClaimsPrincipal user, int? storeId) =>
        {
            // Ο Admin βλέπει όλα τα καταστήματα (ή φιλτράρει με ?storeId=).
            // Ο απλός χρήστης βλέπει ΜΟΝΟ το δικό του κατάστημα, όποιο storeId κι αν στείλει.
            var scopeStoreId = storeId;
            if (!user.IsInRole("Admin"))
            {
                var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                if (!int.TryParse(idClaim, out var userId))
                    return Results.Unauthorized();

                scopeStoreId = await db.Users
                    .Where(u => u.Id == userId)
                    .Select(u => u.StoreId)
                    .FirstOrDefaultAsync();

                if (scopeStoreId is null)
                    return Results.Ok(Array.Empty<LowStockDto>());
            }

            var query = db.ProductStocks.AsNoTracking()
                .Where(ps => ps.ReorderLevel != null && ps.Quantity <= ps.ReorderLevel);

            if (scopeStoreId is not null)
                query = query.Where(ps => ps.StoreId == scopeStoreId);

            var items = await query
                .OrderBy(ps => ps.Quantity - ps.ReorderLevel)   // πρώτα τα πιο κρίσιμα
                .ThenBy(ps => ps.ProductId)
                .Select(ps => new LowStockDto
                {
                    ProductId = ps.ProductId,
                    ProductName = ps.Product!.Name,
                    Sku = ps.Product.Sku,
                    StoreId = ps.StoreId,
                    StoreName = ps.Store!.Name,
                    StoreCode = ps.Store.Code,
                    Quantity = ps.Quantity,
                    ReorderLevel = ps.ReorderLevel!.Value
                })
                .ToListAsync();

            return Results.Ok(items);
        })
            .WithName("GetLowStock")
            .RequireAuthorization();

        return app;
    }
}