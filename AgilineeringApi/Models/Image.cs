namespace AgilineeringApi.Models;

public class Image
{
    public int Id { get; set; }
    public string Filename { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Data { get; set; } = [];
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
}
