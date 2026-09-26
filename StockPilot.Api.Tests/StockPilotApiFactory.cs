using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockPilot.Api.Data;

namespace StockPilot.Api.Tests;

// WebApplicationFactory<Program> boots the REAL StockPilot.Api app in memory:
// same Program.cs, same middleware pipeline, same endpoints. This lets our
// integration tests send real HTTP requests to it. We only override how the
// database is wired up, so tests never touch the real PostgreSQL dev database.
//
// Program.cs uses top-level statements, which normally generate an *internal*
// Program class - WebApplicationFactory<Program> needs it to be public, hence
// the "public partial class Program { }" line added at the end of Program.cs.
public class StockPilotApiFactory : WebApplicationFactory<Program>
{
    // Computed ONCE, when this factory instance is created (shared across all
    // tests in a class via IClassFixture). If this were generated inside the
    // lambda below instead, it would run again on every request (AddDbContext
    // registers its configuration as scoped, i.e. re-invoked per HTTP request),
    // and every request would silently get its own empty database - which is
    // exactly the bug we hit and fixed while building this.
    private readonly string _dbName = $"StockPilotTests_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Jwt:Key/Issuer/Audience only exist in appsettings.Development.json,
        // not in the base appsettings.json - without forcing this environment,
        // the test host wouldn't load them and login would fail while building
        // the JWT.
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // The real Program.cs already registered AppDbContext to use
            // Npgsql (real PostgreSQL). Modern EF Core doesn't store that as
            // one simple "final options" registration - it stores separate
            // composable "configuration steps" (IDbContextOptionsConfiguration)
            // that get merged together later. Removing only DbContextOptions
            // isn't enough on its own: the leftover Npgsql "step" would still
            // get combined with our InMemory one, and EF Core throws because
            // two providers ended up registered at once. So we remove both.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));

            // Swap in EF Core's InMemory provider instead of real PostgreSQL.
            // Good enough for testing plain CRUD/business logic (like the auth
            // endpoints below), but it doesn't understand Postgres-specific
            // features like EF.Functions.ILike used in the /products search -
            // testing that endpoint properly would need a real Postgres
            // instance (e.g. via Testcontainers), which is a topic for later.
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }
}
