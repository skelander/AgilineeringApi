using System.Net;
using System.Net.Http.Json;
using AgilineeringApi.Services;
using Xunit;

namespace AgilineeringApi.Tests;

/// <summary>
/// Boundary tests for input validation limits defined in SecurityConstants.
/// </summary>
public class LoginValidationTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public LoginValidationTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_PasswordExceedsMaxLength_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest("admin", new string('x', 1001)));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_PasswordAtMaxLength_Returns401OrOk()
    {
        // 1000 chars is the limit — should reach auth logic, not be blocked by validation
        var response = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest("admin", new string('x', 1000)));
        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 401 or 200, got {response.StatusCode}");
    }

    [Fact]
    public async Task Login_UsernameExceedsMaxLength_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest(new string('a', 201), "admin"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_UsernameAtMaxLength_Returns401()
    {
        // 200 chars is the limit — should reach auth logic (unknown user → 401)
        var response = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest(new string('a', 200), "admin"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public class ChangePasswordBoundaryTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public ChangePasswordBoundaryTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ChangePassword_NewPasswordExceedsMaxLength_Returns400()
    {
        await _client.AuthenticateAsync();
        var response = await _client.PostAsJsonAsync("/auth/change-password",
            new ChangePasswordRequest("admin", new string('x', 1001)));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_NewPasswordAtMaxLength_Returns204()
    {
        await _client.AuthenticateAsync();
        var response = await _client.PostAsJsonAsync("/auth/change-password",
            new ChangePasswordRequest("admin", new string('x', 1000)));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}

public class PostsNullTagIdsTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public PostsNullTagIdsTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreatePost_WithNullTagIds_Returns201()
    {
        await _client.AuthenticateAsync();
        var response = await _client.PostAsJsonAsync("/posts", new
        {
            title = "No Tags Post",
            content = "Content",
            slug = "no-tags-post",
            published = false,
            // tagIds intentionally omitted — should not throw NullReferenceException
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePost_WithNullTagIds_Returns200()
    {
        await _client.AuthenticateAsync();
        var create = await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("Tag Update Test", "Content", "tag-update-test", false, []));
        var post = await create.Content.ReadFromJsonAsync<PostDetailResponse>();

        var response = await _client.PutAsJsonAsync($"/posts/{post!.Id}", new
        {
            title = "Updated",
            content = "Content",
            slug = "tag-update-test",
            published = false,
            // tagIds omitted
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
