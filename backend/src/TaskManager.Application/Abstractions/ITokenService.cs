using TaskManager.Domain.Users;

namespace TaskManager.Application.Abstractions;

public interface ITokenService
{
    AccessToken CreateToken(User user);
}

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);
