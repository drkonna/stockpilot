using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

using StockPilot.Api.Data;
using StockPilot.Api.Dtos;
using StockPilot.Api.Models;
using static StockPilot.Api.Helpers.ValidationHelper;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using StockPilot.Api.Services.Import;


namespace StockPilot.Api.Endpoints;



public static class ProductEndpoints
{
    private static readonly Expression<Func<Product, ProductResponseDto>> ProductToDto = p => new ProductResponseDto
    {
        Id = p.Id,
        Name = p.Name,
        Sku = p.Sku,
        QuantityInStock = p.ProductStocks.Sum(ps => ps.Quantity),
        UnitPrice = p.UnitPrice,
        CreatedAt = p.CreatedAt,
        CategoryId = p.CategoryId,
        CategoryName = p.Category != null ? p.Category.Name : null,
        SupplierId = p.SupplierId,
        SupplierName = p.Supplier!.Name,
        ProductFamilyId = p.ProductFamilyId,
        ProductFamilyName = p.ProductFamily != null ? p.ProductFamily.Name : null,
        Color = p.Color,
        Size = p.Size
    };

    private const long MaxImportFileBytes = 5 * 1024 * 1024; // 5 MB
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products").WithTags("Products").RequireAuthorization();
        group.MapGet("/", async (
            AppDbContext db,
            string? search,
            int? minQuantity,
            int? maxQuantity,
            decimal? minPrice,
            decimal? maxPrice) =>
        {
            var query = db.Products.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p =>
                    EF.Functions.ILike(p.Name, $"%{search}%") ||
                    EF.Functions.ILike(p.Sku, $"%{search}%"));

            if (minQuantity.HasValue)
                query = query.Where(p => p.ProductStocks.Sum(ps => ps.Quantity) >= minQuantity);
            if (maxQuantity.HasValue)
                query = query.Where(p => p.ProductStocks.Sum(ps => ps.Quantity) <= maxQuantity);

            if (minPrice.HasValue)
                query = query.Where(p => p.UnitPrice >= minPrice);

            if (maxPrice.HasValue)
                query = query.Where(p => p.UnitPrice <= maxPrice);

            return await query.Select(ProductToDto).ToListAsync();
        })
        .WithName("GetProducts");

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
        {
            var product = await db.Products
                .Where(p => p.Id == id)
                .Select(ProductToDto)
                .FirstOrDefaultAsync();

            return product is not null ? Results.Ok(product) : Results.NotFound();
        })
        .WithName("GetProductById");

        group.MapPost("/", async (CreateProductDto dto, AppDbContext db) =>
        {
            if (!TryValidate(dto, out var errors))
                return Results.ValidationProblem(errors);

            if (dto.CategoryId.HasValue && !await db.Categories.AnyAsync(c => c.Id == dto.CategoryId))
                return Results.BadRequest(new { message = "Η κατηγορία δεν υπάρχει." });

            if (!await db.Suppliers.AnyAsync(s => s.Id == dto.SupplierId))
                return Results.BadRequest(new { message = "Ο προμηθευτής δεν υπάρχει." });

            if (dto.ProductFamilyId.HasValue && !await db.ProductFamilies.AnyAsync(f => f.Id == dto.ProductFamilyId))
                return Results.BadRequest(new { message = "Η product family δεν υπάρχει." });

            var product = new Product
            {
                Name = dto.Name,
                Sku = dto.Sku,
                UnitPrice = dto.UnitPrice,
                CategoryId = dto.CategoryId,
                SupplierId = dto.SupplierId,
                ProductFamilyId = dto.ProductFamilyId,
                Color = dto.Color,
                Size = dto.Size,
                CreatedAt = DateTime.UtcNow
            };

            db.Products.Add(product);
            await db.SaveChangesAsync();

            var centralStore = await db.Stores.FirstAsync(s => s.IsCentral);
            db.ProductStocks.Add(new ProductStock
            {
                ProductId = product.Id,
                StoreId = centralStore.Id,
                Quantity = dto.QuantityInStock
            });
            await db.SaveChangesAsync();

            var responseDto = await db.Products
                .Where(p => p.Id == product.Id)
                .Select(ProductToDto)
                .FirstAsync();
            return Results.Created($"/products/{product.Id}", responseDto);
        })
        .WithName("CreateProduct");

        group.MapPut("/{id}", async (int id, UpdateProductDto dto, AppDbContext db) =>
        {
            if (!TryValidate(dto, out var errors))
                return Results.ValidationProblem(errors);

            if (dto.CategoryId.HasValue && !await db.Categories.AnyAsync(c => c.Id == dto.CategoryId))
                return Results.BadRequest(new { message = "Η κατηγορία δεν υπάρχει." });

            if (!await db.Suppliers.AnyAsync(s => s.Id == dto.SupplierId))
                return Results.BadRequest(new { message = "Ο προμηθευτής δεν υπάρχει." });

            if (dto.ProductFamilyId.HasValue && !await db.ProductFamilies.AnyAsync(f => f.Id == dto.ProductFamilyId))
                return Results.BadRequest(new { message = "Η product family δεν υπάρχει." });

            var product = await db.Products.FindAsync(id);
            if (product is null) return Results.NotFound();

            product.Name = dto.Name;
            product.Sku = dto.Sku;
            product.UnitPrice = dto.UnitPrice;
            product.CategoryId = dto.CategoryId;
            product.SupplierId = dto.SupplierId;
            product.ProductFamilyId = dto.ProductFamilyId;
            product.Color = dto.Color;
            product.Size = dto.Size;

            var centralStore = await db.Stores.FirstAsync(s => s.IsCentral);
            var stock = await db.ProductStocks
                .FirstOrDefaultAsync(ps => ps.ProductId == product.Id && ps.StoreId == centralStore.Id);

            if (stock is null)
            {
                stock = new ProductStock { ProductId = product.Id, StoreId = centralStore.Id };
                db.ProductStocks.Add(stock);
            }
            stock.Quantity = dto.QuantityInStock;

            await db.SaveChangesAsync();
            var responseDto = await db.Products
                .Where(p => p.Id == product.Id)
                .Select(ProductToDto)
                .FirstAsync();
            return Results.Ok(responseDto);
        })
        .WithName("UpdateProduct");

        group.MapDelete("/{id}", async (int id, AppDbContext db) =>
        {
            var product = await db.Products.FindAsync(id);
            if (product is null) return Results.NotFound();

            db.Products.Remove(product);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .WithName("DeleteProduct")
        .RequireAuthorization(policy => policy.RequireRole("Admin"));

        group.MapPost("/import", async (
        IFormFile file,
        ProductImportService importService,
        ClaimsPrincipal user,
        CancellationToken ct,
        bool dryRun = false) =>
    {
        if (file.Length == 0)
            return Results.BadRequest(new { message = "Το αρχείο είναι κενό." });
        if (file.Length > MaxImportFileBytes)
            return Results.BadRequest(new { message = "Το αρχείο ξεπερνά το επιτρεπτό μέγεθος (5 MB)." });

        // The user id travels in the token's "sub" claim (see TokenService); the framework may
        // expose it under either name, so both are checked.
        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        int? userId = int.TryParse(idClaim, out var id) ? id : null;

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await importService.ImportAsync(stream, file.FileName, userId, dryRun, ct);
            return Results.Ok(new { dryRun, result.Added, result.Updated, result.Failed, result.Errors });
        }
        catch (ImportFileException ex)
        {
            // Whole-file problems (bad name, unknown store, missing columns, stale date...)
            return Results.BadRequest(new { message = ex.Message });
        }
    })
    .WithName("ImportProducts")
    .DisableAntiforgery()
    .RequireAuthorization(policy => policy.RequireRole("Admin"));
        return app;
    }


}