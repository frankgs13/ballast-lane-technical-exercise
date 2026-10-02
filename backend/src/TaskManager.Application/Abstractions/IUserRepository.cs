using TaskManager.Domain.Users;

namespace TaskManager.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    /// <exception cref="Common.ConflictException">The username or email was taken concurrently.</exception>
    Task AddAsync(User user, CancellationToken cancellationToken);
}
