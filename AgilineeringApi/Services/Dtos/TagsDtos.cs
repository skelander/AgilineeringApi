namespace AgilineeringApi.Services;

public record TagResponse(int Id, string Name, string Slug, string? DudeImageUrl = null);
public record CreateTagRequest(string Name, string Slug);
