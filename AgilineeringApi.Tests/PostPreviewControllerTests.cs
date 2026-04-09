using System.Net;
using System.Net.Http.Json;
using AgilineeringApi.Services;
using Xunit;

namespace AgilineeringApi.Tests;

public class PostPreviewControllerTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public PostPreviewControllerTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<PostDetailResponse> CreateDraftAsync(string slug = "preview-draft")
    {
        await _client.AuthenticateAsync();
        var resp = await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("Draft", "Body", slug, false, []));
        return (await resp.Content.ReadFromJsonAsync<PostDetailResponse>())!;
    }

    // --- Create preview ---

    [Fact]
    public async Task Create_AsAdmin_ReturnsPreviewWithToken()
    {
        var post = await CreateDraftAsync("create-preview-draft");

        var resp = await _client.PostAsJsonAsync($"/posts/{post.Id}/previews",
            new CreatePreviewRequest("secret"));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var preview = await resp.Content.ReadFromJsonAsync<PreviewResponse>();
        Assert.NotNull(preview);
        Assert.NotEmpty(preview!.Token);
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401()
    {
        await _client.AuthenticateAsync();
        var post = await CreateDraftAsync("create-preview-unauth");
        await _client.LogoutAsync();

        var resp = await _client.PostAsJsonAsync($"/posts/{post.Id}/previews",
            new CreatePreviewRequest("secret"));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Create_NonExistentPost_Returns404()
    {
        await _client.AuthenticateAsync();

        var resp = await _client.PostAsJsonAsync("/posts/99999/previews",
            new CreatePreviewRequest("secret"));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Create_PublishedPost_Returns400()
    {
        await _client.AuthenticateAsync();
        var postResp = await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("Published", "Body", "preview-published-post", true, []));
        var post = await postResp.Content.ReadFromJsonAsync<PostDetailResponse>();

        var resp = await _client.PostAsJsonAsync($"/posts/{post!.Id}/previews",
            new CreatePreviewRequest("secret"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]  // too short (< 6 chars)
    public async Task Create_InvalidPassword_Returns400(string password)
    {
        var post = await CreateDraftAsync("create-preview-invalid");

        var resp = await _client.PostAsJsonAsync($"/posts/{post.Id}/previews",
            new CreatePreviewRequest(password));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // --- Check token existence ---

    [Fact]
    public async Task Check_ExistingToken_Returns200()
    {
        var post = await CreateDraftAsync("check-token-exists");
        var createResp = await _client.PostAsJsonAsync($"/posts/{post.Id}/previews",
            new CreatePreviewRequest("secret"));
        var preview = await createResp.Content.ReadFromJsonAsync<PreviewResponse>();
        await _client.LogoutAsync();

        var resp = await _client.GetAsync($"/posts/preview/{preview!.Token}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Check_NonExistentToken_Returns404()
    {
        var resp = await _client.GetAsync("/posts/preview/doesnotexist");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Check_AfterPostDeleted_Returns404()
    {
        var post = await CreateDraftAsync("check-token-after-delete");
        var createResp = await _client.PostAsJsonAsync($"/posts/{post.Id}/previews",
            new CreatePreviewRequest("secret"));
        var preview = await createResp.Content.ReadFromJsonAsync<PreviewResponse>();

        await _client.DeleteAsync($"/posts/{post.Id}");
        await _client.LogoutAsync();

        var resp = await _client.GetAsync($"/posts/preview/{preview!.Token}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // --- Access preview ---

    [Fact]
    public async Task Access_CorrectPassword_ReturnsPost()
    {
        var post = await CreateDraftAsync("access-preview-ok");
        var createResp = await _client.PostAsJsonAsync($"/posts/{post.Id}/previews",
            new CreatePreviewRequest("secret"));
        var preview = await createResp.Content.ReadFromJsonAsync<PreviewResponse>();
        await _client.LogoutAsync();

        var resp = await _client.PostAsJsonAsync($"/posts/preview/{preview!.Token}/access",
            new PreviewAccessRequest("secret"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var result = await resp.Content.ReadFromJsonAsync<PostDetailResponse>();
        Assert.Equal(post.Slug, result!.Slug);
        Assert.False(result.Published);
    }

    [Fact]
    public async Task Access_WrongPassword_Returns401()
    {
        var post = await CreateDraftAsync("access-preview-badpw");
        var createResp = await _client.PostAsJsonAsync($"/posts/{post.Id}/previews",
            new CreatePreviewRequest("secret"));
        var preview = await createResp.Content.ReadFromJsonAsync<PreviewResponse>();
        await _client.LogoutAsync();

        var resp = await _client.PostAsJsonAsync($"/posts/preview/{preview!.Token}/access",
            new PreviewAccessRequest("wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Access_NonExistentToken_Returns401()
    {
        await _client.LogoutAsync();

        var resp = await _client.PostAsJsonAsync("/posts/preview/doesnotexist/access",
            new PreviewAccessRequest("secret"));

        // Returns 401 (not 404) to prevent token enumeration via status code differences
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Access_EmptyPassword_Returns400(string password)
    {
        await _client.LogoutAsync();

        var resp = await _client.PostAsJsonAsync("/posts/preview/sometoken/access",
            new PreviewAccessRequest(password));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task DeletePost_CascadesPreviewDeletion()
    {
        var post = await CreateDraftAsync("cascade-delete-post");
        var createResp = await _client.PostAsJsonAsync($"/posts/{post.Id}/previews",
            new CreatePreviewRequest("secret"));
        var preview = await createResp.Content.ReadFromJsonAsync<PreviewResponse>();

        await _client.DeleteAsync($"/posts/{post.Id}");
        await _client.LogoutAsync();

        var resp = await _client.PostAsJsonAsync($"/posts/preview/{preview!.Token}/access",
            new PreviewAccessRequest("secret"));

        // Returns 401 (not 404) to prevent token enumeration
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
