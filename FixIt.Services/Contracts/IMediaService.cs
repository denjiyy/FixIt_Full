using FixIt.Models.Media;
using FixIt.Models.Enums;
using Microsoft.AspNetCore.Http;

namespace FixIt.Services.Contracts;

public interface IMediaService
{
    /// <summary>
    /// Upload a single file
    /// </summary>
    Task<Media> UploadFileAsync(IFormFile file, string ownerId, MediaReferenceType referenceType, string referenceId);

    /// <summary>
    /// Upload multiple files
    /// </summary>
    Task<List<Media>> UploadFilesAsync(IEnumerable<IFormFile> files, string ownerId, MediaReferenceType referenceType, string referenceId);

    /// <summary>
    /// Get media by ID
    /// </summary>
    Task<Media?> GetMediaByIdAsync(string mediaId);

    /// <summary>
    /// Get file stream for download/display
    /// </summary>
    Task<(Stream stream, string contentType, string fileName)?> GetFileStreamAsync(string mediaId);

    /// <summary>
    /// Delete media and clean up storage
    /// </summary>
    Task DeleteMediaAsync(string mediaId);

    /// <summary>
    /// Get all media for a specific reference (e.g., all photos for an issue)
    /// </summary>
    Task<List<Media>> GetMediaForReferenceAsync(MediaReferenceType referenceType, string referenceId);

    /// <summary>
    /// Validate file upload
    /// </summary>
    (bool isValid, string? errorMessage) ValidateFile(IFormFile file);
}