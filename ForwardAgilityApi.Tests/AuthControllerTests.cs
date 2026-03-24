using System.Net;
using System.Net.Http.Json;
using ForwardAgilityApi.Services;
using Xunit;

namespace ForwardAgilityApi.Tests;

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
    [InlineData("", "")]
    public async Task Login_InvalidCredentials_Returns401(string username, string password)
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(username, password));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
