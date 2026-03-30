namespace AgilineeringApi.Models;

public class PostPreview
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;
    public required string Token { get; set; }
    public required string Name { get; set; }
    public required string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; }
}
