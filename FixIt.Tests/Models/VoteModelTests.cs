using Xunit;
using FixIt.Models.Engagement;
using FixIt.Models.Enums;

namespace FixIt.Tests.Models;

public class VoteModelTests
{
    [Fact]
    public void Vote_CanBeCreated()
    {
        // Arrange
        var issueId = "507f1f77bcf86cd799439011";
        var userId = "507f1f77bcf86cd799439012";
        var voteValue = VoteType.Up;

        // Act
        var vote = new Vote
        {
            IssueId = issueId,
            UserId = userId,
            Value = voteValue
        };

        // Assert
        Assert.Equal(issueId, vote.IssueId);
        Assert.Equal(userId, vote.UserId);
        Assert.Equal(voteValue, vote.Value);
    }

    [Fact]
    public void Vote_Id_CanBeSet()
    {
        // Arrange
        var id = "507f1f77bcf86cd799439011";
        var vote = new Vote();

        // Act
        vote.Id = id;

        // Assert
        Assert.Equal(id, vote.Id);
    }

    [Theory]
    [InlineData(VoteType.Up)]
    [InlineData(VoteType.Down)]
    public void Vote_Value_CanBeChanged(VoteType voteType)
    {
        // Arrange
        var vote = new Vote { Value = VoteType.Up };

        // Act
        vote.Value = voteType;

        // Assert
        Assert.Equal(voteType, vote.Value);
    }

    [Fact]
    public void Vote_CreatedAt_CanBeSet()
    {
        // Arrange
        var now = System.DateTime.UtcNow;
        var vote = new Vote();

        // Act
        vote.CreatedAt = now;

        // Assert
        Assert.Equal(now, vote.CreatedAt);
    }

    [Fact]
    public void Vote_UpVote_HasValueOne()
    {
        // Arrange & Act
        var voteValue = VoteType.Up;

        // Assert
        Assert.Equal(1, (int)voteValue);
    }

    [Fact]
    public void Vote_DownVote_HasValueNegativeOne()
    {
        // Arrange & Act
        var voteValue = VoteType.Down;

        // Assert
        Assert.Equal(-1, (int)voteValue);
    }
}
