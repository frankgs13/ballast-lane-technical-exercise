using Microsoft.AspNetCore.Identity;
using TaskManager.Application.Abstractions;

namespace TaskManager.Infrastructure.Security;

/// <summary>
/// Delegates to ASP.NET Core Identity's battle-tested hasher (PBKDF2 / HMAC-SHA512, salted,
/// versioned format) instead of hand-rolling crypto. Only the hashing component is used;
/// Identity's EF-based stores are not.
/// </summary>
internal sealed class PasswordHasher : IPasswordHasher
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object Subject = new();

    // Verified against when the user does not exist, so both paths cost one PBKDF2 computation.
    private static readonly Lazy<string> DummyHash = new(() => Hasher.HashPassword(Subject, Guid.NewGuid().ToString()));

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return Hasher.HashPassword(Subject, password);
    }

    public bool Verify(string? passwordHash, string password)
    {
        try
        {
            var result = Hasher.VerifyHashedPassword(Subject, passwordHash ?? DummyHash.Value, password);
            return passwordHash is not null && result != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
