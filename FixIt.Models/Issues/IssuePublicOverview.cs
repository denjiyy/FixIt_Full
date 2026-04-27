namespace FixIt.Models.Issues;

public class IssuePublicOverview
{
    public int TotalIssues { get; set; }

    public int NewIssues { get; set; }

    public int ConfirmedIssues { get; set; }

    public int InProgressIssues { get; set; }

    public int FixedIssues { get; set; }

    public int CriticalIssues { get; set; }

    public int CitiesCovered { get; set; }

    public List<Issue> FeaturedIssues { get; set; } = new();

    public int ActiveIssues => NewIssues + ConfirmedIssues + InProgressIssues;

    public int ResolutionRatePercentage =>
        TotalIssues > 0
            ? (int)Math.Round(FixedIssues / (double)TotalIssues * 100, MidpointRounding.AwayFromZero)
            : 0;
}
