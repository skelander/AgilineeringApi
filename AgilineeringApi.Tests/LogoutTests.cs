using System.Net;
using System.Net.Http.Json;
using AgilineeringApi.Services;
using Xunit;

namespace AgilineeringApi.Tests;

// Logout tests need own fixture — they modify auth state
public class LogoutTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public LogoutTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Logout_AsAdmin_Returns204()
    {
        await _client.AuthenticateAsync();
        var response = await _client.PostAsync("/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_Unauthenticated_Returns204()
    {
        // Logout is always safe to call regardless of auth state
        await _client.LogoutAsync();
        var response = await _client.PostAsync("/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_SubsequentAuthenticatedRequest_Returns401()
    {
        await _client.AuthenticateAsync();
        await _client.LogoutAsync();

        var response = await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("Should Fail", "Body", "post-after-logout", true, []));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
