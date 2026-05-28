using FixIt.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using FixIt.Services.Safety;
using FixIt.Services.AI;
using FixIt.Models.Safety;
using FixIt.ViewModels;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Users;
using FixIt.Services.Constants;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FixIt.Controllers;

/// <summary>
/// Safety controller - handles hazard mode and anonymous mode operations
/// Provides APIs for real-time hazard reporting and safety features
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SafetyController : ControllerBase
{
    private readonly IHazardService _hazardService;
    private readonly IRepository<Hazard> _hazardRepo;
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICivicAiService _civicAiService;
    private readonly ILogger<SafetyController> _logger;

    public SafetyController(
        IHazardService hazardService,
        IRepository<Hazard> hazardRepo,
        IRepository<ApplicationUser> userRepo,
        ICivicAiService civicAiService,
        UserManager<ApplicationUser> userManager,
        ILogger<SafetyController> logger)
    {
        _hazardService = hazardService;
        _hazardRepo = hazardRepo;
        _userRepo = userRepo;
        _civicAiService = civicAiService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Get nearby hazards (Hazard Mode - Real-time safety alerts)
    /// Returns all active hazards within specified radius
    /// </summary>
    /// <param name="cityId">City ID</param>
    /// <param name="latitude">User's current latitude</param>
    /// <param name="longitude">User's current longitude</param>
    /// <param name="radiusKm">Search radius in kilometers (default: 5)</param>
    /// <returns>List of nearby hazards with safety information</returns>
    [HttpGet("nearby-hazards")]
    [ProducesResponseType(typeof(ApiResponse<List<HazardAlertResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<List<HazardAlertResponse>>>> GetNearbyHazards(
        [FromQuery] string cityId,
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusKm = 5.0)
    {
        try
        {
            if (string.IsNullOrEmpty(cityId))
                return BadRequest(ApiResponse<object>.CreateError("City ID is required"));

            if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
                return BadRequest(ApiResponse<object>.CreateError("Invalid coordinates"));

            var hazards = await _hazardService.GetNearbyHazardsAsync(cityId, latitude, longitude, radiusKm);
            
            var response = hazards.Select(h => new HazardAlertResponse
            {
                Id = h.Id,
                Type = h.Type.ToString(),
                Severity = h.Severity.ToString(),
                Title = h.Title,
                Description = h.Description,
                Latitude = h.Location.Coordinates.Latitude,
                Longitude = h.Location.Coordinates.Longitude,
                Address = h.Address ?? "Unknown",
                Confirmations = h.Confirmations,
                IsResolved = h.IsResolved,
                Distance = CalculateDistance(latitude, longitude, 
                    h.Location.Coordinates.Latitude, 
                    h.Location.Coordinates.Longitude),
                CreatedAt = h.CreatedAt,
                Reporter = h.IsAnonymous ? "Anonymous" : "Verified User"
            }).OrderBy(h => h.Distance).ToList();

            return Ok(ApiResponse<List<HazardAlertResponse>>.CreateSuccess(response, 
                $"Found {response.Count} hazards nearby"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching nearby hazards");
            return BadRequest(ApiResponse<object>.CreateError("Failed to fetch nearby hazards"));
        }
    }

    /// <summary>
    /// Get critical hazards in city (Safety Filter)
    /// Returns only high and critical severity hazards
    /// </summary>
    /// <param name="cityId">City ID</param>
    /// <returns>List of critical hazards</returns>
    [HttpGet("critical-hazards")]
    [ProducesResponseType(typeof(ApiResponse<List<HazardAlertResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<HazardAlertResponse>>>> GetCriticalHazards(
        [FromQuery] string cityId)
    {
        try
        {
            if (string.IsNullOrEmpty(cityId))
                return BadRequest(ApiResponse<object>.CreateError("City ID is required"));

            var hazards = await _hazardService.GetActiveSafetyHazardsAsync(cityId);

            var response = hazards.Select(h => new HazardAlertResponse
            {
                Id = h.Id,
                Type = h.Type.ToString(),
                Severity = h.Severity.ToString(),
                Title = h.Title,
                Description = h.Description,
                Latitude = h.Location.Coordinates.Latitude,
                Longitude = h.Location.Coordinates.Longitude,
                Address = h.Address ?? "Unknown",
                Confirmations = h.Confirmations,
                IsResolved = h.IsResolved,
                Distance = 0,
                CreatedAt = h.CreatedAt,
                Reporter = h.IsAnonymous ? "Anonymous" : "Verified User"
            }).ToList();

            return Ok(ApiResponse<List<HazardAlertResponse>>.CreateSuccess(response,
                $"Found {response.Count} critical hazards"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching critical hazards");
            return BadRequest(ApiResponse<object>.CreateError("Failed to fetch critical hazards"));
        }
    }

    /// <summary>
    /// Report a hazard (unified endpoint for both anonymous and authenticated reports)
    /// Allows users to report hazards with option to keep identity private
    /// </summary>
    /// <param name="request">Hazard report details</param>
    /// <returns>Created hazard information</returns>
    [HttpPost("report")]
    [ApiAuthorize]
    [ProducesResponseType(typeof(ApiResponse<HazardDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<HazardDetailResponse>>> ReportHazard(
        [FromBody] AnonymousHazardReportRequest request)
    {
        try
        {
            // Validate request
            if (request == null || string.IsNullOrEmpty(request.CityId))
                return BadRequest(ApiResponse<object>.CreateError("City ID is required"));

            if (string.IsNullOrEmpty(request.Title) || string.IsNullOrEmpty(request.Description))
                return BadRequest(ApiResponse<object>.CreateError("Title and description are required"));

            if (request.Latitude < -90 || request.Latitude > 90 || request.Longitude < -180 || request.Longitude > 180)
                return BadRequest(ApiResponse<object>.CreateError("Invalid coordinates"));

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));

            // Check if user has anonymous reporting enabled in their privacy settings
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized(ApiResponse<object>.CreateError("User not found"));

            // Use the user's privacy setting, not the request flag
            bool isAnonymous = user.AnonymousReportingEnabled;

            var hazard = await _hazardService.CreateHazardAsync(
                cityId: request.CityId,
                type: request.Type,
                severity: request.Severity,
                title: request.Title,
                description: request.Description,
                latitude: request.Latitude,
                longitude: request.Longitude,
                address: request.Address ?? "",
                userId: userId,
                isAnonymous: isAnonymous
            );

            _logger.LogInformation(
                isAnonymous ? "Anonymous hazard reported: {HazardId}" : "Hazard reported: {HazardId} by user {UserId}",
                hazard.Id, userId);

            return CreatedAtAction(
                nameof(GetHazardById),
                new { id = hazard.Id },
                ApiResponse<HazardDetailResponse>.CreateSuccess(
                    HazardToDetailResponse(hazard),
                    isAnonymous ? "Hazard reported anonymously" : "Hazard reported successfully"
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting hazard");
            return BadRequest(ApiResponse<object>.CreateError("Failed to report hazard"));
        }
    }

    /// <summary>
    /// Report a hazard anonymously (legacy endpoint - redirects to unified report endpoint)
    /// </summary>
    [HttpPost("report-anonymous")]
    [ApiAuthorize]
    public async Task<ActionResult<ApiResponse<HazardDetailResponse>>> ReportHazardAnonymous(
        [FromBody] AnonymousHazardReportRequest request)
    {
        request.IsAnonymous = true;
        return await ReportHazard(request);
    }

    /// <summary>
    /// Confirm/verify a hazard (Community Safety Feature)
    /// Multiple users can confirm a hazard to increase its credibility
    /// </summary>
    /// <param name="hazardId">Hazard ID</param>
    /// <returns>Updated confirmation count</returns>
    [HttpPost("{hazardId}/confirm")]
    [ApiAuthorize]
    [ProducesResponseType(typeof(ApiResponse<HazardConfirmationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<HazardConfirmationResponse>>> ConfirmHazard(string hazardId)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));

            var success = await _hazardService.ConfirmHazardAsync(hazardId, userId);
            if (!success)
                return NotFound(ApiResponse<object>.CreateError("Hazard not found or already resolved"));

            var hazard = await _hazardService.GetHazardAsync(hazardId);
            if (hazard == null)
                return NotFound(ApiResponse<object>.CreateError("Hazard not found"));

            _logger.LogInformation("Hazard {HazardId} confirmed by user {UserId}", hazardId, userId);

            return Ok(ApiResponse<HazardConfirmationResponse>.CreateSuccess(
                new HazardConfirmationResponse
                {
                    HazardId = hazard.Id,
                    Confirmations = hazard.Confirmations,
                    IsResolved = hazard.IsResolved
                },
                "Hazard confirmed successfully"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming hazard");
            return BadRequest(ApiResponse<object>.CreateError("Failed to confirm hazard"));
        }
    }

    /// <summary>
    /// Resolve a hazard (Administrator Feature)
    /// Mark a hazard as resolved after it has been addressed
    /// </summary>
    /// <param name="hazardId">Hazard ID</param>
    /// <param name="request">Resolution details</param>
    /// <returns>Resolved hazard information</returns>
    [HttpPost("{hazardId}/resolve")]
    [ApiAuthorize(PolicyNames.AdminOnly)]
    [ProducesResponseType(typeof(ApiResponse<HazardDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<HazardDetailResponse>>> ResolveHazard(
        string hazardId,
        [FromBody] ResolveHazardRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));

            var success = await _hazardService.ResolveHazardAsync(hazardId, userId, request.Notes);
            if (!success)
                return NotFound(ApiResponse<object>.CreateError("Hazard not found"));

            var hazard = await _hazardService.GetHazardAsync(hazardId);
            if (hazard == null)
                return NotFound(ApiResponse<object>.CreateError("Hazard not found"));

            _logger.LogInformation("Hazard {HazardId} resolved by user {UserId}", hazardId, userId);

            return Ok(ApiResponse<HazardDetailResponse>.CreateSuccess(
                HazardToDetailResponse(hazard),
                "Hazard resolved successfully"
            ));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving hazard");
            return BadRequest(ApiResponse<object>.CreateError("Failed to resolve hazard"));
        }
    }

    /// <summary>
    /// Get hazard by ID
    /// </summary>
    /// <param name="id">Hazard ID</param>
    /// <returns>Hazard details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<HazardDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<HazardDetailResponse>>> GetHazardById(string id)
    {
        try
        {
            var hazard = await _hazardService.GetHazardAsync(id);
            if (hazard == null)
                return NotFound(ApiResponse<object>.CreateError("Hazard not found"));

            var response = HazardToDetailResponse(hazard);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            bool isReporter = !string.IsNullOrEmpty(userId) && (hazard.ReportedByUserId == userId || hazard.InternalUserId == userId);
            bool isAdmin = User.IsInRole(RoleNames.Admin);

            response.CanEdit = isReporter || isAdmin;
            response.CanDelete = isReporter || isAdmin;
            response.CanRestore = isAdmin && hazard.IsDeleted;

            return Ok(ApiResponse<HazardDetailResponse>.CreateSuccess(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching hazard {HazardId}", id);
            return BadRequest(ApiResponse<object>.CreateError("Failed to fetch hazard"));
        }
    }

    /// <summary>
    /// Get hazard statistics for city (Safety Analytics)
    /// </summary>
    /// <param name="cityId">City ID</param>
    /// <returns>Hazard statistics and breakdown</returns>
    [HttpGet("city/{cityId}/statistics")]
    [ProducesResponseType(typeof(ApiResponse<HazardStatisticsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HazardStatisticsResponse>>> GetHazardStatistics(string cityId)
    {
        try
        {
            var hazards = await _hazardService.GetCityHazardsAsync(cityId, includeResolved: false);
            var breakdown = await _hazardService.GetHazardBreakdownAsync(cityId);

            var response = new HazardStatisticsResponse
            {
                TotalHazards = hazards.Count,
                CriticalHazards = hazards.Count(h => h.Severity == HazardSeverity.Critical),
                HighHazards = hazards.Count(h => h.Severity == HazardSeverity.High),
                MediumHazards = hazards.Count(h => h.Severity == HazardSeverity.Medium),
                LowHazards = hazards.Count(h => h.Severity == HazardSeverity.Low),
                AverageConfirmations = hazards.Count > 0 ? hazards.Average(h => h.Confirmations) : 0,
                HazardsByType = breakdown.ToDictionary(k => k.Key.ToString(), v => v.Value)
            };

            return Ok(ApiResponse<HazardStatisticsResponse>.CreateSuccess(response,
                "Hazard statistics retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching hazard statistics");
            return BadRequest(ApiResponse<object>.CreateError("Failed to fetch hazard statistics"));
        }
    }

    /// <summary>
    /// Get all hazards for a city (optional include resolved)
    /// </summary>
    [HttpGet("city/{cityId}/hazards")]
    [ProducesResponseType(typeof(ApiResponse<List<HazardDetailResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<HazardDetailResponse>>>> GetCityHazards(string cityId, [FromQuery] bool includeResolved = false)
    {
        try
        {
            if (string.IsNullOrEmpty(cityId))
                return BadRequest(ApiResponse<object>.CreateError("City ID is required"));

            var hazards = await _hazardService.GetCityHazardsAsync(cityId, includeResolved);
            var response = hazards.Select(HazardToDetailResponse).ToList();
            return Ok(ApiResponse<List<HazardDetailResponse>>.CreateSuccess(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching city hazards");
            return BadRequest(ApiResponse<object>.CreateError("Failed to fetch hazards"));
        }
    }

    [HttpPost("insights/cluster")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> SummarizeHazardCluster([FromBody] HazardClusterInsightRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.TotalReports <= 0)
        {
            return BadRequest(new { error = "Cluster payload is required." });
        }

        try
        {
            var result = await _civicAiService.GenerateHazardInsightAsync(ToHazardInsightInput(request), cancellationToken);

            return Ok(new
            {
                content = result.Content,
                aiGenerated = result.AiGenerated,
                fallbackUsed = result.FallbackUsed
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating hazard insight");
            return StatusCode(500, new { error = "Failed to generate hazard insight." });
        }
    }

    [HttpPost("insights/cluster/stream")]
    public async Task StreamHazardClusterInsight([FromBody] HazardClusterInsightRequest request, CancellationToken cancellationToken)
    {
        Response.ContentType = "application/x-ndjson";

        if (request == null || request.TotalReports <= 0)
        {
            await WriteNdjsonEventAsync(new AiStreamEvent { Type = "error", Message = "Cluster payload is required." }, cancellationToken);
            return;
        }

        try
        {
            await foreach (var aiEvent in _civicAiService.StreamHazardInsightAsync(ToHazardInsightInput(request), cancellationToken))
            {
                await WriteNdjsonEventAsync(aiEvent, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming hazard insight");
            await WriteNdjsonEventAsync(new AiStreamEvent { Type = "error", Message = "Failed to stream hazard insight." }, cancellationToken);
        }
    }

    /// <summary>
    /// Toggle anonymous reporting for user
    /// </summary>
    /// <param name="request">Toggle request</param>
    /// <returns>Updated anonymous reporting status</returns>
    [HttpPost("anonymous-reporting/toggle")]
    [ApiAuthorize]
    // Browser clients use cookie auth here, so CSRF protection is required.
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(ApiResponse<AnonymousReportingStatusResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AnonymousReportingStatusResponse>>> ToggleAnonymousReporting(
        [FromBody] ToggleAnonymousReportingRequest request)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));

            user.AnonymousReportingEnabled = request.Enabled;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return BadRequest(ApiResponse<object>.CreateError("Failed to update setting"));

            _logger.LogInformation("User {UserId} toggled anonymous reporting: {Enabled}", user.Id, request.Enabled);

            return Ok(ApiResponse<AnonymousReportingStatusResponse>.CreateSuccess(
                new AnonymousReportingStatusResponse
                {
                    AnonymousReportingEnabled = user.AnonymousReportingEnabled
                },
                "Anonymous reporting setting updated"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling anonymous reporting");
            return BadRequest(ApiResponse<object>.CreateError("Failed to update setting"));
        }
    }

    /// <summary>
    /// Updates hazard alert preferences for the current user.
    /// </summary>
    [HttpPost("alert-preferences")]
    [ApiAuthorize]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(ApiResponse<AlertPreferencesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AlertPreferencesResponse>>> UpdateAlertPreferences(
        [FromBody] UpdateAlertPreferencesRequest request)
    {
        if (request == null)
        {
            return BadRequest(ApiResponse<object>.CreateError("Request payload is required."));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));
        }

        if (request.CrimeAlerts.HasValue)
        {
            user.CrimeAlertsEnabled = request.CrimeAlerts.Value;
        }

        if (request.AccidentAlerts.HasValue)
        {
            user.AccidentAlertsEnabled = request.AccidentAlerts.Value;
        }

        if (request.InfrastructureAlerts.HasValue)
        {
            user.InfrastructureAlertsEnabled = request.InfrastructureAlerts.Value;
        }

        if (request.AllHazards.HasValue)
        {
            user.AllHazardAlertsEnabled = request.AllHazards.Value;
        }

        if (request.AlertRadius.HasValue)
        {
            if (request.AlertRadius.Value < 1 || request.AlertRadius.Value > 50)
            {
                return BadRequest(ApiResponse<object>.CreateError("Alert radius must be between 1 and 50 km."));
            }

            user.AlertRadiusKm = request.AlertRadius.Value;
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(ApiResponse<object>.CreateError("Failed to update alert preferences."));
        }

        return Ok(ApiResponse<AlertPreferencesResponse>.CreateSuccess(
            new AlertPreferencesResponse
            {
                CrimeAlerts = user.CrimeAlertsEnabled,
                AccidentAlerts = user.AccidentAlertsEnabled,
                InfrastructureAlerts = user.InfrastructureAlertsEnabled,
                AllHazards = user.AllHazardAlertsEnabled,
                AlertRadiusKm = user.AlertRadiusKm,
                SeverityThreshold = user.HazardSeverityThreshold
            },
            "Alert preferences updated"));
    }

    /// <summary>
    /// Updates hazard severity threshold for notifications.
    /// </summary>
    [HttpPost("alert-preferences/severity")]
    [ApiAuthorize]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(typeof(ApiResponse<AlertPreferencesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AlertPreferencesResponse>>> UpdateAlertSeverityThreshold(
        [FromBody] UpdateAlertSeverityRequest request)
    {
        var normalizedThreshold = NormalizeSeverityThreshold(request?.Threshold);
        if (normalizedThreshold == null)
        {
            return BadRequest(ApiResponse<object>.CreateError("Severity threshold must be one of: All, High, Critical."));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));
        }

        user.HazardSeverityThreshold = normalizedThreshold;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(ApiResponse<object>.CreateError("Failed to update severity threshold."));
        }

        return Ok(ApiResponse<AlertPreferencesResponse>.CreateSuccess(
            new AlertPreferencesResponse
            {
                CrimeAlerts = user.CrimeAlertsEnabled,
                AccidentAlerts = user.AccidentAlertsEnabled,
                InfrastructureAlerts = user.InfrastructureAlertsEnabled,
                AllHazards = user.AllHazardAlertsEnabled,
                AlertRadiusKm = user.AlertRadiusKm,
                SeverityThreshold = user.HazardSeverityThreshold
            },
            "Severity threshold updated"));
    }

    /// <summary>
    /// Delete a hazard (soft delete)
    /// Only the user who reported the hazard or an administrator can delete it
    /// </summary>
    /// <param name="id">Hazard ID</param>
    /// <returns>Success response</returns>
    [HttpDelete("{id}")]
    [ApiAuthorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteHazard(string id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));

            var hazard = await _hazardService.GetHazardAsync(id);
            if (hazard == null)
                return NotFound(ApiResponse<object>.CreateError("Hazard not found"));

            // Check if hazard is already deleted
            if (hazard.IsDeleted)
                return NotFound(ApiResponse<object>.CreateError("Hazard not found"));

            // Only allow deletion by hazard reporter or admins
            bool isReporter = hazard.ReportedByUserId == userId || hazard.InternalUserId == userId;
            bool isAdmin = User.IsInRole(RoleNames.Admin);

            if (!isReporter && !isAdmin)
            {
                return Forbid();
            }

            await _hazardService.SoftDeleteHazardAsync(id, userId);

            _logger.LogInformation("Hazard {HazardId} deleted by user {UserId}", id, userId);

            return Ok(ApiResponse<object>.CreateSuccess(
                new { message = "Hazard deleted successfully" },
                "Hazard deleted"
            ));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Hazard {HazardId} was not found for delete", id);
            return NotFound(ApiResponse<object>.CreateError("Hazard not found"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting hazard {HazardId}", id);
            return BadRequest(ApiResponse<object>.CreateError("Failed to delete hazard"));
        }
    }

    /// <summary>
    /// Restore a soft-deleted hazard (admin only)
    /// </summary>
    [HttpPost("{id}/restore")]
    [ApiAuthorize(PolicyNames.AdminOnly)]
    public async Task<ActionResult<ApiResponse<object>>> RestoreHazard(string id)
    {
        try
        {
            await _hazardService.RestoreHazardAsync(id);
            _logger.LogInformation("Hazard {HazardId} restored by admin", id);
            return Ok(ApiResponse<object>.CreateSuccess(new { message = "Hazard restored" }, "Hazard restored"));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Hazard {HazardId} was not found for restore", id);
            return NotFound(ApiResponse<object>.CreateError("Hazard not found"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring hazard {HazardId}", id);
            return BadRequest(ApiResponse<object>.CreateError("Failed to restore hazard"));
        }
    }

    /// <summary>
    /// Update a hazard (partial update) - allowed for reporter or admin
    /// </summary>
    [HttpPut("{id}")]
    [ApiAuthorize]
    [ProducesResponseType(typeof(ApiResponse<HazardDetailResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HazardDetailResponse>>> UpdateHazard(string id, [FromBody] UpdateHazardRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));

            var hazard = await _hazardService.GetHazardAsync(id);
            if (hazard == null)
                return NotFound(ApiResponse<object>.CreateError("Hazard not found"));

            bool isReporter = hazard.ReportedByUserId == userId || hazard.InternalUserId == userId;
            bool isAdmin = User.IsInRole(RoleNames.Admin);
            if (!isReporter && !isAdmin)
                return Forbid();

            var updated = await _hazardService.UpdateHazardAsync(
                id,
                type: request.Type,
                severity: request.Severity,
                title: request.Title,
                description: request.Description,
                latitude: request.Latitude,
                longitude: request.Longitude,
                address: request.Address,
                expiresAt: request.ExpiresAt);

            return Ok(ApiResponse<HazardDetailResponse>.CreateSuccess(HazardToDetailResponse(updated), "Hazard updated"));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Hazard {HazardId} was not found for update", id);
            return NotFound(ApiResponse<object>.CreateError("Hazard not found"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating hazard {HazardId}", id);
            return BadRequest(ApiResponse<object>.CreateError("Failed to update hazard"));
        }
    }

    // Helper methods

    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth radius in km
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static HazardDetailResponse HazardToDetailResponse(Hazard hazard)
    {
        return new HazardDetailResponse
        {
            Id = hazard.Id,
            Type = hazard.Type.ToString(),
            Severity = hazard.Severity.ToString(),
            Title = hazard.Title,
            Description = hazard.Description,
            Latitude = hazard.Location.Coordinates.Latitude,
            Longitude = hazard.Location.Coordinates.Longitude,
            Address = hazard.Address ?? "Unknown",
            Confirmations = hazard.Confirmations,
            IsResolved = hazard.IsResolved,
            IsAnonymous = hazard.IsAnonymous,
            CreatedAt = hazard.CreatedAt,
            ResolvedAt = hazard.ResolvedAt,
            ResolutionNotes = hazard.ResolutionNotes
        };
    }

    private static HazardInsightInput ToHazardInsightInput(HazardClusterInsightRequest request)
    {
        return new HazardInsightInput
        {
            CityId = request.CityId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            TotalReports = request.TotalReports,
            TotalConfirmations = request.TotalConfirmations,
            FromUtc = request.FromUtc,
            ToUtc = request.ToUtc,
            HazardTypes = request.HazardTypes
                .Select(h => new HazardTypeFrequencyInput
                {
                    Type = h.Type,
                    Count = h.Count
                })
            .ToList()
        };
    }

    private static string? NormalizeSeverityThreshold(string? threshold)
    {
        if (string.IsNullOrWhiteSpace(threshold))
        {
            return null;
        }

        return threshold.Trim().ToLowerInvariant() switch
        {
            "all" => "All",
            "high" => "High",
            "critical" => "Critical",
            _ => null
        };
    }

    private async Task WriteNdjsonEventAsync(AiStreamEvent aiEvent, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(aiEvent);
        await Response.WriteAsync(json + "\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}

// Request/Response DTOs
public class AnonymousHazardReportRequest
{
    public string CityId { get; set; } = string.Empty;
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HazardType Type { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HazardSeverity Severity { get; set; }
    
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Address { get; set; }
    public bool IsAnonymous { get; set; } = false;
}

public class ResolveHazardRequest
{
    public string? Notes { get; set; }
}

public class HazardAlertResponse
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Address { get; set; } = string.Empty;
    public int Confirmations { get; set; }
    public bool IsResolved { get; set; }
    public double Distance { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Reporter { get; set; } = string.Empty;
}

public class HazardDetailResponse
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Address { get; set; } = string.Empty;
    public int Confirmations { get; set; }
    public bool IsResolved { get; set; }
    public bool IsAnonymous { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanRestore { get; set; }
}

public class HazardConfirmationResponse
{
    public string HazardId { get; set; } = string.Empty;
    public int Confirmations { get; set; }
    public bool IsResolved { get; set; }
}

public class HazardStatisticsResponse
{
    public int TotalHazards { get; set; }
    public int CriticalHazards { get; set; }
    public int HighHazards { get; set; }
    public int MediumHazards { get; set; }
    public int LowHazards { get; set; }
    public double AverageConfirmations { get; set; }
    public Dictionary<string, int> HazardsByType { get; set; } = new();
}

public class ToggleAnonymousReportingRequest
{
    public bool Enabled { get; set; }
}

public class AnonymousReportingStatusResponse
{
    public bool AnonymousReportingEnabled { get; set; }
}

public class UpdateAlertPreferencesRequest
{
    public bool? CrimeAlerts { get; set; }
    public bool? AccidentAlerts { get; set; }
    public bool? InfrastructureAlerts { get; set; }
    public bool? AllHazards { get; set; }
    public int? AlertRadius { get; set; }
}

public class UpdateAlertSeverityRequest
{
    public string? Threshold { get; set; }
}

public class AlertPreferencesResponse
{
    public bool CrimeAlerts { get; set; }
    public bool AccidentAlerts { get; set; }
    public bool InfrastructureAlerts { get; set; }
    public bool AllHazards { get; set; }
    public int AlertRadiusKm { get; set; }
    public string SeverityThreshold { get; set; } = "All";
}

public class UpdateHazardRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HazardType? Type { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HazardSeverity? Severity { get; set; }

    public string? Title { get; set; }
    public string? Description { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Address { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class HazardClusterInsightRequest
{
    public string? CityId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int TotalReports { get; set; }
    public int TotalConfirmations { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public List<HazardClusterTypeCount> HazardTypes { get; set; } = new();
}

public class HazardClusterTypeCount
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
}
