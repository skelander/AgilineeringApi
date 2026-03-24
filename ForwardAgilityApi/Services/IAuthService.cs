namespace ForwardAgilityApi.Services;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token);

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
}
