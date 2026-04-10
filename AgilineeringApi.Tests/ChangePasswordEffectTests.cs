using System.Net;
using System.Net.Http.Json;
using AgilineeringApi.Services;
using Xunit;

namespace AgilineeringApi.Tests;

// Each class needs its own fixture — password changes affect subsequent logins
public class ChangePasswordLoginEffectTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public ChangePasswordLoginEffectTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ChangePassword_OldPasswordNoLongerWorks()
    {
        await _client.AuthenticateAsync();
        await _client.PostAsJsonAsync("/auth/change-password",
            new ChangePasswordRequest("admin", "new-secure-password-123"));
        await _client.LogoutAsync();

        var response = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest("admin", "admin"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public class ChangePasswordNewPasswordWorksTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public ChangePasswordNewPasswordWorksTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ChangePassword_NewPasswordAllowsLogin()
    {
        await _client.AuthenticateAsync();
        await _client.PostAsJsonAsync("/auth/change-password",
            new ChangePasswordRequest("admin", "new-secure-password-123"));
        await _client.LogoutAsync();

        var response = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest("admin", "new-secure-password-123"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
