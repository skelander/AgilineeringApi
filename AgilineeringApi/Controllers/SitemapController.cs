using AgilineeringApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgilineeringApi.Controllers;

[ApiController]
public class SitemapController(IPostsService postsService, IConfiguration configuration, ILogger<SitemapController> logger) : ControllerBase
{
    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var siteBaseUrl = configuration["Site:BaseUrl"];
        if (string.IsNullOrWhiteSpace(siteBaseUrl))
        {
            logger.LogError("Sitemap requested but Site:BaseUrl is not configured");
            return StatusCode(500, new { error = "Sitemap is not configured." });
        }
        var baseUrl = siteBaseUrl.TrimEnd('/');

        var result = await postsService.GetAllAsync(includeUnpublished: false, page: 1, pageSize: 1000, ct: ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        foreach (var post in result.Items)
        {
            var escapedSlug = System.Security.SecurityElement.Escape(post.Slug);
            var escapedBase = System.Security.SecurityElement.Escape(baseUrl);
            sb.AppendLine($"  <url><loc>{escapedBase}/blog/{escapedSlug}</loc></url>");
        }
        sb.AppendLine("</urlset>");

        return Content(sb.ToString(), "application/xml");
    }
}
