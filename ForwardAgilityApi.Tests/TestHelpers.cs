using System.Net.Http.Headers;
using System.Net.Http.Json;
using ForwardAgilityApi.Services;

namespace ForwardAgilityApi.Tests;

public static class TestHelpers
{
    public static async Task AuthenticateAsync(this HttpClient client, string username = "admin", string password = "admin")
    {
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
    }
}
