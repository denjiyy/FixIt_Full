using Xunit;
using FixIt.Models.Issues;
using FixIt.Models.Enums;
using FixIt.Models.Common;
using MongoDB.Driver.GeoJsonObjectModel;
using System;

namespace FixIt.Tests.Models;

public class IssueModelTests
{
    [Fact]
    public void Issue_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange
        var title = "Broken street light";
        var description = "Street light on Main St is not working";

        // Act
        var issue = new Issue
        {
            Title = title,
            Description = description,
            Status = IssueStatus.New,
            Priority = IssuePriority.Medium,
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(title, issue.Title);
        Assert.Equal(description, issue.Description);
        Assert.Equal(IssueStatus.New, issue.Status);
        Assert.Equal(IssuePriority.Medium, issue.Priority);
    }

    [Theory]
    [InlineData(IssueStatus.New)]
    [InlineData(IssueStatus.Confirmed)]
    [InlineData(IssueStatus.InProgress)]
    [InlineData(IssueStatus.Fixed)]
    [InlineData(IssueStatus.Rejected)]
    [InlineData(IssueStatus.Duplicate)]
    [InlineData(IssueStatus.Archived)]
    public void Issue_Status_CanBeSetToValidEnumValues(IssueStatus status)
    {
        // Arrange
        var issue = new Issue { Status = IssueStatus.New };

        // Act
        issue.Status = status;

        // Assert
        Assert.Equal(status, issue.Status);
    }

    [Theory]
    [InlineData(IssuePriority.Low)]
    [InlineData(IssuePriority.Medium)]
    [InlineData(IssuePriority.High)]
    [InlineData(IssuePriority.Critical)]
    public void Issue_Priority_CanBeSetToValidEnumValues(IssuePriority priority)
    {
        // Arrange
        var issue = new Issue { Priority = IssuePriority.Low };

        // Act
        issue.Priority = priority;

        // Assert
        Assert.Equal(priority, issue.Priority);
    }

    [Fact]
    public void Issue_DefaultStatus_IsNew()
    {
        // Arrange & Act
        var issue = new Issue();

        // Assert
        Assert.Equal(IssueStatus.New, issue.Status);
    }

    [Fact]
    public void Issue_Upvotes_IncrementWorks()
    {
        // Arrange
        var issue = new Issue { Upvotes = 0 };

        // Act
        issue.Upvotes++;

        // Assert
        Assert.Equal(1, issue.Upvotes);
    }

    [Fact]
    public void Issue_Downvotes_IncrementWorks()
    {
        // Arrange
        var issue = new Issue { Downvotes = 0 };

        // Act
        issue.Downvotes++;

        // Assert
        Assert.Equal(1, issue.Downvotes);
    }

    [Fact]
    public void Issue_CreatedAt_IsSet()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var issue = new Issue { CreatedAt = now };

        // Assert
        Assert.Equal(now, issue.CreatedAt);
    }

    [Fact]
    public void Issue_Id_CanBeSet()
    {
        // Arrange
        var id = "507f1f77bcf86cd799439011";
        var issue = new Issue();

        // Act
        issue.Id = id;

        // Assert
        Assert.Equal(id, issue.Id);
    }

    [Fact]
    public void Issue_CityId_CanBeSet()
    {
        // Arrange
        var cityId = "507f1f77bcf86cd799439012";
        var issue = new Issue();

        // Act
        issue.CityId = cityId;

        // Assert
        Assert.Equal(cityId, issue.CityId);
    }

    [Fact]
    public void Issue_TagIds_InitializedAsEmptySet()
    {
        // Arrange & Act
        var issue = new Issue();

        // Assert
        Assert.NotNull(issue.TagIds);
        Assert.Empty(issue.TagIds);
    }

    [Fact]
    public void Issue_MediaIds_InitializedAsEmptySet()
    {
        // Arrange & Act
        var issue = new Issue();

        // Assert
        Assert.NotNull(issue.MediaIds);
        Assert.Empty(issue.MediaIds);
    }

    [Fact]
    public void Issue_DefaultPriority_IsMedium()
    {
        // Arrange & Act
        var issue = new Issue();

        // Assert
        Assert.Equal(IssuePriority.Medium, issue.Priority);
    }

    [Fact]
    public void Issue_Version_DefaultIsOne()
    {
        // Arrange & Act
        var issue = new Issue();

        // Assert
        Assert.Equal(1, issue.Version);
    }

    [Fact]
    public void Issue_CommentCount_DefaultIsZero()
    {
        // Arrange & Act
        var issue = new Issue();

        // Assert
        Assert.Equal(0, issue.CommentCount);
    }

    [Fact]
    public void Issue_IsAnonymous_CanBeSet()
    {
        // Arrange
        var issue = new Issue { IsAnonymous = false };

        // Act
        issue.IsAnonymous = true;

        // Assert
        Assert.True(issue.IsAnonymous);
    }
}
