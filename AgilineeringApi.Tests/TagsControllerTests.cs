using System.Net;
using System.Net.Http.Json;
using AgilineeringApi.Services;
using Xunit;

namespace AgilineeringApi.Tests;

public class TagsControllerTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public TagsControllerTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_Anonymous_Returns200()
    {
        var response = await _client.GetAsync("/tags");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsAdmin_Returns201()
    {
        await _client.AuthenticateAsync();
        var response = await _client.PostAsJsonAsync("/tags", new CreateTagRequest("Technology", "technology"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var tag = await response.Content.ReadFromJsonAsync<TagResponse>();
        Assert.Equal("Technology", tag!.Name);
        Assert.Equal("technology", tag.Slug);
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/tags", new CreateTagRequest("Test", "test"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateSlug_Returns409()
    {
        await _client.AuthenticateAsync();
        await _client.PostAsJsonAsync("/tags", new CreateTagRequest("Unique Tag", "unique-tag"));
        var response = await _client.PostAsJsonAsync("/tags", new CreateTagRequest("Unique Tag 2", "unique-tag"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAdmin_Returns204()
    {
        await _client.AuthenticateAsync();
        var create = await _client.PostAsJsonAsync("/tags", new CreateTagRequest("Delete Tag", "delete-tag"));
        var tag = await create.Content.ReadFromJsonAsync<TagResponse>();
        var response = await _client.DeleteAsync($"/tags/{tag!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NotFound_Returns404()
    {
        await _client.AuthenticateAsync();
        var response = await _client.DeleteAsync("/tags/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("", "valid-slug")]
    [InlineData("   ", "valid-slug")]
    [InlineData("Valid Name", "")]
    [InlineData("Valid Name", "Invalid Slug")]
    [InlineData("Valid Name", "UPPERCASE")]
    [InlineData("Valid Name", "-leading-hyphen")]
    public async Task Create_InvalidInput_Returns400(string name, string slug)
    {
        await _client.AuthenticateAsync();
        var response = await _client.PostAsJsonAsync("/tags", new CreateTagRequest(name, slug));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_TagUsedByPost_Returns204AndPostLosesTag()
    {
        await _client.AuthenticateAsync();
        var tagResp = await _client.PostAsJsonAsync("/tags", new CreateTagRequest("Removable", "removable"));
        var tag = await tagResp.Content.ReadFromJsonAsync<TagResponse>();

        await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("Post with tag", "Body", "post-with-removable-tag", true, [tag!.Id]));

        var deleteResp = await _client.DeleteAsync($"/tags/{tag!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        // Post still exists but no longer has the tag
        var post = await _client.GetFromJsonAsync<PostDetailResponse>("/posts/post-with-removable-tag");
        Assert.NotNull(post);
        Assert.DoesNotContain(post!.Tags, t => t.Id == tag.Id);
    }

    [Fact]
    public async Task PostWithTags_TagsReturnedInResponse()
    {
        await _client.AuthenticateAsync();
        var tagResp = await _client.PostAsJsonAsync("/tags", new CreateTagRequest("C#", "csharp"));
        var tag = await tagResp.Content.ReadFromJsonAsync<TagResponse>();

        var postResp = await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("Tagged Post", "Body", "tagged-post", true, [tag!.Id]));
        var post = await postResp.Content.ReadFromJsonAsync<PostDetailResponse>();

        Assert.NotNull(post);
        Assert.Contains(post!.Tags, t => t.Slug == "csharp");
    }
}
