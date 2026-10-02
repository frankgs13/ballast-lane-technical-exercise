namespace TaskManager.Application.Common;

/// <summary>Deliberately generic: never reveal whether the username or the password was wrong.</summary>
public sealed class InvalidCredentialsException() : Exception("Invalid username or password.");
