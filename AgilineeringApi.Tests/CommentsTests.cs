using System.Net;
using System.Net.Http.Json;
using AgilineeringApi.Services;
using Xunit;

namespace AgilineeringApi.Tests;

public class CommentsTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public CommentsTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(int PostId, string Token)> CreatePreviewAsync(string slug)
    {
        await _client.AuthenticateAsync();
        var postResp = await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("Draft", "Body", slug, false, []));
        var post = await postResp.Content.ReadFromJsonAsync<PostDetailResponse>();

        var previewResp = await _client.PostAsJsonAsync($"/posts/{post!.Id}/previews",
            new CreatePreviewRequest("secret123"));
        var preview = await previewResp.Content.ReadFromJsonAsync<PreviewResponse>();

        await _client.LogoutAsync();
        return (post.Id, preview!.Token);
    }

    // --- AddComment ---

    [Fact]
    public async Task AddComment_ValidCredentials_Returns201()
    {
        var (_, token) = await CreatePreviewAsync("comment-add-ok");

        var response = await _client.PostAsJsonAsync($"/posts/preview/{token}/comments",
            new CreateCommentRequest("secret123", "Great post!"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var comment = await response.Content.ReadFromJsonAsync<CommentResponse>();
        Assert.Equal("Great post!", comment!.Body);
    }

    [Fact]
    public async Task AddComment_WrongPassword_Returns401()
    {
        var (_, token) = await CreatePreviewAsync("comment-add-badpw");

        var response = await _client.PostAsJsonAsync($"/posts/preview/{token}/comments",
            new CreateCommentRequest("wrongpassword", "Great post!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddComment_NonExistentToken_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/posts/preview/doesnotexist/comments",
            new CreateCommentRequest("secret123", "Great post!"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddComment_EmptyPassword_Returns400(string password)
    {
        var response = await _client.PostAsJsonAsync("/posts/preview/sometoken/comments",
            new CreateCommentRequest(password, "Great post!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddComment_EmptyBody_Returns400(string body)
    {
        var response = await _client.PostAsJsonAsync("/posts/preview/sometoken/comments",
            new CreateCommentRequest("secret123", body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddComment_BodyTooLong_Returns400()
    {
        var (_, token) = await CreatePreviewAsync("comment-toolong");

        var response = await _client.PostAsJsonAsync($"/posts/preview/{token}/comments",
            new CreateCommentRequest("secret123", new string('x', 5001)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddComment_BodyAtLimit_Returns201()
    {
        var (_, token) = await CreatePreviewAsync("comment-atlimit");

        var response = await _client.PostAsJsonAsync($"/posts/preview/{token}/comments",
            new CreateCommentRequest("secret123", new string('x', 5000)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // --- GetComments ---

    [Fact]
    public async Task GetComments_ValidCredentials_ReturnsComments()
    {
        var (_, token) = await CreatePreviewAsync("comments-list-ok");

        await _client.PostAsJsonAsync($"/posts/preview/{token}/comments",
            new CreateCommentRequest("secret123", "First comment"));
        await _client.PostAsJsonAsync($"/posts/preview/{token}/comments",
            new CreateCommentRequest("secret123", "Second comment"));

        var response = await _client.PostAsJsonAsync($"/posts/preview/{token}/comments/list",
            new PreviewAccessRequest("secret123"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var comments = await response.Content.ReadFromJsonAsync<List<CommentResponse>>();
        Assert.NotNull(comments);
        Assert.Equal(2, comments!.Count);
    }

    [Fact]
    public async Task GetComments_WrongPassword_Returns401()
    {
        var (_, token) = await CreatePreviewAsync("comments-list-badpw");

        var response = await _client.PostAsJsonAsync($"/posts/preview/{token}/comments/list",
            new PreviewAccessRequest("wrongpassword"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetComments_NonExistentToken_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/posts/preview/doesnotexist/comments/list",
            new PreviewAccessRequest("secret123"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetComments_EmptyPassword_Returns400(string password)
    {
        var response = await _client.PostAsJsonAsync("/posts/preview/sometoken/comments/list",
            new PreviewAccessRequest(password));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

