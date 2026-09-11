using StockPilot.Api.Models;
using Microsoft.EntityFrameworkCore;
using StockPilot.Api.Data;
using Scalar.AspNetCore;
using System.ComponentModel.DataAnnotations;
using StockPilot.Api.Dtos;

static bool TryValidate<T>(T model, out IDictionary<string, string[]> errors)
{
    var context = new ValidationContext(model!);
    var results = new List<ValidationResult>();
    var isValid = Validator.TryValidateObject(model!, context, results, validateAllProperties: true);

    errors = results
        .SelectMany(r => r.MemberNames.Select(memberName => new { memberName, r.ErrorMessage }))
        .GroupBy(x => x.memberName)
        .ToDictionary(
            g => g.Key,
            g => g.Select(x => x.ErrorMessage ?? "Μη έγκυρη τιμή.").ToArray());

    return isValid;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();app.MapScalarApiReference();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/products", async (AppDbContext db) =>
    await db.Products.ToListAsync())
    .WithName("GetProducts");

app.MapGet("/products/{id}", async (int id, AppDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    return product is not null ? Results.Ok(product) : Results.NotFound();
})
    .WithName("GetProductById");

app.MapPost("/products", async (CreateProductDto dto, AppDbContext db) =>
{
    if (!TryValidate(dto, out var errors))
        return Results.ValidationProblem(errors);

    var product = new Product
    {
        Name = dto.Name,
        Sku = dto.Sku,
        QuantityInStock = dto.QuantityInStock,
        UnitPrice = dto.UnitPrice,
        CategoryId = dto.CategoryId,
        CreatedAt = DateTime.UtcNow
    };

    db.Products.Add(product);
    await db.SaveChangesAsync();
    return Results.Created($"/products/{product.Id}", product);
})
    .WithName("CreateProduct");

app.MapPut("/products/{id}", async (int id, UpdateProductDto dto, AppDbContext db) =>
{
    if (!TryValidate(dto, out var errors))
        return Results.ValidationProblem(errors);

    var product = await db.Products.FindAsync(id);
    if (product is null) return Results.NotFound();

    product.Name = dto.Name;
    product.Sku = dto.Sku;
    product.QuantityInStock = dto.QuantityInStock;
    product.UnitPrice = dto.UnitPrice;
    product.CategoryId = dto.CategoryId;

    await db.SaveChangesAsync();
    return Results.Ok(product);
})
    .WithName("UpdateProduct");

app.MapDelete("/products/{id}", async (int id, AppDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    if (product is null) return Results.NotFound();

    db.Products.Remove(product);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
    .WithName("DeleteProduct");

app.MapGet("/categories", async (AppDbContext db) =>
    await db.Categories.ToListAsync())
    .WithName("GetCategories");

app.MapGet("/categories/{id}", async (int id, AppDbContext db) =>
{
    var category = await db.Categories.FindAsync(id);
    return category is not null ? Results.Ok(category) : Results.NotFound();
})
    .WithName("GetCategoryById");

app.MapPost("/categories", async (CreateCategoryDto dto, AppDbContext db) =>
{
    if (!TryValidate(dto, out var errors))
        return Results.ValidationProblem(errors);

    var category = new Category { Name = dto.Name };
    db.Categories.Add(category);
    await db.SaveChangesAsync();
    return Results.Created($"/categories/{category.Id}", category);
})
    .WithName("CreateCategory");

app.MapPut("/categories/{id}", async (int id, UpdateCategoryDto dto, AppDbContext db) =>
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

app.MapDelete("/categories/{id}", async (int id, AppDbContext db) =>
{
    var category = await db.Categories.FindAsync(id);
    if (category is null) return Results.NotFound();

    db.Categories.Remove(category);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
    .WithName("DeleteCategory");
app.Run();