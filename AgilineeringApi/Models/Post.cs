namespace AgilineeringApi.Models;

public class Post
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public required string Slug { get; set; }
    public bool Published { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public ICollection<Tag> Tags { get; set; } = [];
}
