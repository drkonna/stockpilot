using StockPilot.Api.Models;
using Microsoft.EntityFrameworkCore;
using StockPilot.Api.Data;
using Scalar.AspNetCore;
using System.ComponentModel.DataAnnotations;
using StockPilot.Api.Dtos;
using StockPilot.Api.Middleware;
using StockPilot.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;


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

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSingleton<ITokenService, TokenService>();builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();
var app = builder.Build();app.MapScalarApiReference();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

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
    .WithName("CreateProduct")
    .RequireAuthorization();

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
    .WithName("UpdateProduct")
    .RequireAuthorization();

app.MapDelete("/products/{id}", async (int id, AppDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    if (product is null) return Results.NotFound();

    db.Products.Remove(product);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
    .WithName("DeleteProduct")
    .RequireAuthorization();

app.MapGet("/categories", async (AppDbContext db) =>
    await db.Categories.ToListAsync())
    .WithName("GetCategories");

app.MapGet("/categories/{id}", async (int id, AppDbContext db) =>
{
    var category = await db.Categories.FindAsync(id);
    return category is not null ? Results.Ok(category) : Results.NotFound();
})
    .WithName("GetCategoryById")
    .RequireAuthorization();

app.MapPost("/categories", async (CreateCategoryDto dto, AppDbContext db) =>
{
    if (!TryValidate(dto, out var errors))
        return Results.ValidationProblem(errors);

    var category = new Category { Name = dto.Name };
    db.Categories.Add(category);
    await db.SaveChangesAsync();
    return Results.Created($"/categories/{category.Id}", category);
})
    .WithName("CreateCategory")
    .RequireAuthorization();

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
    .WithName("UpdateCategory")
    .RequireAuthorization();

app.MapDelete("/categories/{id}", async (int id, AppDbContext db) =>
{
    var category = await db.Categories.FindAsync(id);
    if (category is null) return Results.NotFound();

    db.Categories.Remove(category);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
    .WithName("DeleteCategory")
    .RequireAuthorization(policy => policy.RequireRole("Admin"));

app.MapPost("/auth/register", async (RegisterDto dto, AppDbContext db, ILogger<Program> logger) =>
{
    if (!TryValidate(dto, out var errors))
        return Results.ValidationProblem(errors);

    var emailTaken = await db.Users.AnyAsync(u => u.Email == dto.Email);
    if (emailTaken){
        logger.LogWarning("Προσπάθεια εγγραφής με ήδη υπαρκτό email: {Email}", dto.Email);
        return Results.Conflict(new { message = "Υπάρχει ήδη χρήστης με αυτό το email." });
    }

    var user = new User
    {
        Email = dto.Email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
        CreatedAt = DateTime.UtcNow
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    logger.LogInformation("Νέος χρήστης εγγράφηκε: {Email} (Id: {UserId})", user.Email, user.Id);
    
    return Results.Created($"/users/{user.Id}", new { user.Id, user.Email, user.Role, user.CreatedAt });
})
    .WithName("Register");

app.MapPost("/auth/login", async (LoginDto dto, AppDbContext db, ITokenService tokenService, ILogger<Program> logger) =>
{
    if (!TryValidate(dto, out var errors))
        return Results.ValidationProblem(errors);

    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
    if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash)){
        logger.LogWarning("Αποτυχημένη προσπάθεια login για {Email}", dto.Email);
        return Results.Unauthorized();
            
    }
    logger.LogInformation("Επιτυχές login για {Email}", user.Email);
    var token = tokenService.GenerateToken(user);
    return Results.Ok(new { token });
})
    .WithName("Login");
app.Run();