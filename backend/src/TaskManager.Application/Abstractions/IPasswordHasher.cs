namespace TaskManager.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Returns <c>false</c> when <paramref name="passwordHash"/> is null, but still performs a full
    /// hash computation so that "unknown user" and "wrong password" take the same time.
    /// </summary>
    bool Verify(string? passwordHash, string password);
}
