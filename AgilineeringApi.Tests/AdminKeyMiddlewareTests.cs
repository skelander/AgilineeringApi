using System.Net;
using System.Net.Http.Json;
using AgilineeringApi.Services;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace AgilineeringApi.Tests;

/// <summary>
/// Factory that does NOT add X-Admin-Key to default request headers.
/// Used to verify AdminKeyMiddleware blocks requests without the key.
/// </summary>
public class NoAdminKeyFactory : AgilineeringFactory
{
    protected override void ConfigureClient(HttpClient client)
    {
        // Intentionally do NOT call base — omits X-Admin-Key header
    }
}

/// <summary>
/// Tests for AdminKeyMiddleware — verifies that write endpoints require the X-Admin-Key header
/// and that public write endpoints are exempt from this requirement.
/// </summary>
public class AdminKeyMiddlewareTests : IClassFixture<NoAdminKeyFactory>
{
    private readonly HttpClient _clientWithKey;
    private readonly HttpClient _clientWithoutKey;

    public AdminKeyMiddlewareTests(NoAdminKeyFactory factory)
    {
        _clientWithoutKey = factory.CreateClient(); // no X-Admin-Key (ConfigureClient overridden)
        _clientWithKey = factory.CreateClient();
        _clientWithKey.DefaultRequestHeaders.Add("X-Admin-Key", "test-admin-key");
    }

    [Fact]
    public async Task Post_WithValidAdminKey_IsNotBlockedByMiddleware()
    {
        await _clientWithKey.AuthenticateAsync();
        var response = await _clientWithKey.PostAsJsonAsync("/tags",
            new CreateTagRequest("KeyTest", "key-test"));
        // Middleware should pass — result depends on auth/business logic, not middleware block
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithoutAdminKey_Returns403()
    {
        var response = await _clientWithoutKey.PostAsJsonAsync("/tags",
            new CreateTagRequest("NoKey", "no-key"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithoutAdminKey_Returns403()
    {
        var response = await _clientWithoutKey.PutAsJsonAsync("/posts/1", new
        {
            title = "T", content = "C", slug = "s", published = false, tagIds = Array.Empty<int>()
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutAdminKey_Returns403()
    {
        var response = await _clientWithoutKey.DeleteAsync("/posts/1");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithoutAdminKey_IsNotBlocked()
    {
        var response = await _clientWithoutKey.GetAsync("/posts");
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // --- Public write endpoints must be exempt ---

    [Fact]
    public async Task Login_WithoutAdminKey_IsNotBlocked()
    {
        var response = await _clientWithoutKey.PostAsJsonAsync("/auth/login",
            new LoginRequest("admin", "wrongpassword"));
        // Should get 401, not 403 from middleware
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutAdminKey_IsNotBlocked()
    {
        var response = await _clientWithoutKey.PostAsync("/auth/logout", null);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PreviewAccess_WithoutAdminKey_IsNotBlocked()
    {
        var response = await _clientWithoutKey.PostAsJsonAsync("/posts/preview/nonexistent-token/access",
            new { password = "secret123" });
        // Should get 401 (invalid token), not 403 from middleware
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PreviewComments_WithoutAdminKey_IsNotBlocked()
    {
        var response = await _clientWithoutKey.PostAsJsonAsync("/posts/preview/nonexistent-token/comments/list",
            new { password = "secret123" });
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddComment_WithoutAdminKey_IsNotBlocked()
    {
        var response = await _clientWithoutKey.PostAsJsonAsync("/posts/preview/nonexistent-token/comments",
            new { password = "secret123", body = "Hello" });
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Middleware_ErrorResponse_HasJsonErrorBody()
    {
        var response = await _clientWithoutKey.PostAsJsonAsync("/tags",
            new CreateTagRequest("ErrorBody", "error-body"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(body);
        Assert.True(body!.ContainsKey("error"));
    }
}
