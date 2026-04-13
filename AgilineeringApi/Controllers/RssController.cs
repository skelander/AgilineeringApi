using System.Text;
using System.Xml.Linq;
using AgilineeringApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgilineeringApi.Controllers;

[ApiController]
[Route("rss.xml")]
public class RssController(IPostsService postsService, IConfiguration config, ILogger<RssController> logger) : ControllerBase
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    [HttpGet]
    [ResponseCache(Duration = 900)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var siteBaseUrl = config["Site:BaseUrl"];
        if (string.IsNullOrWhiteSpace(siteBaseUrl))
        {
            logger.LogError("RSS feed requested but Site:BaseUrl is not configured");
            return StatusCode(500, new { error = "RSS feed is not configured." });
        }
        var siteUrl = siteBaseUrl.TrimEnd('/');

        var result = await postsService.GetAllAsync(includeUnpublished: false, page: 1, pageSize: 20, ct: ct);

        var items = result.Items.Select(post =>
        {
            var url = $"{siteUrl}/blog/{post.Slug}";
            return new XElement("item",
                new XElement("title", post.Title),
                new XElement("link", url),
                new XElement("guid", url),
                new XElement("pubDate", post.CreatedAt.ToUniversalTime().ToString("R")));
        });

        var feed = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("rss",
                new XAttribute("version", "2.0"),
                new XAttribute(XNamespace.Xmlns + "atom", Atom),
                new XElement("channel",
                    new XElement("title", "Agilineering"),
                    new XElement("link", siteUrl),
                    new XElement("description", "En blogg om agil mjukvaruutveckling"),
                    new XElement("language", "sv"),
                    new XElement(Atom + "link",
                        new XAttribute("href", $"{siteUrl}/rss.xml"),
                        new XAttribute("rel", "self"),
                        new XAttribute("type", "application/rss+xml")),
                    items)));

        var sb = new StringBuilder();
        feed.Save(new StringWriter(sb));
        return Content(sb.ToString(), "application/rss+xml; charset=utf-8");
    }
}
