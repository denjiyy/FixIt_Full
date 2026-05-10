using FixIt.Data.Repository.Contracts;
using FixIt.Models.Media;
using FixIt.Models.Enums;
using FixIt.Services.Contracts;
using FixIt.Services.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FixIt.Services;

public class MediaService : IMediaService
{
    private readonly IRepository<Models.Media.Media> _mediaRepo;
    private readonly IRepository<MediaReference> _mediaRefRepo;
    private readonly IFileStorage _fileStorage;
    private readonly CloudinaryService? _cloudinaryService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MediaService> _logger;

    private readonly long _maxFileSizeBytes;
    private readonly long _maxVideoFileSizeBytes;
    private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png" };
    private readonly string[] _allowedImageMimeTypes = { "image/jpeg", "image/png" };
    private readonly string[] _allowedVideoExtensions = { ".mp4", ".webm" };
    private readonly string[] _allowedVideoMimeTypes = { "video/mp4", "video/webm" };

    public MediaService(
        IRepository<Models.Media.Media> mediaRepo,
        IRepository<MediaReference> mediaRefRepo,
        IFileStorage fileStorage,
        CloudinaryService? cloudinaryService,
        IConfiguration configuration,
        ILogger<MediaService> logger)
    {
        _mediaRepo = mediaRepo;
        _mediaRefRepo = mediaRefRepo;
        _fileStorage = fileStorage;
        _cloudinaryService = cloudinaryService;
        _configuration = configuration;
        _logger = logger;

        // Get max file sizes from config (default 5MB for images, 100MB for videos)
        _maxFileSizeBytes = configuration.GetValue<long>("Media:MaxFileSizeBytes", 5 * 1024 * 1024);
        _maxVideoFileSizeBytes = configuration.GetValue<long>("Media:MaxVideoFileSizeBytes", 100 * 1024 * 1024);
    }

