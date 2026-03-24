namespace ForwardAgilityApi.Services;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Role);
public record LoginResult(LoginResponse? Response, string? Error, DateTime? LockedUntil);

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request);
}
