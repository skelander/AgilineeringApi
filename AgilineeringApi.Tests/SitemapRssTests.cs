using System.Net;
using System.Net.Http.Json;
using AgilineeringApi.Services;
using Xunit;

namespace AgilineeringApi.Tests;

public class SitemapRssTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public SitemapRssTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    // --- Sitemap ---

    [Fact]
    public async Task Sitemap_Returns200WithXml()
    {
        var response = await _client.GetAsync("/sitemap.xml");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("xml", response.Content.Headers.ContentType?.MediaType ?? "");
    }

    [Fact]
    public async Task Sitemap_IncludesPublishedPost()
    {
        await _client.AuthenticateAsync();
        await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("Sitemap Post", "Body", "sitemap-post", true, []));
        await _client.LogoutAsync();

        var response = await _client.GetAsync("/sitemap.xml");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("sitemap-post", body);
    }

    [Fact]
    public async Task Sitemap_ExcludesUnpublishedPost()
    {
        await _client.AuthenticateAsync();
        await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("Draft", "Body", "sitemap-draft-post", false, []));
        await _client.LogoutAsync();

        var response = await _client.GetAsync("/sitemap.xml");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("sitemap-draft-post", body);
    }

    [Fact]
    public async Task Sitemap_IsValidXml()
    {
        var response = await _client.GetAsync("/sitemap.xml");
        var body = await response.Content.ReadAsStringAsync();

        var ex = Record.Exception(() => System.Xml.Linq.XDocument.Parse(body));
        Assert.Null(ex);
    }

    // --- RSS ---

    [Fact]
    public async Task Rss_Returns200WithXml()
    {
        var response = await _client.GetAsync("/rss.xml");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("xml", response.Content.Headers.ContentType?.MediaType ?? "");
    }

    [Fact]
    public async Task Rss_IncludesPublishedPost()
    {
        await _client.AuthenticateAsync();
        var slug = $"rss-test-post-{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("RSS Unique Post", "Body", slug, true, []));
        await _client.LogoutAsync();

        // Bypass response cache so we get fresh content
        using var request = new HttpRequestMessage(HttpMethod.Get, "/rss.xml");
        request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains(slug, body);
    }

    [Fact]
    public async Task Rss_ExcludesUnpublishedPost()
    {
        await _client.AuthenticateAsync();
        await _client.PostAsJsonAsync("/posts",
            new CreatePostRequest("RSS Draft", "Body", "rss-draft-post", false, []));
        await _client.LogoutAsync();

        var response = await _client.GetAsync("/rss.xml");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("rss-draft-post", body);
    }

    [Fact]
    public async Task Rss_IsValidXml()
    {
        var response = await _client.GetAsync("/rss.xml");
        var body = await response.Content.ReadAsStringAsync();

        var ex = Record.Exception(() => System.Xml.Linq.XDocument.Parse(body));
        Assert.Null(ex);
    }

    [Fact]
    public async Task Rss_ContainsRequiredChannelElements()
    {
        var response = await _client.GetAsync("/rss.xml");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("<title>", body);
        Assert.Contains("<link>", body);
        Assert.Contains("<description>", body);
    }
}
