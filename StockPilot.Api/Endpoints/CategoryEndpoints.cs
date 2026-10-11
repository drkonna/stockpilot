using Microsoft.EntityFrameworkCore;

using StockPilot.Api.Data;
using StockPilot.Api.Dtos;
using StockPilot.Api.Models;
using static StockPilot.Api.Helpers.ValidationHelper;




namespace StockPilot.Api.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/categories").WithTags("Categories").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db) =>
            await db.Categories
                .Select(c => new CategoryResponseDto { Id = c.Id, Name = c.Name })
                .ToListAsync())
            .WithName("GetCategories");

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
        {
            var category = await db.Categories
                .Where(c => c.Id == id)
                .Select(c => new CategoryResponseDto { Id = c.Id, Name = c.Name })
                .FirstOrDefaultAsync();

            return category is not null ? Results.Ok(category) : Results.NotFound();
        })
            .WithName("GetCategoryById");
            

        group.MapPost("/", async (CreateCategoryDto dto, AppDbContext db) =>
        {
            if (!TryValidate(dto, out var errors))
                return Results.ValidationProblem(errors);

            var category = new Category { Name = dto.Name };
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            return Results.Created($"/categories/{category.Id}", category);
        })
            .WithName("CreateCategory");

        group.MapPut("/{id}", async (int id, UpdateCategoryDto dto, AppDbContext db) =>
        {
            if (!TryValidate(dto, out var errors))
                return Results.ValidationProblem(errors);

            var category = await db.Categories.FindAsync(id);
            if (category is null) return Results.NotFound();

            category.Name = dto.Name;
            await db.SaveChangesAsync();
            return Results.Ok(category);
        })
            .WithName("UpdateCategory");

        group.MapDelete("/{id}", async (int id, AppDbContext db) =>
        {
            var category = await db.Categories.FindAsync(id);
            if (category is null) return Results.NotFound();

            db.Categories.Remove(category);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
            .WithName("DeleteCategory")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));
        return app;
    }
}