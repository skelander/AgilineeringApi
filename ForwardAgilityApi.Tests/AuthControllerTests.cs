using System.Net;
using System.Net.Http.Json;
using ForwardAgilityApi.Services;
using Xunit;

namespace ForwardAgilityApi.Tests;

// Basic auth tests — at most 1 failed admin attempt total, well below lockout threshold (3)
public class AuthControllerTests : IClassFixture<ForwardAgilityFactory>
{
    private readonly HttpClient _client;

    public AuthControllerTests(ForwardAgilityFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_ValidAdminCredentials_ReturnsToken()
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "admin"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body?.Token);
    }

    [Theory]
    [InlineData("admin", "wrong")]
    [InlineData("nobody", "admin")]
    public async Task Login_InvalidCredentials_Returns401(string username, string password)
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(username, password));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("admin", "")]
    [InlineData("", "admin")]
    [InlineData("   ", "admin")]
    public async Task Login_EmptyCredentials_Returns400(string username, string password)
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(username, password));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

// Lockout tests — separate fixture so each class starts with a fresh DB
public class AuthLockoutTests : IClassFixture<ForwardAgilityFactory>
{
    private readonly HttpClient _client;

    public AuthLockoutTests(ForwardAgilityFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_AfterMaxFailedAttempts_ReturnsLocked()
    {
        // MaxFailedLoginAttempts = 3 in test config
        for (var i = 0; i < 3; i++)
            await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "wrongpassword"));

        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "wrongpassword"));
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Login_CorrectPasswordWhileLocked_ReturnsLocked()
    {
        for (var i = 0; i < 3; i++)
            await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "wrongpassword"));

        // Even correct password is blocked while locked
        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "admin"));
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Login_LockedResponse_ContainsLockedUntil()
    {
        for (var i = 0; i < 3; i++)
            await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "wrongpassword"));

        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "wrongpassword"));
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("lockedUntil"));
    }
}

// Reset test — separate fixture so it starts with zero failed attempts
public class AuthResetTests : IClassFixture<ForwardAgilityFactory>
{
    private readonly HttpClient _client;

    public AuthResetTests(ForwardAgilityFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_SuccessfulLogin_ResetsFailedAttempts()
    {
        // Two failed then one successful — resets the counter
        await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "wrongpassword"));
        await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "wrongpassword"));
        await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "admin"));

        // Counter reset — next failure should return 401, not 429
        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "wrongpassword"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
