namespace FixIt.Services.Constants;

/// <summary>
/// Centralized HTTP error response messages for the FixIt application.
/// </summary>
public static class HttpErrorMessages
{
    // User and authentication errors
    public const string UserIdentityNotFound = "User identity not found";
    public const string UnauthorizedAccess = "Unauthorized access";
    public const string AnonymousPostingNotEnabled = "Anonymous posting is not enabled for your account";

    // Issue operation errors
    public const string IssueNotFound = "Issue not found";
    public const string FailedToCreateIssue = "Failed to create issue";
    public const string FailedToFetchIssue = "Failed to fetch issue";
    public const string FailedToFetchIssues = "Failed to fetch issues";
    public const string FailedToSearchIssues = "Failed to search issues";
    public const string FailedToUpdateIssueStatus = "Failed to update issue status";
    public const string FailedToUpdateIssuePriority = "Failed to update issue priority";
    public const string FailedToUpdateIssue = "Failed to update issue";
    public const string FailedToDeleteIssue = "Failed to delete issue";
    public const string FailedToAddIssueTag = "Failed to add tag to issue";
    public const string FailedToRemoveIssueTag = "Failed to remove tag from issue";

    // Issue voting errors
    public const string FailedToRecordVote = "Failed to record vote";
    public const string FailedToRemoveVote = "Failed to remove vote";

    // Issue comment errors
    public const string FailedToAddComment = "Failed to add comment";
    public const string FailedToFetchComments = "Failed to fetch comments";
    public const string FailedToDeleteComment = "Failed to delete comment";

    // Tag operation errors
    public const string TagNotFound = "Tag not found";
    public const string FailedToCreateTag = "Failed to create tag";
    public const string FailedToFetchTags = "Failed to fetch tags";
    public const string FailedToFetchTagAutocomplete = "Failed to fetch tag autocomplete";
    public const string FailedToDeleteTag = "Failed to delete tag";


    public const string FailedToFetchCampaigns = "Failed to fetch campaigns";
    public const string FailedToCreateCampaign = "Failed to create campaign";
    public const string FailedToFetchCampaign = "Failed to fetch campaign";
    public const string FailedToContributeToCampaign = "Failed to contribute to campaign";

    // Generic API errors
    public const string InternalServerError = "Internal server error";
    public const string BadRequest = "Bad request";
    public const string NotFound = "Not found";
    public const string Forbidden = "Access denied";
}
