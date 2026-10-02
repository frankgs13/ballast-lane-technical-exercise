namespace TaskManager.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
}
