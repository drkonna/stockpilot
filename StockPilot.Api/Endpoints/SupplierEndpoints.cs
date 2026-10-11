using Microsoft.EntityFrameworkCore;

using StockPilot.Api.Data;
using StockPilot.Api.Dtos;
using StockPilot.Api.Models;
using static StockPilot.Api.Helpers.ValidationHelper;




namespace StockPilot.Api.Endpoints;

public static class SupplierEndpoints
{
    public static IEndpointRouteBuilder MapSupplierEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/suppliers").WithTags("Suppliers").RequireAuthorization();
        group.MapGet("/", async (AppDbContext db) =>
        await db.Suppliers
            .Select(s => new SupplierResponseDto { Id = s.Id, Name = s.Name })
            .ToListAsync())
        .WithName("GetSuppliers");

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
        {
            var supplier = await db.Suppliers
                .Where(s => s.Id == id)
                .Select(s => new SupplierResponseDto { Id = s.Id, Name = s.Name })
                .FirstOrDefaultAsync();
            return supplier is not null ? Results.Ok(supplier) : Results.NotFound();
        })
            .WithName("GetSupplierById");

        group.MapPost("/", async (CreateSupplierDto dto, AppDbContext db) =>
        {
            if (!TryValidate(dto, out var errors))
                return Results.ValidationProblem(errors);

            var supplier = new Supplier { Name = dto.Name };
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync();
            return Results.Created($"/suppliers/{supplier.Id}", supplier);
        })
            .WithName("CreateSupplier");

        group.MapPut("/{id}", async (int id, UpdateSupplierDto dto, AppDbContext db) =>
        {
            if (!TryValidate(dto, out var errors))
                return Results.ValidationProblem(errors);

            var supplier = await db.Suppliers.FindAsync(id);
            if (supplier is null) return Results.NotFound();

            supplier.Name = dto.Name;
            await db.SaveChangesAsync();
            return Results.Ok(supplier);
        })
            .WithName("UpdateSupplier");

        group.MapDelete("/{id}", async (int id, AppDbContext db) =>
        {
            var supplier = await db.Suppliers.FindAsync(id);
            if (supplier is null) return Results.NotFound();

            db.Suppliers.Remove(supplier);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
            .WithName("DeleteSupplier")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));
        return app;
    }
}