using System.Net;
using System.Net.Http.Json;
using AgilineeringApi.Services;
using Xunit;

namespace AgilineeringApi.Tests;

public class PostsControllerTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public PostsControllerTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_Anonymous_ReturnsOnlyPublished()
    {
        await _client.AuthenticateAsync();
        await _client.PostAsJsonAsync("/posts", new CreatePostRequest("Draft", "Body", "draft-post", false, []));
        await _client.PostAsJsonAsync("/posts", new CreatePostRequest("Published", "Body", "published-post", true, []));
        await _client.LogoutAsync();

        var result = await _client.GetFromJsonAsync<PagedResult<PostSummaryResponse>>("/posts");
        var posts = result?.Items.ToList();
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

        var result = await _client.GetFromJsonAsync<PagedResult<PostSummaryResponse>>("/posts?pageSize=50");
        var posts = result?.Items.ToList();
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
        await _client.LogoutAsync();
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
        await _client.LogoutAsync();

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
    public async Task GetBySlug_UnpublishedPost_Anonymous_Returns404()
    {
        await _client.AuthenticateAsync();
        await _client.PostAsJsonAsync("/posts", new CreatePostRequest("Hidden Draft", "Body", "hidden-draft", false, []));
        await _client.LogoutAsync();

        var response = await _client.GetAsync("/posts/hidden-draft");
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

    [Theory]
    [InlineData("", "content", "valid-slug")]
    [InlineData("   ", "content", "valid-slug")]
    [InlineData("title", "", "valid-slug")]
    [InlineData("title", "content", "")]
    [InlineData("title", "content", "Invalid Slug")]
    [InlineData("title", "content", "UPPERCASE")]
    [InlineData("title", "content", "-leading-hyphen")]
    [InlineData("title", "content", "trailing-hyphen-")]
    public async Task Create_InvalidInput_Returns400(string title, string content, string slug)
    {
        await _client.AuthenticateAsync();
        var response = await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest(title, content, slug, true, []));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_NonExistentTagIds_Returns400()
    {
        await _client.AuthenticateAsync();
        var response = await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("Title", "Content", "tag-id-test", true, [99999]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_InvalidSlug_Returns400()
    {
        await _client.AuthenticateAsync();
        var create = await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("Original", "Body", "update-validation", true, []));
        var created = await create.Content.ReadFromJsonAsync<PostDetailResponse>();

        var response = await _client.PutAsJsonAsync($"/posts/{created!.Id}",
            new UpdatePostRequest("Title", "Content", "Invalid Slug", true, []));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_NonExistentTagIds_Returns400()
    {
        await _client.AuthenticateAsync();
        var create = await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("Tag Update Test", "Body", "tag-update-test", true, []));
        var created = await create.Content.ReadFromJsonAsync<PostDetailResponse>();

        var response = await _client.PutAsJsonAsync($"/posts/{created!.Id}",
            new UpdatePostRequest("Title", "Content", "tag-update-test", true, [99999]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_Pagination_ReturnsCorrectPage()
    {
        await _client.AuthenticateAsync();
        await _client.PostAsJsonAsync("/posts", new CreatePostRequest("Page Post 1", "Body", "page-post-1", true, []));
        await _client.PostAsJsonAsync("/posts", new CreatePostRequest("Page Post 2", "Body", "page-post-2", true, []));
        await _client.PostAsJsonAsync("/posts", new CreatePostRequest("Page Post 3", "Body", "page-post-3", true, []));

        var result = await _client.GetFromJsonAsync<PagedResult<PostSummaryResponse>>("/posts?page=1&pageSize=2");
        Assert.NotNull(result);
        Assert.Equal(2, result!.Items.Count());
        Assert.True(result.TotalCount >= 3);
        Assert.True(result.TotalPages >= 2);
    }

    [Fact]
    public async Task GetAll_PageZero_TreatsAsPageOne()
    {
        var result = await _client.GetFromJsonAsync<PagedResult<PostSummaryResponse>>("/posts?page=0");
        Assert.NotNull(result);
        Assert.Equal(1, result!.Page);
    }

    [Fact]
    public async Task GetAll_PageSizeOverMax_ClampsTo50()
    {
        var result = await _client.GetFromJsonAsync<PagedResult<PostSummaryResponse>>("/posts?pageSize=100");
        Assert.NotNull(result);
        Assert.Equal(50, result!.PageSize);
    }

    [Fact]
    public async Task GetAll_TagFilter_ReturnsOnlyMatchingPosts()
    {
        await _client.AuthenticateAsync();
        var tagResp = await _client.PostAsJsonAsync("/tags", new CreateTagRequest("Filter Tag", "filter-tag"));
        var tag = await tagResp.Content.ReadFromJsonAsync<TagResponse>();

        await _client.PostAsJsonAsync("/posts", new CreatePostRequest("Tagged", "Body", "tagged-filter-post", true, [tag!.Id]));
        await _client.PostAsJsonAsync("/posts", new CreatePostRequest("Untagged", "Body", "untagged-filter-post", true, []));
        await _client.LogoutAsync();

        var result = await _client.GetFromJsonAsync<PagedResult<PostSummaryResponse>>("/posts?tag=filter-tag");
        Assert.NotNull(result);
        Assert.Contains(result!.Items, p => p.Slug == "tagged-filter-post");
        Assert.DoesNotContain(result.Items, p => p.Slug == "untagged-filter-post");
    }

    [Theory]
    [InlineData("Invalid Tag")]
    [InlineData("tag with spaces")]
    [InlineData("tag!special")]
    [InlineData("UPPERCASE")]
    public async Task GetAll_InvalidTagFormat_Returns400(string tag)
    {
        var response = await _client.GetAsync($"/posts?tag={Uri.EscapeDataString(tag)}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_InvalidTag_ReturnsEmptyPage()
    {
        var result = await _client.GetFromJsonAsync<PagedResult<PostSummaryResponse>>("/posts?tag=nonexistent-tag");
        Assert.NotNull(result);
        Assert.Empty(result!.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetSitemap_Returns200WithXml()
    {
        await _client.AuthenticateAsync();
        await _client.PostAsJsonAsync("/posts", new CreatePostRequest("Sitemap Post", "Body", "sitemap-post", true, []));

        var response = await _client.GetAsync("/sitemap.xml");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        var xml = await response.Content.ReadAsStringAsync();
        Assert.Contains("sitemap-post", xml);
        Assert.Contains("<urlset", xml);
    }

    [Fact]
    public async Task GetSitemap_DoesNotIncludeUnpublishedPosts()
    {
        await _client.AuthenticateAsync();
        await _client.PostAsJsonAsync("/posts", new CreatePostRequest("Hidden Sitemap", "Body", "hidden-sitemap-post", false, []));

        var response = await _client.GetAsync("/sitemap.xml");
        var xml = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hidden-sitemap-post", xml);
    }
}
