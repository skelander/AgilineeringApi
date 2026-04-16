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
    public async Task AddComment_NonExistentToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/posts/preview/doesnotexist/comments",
            new CreateCommentRequest("secret123", "Great post!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

    // --- UpdateComment ---

    [Fact]
    public async Task UpdateComment_ValidCredentials_Returns200WithUpdatedBody()
    {
        var (_, token) = await CreatePreviewAsync("comment-update-ok");
        var addResp = await _client.PostAsJsonAsync($"/posts/preview/{token}/comments",
            new CreateCommentRequest("secret123", "Original body"));
        var comment = await addResp.Content.ReadFromJsonAsync<CommentResponse>();

        var response = await _client.PutAsJsonAsync($"/posts/preview/{token}/comments/{comment!.Id}",
            new UpdateCommentRequest("secret123", "Updated body"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CommentResponse>();
        Assert.Equal("Updated body", updated!.Body);
        Assert.Equal(comment.Id, updated.Id);
        Assert.Equal(comment.CreatedAt, updated.CreatedAt);
    }

    [Fact]
    public async Task UpdateComment_WrongPassword_Returns401()
    {
        var (_, token) = await CreatePreviewAsync("comment-update-badpw");
        var addResp = await _client.PostAsJsonAsync($"/posts/preview/{token}/comments",
            new CreateCommentRequest("secret123", "Original"));
        var comment = await addResp.Content.ReadFromJsonAsync<CommentResponse>();

        var response = await _client.PutAsJsonAsync($"/posts/preview/{token}/comments/{comment!.Id}",
            new UpdateCommentRequest("wrongpassword", "Updated"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateComment_NonExistentComment_Returns401()
    {
        var (_, token) = await CreatePreviewAsync("comment-update-notfound");

        var response = await _client.PutAsJsonAsync($"/posts/preview/{token}/comments/99999",
            new UpdateCommentRequest("secret123", "Updated"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateComment_EmptyBody_Returns400(string body)
    {
        var (_, token) = await CreatePreviewAsync($"comment-update-emptybody-{body.Length}");
        var addResp = await _client.PostAsJsonAsync($"/posts/preview/{token}/comments",
            new CreateCommentRequest("secret123", "Original"));
        var comment = await addResp.Content.ReadFromJsonAsync<CommentResponse>();

        var response = await _client.PutAsJsonAsync($"/posts/preview/{token}/comments/{comment!.Id}",
            new UpdateCommentRequest("secret123", body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateComment_BodyTooLong_Returns400()
    {
        var (_, token) = await CreatePreviewAsync("comment-update-toolong");
        var addResp = await _client.PostAsJsonAsync($"/posts/preview/{token}/comments",
            new CreateCommentRequest("secret123", "Original"));
        var comment = await addResp.Content.ReadFromJsonAsync<CommentResponse>();

        var response = await _client.PutAsJsonAsync($"/posts/preview/{token}/comments/{comment!.Id}",
            new UpdateCommentRequest("secret123", new string('x', 5001)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- DeleteComment ---

    [Fact]
    public async Task DeleteComment_ValidCredentials_Returns204()
    {
        var (_, token) = await CreatePreviewAsync("comment-delete-ok");
        var addResp = await _client.PostAsJsonAsync($"/posts/preview/{token}/comments",
            new CreateCommentRequest("secret123", "To be deleted"));
        var comment = await addResp.Content.ReadFromJsonAsync<CommentResponse>();

        var response = await _client.DeleteAsJsonAsync($"/posts/preview/{token}/comments/{comment!.Id}",
            new DeleteCommentRequest("secret123"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteComment_IsGoneFromList()
    {
        var (_, token) = await CreatePreviewAsync("comment-delete-gone");
        var addResp = await _client.PostAsJsonAsync($"/posts/preview/{token}/comments",
            new CreateCommentRequest("secret123", "To be deleted"));
        var comment = await addResp.Content.ReadFromJsonAsync<CommentResponse>();

        await _client.DeleteAsJsonAsync($"/posts/preview/{token}/comments/{comment!.Id}",
            new DeleteCommentRequest("secret123"));

        var listResp = await _client.PostAsJsonAsync($"/posts/preview/{token}/comments/list",
            new PreviewAccessRequest("secret123"));
        var comments = await listResp.Content.ReadFromJsonAsync<List<CommentResponse>>();
        Assert.DoesNotContain(comments!, c => c.Id == comment.Id);
    }

    [Fact]
    public async Task DeleteComment_WrongPassword_Returns401()
    {
        var (_, token) = await CreatePreviewAsync("comment-delete-badpw");
        var addResp = await _client.PostAsJsonAsync($"/posts/preview/{token}/comments",
            new CreateCommentRequest("secret123", "Original"));
        var comment = await addResp.Content.ReadFromJsonAsync<CommentResponse>();

        var response = await _client.DeleteAsJsonAsync($"/posts/preview/{token}/comments/{comment!.Id}",
            new DeleteCommentRequest("wrongpassword"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteComment_NonExistentComment_Returns401()
    {
        var (_, token) = await CreatePreviewAsync("comment-delete-notfound");

        var response = await _client.DeleteAsJsonAsync($"/posts/preview/{token}/comments/99999",
            new DeleteCommentRequest("secret123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
    public async Task GetComments_NonExistentToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/posts/preview/doesnotexist/comments/list",
            new PreviewAccessRequest("secret123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

