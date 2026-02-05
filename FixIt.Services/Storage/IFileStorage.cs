namespace FixIt.Services.Storage;

/// <summary>
/// Abstraction for file storage - allows swapping between local, Azure Blob, S3, etc.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Save a file to storage
    /// </summary>
    Task SaveFileAsync(string path, Stream fileStream);

    /// <summary>
    /// Get a file stream from storage
    /// </summary>
    Task<Stream?> GetFileStreamAsync(string path);

    /// <summary>
    /// Delete a file from storage
    /// </summary>
    Task DeleteFileAsync(string path);

    /// <summary>
    /// Check if file exists
    /// </summary>
    Task<bool> FileExistsAsync(string path);

    /// <summary>
    /// Get file URL (for direct access if supported)
    /// </summary>
    string? GetFileUrl(string path);
}