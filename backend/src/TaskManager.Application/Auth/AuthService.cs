using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Domain.Common;
using TaskManager.Domain.Users;

namespace TaskManager.Application.Auth;

internal sealed class AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    TimeProvider timeProvider) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new ValidationErrors();
        User.Validate(request.Username, request.Email, errors);
        PasswordPolicy.Validate(request.Password, errors);
        errors.ThrowIfAny();

        var username = User.NormalizeUsername(request.Username);
        var email = User.NormalizeEmail(request.Email);

        if (await userRepository.UsernameExistsAsync(username, cancellationToken))
        {
            throw new ConflictException("username", "Username is already taken.");
        }

        if (await userRepository.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("email", "Email is already registered.");
        }

        var user = User.Create(username, email, passwordHasher.Hash(request.Password!), timeProvider.GetUtcNow().UtcDateTime);
        await userRepository.AddAsync(user, cancellationToken);

        return CreateAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new ValidationErrors();
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            errors.Add("username", "Username is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors.Add("password", "Password is required.");
        }

        errors.ThrowIfAny();

        var user = await userRepository.GetByUsernameAsync(User.NormalizeUsername(request.Username), cancellationToken);

        // Verify even when the user does not exist so response time does not reveal valid usernames.
        if (!passwordHasher.Verify(user?.PasswordHash, request.Password!) || user is null)
        {
            throw new InvalidCredentialsException();
        }

        return CreateAuthResponse(user);
    }

    public async Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        return ToResponse(user);
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        var token = tokenService.CreateToken(user);
        return new AuthResponse(token.Token, token.ExpiresAtUtc, ToResponse(user));
    }

    private static UserResponse ToResponse(User user) => new(user.Id, user.Username, user.Email, user.CreatedAtUtc);
}
