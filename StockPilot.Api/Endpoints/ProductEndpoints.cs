using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

using StockPilot.Api.Data;
using StockPilot.Api.Dtos;
using StockPilot.Api.Models;
using static StockPilot.Api.Helpers.ValidationHelper;
using StockPilot.Api.Helpers;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using StockPilot.Api.Services.Import;
using Microsoft.Extensions.Options;


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

    // Only whitelisted columns can be sorted. The final ThenBy(Id) breaks ties, so the same
// row can never appear on two pages (or be skipped) when many rows share a value.
private static IOrderedQueryable<Product> ApplySort(IQueryable<Product> query, string? sortBy, bool descending) =>
    (sortBy?.ToLowerInvariant(), descending) switch
    {
        ("name", false) => query.OrderBy(p => p.Name).ThenBy(p => p.Id),
        ("name", true) => query.OrderByDescending(p => p.Name).ThenBy(p => p.Id),
        ("sku", false) => query.OrderBy(p => p.Sku).ThenBy(p => p.Id),
        ("sku", true) => query.OrderByDescending(p => p.Sku).ThenBy(p => p.Id),
        ("quantityinstock", false) => query.OrderBy(p => p.ProductStocks.Sum(ps => ps.Quantity)).ThenBy(p => p.Id),
        ("quantityinstock", true) => query.OrderByDescending(p => p.ProductStocks.Sum(ps => ps.Quantity)).ThenBy(p => p.Id),
        ("unitprice", false) => query.OrderBy(p => p.UnitPrice).ThenBy(p => p.Id),
        ("unitprice", true) => query.OrderByDescending(p => p.UnitPrice).ThenBy(p => p.Id),
        _ => query.OrderBy(p => p.Id),
    };


    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products").WithTags("Products").RequireAuthorization();
        group.MapGet("/", async (
            AppDbContext db,
            HttpResponse response,
            IOptions<PagingOptions> paging,
            string? search,
            int? minQuantity,
            int? maxQuantity,
            decimal? minPrice,
            decimal? maxPrice,
            string? sortBy,
            string? sortDir,
            int page = 1,
            int pageSize = 10) =>
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

            // Out-of-range values are corrected instead of rejected
            page = Math.Clamp(page, 1, 1_000_000);
            pageSize = Math.Clamp(pageSize, 1, paging.Value.MaxPageSize);

            // The total is counted on the filtered query, before Skip/Take
            response.Headers["X-Total-Count"] = (await query.CountAsync()).ToString();

            var descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

            return await ApplySort(query, sortBy, descending)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ProductToDto)
                .ToListAsync();
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

            var sku = dto.Sku.Trim();
            var skuKey = sku.ToLower();
            if (await db.Products.AnyAsync(p => p.Sku.ToLower() == skuKey))
                return Results.Conflict(new { message = "Υπάρχει ήδη προϊόν με αυτό το SKU." });

            var product = new Product
            {
                Name = dto.Name,
                Sku = sku,
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

            var sku = dto.Sku.Trim();
            var skuKey = sku.ToLower();
            if (await db.Products.AnyAsync(p => p.Id != id && p.Sku.ToLower() == skuKey))
                return Results.Conflict(new { message = "Υπάρχει ήδη άλλο προϊόν με αυτό το SKU." });

            var product = await db.Products.FindAsync(id);
            if (product is null) return Results.NotFound();

            product.Name = dto.Name;
            product.Sku = sku;
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
            IOptions<ImportOptions> options,
            ClaimsPrincipal user,
            CancellationToken ct,
            bool dryRun = false) =>
        {
            if (file.Length == 0)
                return Results.BadRequest(new { message = "Το αρχείο είναι κενό." });
            if (file.Length > options.Value.MaxFileSizeMb * 1024 * 1024)
                return Results.BadRequest(new { message = $"Το αρχείο ξεπερνά το επιτρεπτό μέγεθος ({options.Value.MaxFileSizeMb} MB)." });

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