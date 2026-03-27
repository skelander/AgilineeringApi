using System.Net;
using System.Net.Http.Json;
using ForwardAgilityApi.Services;
using Xunit;

namespace ForwardAgilityApi.Tests;

public class PostPreviewControllerTests : IClassFixture<ForwardAgilityFactory>
{
    private readonly HttpClient _client;

    public PostPreviewControllerTests(ForwardAgilityFactory factory)
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
            new CreatePreviewRequest("Anna", "secret"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var preview = await resp.Content.ReadFromJsonAsync<PreviewResponse>();
        Assert.NotNull(preview);
        Assert.Equal("Anna", preview!.Name);
        Assert.NotEmpty(preview.Token);
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401()
    {
        await _client.AuthenticateAsync();
        var post = await CreateDraftAsync("create-preview-unauth");
        _client.DefaultRequestHeaders.Authorization = null;

        var resp = await _client.PostAsJsonAsync($"/posts/{post.Id}/previews",
            new CreatePreviewRequest("Anna", "secret"));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Create_NonExistentPost_Returns404()
    {
        await _client.AuthenticateAsync();

        var resp = await _client.PostAsJsonAsync("/posts/99999/previews",
            new CreatePreviewRequest("Anna", "secret"));

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
            new CreatePreviewRequest("Anna", "secret"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Theory]
    [InlineData("", "secret")]
    [InlineData("Anna", "")]
    [InlineData("   ", "secret")]
    [InlineData("Anna", "abc")]   // password too short (< 6 chars)
    public async Task Create_InvalidInput_Returns400(string name, string password)
    {
        var post = await CreateDraftAsync("create-preview-invalid");

        var resp = await _client.PostAsJsonAsync($"/posts/{post.Id}/previews",
            new CreatePreviewRequest(name, password));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // --- Access preview ---

    [Fact]
    public async Task Access_CorrectCredentials_ReturnsPost()
    {
        var post = await CreateDraftAsync("access-preview-ok");
        var createResp = await _client.PostAsJsonAsync($"/posts/{post.Id}/previews",
            new CreatePreviewRequest("Anna", "secret"));
        var preview = await createResp.Content.ReadFromJsonAsync<PreviewResponse>();
        _client.DefaultRequestHeaders.Authorization = null;

        var resp = await _client.PostAsJsonAsync($"/posts/preview/{preview!.Token}/access",
            new PreviewAccessRequest("Anna", "secret"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var result = await resp.Content.ReadFromJsonAsync<PostDetailResponse>();
        Assert.Equal(post.Slug, result!.Slug);
        Assert.False(result.Published);
    }

    [Fact]
    public async Task Access_NameCaseInsensitive_ReturnsPost()
    {
        var post = await CreateDraftAsync("access-preview-case");
        var createResp = await _client.PostAsJsonAsync($"/posts/{post.Id}/previews",
            new CreatePreviewRequest("Anna", "secret"));
        var preview = await createResp.Content.ReadFromJsonAsync<PreviewResponse>();
        _client.DefaultRequestHeaders.Authorization = null;

        var resp = await _client.PostAsJsonAsync($"/posts/preview/{preview!.Token}/access",
            new PreviewAccessRequest("ANNA", "secret"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Access_WrongPassword_Returns401()
    {
        var post = await CreateDraftAsync("access-preview-badpw");
        var createResp = await _client.PostAsJsonAsync($"/posts/{post.Id}/previews",
            new CreatePreviewRequest("Anna", "secret"));
        var preview = await createResp.Content.ReadFromJsonAsync<PreviewResponse>();
        _client.DefaultRequestHeaders.Authorization = null;

        var resp = await _client.PostAsJsonAsync($"/posts/preview/{preview!.Token}/access",
            new PreviewAccessRequest("Anna", "wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Access_WrongName_Returns401()
    {
        var post = await CreateDraftAsync("access-preview-badname");
        var createResp = await _client.PostAsJsonAsync($"/posts/{post.Id}/previews",
            new CreatePreviewRequest("Anna", "secret"));
        var preview = await createResp.Content.ReadFromJsonAsync<PreviewResponse>();
        _client.DefaultRequestHeaders.Authorization = null;

        var resp = await _client.PostAsJsonAsync($"/posts/preview/{preview!.Token}/access",
            new PreviewAccessRequest("Kalle", "secret"));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Access_NonExistentToken_Returns404()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var resp = await _client.PostAsJsonAsync("/posts/preview/doesnotexist/access",
            new PreviewAccessRequest("Anna", "secret"));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Theory]
    [InlineData("", "secret")]
    [InlineData("Anna", "")]
    [InlineData("   ", "secret")]
    public async Task Access_EmptyCredentials_Returns400(string name, string password)
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var resp = await _client.PostAsJsonAsync("/posts/preview/sometoken/access",
            new PreviewAccessRequest(name, password));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // --- Get previews ---

    [Fact]
    public async Task GetByPost_AsAdmin_ReturnsList()
    {
        var post = await CreateDraftAsync("get-previews-draft");
        await _client.PostAsJsonAsync($"/posts/{post.Id}/previews", new CreatePreviewRequest("A", "secret"));
        await _client.PostAsJsonAsync($"/posts/{post.Id}/previews", new CreatePreviewRequest("B", "secret"));

        var resp = await _client.GetAsync($"/posts/{post.Id}/previews");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var list = await resp.Content.ReadFromJsonAsync<List<PreviewResponse>>();
        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
    }

    [Fact]
    public async Task GetByPost_Unauthenticated_Returns401()
    {
        await _client.AuthenticateAsync();
        var post = await CreateDraftAsync("get-previews-unauth");
        _client.DefaultRequestHeaders.Authorization = null;

        var resp = await _client.GetAsync($"/posts/{post.Id}/previews");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GetByPost_NonExistentPost_Returns404()
    {
        await _client.AuthenticateAsync();

        var resp = await _client.GetAsync("/posts/99999/previews");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // --- Delete preview ---

    [Fact]
    public async Task Delete_AsAdmin_Returns204()
    {
        var post = await CreateDraftAsync("delete-preview-draft");
        var createResp = await _client.PostAsJsonAsync($"/posts/{post.Id}/previews",
            new CreatePreviewRequest("Anna", "secret"));
        var preview = await createResp.Content.ReadFromJsonAsync<PreviewResponse>();

        var resp = await _client.DeleteAsync($"/posts/{post.Id}/previews/{preview!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404()
    {
        await _client.AuthenticateAsync();

        var resp = await _client.DeleteAsync("/posts/1/previews/99999");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task DeletePost_CascadesPreviewDeletion()
    {
        var post = await CreateDraftAsync("cascade-delete-post");
        var createResp = await _client.PostAsJsonAsync($"/posts/{post.Id}/previews",
            new CreatePreviewRequest("Anna", "secret"));
        var preview = await createResp.Content.ReadFromJsonAsync<PreviewResponse>();

        await _client.DeleteAsync($"/posts/{post.Id}");
        _client.DefaultRequestHeaders.Authorization = null;

        var resp = await _client.PostAsJsonAsync($"/posts/preview/{preview!.Token}/access",
            new PreviewAccessRequest("Anna", "secret"));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Access_AfterDelete_Returns404()
    {
        var post = await CreateDraftAsync("access-after-delete");
        var createResp = await _client.PostAsJsonAsync($"/posts/{post.Id}/previews",
            new CreatePreviewRequest("Anna", "secret"));
        var preview = await createResp.Content.ReadFromJsonAsync<PreviewResponse>();
        await _client.DeleteAsync($"/posts/{post.Id}/previews/{preview!.Id}");
        _client.DefaultRequestHeaders.Authorization = null;

        var resp = await _client.PostAsJsonAsync($"/posts/preview/{preview.Token}/access",
            new PreviewAccessRequest("Anna", "secret"));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
