using Microsoft.EntityFrameworkCore;

using StockPilot.Api.Data;
using StockPilot.Api.Dtos;
using StockPilot.Api.Models;
using StockPilot.Api.Services;
using static StockPilot.Api.Helpers.ValidationHelper;




namespace StockPilot.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth").AllowAnonymous();
        group.MapPost("/register", async (RegisterDto dto, AppDbContext db, ILogger<Program> logger) =>
        {
            if (!TryValidate(dto, out var errors))
                return Results.ValidationProblem(errors);

            var emailTaken = await db.Users.AnyAsync(u => u.Email == dto.Email);
            if (emailTaken)
            {
                logger.LogWarning("Προσπάθεια εγγραφής με ήδη υπαρκτό email: {Email}", dto.Email);
                return Results.Conflict(new { message = "Υπάρχει ήδη χρήστης με αυτό το email." });
            }
            var centralStore = await db.Stores.FirstAsync(s => s.IsCentral);
            var user = new User
            {
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                StoreId = centralStore.Id,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            logger.LogInformation("Νέος χρήστης εγγράφηκε: {Email} (Id: {UserId})", user.Email, user.Id);

            return Results.Created($"/users/{user.Id}", new { user.Id, user.Email, user.Role, user.CreatedAt });
        })
        .WithName("Register");

        group.MapPost("/login", async (LoginDto dto, AppDbContext db, ITokenService tokenService, ILogger<Program> logger) =>
        {
            if (!TryValidate(dto, out var errors))
                return Results.ValidationProblem(errors);

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                logger.LogWarning("Αποτυχημένη προσπάθεια login για {Email}", dto.Email);
                return Results.Unauthorized();

            }
            logger.LogInformation("Επιτυχές login για {Email}", user.Email);
            var token = tokenService.GenerateToken(user);
            return Results.Ok(new { token });
        })
            .WithName("Login");
        return app;
    }
}