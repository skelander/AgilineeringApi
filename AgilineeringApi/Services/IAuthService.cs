namespace AgilineeringApi.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request);
    Task<ServiceResult> ChangePasswordAsync(int userId, ChangePasswordRequest request);
}
