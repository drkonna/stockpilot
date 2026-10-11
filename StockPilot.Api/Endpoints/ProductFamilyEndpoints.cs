using Microsoft.EntityFrameworkCore;

using StockPilot.Api.Data;
using StockPilot.Api.Dtos;
using StockPilot.Api.Models;
using static StockPilot.Api.Helpers.ValidationHelper;




namespace StockPilot.Api.Endpoints;

public static class ProductFamilyEndpoints
{
    public static IEndpointRouteBuilder MapProductFamilyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/productfamilies").WithTags("ProductFamilies").RequireAuthorization();
        group.MapGet("/", async (AppDbContext db) =>
        await db.ProductFamilies
            .Select(f => new ProductFamilyResponseDto { Id = f.Id, Name = f.Name })
            .ToListAsync())
        .WithName("GetProductFamilies");

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
        {
            var family = await db.ProductFamilies
                .Where(f => f.Id == id)
                .Select(f => new ProductFamilyResponseDto { Id = f.Id, Name = f.Name })
                .FirstOrDefaultAsync();
            return family is not null ? Results.Ok(family) : Results.NotFound();
        })
            .WithName("GetProductFamilyById");

        group.MapPost("/", async (CreateProductFamilyDto dto, AppDbContext db) =>
        {
            if (!TryValidate(dto, out var errors))
                return Results.ValidationProblem(errors);

            var family = new ProductFamily { Name = dto.Name };
            db.ProductFamilies.Add(family);
            await db.SaveChangesAsync();
            return Results.Created($"/productfamilies/{family.Id}", family);
        })
            .WithName("CreateProductFamily");

        group.MapPut("/{id}", async (int id, UpdateProductFamilyDto dto, AppDbContext db) =>
        {
            if (!TryValidate(dto, out var errors))
                return Results.ValidationProblem(errors);

            var family = await db.ProductFamilies.FindAsync(id);
            if (family is null) return Results.NotFound();

            family.Name = dto.Name;
            await db.SaveChangesAsync();
            return Results.Ok(family);
        })
            .WithName("UpdateProductFamily");

        group.MapDelete("/{id}", async (int id, AppDbContext db) =>
        {
            var family = await db.ProductFamilies.FindAsync(id);
            if (family is null) return Results.NotFound();

            db.ProductFamilies.Remove(family);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
            .WithName("DeleteProductFamily")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));
        return app;
    }
}