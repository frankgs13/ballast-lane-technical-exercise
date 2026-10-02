namespace TaskManager.Application.Auth;

public sealed record RegisterRequest(string? Username, string? Email, string? Password);

public sealed record LoginRequest(string? Username, string? Password);

public sealed record UserResponse(Guid Id, string Username, string Email, DateTime CreatedAtUtc);

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, UserResponse User);
