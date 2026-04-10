namespace AgilineeringApi.Services;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Role);
public record LoginRoleResponse(string Role);
public record LoginResult(LoginResponse? Response, string? Error, DateTime? LockedUntil);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
