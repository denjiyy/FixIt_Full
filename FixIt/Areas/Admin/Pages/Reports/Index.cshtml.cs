using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FixIt.Models.Moderation;
using FixIt.Models.Enums;
using FixIt.Data.Repository.Contracts;
using FixIt.Services.Constants;
using FixIt.Services.Contracts;
using System.Security.Claims;

namespace FixIt.Areas.Admin.Pages.Reports;

[Authorize(Policy = PolicyNames.AdminArea)]
public class IndexModel : PageModel
{
    private readonly IRepository<ContentReport> _reportRepository;
    private readonly ILogger<IndexModel> _logger;
    private readonly IAuditService _auditService;

    public List<ContentReport> Reports { get; set; } = new();
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int TotalReports { get; set; }
    public int TotalPages { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    public IndexModel(IRepository<ContentReport> reportRepository, ILogger<IndexModel> logger, IAuditService auditService)
    {
        _reportRepository = reportRepository;
        _logger = logger;
        _auditService = auditService;
    }

    public async Task OnGetAsync(int pageNumber = 1)
    {
        try
        {
            PageNumber = pageNumber;
            var allReports = await _reportRepository.FindAsync(r => true);
            var reportsList = allReports.ToList();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                reportsList = reportsList.Where(r => 
                    r.Reason.ToString().Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (r.Details ?? "").Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            TotalReports = reportsList.Count;
            TotalPages = (int)Math.Ceiling(TotalReports / (double)PageSize);

            Reports = reportsList
                .OrderByDescending(r => r.CreatedAt)
                .Skip((pageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            _logger.LogInformation("Admin viewed reports list");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading reports");
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(string reportId)
    {
        try
        {
            var report = await _reportRepository.GetByIdAsync(reportId);
            if (report == null)
                return NotFound();

            report.Status = ReportStatus.Upheld;
            report.ReviewedAt = DateTime.UtcNow;

            await _reportRepository.ReplaceAsync(reportId, report);

            _logger.LogInformation($"Report {report.Id} approved by admin");
            
            var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
            await _auditService.LogEventAsync(
                eventType: "ReportApproved",
                action: "Approve",
                resource: "ContentReport",
                resourceId: reportId,
                changes: new Dictionary<string, object>
                {
                    { "Status", ReportStatus.Upheld }
                },
                status: "Success"
            );
            
            TempData["SuccessMessage"] = "Report approved.";

            return RedirectToPage(new { pageNumber = PageNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving report");
            TempData["ErrorMessage"] = "Error approving report";
            return RedirectToPage(new { pageNumber = PageNumber });
        }
    }

    public async Task<IActionResult> OnPostRejectAsync(string reportId)
    {
        try
        {
            var report = await _reportRepository.GetByIdAsync(reportId);
            if (report == null)
                return NotFound();

            report.Status = ReportStatus.Dismissed;
            report.ReviewedAt = DateTime.UtcNow;

            await _reportRepository.ReplaceAsync(reportId, report);

            _logger.LogInformation($"Report {report.Id} rejected by admin");
            
            var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
            await _auditService.LogEventAsync(
                eventType: "ReportRejected",
                action: "Reject",
                resource: "ContentReport",
                resourceId: reportId,
                changes: new Dictionary<string, object>
                {
                    { "Status", ReportStatus.Dismissed }
                },
                status: "Success"
            );
            
            TempData["SuccessMessage"] = "Report rejected.";

            return RedirectToPage(new { pageNumber = PageNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting report");
            TempData["ErrorMessage"] = "Error rejecting report";
            return RedirectToPage(new { pageNumber = PageNumber });
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(string reportId)
    {
        try
        {
            // Only admins can delete reports
            if (!User.IsInRole(RoleNames.Admin))
            {
                _logger.LogWarning($"Moderator {User?.Identity?.Name} attempted to delete report {reportId}");
                TempData["ErrorMessage"] = "Only admins can delete reports.";
                return RedirectToPage(new { pageNumber = PageNumber });
            }

            var report = await _reportRepository.GetByIdAsync(reportId);
            if (report == null)
                return NotFound();

            await _reportRepository.DeleteAsync(reportId);

            _logger.LogWarning($"Report {report.Id} deleted by admin");
            
            var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
            await _auditService.LogEventAsync(
                eventType: "ReportDeleted",
                action: "Delete",
                resource: "ContentReport",
                resourceId: reportId,
                changes: new Dictionary<string, object>
                {
                    { "Reason", report.Reason },
                    { "Status", report.Status }
                },
                status: "Success"
            );
            
            TempData["SuccessMessage"] = "Report deleted.";

            return RedirectToPage(new { pageNumber = PageNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting report");
            TempData["ErrorMessage"] = "Error deleting report";
            return RedirectToPage(new { pageNumber = PageNumber });
        }
    }
}
