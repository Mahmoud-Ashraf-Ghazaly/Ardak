namespace Ardak.Application.Interfaces;

/// <summary>
/// Storage abstraction for securely storing, reading, and managing raw document streams.
/// Physical paths, credentials, and storage details are encapsulated within implementations.
/// </summary>
public interface IDocumentStorageService
{
    /// <summary>
    /// Saves a document stream securely and returns an opaque, unique storage key.
    /// </summary>
    Task<string> SaveFileAsync(Stream stream, string originalFileName, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens and returns a readable stream for the document matching the given storage key.
    /// Returns null if the file does not exist.
    /// </summary>
    Task<Stream?> GetFileStreamAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from storage if requested.
    /// </summary>
    Task<bool> DeleteFileAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a file exists in storage for the given storage key.
    /// </summary>
    Task<bool> FileExistsAsync(string storageKey, CancellationToken cancellationToken = default);
}
