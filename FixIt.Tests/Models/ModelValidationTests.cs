using Xunit;
using FixIt.Models.Gamification;
using FixIt.Models.Engagement;
using FixIt.Models.Issues;
using FixIt.Models.Enums;

namespace FixIt.Tests.Models;

public class ModelValidationTests
{
    [Fact]
    public void Issue_WithValidProperties_CreatesSuccessfully()
    {
        // Arrange & Act
        var issue = new Issue
        {
            Id = "issue1",
            Title = "Broken Pothole",
            Description = "Large pothole on Main Street",
            CityId = "city1",
            Status = IssueStatus.New
        };

        // Assert
        Assert.NotNull(issue);
        Assert.Equal("Broken Pothole", issue.Title);
        Assert.Equal(IssueStatus.New, issue.Status);
    }

    [Fact]
    public void Issue_WithMaxLengthTitle_IsValid()
    {
        // Arrange
        var maxTitle = new string('a', 200);

        // Act
        var issue = new Issue { Id = "1", Title = maxTitle, CityId = "city1" };

        // Assert
        Assert.Equal(200, issue.Title.Length);
    }

    [Fact]
    public void Vote_WithUpValue_CreatesSuccessfully()
    {
        // Arrange & Act
        var vote = new Vote
        {
            Id = "vote1",
            IssueId = "issue1",
            UserId = "user1",
            Value = VoteType.Up
        };

        // Assert
        Assert.NotNull(vote);
        Assert.Equal(VoteType.Up, vote.Value);
    }

    [Fact]
    public void Vote_WithDownValue_CreatesSuccessfully()
    {
        // Arrange & Act
        var vote = new Vote
        {
            Id = "vote2",
            IssueId = "issue1",
            UserId = "user2",
            Value = VoteType.Down
        };

        // Assert
        Assert.NotNull(vote);
        Assert.Equal(VoteType.Down, vote.Value);
    }

    [Fact]
    public void Comment_WithValidProperties_CreatesSuccessfully()
    {
        // Arrange & Act
        var comment = new Comment
        {
            Id = "comment1",
            IssueId = "issue1",
            AuthorId = "user1",
            Text = "This is a helpful comment",
            IsAnonymous = false
        };

        // Assert
        Assert.NotNull(comment);
        Assert.Equal("This is a helpful comment", comment.Text);
        Assert.False(comment.IsAnonymous);
    }

    [Fact]
    public void UserReputation_WithInitialValues_CreatesSuccessfully()
    {
        // Arrange & Act
        var reputation = new UserReputation
        {
            Id = "user1",
            UserId = "user1",
            TotalPoints = 0,
            TrustLevel = 0
        };

        // Assert
        Assert.NotNull(reputation);
        Assert.Equal(0, reputation.TotalPoints);
        Assert.Equal(0, reputation.TrustLevel);
    }

    [Fact]
    public void Tag_WithValidProperties_CreatesSuccessfully()
    {
        // Arrange & Act
        var tag = new Tag
        {
            Id = "tag1",
            Name = "pothole",
            Category = "infrastructure",
            IsApproved = true
        };

        // Assert
        Assert.NotNull(tag);
        Assert.Equal("pothole", tag.Name);
        Assert.True(tag.IsApproved);
    }

    [Fact]
    public void IssueStatus_EnumValuesAreCorrect()
    {
        // Assert - verify enum values exist
        Assert.True(Enum.IsDefined(typeof(IssueStatus), IssueStatus.New));
        Assert.True(Enum.IsDefined(typeof(IssueStatus), IssueStatus.InProgress));
        Assert.True(Enum.IsDefined(typeof(IssueStatus), IssueStatus.Fixed));
    }

    [Fact]
    public void VoteType_EnumValuesAreCorrect()
    {
        // Assert - verify enum values
        Assert.Equal(-1, (int)VoteType.Down);
        Assert.Equal(1, (int)VoteType.Up);
    }

    [Fact]
    public void MediaReferenceType_EnumValuesAreCorrect()
    {
        // Assert - verify enum values exist
        Assert.True(Enum.IsDefined(typeof(MediaReferenceType), MediaReferenceType.Issue));
        Assert.True(Enum.IsDefined(typeof(MediaReferenceType), MediaReferenceType.Comment));
    }

    [Fact]
    public void Comment_IsAnonymous_DefaultsToFalse()
    {
        // Arrange & Act
        var comment = new Comment
        {
            Id = "comment1",
            IssueId = "issue1",
            AuthorId = "user1",
            Text = "Comment text"
        };

        // Assert
        Assert.False(comment.IsAnonymous);
    }

    [Fact]
    public void Vote_CreatedAt_IsSetByDefault()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var vote = new Vote
        {
            Id = "vote1",
            IssueId = "issue1",
            UserId = "user1",
            Value = VoteType.Up
        };

        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.True(vote.CreatedAt >= beforeCreation && vote.CreatedAt <= afterCreation.AddSeconds(1));
    }
}
