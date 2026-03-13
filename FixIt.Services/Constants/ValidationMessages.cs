namespace FixIt.Services.Constants;

/// <summary>
/// Centralized validation error messages for the FixIt application.
/// </summary>
public static class ValidationMessages
{
    // Issue validation messages
    public const string IssuesTitleRequired = "Title is required";
    public const string IssuesTitleTooLong = "Title must be 200 characters or less";
    public const string IssuesDescriptionRequired = "Description is required";
    public const string IssuesDescriptionTooLong = "Description must be 5000 characters or less";
    public const string IssuesIdRequired = "Issue ID is required";
    public const string IssuesAuthorIdRequired = "Author ID is required";
    public const string IssuesLongitudeRequired = "Longitude is required";
    public const string IssuesLatitudeRequired = "Latitude is required";
    public const string IssuesCityIdRequired = "City ID is required";
    public const string IssuesStatusRequired = "New status is required";
    public const string IssuesPriorityRequired = "Priority is required";
    public const string IssuesVoteTypeRequired = "Vote type is required";

    // Issue comment validation messages
    public const string IssuesCommentTextRequired = "Comment text is required";
    public const string IssuesCommentTooLong = "Comment must not exceed 5000 characters";

    // Tag validation messages
    public const string TagsNameRequired = "Tag name is required";
    public const string TagsNameTooLong = "Tag name must be 50 characters or less";
    public const string TagsPrefixRequired = "Prefix is required";



    // Generic validation messages
    public const string InvalidInput = "Invalid input provided";
    public const string OperationFailed = "The operation could not be completed";
}
