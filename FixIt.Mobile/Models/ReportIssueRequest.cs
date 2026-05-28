namespace FixIt.Mobile.Models;

public class ReportIssueRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string CityId { get; set; } = string.Empty;
    public string? Address { get; set; }
    public List<PhotoAttachment> Photos { get; set; } = new();
}
