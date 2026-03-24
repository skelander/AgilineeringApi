namespace ForwardAgilityApi.Models;

public class Tag
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public ICollection<Post> Posts { get; set; } = [];
}
