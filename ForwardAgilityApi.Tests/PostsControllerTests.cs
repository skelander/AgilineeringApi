using System.Net;
using System.Net.Http.Json;
using ForwardAgilityApi.Services;
using Xunit;

namespace ForwardAgilityApi.Tests;

public class PostsControllerTests : IClassFixture<ForwardAgilityFactory>
{
    private readonly HttpClient _client;

    public PostsControllerTests(ForwardAgilityFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_Anonymous_ReturnsOnlyPublished()
    {
        await _client.AuthenticateAsync();
        await _client.PostAsJsonAsync("/posts", new CreatePostRequest("Draft", "Body", "draft-post", false, []));
        await _client.PostAsJsonAsync("/posts", new CreatePostRequest("Published", "Body", "published-post", true, []));
        _client.DefaultRequestHeaders.Authorization = null;

        var posts = await _client.GetFromJsonAsync<List<PostSummaryResponse>>("/posts");
        Assert.NotNull(posts);
        Assert.All(posts, p => Assert.True(p.Published));
        Assert.Contains(posts, p => p.Slug == "published-post");
        Assert.DoesNotContain(posts, p => p.Slug == "draft-post");
    }

    [Fact]
    public async Task GetAll_Admin_ReturnsAllPosts()
    {
        await _client.AuthenticateAsync();
        await _client.PostAsJsonAsync("/posts", new CreatePostRequest("Admin Draft", "Body", "admin-draft", false, []));

        var posts = await _client.GetFromJsonAsync<List<PostSummaryResponse>>("/posts");
        Assert.NotNull(posts);
        Assert.Contains(posts, p => p.Slug == "admin-draft");
    }

    [Fact]
    public async Task Create_AsAdmin_Returns201()
    {
        await _client.AuthenticateAsync();
        var response = await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("Hello World", "Content here", "hello-world", true, []));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var post = await response.Content.ReadFromJsonAsync<PostDetailResponse>();
        Assert.Equal("hello-world", post!.Slug);
        Assert.Equal("admin", post.AuthorUsername);
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("Test", "Content", "test-slug", true, []));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateSlug_Returns409()
    {
        await _client.AuthenticateAsync();
        await _client.PostAsJsonAsync("/posts", new CreatePostRequest("First", "Body", "dup-slug", true, []));
        var response = await _client.PostAsJsonAsync("/posts", new CreatePostRequest("Second", "Body", "dup-slug", true, []));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetBySlug_PublishedPost_ReturnsPost()
    {
        await _client.AuthenticateAsync();
        await _client.PostAsJsonAsync("/posts", new CreatePostRequest("Slugged", "Body", "slugged-post", true, []));
        _client.DefaultRequestHeaders.Authorization = null;

        var post = await _client.GetFromJsonAsync<PostDetailResponse>("/posts/slugged-post");
        Assert.NotNull(post);
        Assert.Equal("Slugged", post.Title);
    }

    [Fact]
    public async Task GetBySlug_NotFound_Returns404()
    {
        var response = await _client.GetAsync("/posts/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsAdmin_Returns200()
    {
        await _client.AuthenticateAsync();
        var create = await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("Original", "Body", "update-me", true, []));
        var created = await create.Content.ReadFromJsonAsync<PostDetailResponse>();

        var response = await _client.PutAsJsonAsync($"/posts/{created!.Id}",
            new UpdatePostRequest("Updated", "New body", "update-me", true, []));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<PostDetailResponse>();
        Assert.Equal("Updated", updated!.Title);
    }

    [Fact]
    public async Task Delete_AsAdmin_Returns204()
    {
        await _client.AuthenticateAsync();
        var create = await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("Delete Me", "Body", "delete-me", true, []));
        var created = await create.Content.ReadFromJsonAsync<PostDetailResponse>();

        var response = await _client.DeleteAsync($"/posts/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NotFound_Returns404()
    {
        await _client.AuthenticateAsync();
        var response = await _client.DeleteAsync("/posts/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
