namespace TaskManager.Application.Common;

public sealed class ConflictException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
