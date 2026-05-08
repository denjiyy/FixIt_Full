using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Text.Json;
using FixIt.Models.Users;
using FixIt.Models.Common;
using FixIt.Models.Issues;
using FixIt.Models.Engagement;
using FixIt.Models.Safety;
using FixIt.Models.Media;
using FixIt.Data.Repository.Contracts;
using FixIt.Services.Contracts;

namespace FixIt.Areas.Identity.Pages.Account.Manage
{
    [Authorize]
    public class ManageModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<ManageModel> _logger;
        private readonly IRepository<Issue> _issueRepository;
        private readonly IRepository<Comment> _commentRepository;
        private readonly IRepository<Hazard> _hazardRepository;
        private readonly IRepository<Vote> _voteRepository;
        private readonly IRepository<ViewEvent> _viewEventRepository;
        private readonly IRepository<Media> _mediaRepository;
        private readonly IMediaService _mediaService;

        public ManageModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<ManageModel> logger,
            IRepository<Issue> issueRepository,
            IRepository<Comment> commentRepository,
            IRepository<Hazard> hazardRepository,
            IRepository<Vote> voteRepository,
            IRepository<ViewEvent> viewEventRepository,
            IRepository<Media> mediaRepository,
            IMediaService mediaService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _issueRepository = issueRepository;
            _commentRepository = commentRepository;
            _hazardRepository = hazardRepository;
            _voteRepository = voteRepository;
            _viewEventRepository = viewEventRepository;
            _mediaRepository = mediaRepository;
            _mediaService = mediaService;
        }

        [TempData]
        public string? StatusMessage { get; set; }

        [TempData]
        public bool StatusMessageIsError { get; set; }

        public ApplicationUser? CurrentUser { get; set; }
        public string? Email { get; set; }
        public bool IsEmailConfirmed { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            CurrentUser = user;
            Email = await _userManager.GetEmailAsync(user);
            IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);

            return Page();
        }

        public async Task<IActionResult> OnPostExportDataAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var userId = user.Id.ToString();
            var userIssues = (await _issueRepository.FindAsync(i => i.Reporter.Id == userId)).ToList();
            var userComments = (await _commentRepository.FindAsync(c => c.AuthorId == userId)).ToList();
            var userHazards = (await _hazardRepository.FindAsync(h => h.ReportedByUserId == userId || h.InternalUserId == userId)).ToList();
            var userVotes = (await _voteRepository.FindAsync(v => v.UserId == userId)).ToList();
            var userViews = (await _viewEventRepository.FindAsync(v => v.UserId == userId)).ToList();
            var userMedia = (await _mediaRepository.FindAsync(m => m.OwnerId == userId)).ToList();

            var exportPayload = new
            {
                exportedAtUtc = DateTime.UtcNow,
                account = new
                {
                    id = userId,
                    user.UserName,
                    user.Email,
                    user.DisplayName,
                    user.Bio,
                    user.CreatedAt,
                    user.ProfileVisibility,
                    user.AnonymousReportingEnabled,
                    user.EmailNotificationsEnabled,
                    user.ReceiveHealthReports,
                    user.ReceiveHazardAlerts,
                    user.ReceiveWeeklyReminders,
                    user.CrimeAlertsEnabled,
                    user.AccidentAlertsEnabled,
                    user.InfrastructureAlertsEnabled,
                    user.AllHazardAlertsEnabled,
                    user.AlertRadiusKm,
                    user.HazardSeverityThreshold
                },
                issues = userIssues.Select(i => new
                {
                    i.Id,
                    i.Title,
                    i.Description,
                    i.IsAnonymous,
                    i.Status,
                    i.Priority,
                    i.CreatedAt,
                    i.UpdatedAt
                }),
                comments = userComments.Select(c => new
                {
                    c.Id,
                    c.IssueId,
                    c.Text,
                    c.IsAnonymous,
                    c.IsDeleted,
                    c.CreatedAt
                }),
                hazards = userHazards.Select(h => new
                {
                    h.Id,
                    h.Title,
                    h.Description,
                    h.Type,
                    h.Severity,
                    h.IsAnonymous,
                    h.IsResolved,
                    h.CreatedAt,
                    h.UpdatedAt
                }),
                votes = userVotes.Select(v => new
                {
                    v.Id,
                    v.IssueId,
                    v.Value,
                    v.CreatedAt
                }),
                viewEvents = userViews.Select(v => new
                {
                    v.Id,
                    v.IssueId,
                    v.ViewedAt
                }),
                media = userMedia.Select(m => new
                {
                    m.Id,
                    m.Type,
                    m.MimeType,
                    m.SizeBytes,
                    m.CreatedAt
                })
            };

            var json = JsonSerializer.Serialize(exportPayload, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return File(
                Encoding.UTF8.GetBytes(json),
                "application/json",
                $"fixit-personal-data-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
        }

        public async Task<IActionResult> OnPostDeleteAccountAsync(string confirmation)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!string.Equals(confirmation?.Trim(), "DELETE", StringComparison.Ordinal))
            {
                StatusMessage = "Type DELETE exactly to confirm account deletion.";
                StatusMessageIsError = true;
                return RedirectToPage();
            }

            var userId = user.Id.ToString();
            var deletedAtUtc = DateTime.UtcNow;
            var anonymizedDisplayName = $"Deleted User {userId[..Math.Min(8, userId.Length)]}";

            // Remove linkage from user-generated content while preserving public issue history.
            var userIssues = (await _issueRepository.FindAsync(i => i.Reporter.Id == userId)).ToList();
            foreach (var issue in userIssues)
            {
                issue.IsAnonymous = true;
                issue.Reporter = new UserSummary
                {
                    Id = userId,
                    DisplayName = anonymizedDisplayName,
                    AvatarUrl = null
                };
                issue.UpdatedAt = deletedAtUtc;
                await _issueRepository.ReplaceAsync(issue.Id, issue);
            }

            var userComments = (await _commentRepository.FindAsync(c => c.AuthorId == userId)).ToList();
            foreach (var comment in userComments)
            {
                comment.IsAnonymous = true;
                comment.IsDeleted = true;
                comment.Text = "[deleted by user request]";
                comment.Author = null;
                comment.MediaIds.Clear();
                await _commentRepository.ReplaceAsync(comment.Id, comment);
            }

            var userHazards = (await _hazardRepository.FindAsync(h => h.ReportedByUserId == userId || h.InternalUserId == userId)).ToList();
            foreach (var hazard in userHazards)
            {
                hazard.IsAnonymous = true;
                hazard.ReportedByUserId = null;
                hazard.InternalUserId = null;
                hazard.UpdatedAt = deletedAtUtc;
                await _hazardRepository.ReplaceAsync(hazard.Id, hazard);
            }

            var userVotes = (await _voteRepository.FindAsync(v => v.UserId == userId)).ToList();
            foreach (var vote in userVotes)
            {
                await _voteRepository.DeleteAsync(vote.Id);
            }

            var userViews = (await _viewEventRepository.FindAsync(v => v.UserId == userId)).ToList();
            foreach (var viewEvent in userViews)
            {
                viewEvent.UserId = null;
                viewEvent.IpAddress = null;
                await _viewEventRepository.ReplaceAsync(viewEvent.Id, viewEvent);
            }

            var userMedia = (await _mediaRepository.FindAsync(m => m.OwnerId == userId)).ToList();
            foreach (var media in userMedia)
            {
                await _mediaService.DeleteMediaAsync(media.Id);
            }

            user.IsDeleted = true;
            user.DeletedAt = deletedAtUtc;
            user.DisplayName = anonymizedDisplayName;
            user.Bio = null;
            user.AvatarMediaId = null;
            user.PreferredCityId = null;
            user.OfficialDepartment = null;
            user.OfficialTitle = null;
            user.IsVerifiedOfficial = false;
            user.EmailNotificationsEnabled = false;
            user.ReceiveHealthReports = false;
            user.ReceiveHazardAlerts = false;
            user.ReceiveWeeklyReminders = false;
            user.CrimeAlertsEnabled = false;
            user.AccidentAlertsEnabled = false;
            user.InfrastructureAlertsEnabled = false;
            user.AllHazardAlertsEnabled = false;
            user.ProfileVisibility = "private";
            user.HazardSeverityThreshold = "All";
            user.ExternalIdentities.Clear();
            user.AnonymousReportingEnabled = true;
            user.RefreshTokenVersion += 1;
            user.LastRefreshTokenIssuedAt = deletedAtUtc;
            user.IsBanned = true;
            user.BannedAt = deletedAtUtc;
            user.BannedReason = "Account deleted by user request";

            // Reassign non-identifying credentials and clear contact fields.
            var deletedUserName = $"deleted_{Guid.NewGuid():N}";
            user.UserName = deletedUserName;
            user.NormalizedUserName = deletedUserName.ToUpperInvariant();
            user.Email = null;
            user.NormalizedEmail = null;
            user.PhoneNumber = null;
            user.PhoneNumberConfirmed = false;
            user.EmailConfirmed = false;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                StatusMessage = "Account deletion failed. Please contact support.";
                StatusMessageIsError = true;
                _logger.LogWarning("Failed to delete account for user {UserId}: {Errors}",
                    userId,
                    string.Join("; ", updateResult.Errors.Select(e => e.Description)));
                return RedirectToPage();
            }

            await _signInManager.SignOutAsync();
            _logger.LogInformation("User {UserId} deleted their account", userId);
            return Redirect("/?accountDeleted=true");
        }
    }
}
