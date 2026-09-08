using StockPilot.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Προσωρινή λίστα στη μνήμη — θα αντικατασταθεί από PostgreSQL + EF Core στις εβδομάδες 3-4
var products = new List<Product>
{
    new Product { Id = 1, Name = "Wireless Mouse", Sku = "WM-001", Price = 19.99m, QuantityInStock = 150 },
    new Product { Id = 2, Name = "Mechanical Keyboard", Sku = "MK-002", Price = 89.50m, QuantityInStock = 60 },
    new Product { Id = 3, Name = "USB-C Hub", Sku = "UCH-003", Price = 34.00m, QuantityInStock = 200 }
};

app.MapGet("/products", () => products)
    .WithName("GetProducts");

app.MapGet("/products/{id}", (int id) =>
{
    var product = products.FirstOrDefault(p => p.Id == id);
    return product is not null ? Results.Ok(product) : Results.NotFound();
})
    .WithName("GetProductById");

app.MapPost("/products", (Product newProduct) =>
{
    newProduct.Id = products.Count > 0 ? products.Max(p => p.Id) + 1 : 1;
    products.Add(newProduct);
    return Results.Created($"/products/{newProduct.Id}", newProduct);
})
    .WithName("CreateProduct");

app.Run();