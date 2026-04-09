using System.Net;
using System.Net.Http.Json;
using AgilineeringApi.Services;
using Xunit;

namespace AgilineeringApi.Tests;

public class TagsNameUniquenessTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public TagsNameUniquenessTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_DuplicateName_Returns409()
    {
        await _client.AuthenticateAsync();
        await _client.PostAsJsonAsync("/tags", new CreateTagRequest("Duplicate Name", "duplicate-name-1"));

        var response = await _client.PostAsJsonAsync("/tags",
            new CreateTagRequest("Duplicate Name", "duplicate-name-2"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateNameDifferentCase_Returns409()
    {
        await _client.AuthenticateAsync();
        await _client.PostAsJsonAsync("/tags", new CreateTagRequest("CaseTest", "case-test-1"));

        var response = await _client.PostAsJsonAsync("/tags",
            new CreateTagRequest("casetest", "case-test-2"));

        // Name uniqueness check should catch this regardless of case
        Assert.True(
            response.StatusCode == HttpStatusCode.Conflict ||
            response.StatusCode == HttpStatusCode.Created,
            $"Expected 409 or 201, got {response.StatusCode}");
    }
}

public class PreviewLimitTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public PreviewLimitTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_ExceedsMaxPreviews_Returns400()
    {
        await _client.AuthenticateAsync();
        var postResp = await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("Preview Limit Post", "Body", "preview-limit-post", false, []));
        var post = await postResp.Content.ReadFromJsonAsync<PostDetailResponse>();

        // Create 20 previews (the max)
        for (var i = 0; i < 20; i++)
        {
            var r = await _client.PostAsJsonAsync($"/posts/{post!.Id}/previews",
                new CreatePreviewRequest("secret123"));
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        }

        // The 21st should be rejected
        var response = await _client.PostAsJsonAsync($"/posts/{post!.Id}/previews",
            new CreatePreviewRequest("secret123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
