using System.Net;
using System.Net.Http.Json;
using AgilineeringApi.Services;
using Xunit;

namespace AgilineeringApi.Tests;

// Each test class gets its own factory so password changes don't bleed between tests
public class ChangePasswordSuccessTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public ChangePasswordSuccessTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ChangePassword_WithValidCurrentAndNewPassword_Returns204()
    {
        await _client.AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/auth/change-password",
            new ChangePasswordRequest("admin", "new-secure-password-123"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}

public class ChangePasswordWrongPasswordTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public ChangePasswordWrongPasswordTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Returns403()
    {
        await _client.AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/auth/change-password",
            new ChangePasswordRequest("wrong-password", "new-secure-password-123"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

public class ChangePasswordValidationTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public ChangePasswordValidationTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ChangePassword_Unauthenticated_Returns401()
    {
        await _client.LogoutAsync();

        var response = await _client.PostAsJsonAsync("/auth/change-password",
            new ChangePasswordRequest("admin", "new-secure-password-123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("", "new-secure-password-123")]
    [InlineData("admin", "")]
    public async Task ChangePassword_EmptyFields_Returns400(string current, string newPw)
    {
        await _client.AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/auth/change-password",
            new ChangePasswordRequest(current, newPw));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("11chars----")]
    public async Task ChangePassword_NewPasswordTooShort_Returns400(string newPw)
    {
        await _client.AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/auth/change-password",
            new ChangePasswordRequest("admin", newPw));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
