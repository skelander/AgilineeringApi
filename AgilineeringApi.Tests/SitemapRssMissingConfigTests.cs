using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AgilineeringApi.Tests;

/// <summary>
/// Factory without Site:BaseUrl configured — used to test error handling
/// when required configuration is missing.
/// </summary>
public class NoBaseUrlFactory : AgilineeringFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        // Override Site:BaseUrl to empty string to simulate missing config
        builder.UseSetting("Site:BaseUrl", "");
    }
}

public class SitemapRssMissingConfigTests : IClassFixture<NoBaseUrlFactory>
{
    private readonly HttpClient _client;

    public SitemapRssMissingConfigTests(NoBaseUrlFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Sitemap_WhenBaseUrlNotConfigured_Returns500()
    {
        var response = await _client.GetAsync("/sitemap.xml");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Sitemap_WhenBaseUrlNotConfigured_ReturnsJsonError()
    {
        var response = await _client.GetAsync("/sitemap.xml");
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(body);
        Assert.True(body!.ContainsKey("error"));
    }

    [Fact]
    public async Task Rss_WhenBaseUrlNotConfigured_Returns500()
    {
        var response = await _client.GetAsync("/rss.xml");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Rss_WhenBaseUrlNotConfigured_ReturnsJsonError()
    {
        var response = await _client.GetAsync("/rss.xml");
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(body);
        Assert.True(body!.ContainsKey("error"));
    }
}

/// <summary>
/// Factory with Site:BaseUrl set to whitespace — IsNullOrWhiteSpace should catch this.
/// </summary>
public class WhitespaceBaseUrlFactory : AgilineeringFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Site:BaseUrl", "   ");
    }
}

public class SitemapRssWhitespaceConfigTests : IClassFixture<WhitespaceBaseUrlFactory>
{
    private readonly HttpClient _client;

    public SitemapRssWhitespaceConfigTests(WhitespaceBaseUrlFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Sitemap_WhenBaseUrlIsWhitespace_Returns500()
    {
        var response = await _client.GetAsync("/sitemap.xml");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Rss_WhenBaseUrlIsWhitespace_Returns500()
    {
        var response = await _client.GetAsync("/rss.xml");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
