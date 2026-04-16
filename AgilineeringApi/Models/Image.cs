namespace AgilineeringApi.Models;

public class Image
{
    public int Id { get; set; }
    public required string Filename { get; set; }
    public required string ContentType { get; set; }
    public required byte[] Data { get; set; }
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
}
