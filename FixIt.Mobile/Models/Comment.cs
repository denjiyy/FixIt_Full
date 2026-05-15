namespace FixIt.Mobile.Models;

public class Comment
{
    public string Id { get; set; } = string.Empty;
    public string IssueId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorInitials => string.IsNullOrWhiteSpace(AuthorName) ? "F" : AuthorName[0].ToString().ToUpperInvariant();
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