    public async Task<Models.Media.Media> UploadFileAsync(
        IFormFile file, 
        string ownerId, 
        MediaReferenceType referenceType, 
        string referenceId)
    {
        // Validate file
        var (isValid, errorMessage) = ValidateFile(file);
        if (!isValid)
        {
            throw new InvalidOperationException(errorMessage);
        }

        // Try to upload to Cloudinary first (if available), otherwise use local storage
        string? cloudinaryUrl = null;
        string? storagePath = null;

        if (_cloudinaryService != null)
        {
            _logger.LogInformation("Uploading file {FileName} to Cloudinary", file.FileName);
            cloudinaryUrl = await _cloudinaryService.UploadAsync(file);
            
            if (cloudinaryUrl == null)
            {
                _logger.LogWarning("Cloudinary upload failed for {FileName}, falling back to local storage", file.FileName);
            }
        }

        // If Cloudinary upload failed or unavailable, use local file storage
        if (cloudinaryUrl == null)
        {
            _logger.LogInformation("Using local file storage for {FileName}", file.FileName);
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            storagePath = $"uploads/{DateTime.UtcNow:yyyy/MM/dd}/{uniqueFileName}";

            try
            {
                // Upload to storage
                await using var stream = file.OpenReadStream();
                await _fileStorage.SaveFileAsync(storagePath, stream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload file {FileName} to local storage", file.FileName);
                throw new InvalidOperationException("Failed to upload file", ex);
            }
        }

        // Create thumbnail for images only (not for videos) - local storage only
        string? thumbnailPath = null;
        if (IsImage(file) && storagePath != null)
        {
            thumbnailPath = await CreateThumbnailAsync(file, storagePath);
        }

        // Create media record
        var media = new Models.Media.Media
        {
            OwnerId = ownerId,
            Type = DetermineMediaType(file),
            MimeType = file.ContentType,
            SizeBytes = file.Length,
            StoragePath = storagePath,
            CloudinaryUrl = cloudinaryUrl,
            ThumbnailPath = thumbnailPath,
            CreatedAt = DateTime.UtcNow
        };

        await _mediaRepo.InsertAsync(media);

        // Create media reference
        var mediaRef = new MediaReference
        {
            MediaId = media.Id,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            CreatedAt = DateTime.UtcNow
        };

        await _mediaRefRepo.InsertAsync(mediaRef);

        _logger.LogInformation("Uploaded file {FileName} ({MediaType}) for user {UserId}", 
            file.FileName, DetermineMediaType(file), ownerId);

        return media;
    }

    public async Task<List<Models.Media.Media>> UploadFilesAsync(
        IEnumerable<IFormFile> files, 
        string ownerId, 
        MediaReferenceType referenceType, 
        string referenceId)
    {
        var uploadedMedia = new List<Models.Media.Media>();
        var fileList = files.ToList();

        // Validate count
        var maxFiles = _configuration.GetValue<int>("Media:MaxFilesPerUpload", 10);
        if (fileList.Count > maxFiles)
        {
            throw new InvalidOperationException($"Cannot upload more than {maxFiles} files at once");
        }

        foreach (var file in fileList)
        {
            try
            {
                var media = await UploadFileAsync(file, ownerId, referenceType, referenceId);
                uploadedMedia.Add(media);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload file {FileName} in batch", file.FileName);
                
                // Clean up already uploaded files on error
                foreach (var uploaded in uploadedMedia)
                {
                    try
                    {
                        await DeleteMediaAsync(uploaded.Id);
                    }
                    catch { /* Ignore cleanup errors */ }
                }

                throw;
            }
        }

        return uploadedMedia;
    }

    public async Task<Models.Media.Media?> GetMediaByIdAsync(string mediaId)
    {
        return await _mediaRepo.GetByIdAsync(mediaId);
    }

    public async Task<(Stream stream, string contentType, string fileName)?> GetFileStreamAsync(string mediaId)
    {
        var media = await _mediaRepo.GetByIdAsync(mediaId);
        if (media == null)
        {
            return null;
        }

        // If media is stored in Cloudinary, we cannot directly return a stream
        // Instead, the caller should use the CloudinaryUrl directly
        if (!string.IsNullOrEmpty(media.CloudinaryUrl))
        {
            _logger.LogWarning("Media {MediaId} is stored in Cloudinary and cannot be streamed via local storage. Use CloudinaryUrl instead.", mediaId);
            return null;
        }

        if (string.IsNullOrEmpty(media.StoragePath))
        {
            _logger.LogWarning("Media {MediaId} has no storage path or Cloudinary URL", mediaId);
            return null;
        }

        var stream = await _fileStorage.GetFileStreamAsync(media.StoragePath);
        if (stream == null)
        {
            return null;
        }

        var fileName = Path.GetFileName(media.StoragePath);
        return (stream, media.MimeType, fileName);
    }

    public async Task DeleteMediaAsync(string mediaId)
    {
        var media = await _mediaRepo.GetByIdAsync(mediaId);
        if (media == null)
        {
            return;
        }

        // Check if media is referenced elsewhere
        var references = await _mediaRefRepo.FindAsync(r => r.MediaId == mediaId);
        if (references.Count() > 1)
        {
            _logger.LogWarning("Media {MediaId} has multiple references, not deleting from storage", mediaId);
            return;
        }

        // Delete from storage (local or Cloudinary)
        try
        {
            if (!string.IsNullOrEmpty(media.StoragePath))
            {
                await _fileStorage.DeleteFileAsync(media.StoragePath);
            
                if (!string.IsNullOrEmpty(media.ThumbnailPath))
                {
                    await _fileStorage.DeleteFileAsync(media.ThumbnailPath);
                }
            }
            else if (!string.IsNullOrEmpty(media.CloudinaryUrl))
            {
                // Note: We don't delete from Cloudinary to avoid accidental deletion
                // and because the URL might be used elsewhere. Rely on Cloudinary's retention policy.
                _logger.LogInformation("Media {MediaId} is stored in Cloudinary. Manual deletion from Cloudinary not implemented.", mediaId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete media files for {MediaId}", mediaId);
        }

        // Delete media references
        foreach (var reference in references)
        {
            await _mediaRefRepo.DeleteAsync(reference.Id);
        }

        // Delete media record
        await _mediaRepo.DeleteAsync(mediaId);

        _logger.LogInformation("Deleted media {MediaId}", mediaId);
    }

    public async Task<List<Models.Media.Media>> GetMediaForReferenceAsync(
        MediaReferenceType referenceType, 
        string referenceId)
    {
        var references = await _mediaRefRepo.FindAsync(
            r => r.ReferenceType == referenceType && r.ReferenceId == referenceId
        );

        var mediaList = new List<Models.Media.Media>();
        foreach (var reference in references)
        {
            var media = await _mediaRepo.GetByIdAsync(reference.MediaId);
            if (media != null)
            {
                mediaList.Add(media);
            }
        }

        return mediaList.OrderBy(m => m.CreatedAt).ToList();
    }

    public (bool isValid, string? errorMessage) ValidateFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return (false, "File is empty");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var isVideo = _allowedVideoExtensions.Contains(extension);
        var isImage = _allowedImageExtensions.Contains(extension);

        if (!isVideo && !isImage)
        {
            var allowedTypes = string.Join(", ", _allowedImageExtensions.Concat(_allowedVideoExtensions));
            return (false, $"File type {extension} is not allowed. Allowed types: {allowedTypes}");
        }

        // Check file size based on type
        var maxSize = isVideo ? _maxVideoFileSizeBytes : _maxFileSizeBytes;
        if (file.Length > maxSize)
        {
            var maxSizeMB = maxSize / (1024 * 1024);
            return (false, $"File size exceeds maximum allowed size of {maxSizeMB}MB");
        }

        // Validate MIME type
        var mimeTypeLower = file.ContentType.ToLowerInvariant();
        var allowedMimeTypes = isVideo ? _allowedVideoMimeTypes : _allowedImageMimeTypes;
        
        if (!allowedMimeTypes.Contains(mimeTypeLower))
        {
            var allowedMimes = string.Join(", ", allowedMimeTypes);
            return (false, $"File MIME type {file.ContentType} is not allowed. Allowed types: {allowedMimes}");
        }

        return (true, null);
    }

    private static bool IsImage(IFormFile file)
    {
        return file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVideo(IFormFile file)
    {
        return file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
    }

    private static MediaType DetermineMediaType(IFormFile file)
    {
        if (file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return MediaType.Image;
        if (file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return MediaType.Video;
        return MediaType.Document;
    }

    private async Task<string?> CreateThumbnailAsync(IFormFile file, string originalPath)
    {
        try
        {
            // For now, just return null - thumbnail generation requires image processing library
            // In production, you'd use ImageSharp, SkiaSharp, or similar
            // Example: resize to 300x300, save to thumbnails/ folder
            
            _logger.LogInformation("Thumbnail generation not implemented yet for {Path}", originalPath);
            return await Task.FromResult((string?)null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create thumbnail for {Path}", originalPath);
            return null;
        }
    }
}
