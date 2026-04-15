namespace AgilineeringApi.Services;

public record CreatePreviewRequest(string Password);
public record PreviewResponse(int Id, string Token, DateTime CreatedAt);
public record PreviewWithCommentsResponse(int Id, string Token, DateTime CreatedAt, DateTime? LastAccessedAt, IEnumerable<CommentResponse> Comments);
public record PreviewAccessRequest(string Password);
public record CreateCommentRequest(string Password, string Body);
public record CommentResponse(int Id, string Body, DateTime CreatedAt);
