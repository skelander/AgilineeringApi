namespace AgilineeringApi.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<ServiceResult> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct = default);
}
