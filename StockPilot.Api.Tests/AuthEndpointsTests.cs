using System.Net;
using System.Net.Http.Json;
using StockPilot.Api.Dtos;
using Xunit;

namespace StockPilot.Api.Tests;

// Integration tests: unlike TokenServiceTests/CreateProductDtoValidationTests,
// these don't isolate a single class - they send real HTTP requests through
// the whole app (routing, model binding, validation, the endpoint's own logic,
// and the database access) and check the actual HTTP response. This is what
// catches bugs that only show up when all the pieces work together.
//
// IClassFixture<StockPilotApiFactory> tells xUnit: create ONE
// StockPilotApiFactory (and therefore one in-memory "app + database") and
// share it across every test in this class, instead of paying the cost of
// booting the whole app again for each test.
public class AuthEndpointsTests : IClassFixture<StockPilotApiFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(StockPilotApiFactory factory)
    {
        // A real HttpClient wired directly to the in-memory test server -
        // requests never touch a real network, but otherwise behave like a
        // normal client would.
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsCreated()
    {
        // A random email per test run: since this factory/database is shared
        // across all three tests in this class, unique emails keep the tests
        // independent of each other (and of previous test runs) instead of
        // relying on a fresh database per test.
        var dto = new RegisterDto
        {
            Email = $"test_{Guid.NewGuid()}@stockpilot.gr",
            Password = "SecurePassword123!"
        };

        var response = await _client.PostAsJsonAsync("/auth/register", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        var dto = new RegisterDto
        {
            Email = $"dup_{Guid.NewGuid()}@stockpilot.gr",
            Password = "SecurePassword123!"
        };

        // Register the same email twice in a row: the first call should
        // succeed, and the second should be rejected by the endpoint's own
        // "email already taken" check (Program.cs's /auth/register handler).
        await _client.PostAsJsonAsync("/auth/register", dto);
        var secondResponse = await _client.PostAsJsonAsync("/auth/register", dto);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsToken()
    {
        // Register a fresh user first, then immediately try to log in with
        // the same credentials - this exercises the full register -> login
        // flow exactly the way the real frontend does.
        var dto = new RegisterDto
        {
            Email = $"login_{Guid.NewGuid()}@stockpilot.gr",
            Password = "SecurePassword123!"
        };
        await _client.PostAsJsonAsync("/auth/register", dto);

        var loginResponse = await _client.PostAsJsonAsync("/auth/login", new LoginDto
        {
            Email = dto.Email,
            Password = dto.Password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        // Not just the status code: also check that a real, non-empty JWT
        // came back in the response body, since that's what the frontend
        // actually depends on to stay logged in.
        var body = await loginResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.False(string.IsNullOrWhiteSpace(body?["token"]));
    }
}
