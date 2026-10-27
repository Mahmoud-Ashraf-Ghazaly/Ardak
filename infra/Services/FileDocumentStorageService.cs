using Ardak.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ardak.Infrastructure.Services;

/// <summary>
/// Secure local filesystem storage provider for sensitive documents.
/// Files are stored in a non-public folder outside wwwroot.
/// Internal storage paths and structure are never exposed to callers or clients.
/// </summary>
public class FileDocumentStorageService : IDocumentStorageService
{
    private readonly string _storageRoot;
    private readonly ILogger<FileDocumentStorageService> _logger;

    public FileDocumentStorageService(IConfiguration configuration, ILogger<FileDocumentStorageService> logger)
    {
        _logger = logger;

        // Retrieve configured path or default to a secure private folder outside wwwroot
        var configuredPath = configuration["Storage:SecureDocumentPath"];
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            var baseDir = AppContext.BaseDirectory;
            _storageRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "App_Data", "SecureDocuments"));
        }
        else
        {
            _storageRoot = Path.GetFullPath(configuredPath);
        }

        EnsureDirectoryExists(_storageRoot);
    }

    /// <summary>
    /// Constructor for testing or custom root directory injection.
    /// </summary>
    public FileDocumentStorageService(string storageRoot, ILogger<FileDocumentStorageService> logger)
    {
        _logger = logger;
        _storageRoot = Path.GetFullPath(storageRoot);
        EnsureDirectoryExists(_storageRoot);
    }

    public async Task<string> SaveFileAsync(Stream stream, string originalFileName, string contentType, CancellationToken cancellationToken = default)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        // Generate non-guessable, secure storage key token (no original path info)
        var storageKey = $"{Guid.NewGuid():N}.bin";
        var targetPath = ResolveSafePath(storageKey);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using (var fileStream = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        _logger.LogInformation("Securely saved document to private storage under storage key token.");

        return storageKey;
    }

    public Task<Stream?> GetFileStreamAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return Task.FromResult<Stream?>(null);
        }

        var path = ResolveSafePath(storageKey);
        if (!File.Exists(path))
        {
            _logger.LogWarning("Document file not found for requested storage token.");
            return Task.FromResult<Stream?>(null);
        }

        // Return a readable file stream with FileShare.Read
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> DeleteFileAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return Task.FromResult(false);
        }

        var path = ResolveSafePath(storageKey);
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                _logger.LogInformation("Deleted document file from private storage.");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete file from storage.");
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(false);
    }

    public Task<bool> FileExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return Task.FromResult(false);
        }

        var path = ResolveSafePath(storageKey);
        return Task.FromResult(File.Exists(path));
    }

    private string ResolveSafePath(string storageKey)
    {
        // Prevent path traversal
        var cleanKey = Path.GetFileName(storageKey);
        var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, cleanKey));

        if (!fullPath.StartsWith(_storageRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Path traversal attempt detected.");
        }

        return fullPath;
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}
