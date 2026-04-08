using System.Net.Http.Json;
using AgilineeringApi.Services;

namespace AgilineeringApi.Tests;

public static class TestHelpers
{
    public static async Task AuthenticateAsync(this HttpClient client, string username = "admin", string password = "admin")
    {
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        // auth_token cookie is handled automatically by the test client's cookie container
    }

    public static async Task LogoutAsync(this HttpClient client)
    {
        await client.PostAsync("/auth/logout", null);
    }
}
