using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using StockPilot.Api.Models;
using StockPilot.Api.Services;
using Xunit;

namespace StockPilot.Api.Tests;

// Unit tests for TokenService: a plain class with one dependency (IConfiguration)
// injected via its constructor, and no database/HTTP involved. That's exactly
// what makes it a good "unit" to test in isolation.
public class TokenServiceTests
{
    // Builds a fake IConfiguration in memory, instead of reading the real
    // appsettings.json. This is the core trick that makes this a *unit* test:
    // TokenService doesn't know or care where its configuration comes from,
    // so we can feed it made-up values without touching real app settings.
    private static IConfiguration BuildTestConfiguration()
    {
        var settings = new Dictionary<string, string?>
        {
            { "Jwt:Key", "this-is-a-test-secret-key-at-least-32-chars-long" },
            { "Jwt:Issuer", "StockPilot.Tests" },
            { "Jwt:Audience", "StockPilot.Tests" },
            { "Jwt:ExpiryMinutes", "60" }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    [Fact]
    public void GenerateToken_IncludesCorrectClaims()
    {
        // Arrange: fake configuration + a fake user, nothing real involved.
        var configuration = BuildTestConfiguration();
        var tokenService = new TokenService(configuration);
        var user = new User
        {
            Id = 42,
            Email = "test@stockpilot.gr",
            Role = "Admin",
            PasswordHash = "irrelevant-for-this-test"
        };

        // Act: call the one method we're actually testing.
        var token = tokenService.GenerateToken(user);

        // Assert: decode the JWT and check that the claims we care about
        // (sub/email/role) were set correctly. We use JwtSecurityTokenHandler
        // to read the token back into its parts, the same way a client would.
        Assert.False(string.IsNullOrWhiteSpace(token));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("42", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("test@stockpilot.gr", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("Admin", jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void GenerateToken_SetsExpiryBasedOnConfiguration()
    {
        // Arrange
        var configuration = BuildTestConfiguration();
        var tokenService = new TokenService(configuration);
        var user = new User { Id = 1, Email = "x@x.com", Role = "User", PasswordHash = "x" };

        // Act
        var token = tokenService.GenerateToken(user);

        // Assert: the token's expiry (ValidTo) should match "now + Jwt:ExpiryMinutes"
        // from our fake configuration (60 minutes). We allow a small tolerance
        // (5 seconds) instead of exact equality, because a tiny amount of real
        // time passes between DateTime.UtcNow here and inside TokenService.
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var expectedExpiry = DateTime.UtcNow.AddMinutes(60);

        Assert.True(Math.Abs((jwt.ValidTo - expectedExpiry).TotalSeconds) < 5);
    }
}
