using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FixIt.Services.Storage;

/// <summary>
/// Local file system storage implementation
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _baseStoragePath;
    private readonly string _normalizedBaseStoragePath;
    private readonly ILogger<LocalFileStorage> _logger;

    public LocalFileStorage(IConfiguration configuration, ILogger<LocalFileStorage> logger)
    {
        _logger = logger;
        
        // Get storage path from config or use default
        _baseStoragePath = configuration["Media:StoragePath"] 
            ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        _normalizedBaseStoragePath = Path.GetFullPath(_baseStoragePath);

        // Ensure directory exists
        Directory.CreateDirectory(_normalizedBaseStoragePath);
        
        _logger.LogInformation("Using local file storage at: {Path}", _normalizedBaseStoragePath);
    }

    public async Task SaveFileAsync(string path, Stream fileStream)
    {
        var fullPath = GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStreamOutput = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await fileStream.CopyToAsync(fileStreamOutput);
        
        _logger.LogDebug("Saved file to {Path}", fullPath);
    }

    public Task<Stream?> GetFileStreamAsync(string path)
    {
        var fullPath = GetFullPath(path);
        
        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("File not found: {Path}", fullPath);
            return Task.FromResult<Stream?>(null);
        }

        // Return FileStream directly for seekability (important for video playback)
        // FileStream is seekable, allowing browsers to read video metadata and seek to different parts
        var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        
        _logger.LogDebug("Retrieved file stream for {Path}", fullPath);
        return Task.FromResult<Stream?>(fileStream);
    }

    public Task DeleteFileAsync(string path)
    {
        var fullPath = GetFullPath(path);
        
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogDebug("Deleted file: {Path}", fullPath);
        }

        return Task.CompletedTask;
    }

    public Task<bool> FileExistsAsync(string path)
    {
        var fullPath = GetFullPath(path);
        return Task.FromResult(File.Exists(fullPath));
    }

    public string? GetFileUrl(string path)
    {
        // For local storage, return the web-accessible path
        // This assumes files are stored in wwwroot/uploads
        return $"/uploads/{path.Replace("uploads/", "")}";
    }

    private string GetFullPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException("Storage path cannot be empty.");
        }

        // Normalize separators and avoid absolute-path injection.
        var normalizedRelativePath = relativePath.Trim().Replace('\\', '/').TrimStart('/');
        if (Path.IsPathRooted(normalizedRelativePath))
        {
            throw new UnauthorizedAccessException("Absolute paths are not allowed for file storage operations.");
        }

        var candidatePath = Path.GetFullPath(Path.Combine(_normalizedBaseStoragePath, normalizedRelativePath));
        var isWithinBasePath =
            candidatePath.Equals(_normalizedBaseStoragePath, StringComparison.Ordinal)
            || candidatePath.StartsWith(_normalizedBaseStoragePath + Path.DirectorySeparatorChar, StringComparison.Ordinal);

        if (!isWithinBasePath)
        {
            throw new UnauthorizedAccessException("Path traversal attempt detected.");
        }

        return candidatePath;
    }
}
