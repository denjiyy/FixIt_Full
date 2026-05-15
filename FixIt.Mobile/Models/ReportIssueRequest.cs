using System.Text.Json.Serialization;

namespace FixIt.Mobile.Models;

public class ReportIssueRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    [JsonIgnore]
    public Stream PhotoStream { get; set; } = Stream.Null;
    public string PhotoFileName { get; set; } = "issue-photo.jpg";
}
