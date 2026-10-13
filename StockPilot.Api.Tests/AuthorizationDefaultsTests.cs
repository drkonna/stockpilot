using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace StockPilot.Api.Tests;

// These tests guard the "secure by default" rule: every endpoint requires an
// authenticated user unless it is explicitly marked AllowAnonymous. They exist
// so that a forgotten RequireAuthorization() on a future endpoint (or an
// accidental AllowAnonymous) is caught by the test run instead of by an attacker.
public class AuthorizationDefaultsTests : IClassFixture<StockPilotApiFactory>
{
    private readonly StockPilotApiFactory _factory;

    public AuthorizationDefaultsTests(StockPilotApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FallbackPolicy_RequiresAuthenticatedUser()
    {
        // The fallback policy is applied to every endpoint that has no
        // authorization metadata of its own. If it is removed, such endpoints
        // silently become public.
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        var fallback = await provider.GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.Contains(fallback!.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public void OnlyExpectedEndpoints_AllowAnonymous()
    {
        // EndpointDataSource holds every route registered in the app. We list
        // those explicitly marked AllowAnonymous and check that each one is on
        // the allowed list: auth (register/login) and the API documentation.
        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();

        var anonymousRoutes = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => e.RoutePattern.RawText ?? "")
            .ToList();

        var unexpected = anonymousRoutes
            .Where(route => !route.StartsWith("/auth/")
                         && !route.StartsWith("/scalar")
                         && !route.StartsWith("/openapi"))
            .ToList();

        Assert.True(unexpected.Count == 0,
            "Unexpected anonymous endpoints: " + string.Join(", ", unexpected));
    }

    [Theory]
    [InlineData("/products")]
    [InlineData("/categories")]
    [InlineData("/suppliers")]
    [InlineData("/productfamilies")]
    [InlineData("/stores")]
    [InlineData("/products/1/stock")]
    public async Task ProtectedEndpoints_WithoutToken_ReturnUnauthorized(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}