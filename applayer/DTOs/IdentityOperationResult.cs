namespace Ardak.Application.DTOs;

public class IdentityOperationResult
{
    public bool Succeeded { get; private set; }
    public IReadOnlyList<string> Errors { get; private set; } = [];

    public static IdentityOperationResult Success() => new() { Succeeded = true };

    public static IdentityOperationResult Failure(IEnumerable<string> errors) => new()
    {
        Succeeded = false,
        Errors = errors.ToList()
    };

    public static IdentityOperationResult Failure(string error) => new()
    {
        Succeeded = false,
        Errors = [error]
    };
}
