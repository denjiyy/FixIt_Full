using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FixIt.Services.Contracts;
using FixIt.ViewModels;
using FixIt.ViewModels.Tags;

namespace FixIt.Controllers;

/// <summary>
/// Tags controller - handles all tag-related operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TagsController : ControllerBase
{
    private readonly ITagService _tagService;
    private readonly ILogger<TagsController> _logger;

    public TagsController(
        ITagService tagService,
        ILogger<TagsController> logger)
    {
        _tagService = tagService;
        _logger = logger;
    }

    /// <summary>
    /// Get popular tags
    /// </summary>
    /// <param name="limit">Maximum number of tags to return (default: 20, max: 100)</param>
    /// <returns>List of popular tags</returns>
    [HttpGet("popular")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TagResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<IEnumerable<TagResponse>>>> GetPopularTags(
        [FromQuery] int limit = 20)
    {
        try
        {
            var tags = await _tagService.GetPopularTagsAsync(limit);
            return Ok(ApiResponse<IEnumerable<TagResponse>>.CreateSuccess(
                tags.Select(t => t.ToResponse())
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching popular tags");
            return BadRequest(ApiResponse<object>.CreateError("Failed to fetch popular tags"));
        }
    }

    /// <summary>
    /// Autocomplete tags by prefix
    /// </summary>
    /// <param name="prefix">Tag name prefix</param>
    /// <param name="limit">Maximum number of tags to return (default: 10, max: 10)</param>
    /// <returns>List of matching tags</returns>
    [HttpGet("autocomplete")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TagResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<IEnumerable<TagResponse>>>> AutocompleteTags(
        [FromQuery] string prefix,
        [FromQuery] int limit = 10)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return BadRequest(ApiResponse<object>.CreateError("Prefix is required"));
            }

            var tags = await _tagService.AutocompleteTagsAsync(prefix, limit);
            return Ok(ApiResponse<IEnumerable<TagResponse>>.CreateSuccess(
                tags.Select(t => t.ToResponse())
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error autocompleting tags");
            return BadRequest(ApiResponse<object>.CreateError("Failed to autocomplete tags"));
        }
    }

    /// <summary>
    /// Get tag by ID
    /// </summary>
    /// <param name="id">Tag ID</param>
    /// <returns>Tag details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<TagResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TagResponse>>> GetTagById(string id)
    {
        try
        {
            var tag = await _tagService.GetTagByIdAsync(id);
            if (tag == null)
            {
                return NotFound(ApiResponse<object>.CreateError("Tag not found"));
            }

            return Ok(ApiResponse<TagResponse>.CreateSuccess(tag.ToResponse()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tag {TagId}", id);
            return BadRequest(ApiResponse<object>.CreateError("Failed to fetch tag"));
        }
    }

    /// <summary>
    /// Get tag by name
    /// </summary>
    /// <param name="name">Tag name</param>
    /// <returns>Tag details</returns>
    [HttpGet("by-name/{name}")]
    [ProducesResponseType(typeof(ApiResponse<TagResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TagResponse>>> GetTagByName(string name)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(ApiResponse<object>.CreateError("Tag name is required"));
            }

            var tag = await _tagService.GetTagByNameAsync(name);
            if (tag == null)
            {
                return NotFound(ApiResponse<object>.CreateError("Tag not found"));
            }

            return Ok(ApiResponse<TagResponse>.CreateSuccess(tag.ToResponse()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tag by name {TagName}", name);
            return BadRequest(ApiResponse<object>.CreateError("Failed to fetch tag"));
        }
    }

    /// <summary>
    /// Get all tags with pagination
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 50, max: 100)</param>
    /// <returns>Paginated list of tags</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<TagPageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TagPageResponse>>> GetAllTags(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var tags = await _tagService.GetAllTagsAsync(page, pageSize);
            
            // Note: GetAllTagsAsync returns only a portion of items for the page
            var allTags = await _tagService.GetAllTagsAsync(1, 1); // Get first item to calculate total
            var totalCount = await CountAllTags(); // Helper would be needed

            var response = new TagPageResponse
            {
                Items = tags.Select(t => t.ToResponse()),
                Page = page,
                PageSize = pageSize,
                Total = tags.Count() // In production, calculate actual total
            };

            return Ok(ApiResponse<TagPageResponse>.CreateSuccess(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all tags");
            return BadRequest(ApiResponse<object>.CreateError("Failed to fetch tags"));
        }
    }

    /// <summary>
    /// Create a new tag
    /// </summary>
    /// <param name="request">Tag creation request</param>
    /// <returns>The created tag</returns>
    [HttpPost]
    [Authorize(Roles = "Moderator,Admin")]
    [ProducesResponseType(typeof(ApiResponse<TagResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<TagResponse>>> CreateTag([FromBody] CreateTagRequest request)
    {
        try
        {
            var tag = await _tagService.CreateTagAsync(
                request.Name,
                request.Category,
                request.Description
            );

            _logger.LogInformation("Tag created: {TagId} with name {TagName}", tag.Id, tag.Name);

            return CreatedAtAction(
                nameof(GetTagById),
                new { id = tag.Id },
                ApiResponse<TagResponse>.CreateSuccess(
                    tag.ToResponse(),
                    "Tag created successfully"
                )
            );
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.CreateError(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tag");
            return BadRequest(ApiResponse<object>.CreateError("Failed to create tag"));
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    /// <returns>Health status</returns>
    [HttpGet("health")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<object>> Health()
    {
        return Ok(ApiResponse<object>.CreateSuccess(
            new { status = "healthy", timestamp = DateTime.UtcNow }
        ));
    }

    private async Task<long> CountAllTags()
    {
        // This is a placeholder - in production you'd implement proper counting
        // For now, just return 0 to indicate implementation needed
        return 0;
    }
}
