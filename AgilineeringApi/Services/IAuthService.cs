namespace AgilineeringApi.Services;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Role);
public record LoginResult(LoginResponse? Response, string? Error, DateTime? LockedUntil);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request);
    Task<ServiceResult> ChangePasswordAsync(int userId, ChangePasswordRequest request);
}
