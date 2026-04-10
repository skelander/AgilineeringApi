namespace AgilineeringApi.Services;

public record TagResponse(int Id, string Name, string Slug);
public record CreateTagRequest(string Name, string Slug);
