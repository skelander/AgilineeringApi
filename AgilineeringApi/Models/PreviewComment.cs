namespace AgilineeringApi.Models;

public class PreviewComment
{
    public int Id { get; set; }
    public int PreviewId { get; set; }
    public PostPreview Preview { get; set; } = null!;
    public required string Body { get; set; }
    public DateTime CreatedAt { get; set; }
}
