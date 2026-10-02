using TaskManager.Domain.Common;

namespace TaskManager.Application.Auth;

internal static class PasswordPolicy
{
    public const int MinLength = 8;

    // Upper bound protects the hasher from very large inputs (cheap DoS vector).
    public const int MaxLength = 128;

    public static void Validate(string? password, ValidationErrors errors)
    {
        if (string.IsNullOrEmpty(password))
        {
            errors.Add("password", "Password is required.");
            return;
        }

        if (password.Length is < MinLength or > MaxLength)
        {
            errors.Add("password", $"Password must be between {MinLength} and {MaxLength} characters.");
        }

        if (!password.Any(char.IsUpper))
        {
            errors.Add("password", "Password must contain an uppercase letter.");
        }

        if (!password.Any(char.IsLower))
        {
            errors.Add("password", "Password must contain a lowercase letter.");
        }

        if (!password.Any(char.IsDigit))
        {
            errors.Add("password", "Password must contain a digit.");
        }
    }
}
