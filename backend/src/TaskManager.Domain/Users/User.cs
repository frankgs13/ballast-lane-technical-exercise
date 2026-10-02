using System.Text.RegularExpressions;
using TaskManager.Domain.Common;

namespace TaskManager.Domain.Users;

public sealed partial class User
{
    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 50;
    public const int EmailMaxLength = 254;

    private User(Guid id, string username, string email, string passwordHash, DateTime createdAtUtc)
    {
        Id = id;
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }

    public string Username { get; }

    public string Email { get; }

    /// <summary>Only the hash is ever stored; the plain password never reaches the domain.</summary>
    public string PasswordHash { get; }

    public DateTime CreatedAtUtc { get; }

    public static User Create(string? username, string? email, string passwordHash, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        }

        var errors = new ValidationErrors();

        var normalizedUsername = username?.Trim() ?? string.Empty;
        if (normalizedUsername.Length is < UsernameMinLength or > UsernameMaxLength)
        {
            errors.Add("username", $"Username must be between {UsernameMinLength} and {UsernameMaxLength} characters.");
        }
        else if (!UsernameRegex().IsMatch(normalizedUsername))
        {
            errors.Add("username", "Username may only contain letters, numbers, '.', '_' and '-'.");
        }

        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedEmail.Length == 0)
        {
            errors.Add("email", "Email is required.");
        }
        else if (normalizedEmail.Length > EmailMaxLength || !EmailRegex().IsMatch(normalizedEmail))
        {
            errors.Add("email", "Email format is invalid.");
        }

        errors.ThrowIfAny();

        return new User(Guid.NewGuid(), normalizedUsername, normalizedEmail, passwordHash, nowUtc);
    }

    public static User Rehydrate(Guid id, string username, string email, string passwordHash, DateTime createdAtUtc) =>
        new(id, username, email, passwordHash, createdAtUtc);

    [GeneratedRegex("^[a-zA-Z0-9._-]+$")]
    private static partial Regex UsernameRegex();

    // Pragmatic check (something@something.tld); full RFC 5322 validation is not worth the complexity.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
