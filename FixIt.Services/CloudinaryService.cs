using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FixIt.Services;

/// <summary>
/// Service for uploading media files to Cloudinary.
/// Handles both image and video uploads with automatic media type detection.
/// </summary>
public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryService> _logger;
    private readonly long _maxFileSizeBytes;
    private readonly long _maxVideoFileSizeBytes;

    public CloudinaryService(
        Cloudinary cloudinary,
        IConfiguration configuration,
        ILogger<CloudinaryService> logger)
    {
        _cloudinary = cloudinary;
        _logger = logger;

        // Get max file sizes from config (default 5MB for images, 100MB for videos)
        _maxFileSizeBytes = configuration.GetValue<long>("Media:MaxFileSizeBytes", 5 * 1024 * 1024);
        _maxVideoFileSizeBytes = configuration.GetValue<long>("Media:MaxVideoFileSizeBytes", 100 * 1024 * 1024);
    }

    /// <summary>
    /// Uploads an image file to Cloudinary and returns the secure URL.
    /// </summary>
    /// <param name="file">The image file to upload</param>
    /// <returns>The Cloudinary secure URL, or null if upload fails</returns>
    public async Task<string?> UploadImageAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("Attempted to upload null or empty image file");
            return null;
        }

        if (file.Length > _maxFileSizeBytes)
        {
            _logger.LogWarning("Image file {FileName} exceeds maximum size limit ({MaxSize} bytes)", 
                file.FileName, _maxFileSizeBytes);
            return null;
        }

        try
        {
            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "fixit/images",
                Overwrite = false,
                Invalidate = false
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                _logger.LogError("Cloudinary image upload failed for {FileName}: {Error}", 
                    file.FileName, uploadResult.Error.Message);
                return null;
            }

            _logger.LogInformation("Image {FileName} uploaded to Cloudinary: {PublicId}", 
                file.FileName, uploadResult.PublicId);

            return uploadResult.SecureUrl.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while uploading image {FileName} to Cloudinary", file.FileName);
            return null;
        }
    }

    /// <summary>
    /// Uploads a video file to Cloudinary and returns the secure URL.
    /// </summary>
    /// <param name="file">The video file to upload</param>
    /// <returns>The Cloudinary secure URL, or null if upload fails</returns>
    public async Task<string?> UploadVideoAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("Attempted to upload null or empty video file");
            return null;
        }

        if (file.Length > _maxVideoFileSizeBytes)
        {
            _logger.LogWarning("Video file {FileName} exceeds maximum size limit ({MaxSize} bytes)", 
                file.FileName, _maxVideoFileSizeBytes);
            return null;
        }

        try
        {
            using var stream = file.OpenReadStream();
            var uploadParams = new VideoUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "fixit/videos",
                Overwrite = false,
                Invalidate = false,
                EagerAsync = true  // Process asynchronously for better performance
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                _logger.LogError("Cloudinary video upload failed for {FileName}: {Error}", 
                    file.FileName, uploadResult.Error.Message);
                return null;
            }

            _logger.LogInformation("Video {FileName} uploaded to Cloudinary: {PublicId}", 
                file.FileName, uploadResult.PublicId);

            return uploadResult.SecureUrl.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while uploading video {FileName} to Cloudinary", file.FileName);
            return null;
        }
    }

    /// <summary>
    /// Uploads a file to Cloudinary with automatic media type detection (image or video).
    /// Uses ResourceType.Auto to let Cloudinary determine the media type.
    /// </summary>
    /// <param name="file">The file to upload</param>
    /// <returns>The Cloudinary secure URL, or null if upload fails</returns>
    public async Task<string?> UploadAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("Attempted to upload null or empty file");
            return null;
        }

        // Determine file type and use appropriate upload method
        if (IsImage(file))
        {
            return await UploadImageAsync(file);
        }
        else if (IsVideo(file))
        {
            return await UploadVideoAsync(file);
        }
        else
        {
            _logger.LogWarning("File {FileName} has unsupported media type: {ContentType}", 
                file.FileName, file.ContentType);
            return null;
        }
    }

    private static bool IsImage(IFormFile file)
    {
        var allowedImageMimeTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
        return allowedImageMimeTypes.Contains(file.ContentType?.ToLowerInvariant());
    }

    private static bool IsVideo(IFormFile file)
    {
        var allowedVideoMimeTypes = new[] { "video/mp4", "video/webm" };
        return allowedVideoMimeTypes.Contains(file.ContentType?.ToLowerInvariant());
    }
}
