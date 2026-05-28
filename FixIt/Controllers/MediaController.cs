using FixIt.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FixIt.Services.Contracts;
using FixIt.Services.Constants;
using FixIt.ViewModels;

namespace FixIt.Controllers;

/// <summary>
/// Media controller - handles file uploads and downloads
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MediaController : ControllerBase
{
    private readonly IMediaService _mediaService;
    private readonly ILogger<MediaController> _logger;

    public MediaController(IMediaService mediaService, ILogger<MediaController> logger)
    {
        _mediaService = mediaService;
        _logger = logger;
    }

    /// <summary>
    /// Get media file by ID
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMedia(string id)
    {
        try
        {
            var result = await _mediaService.GetFileStreamAsync(id);
            
            if (result == null)
            {
                return NotFound(ApiResponse<object>.CreateError("Media not found"));
            }

            var (stream, contentType, fileName) = result.Value;
            
            // Set required headers for video/image streaming
            Response.Headers["Content-Length"] = stream.Length.ToString();
            Response.Headers["Accept-Ranges"] = "bytes";
            
            // Return file without fileDownloadName so it plays inline in video/image tags
            // enableRangeProcessing = true enables HTTP range requests for seeking in videos
            return File(stream, contentType, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving media {MediaId}", id);
            return StatusCode(500, ApiResponse<object>.CreateError("Failed to retrieve media"));
        }
    }

    /// <summary>
    /// Get media metadata
    /// </summary>
    [HttpGet("{id}/info")]
    [AllowAnonymous]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> GetMediaInfo(string id)
    {
        try
        {
            var media = await _mediaService.GetMediaByIdAsync(id);
            
            if (media == null)
            {
                return NotFound(ApiResponse<object>.CreateError("Media not found"));
            }

            var info = new
            {
                id = media.Id,
                type = media.Type.ToString(),
                mimeType = media.MimeType,
                sizeBytes = media.SizeBytes,
                createdAt = media.CreatedAt
            };

            return Ok(ApiResponse<object>.CreateSuccess(info));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving media info {MediaId}", id);
            return StatusCode(500, ApiResponse<object>.CreateError("Failed to retrieve media info"));
        }
    }

    /// <summary>
    /// Delete media (owner only)
    /// </summary>
    [HttpDelete("{id}")]
    [ApiAuthorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteMedia(string id)
    {
        try
        {
            var media = await _mediaService.GetMediaByIdAsync(id);
            
            if (media == null)
            {
                return NotFound(ApiResponse<object>.CreateError("Media not found"));
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            // Only owner or admin can delete
            if (media.OwnerId != userId && !User.IsInRole(RoleNames.Admin))
            {
                return Forbid();
            }

            await _mediaService.DeleteMediaAsync(id);

            _logger.LogInformation("User {UserId} deleted media {MediaId}", userId, id);

            return Ok(ApiResponse<object>.CreateSuccess(
                new { message = "Media deleted successfully" },
                "Media deleted"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting media {MediaId}", id);
            return StatusCode(500, ApiResponse<object>.CreateError("Failed to delete media"));
        }
    }
}
