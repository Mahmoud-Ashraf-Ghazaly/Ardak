namespace Ardak.Application.Interfaces;

public class FileValidationResult
{
    public bool IsValid { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string NormalizedExtension { get; private set; } = string.Empty;
    public string SanitizedFileName { get; private set; } = string.Empty;

    public static FileValidationResult Success(string sanitizedFileName, string normalizedExtension) =>
        new() { IsValid = true, SanitizedFileName = sanitizedFileName, NormalizedExtension = normalizedExtension };

    public static FileValidationResult Failure(string error) =>
        new() { IsValid = false, ErrorMessage = error };
}

public interface IDocumentFileValidator
{
    long MaxFileSizeBytes { get; }
    IReadOnlySet<string> AllowedExtensions { get; }

    FileValidationResult Validate(Stream stream, string fileName, string? contentType, long fileLength);
}
