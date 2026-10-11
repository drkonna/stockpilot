using Microsoft.EntityFrameworkCore;
using StockPilot.Api.Data;
using StockPilot.Api.Dtos;
using StockPilot.Api.Models;
using static StockPilot.Api.Helpers.ValidationHelper;

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
                        .Sum(ps => ps.Quantity)
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
            await db.SaveChangesAsync();

            return Results.Ok(new StoreStockDto
            {
                StoreId = store.Id,
                StoreName = store.Name,
                StoreCode = store.Code,
                IsCentral = store.IsCentral,
                Quantity = stock.Quantity
            });
        })
            .WithName("SetProductStock")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        return app;
    }
}