using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FixIt.Services.Storage;

/// <summary>
/// Local file system storage implementation
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _baseStoragePath;
    private readonly ILogger<LocalFileStorage> _logger;

    public LocalFileStorage(IConfiguration configuration, ILogger<LocalFileStorage> logger)
    {
        _logger = logger;
        
        // Get storage path from config or use default
        _baseStoragePath = configuration["Media:StoragePath"] 
            ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

        // Ensure directory exists
        Directory.CreateDirectory(_baseStoragePath);
        
        _logger.LogInformation("Using local file storage at: {Path}", _baseStoragePath);
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

    public async Task<Stream?> GetFileStreamAsync(string path)
    {
        var fullPath = GetFullPath(path);
        
        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("File not found: {Path}", fullPath);
            return null;
        }

        var memoryStream = new MemoryStream();
        await using (var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
        {
            await fileStream.CopyToAsync(memoryStream);
        }
        
        memoryStream.Position = 0;
        return memoryStream;
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
        // Remove leading slash if present
        relativePath = relativePath.TrimStart('/');
        
        return Path.Combine(_baseStoragePath, relativePath);
    }
}