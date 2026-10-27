using Ardak.Application.Interfaces;

namespace Ardak.Infrastructure.Services;

public class DocumentFileValidator : IDocumentFileValidator
{
    public long MaxFileSizeBytes { get; } = 25 * 1024 * 1024; // 25 MB default

    public IReadOnlySet<string> AllowedExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".png",
        ".jpg",
        ".jpeg",
        ".doc",
        ".docx",
        ".xls",
        ".xlsx",
        ".tif",
        ".tiff",
        ".txt",
        ".csv"
    };

    private static readonly HashSet<string> BlacklistedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".sh", ".ps1", ".js", ".vbs",
        ".php", ".asp", ".aspx", ".html", ".htm", ".jar", ".msi", ".scr", ".pif"
    };

    public DocumentFileValidator(long? maxFileSizeBytes = null)
    {
        if (maxFileSizeBytes.HasValue && maxFileSizeBytes.Value > 0)
        {
            MaxFileSizeBytes = maxFileSizeBytes.Value;
        }
    }

    public FileValidationResult Validate(Stream stream, string fileName, string? contentType, long fileLength)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return FileValidationResult.Failure("File name is missing or invalid.");
        }

        // Prevent path traversal
        if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
        {
            return FileValidationResult.Failure("Invalid characters detected in file name.");
        }

        var sanitizedFileName = Path.GetFileName(fileName).Trim();
        if (string.IsNullOrEmpty(sanitizedFileName))
        {
            return FileValidationResult.Failure("Invalid file name.");
        }

        var ext = Path.GetExtension(sanitizedFileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext))
        {
            return FileValidationResult.Failure("File must have a valid extension.");
        }

        if (BlacklistedExtensions.Contains(ext))
        {
            return FileValidationResult.Failure($"Executable or script file type '{ext}' is strictly prohibited.");
        }

        if (!AllowedExtensions.Contains(ext))
        {
            return FileValidationResult.Failure($"File type '{ext}' is not permitted. Allowed types: {string.Join(", ", AllowedExtensions)}.");
        }

        if (fileLength <= 0)
        {
            return FileValidationResult.Failure("The uploaded file is empty.");
        }

        if (fileLength > MaxFileSizeBytes)
        {
            var maxMb = MaxFileSizeBytes / (1024 * 1024);
            return FileValidationResult.Failure($"The file size ({fileLength / (1024.0 * 1024.0):F2} MB) exceeds the maximum allowed limit of {maxMb} MB.");
        }

        // Verify magic bytes if stream is seekable and has sufficient data
        if (stream.CanSeek && stream.CanRead && fileLength >= 4)
        {
            var originalPos = stream.Position;
            try
            {
                var header = new byte[8];
                int bytesRead = stream.Read(header, 0, Math.Min((int)fileLength, header.Length));
                stream.Position = originalPos;

                if (bytesRead >= 4)
                {
                    if (ext == ".pdf")
                    {
                        // %PDF
                        if (header[0] != 0x25 || header[1] != 0x50 || header[2] != 0x44 || header[3] != 0x46)
                        {
                            return FileValidationResult.Failure("File content does not match standard PDF format.");
                        }
                    }
                    else if (ext == ".png")
                    {
                        // 0x89 PNG
                        if (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47)
                        {
                            return FileValidationResult.Failure("File content does not match standard PNG format.");
                        }
                    }
                    else if (ext == ".jpg" || ext == ".jpeg")
                    {
                        // 0xFF 0xD8 0xFF
                        if (header[0] != 0xFF || header[1] != 0xD8 || header[2] != 0xFF)
                        {
                            return FileValidationResult.Failure("File content does not match standard JPEG format.");
                        }
                    }
                }
            }
            catch
            {
                // In case stream seeking encounters an issue, maintain original position and continue
                stream.Position = originalPos;
            }
        }

        return FileValidationResult.Success(sanitizedFileName, ext);
    }
}
