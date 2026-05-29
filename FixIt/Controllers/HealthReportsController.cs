using FixIt.Services.Analytics;
using FixIt.Services.Analytics.Models;
using FixIt.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Controllers;

/// <summary>
/// City health report endpoint consumed by the mobile app at
/// <c>api/health-reports/{cityId}</c>. Public, aggregate data.
/// </summary>
[ApiController]
[Route("api/health-reports")]
[Produces("application/json")]
public class HealthReportsController : ControllerBase
{
    private readonly IHealthReportService _healthReportService;
    private readonly ILogger<HealthReportsController> _logger;

    public HealthReportsController(IHealthReportService healthReportService, ILogger<HealthReportsController> logger)
    {
        _healthReportService = healthReportService;
        _logger = logger;
    }

    [HttpGet("{cityId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<HealthReport>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<HealthReport>>> GetCityHealthReport(string cityId)
    {
        if (string.IsNullOrWhiteSpace(cityId))
        {
            return BadRequest(ApiResponse<object>.CreateError("City ID is required"));
        }

        try
        {
            var report = await _healthReportService.GetCityHealthReportAsync(cityId);
            return Ok(ApiResponse<HealthReport>.CreateSuccess(report));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching health report for city {CityId}", cityId);
            return BadRequest(ApiResponse<object>.CreateError("Failed to fetch health report"));
        }
    }
}
