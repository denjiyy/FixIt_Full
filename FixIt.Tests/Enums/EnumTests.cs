using Xunit;
using FixIt.Models.Enums;

namespace FixIt.Tests.Enums;

public class IssueStatusEnumTests
{
    [Fact]
    public void IssueStatus_HasAllRequiredValues()
    {
        // Assert
        Assert.True(System.Enum.IsDefined(typeof(IssueStatus), IssueStatus.New));
        Assert.True(System.Enum.IsDefined(typeof(IssueStatus), IssueStatus.Confirmed));
        Assert.True(System.Enum.IsDefined(typeof(IssueStatus), IssueStatus.InProgress));
        Assert.True(System.Enum.IsDefined(typeof(IssueStatus), IssueStatus.Fixed));
        Assert.True(System.Enum.IsDefined(typeof(IssueStatus), IssueStatus.Rejected));
        Assert.True(System.Enum.IsDefined(typeof(IssueStatus), IssueStatus.Duplicate));
        Assert.True(System.Enum.IsDefined(typeof(IssueStatus), IssueStatus.Archived));
    }

    [Fact]
    public void IssueStatus_CanBeParsedFromString()
    {
        // Act & Assert
        Assert.True(System.Enum.TryParse(nameof(IssueStatus.New), out IssueStatus status));
        Assert.Equal(IssueStatus.New, status);
    }

    [Fact]
    public void IssueStatus_CanBeConvertedToString()
    {
        // Act
        var statusStr = IssueStatus.Fixed.ToString();

        // Assert
        Assert.Equal("Fixed", statusStr);
    }
}

public class IssuePriorityEnumTests
{
    [Fact]
    public void IssuePriority_HasAllRequiredValues()
    {
        // Assert
        Assert.True(System.Enum.IsDefined(typeof(IssuePriority), IssuePriority.Low));
        Assert.True(System.Enum.IsDefined(typeof(IssuePriority), IssuePriority.Medium));
        Assert.True(System.Enum.IsDefined(typeof(IssuePriority), IssuePriority.High));
        Assert.True(System.Enum.IsDefined(typeof(IssuePriority), IssuePriority.Critical));
    }

    [Fact]
    public void IssuePriority_CanBeParsedFromString()
    {
        // Act & Assert
        Assert.True(System.Enum.TryParse(nameof(IssuePriority.Critical), out IssuePriority priority));
        Assert.Equal(IssuePriority.Critical, priority);
    }

    [Theory]
    [InlineData(IssuePriority.Low, 0)]
    [InlineData(IssuePriority.Medium, 1)]
    [InlineData(IssuePriority.High, 2)]
    [InlineData(IssuePriority.Critical, 3)]
    public void IssuePriority_HasOrderedValues(IssuePriority priority, int expectedValue)
    {
        // Assert
        Assert.Equal(expectedValue, (int)priority);
    }
}

public class UserRoleEnumTests
{
    [Fact]
    public void UserRole_HasAllRequiredValues()
    {
        // Assert
        Assert.True(System.Enum.IsDefined(typeof(UserRole), UserRole.User));
        Assert.True(System.Enum.IsDefined(typeof(UserRole), UserRole.Moderator));
        Assert.True(System.Enum.IsDefined(typeof(UserRole), UserRole.Admin));
    }

    [Fact]
    public void UserRole_CanBeParsedFromString()
    {
        // Act & Assert
        Assert.True(System.Enum.TryParse(nameof(UserRole.Admin), out UserRole role));
        Assert.Equal(UserRole.Admin, role);
    }

    [Fact]
    public void UserRole_AdminIsHighestLevel()
    {
        // Assert
        Assert.Equal(2, (int)UserRole.Admin);
    }
}

public class IssueTypeEnumTests
{
    [Fact]
    public void IssueType_HasBaseValue()
    {
        // Assert
        Assert.True(System.Enum.IsDefined(typeof(IssueType), IssueType.Base));
    }

    [Fact]
    public void IssueType_CanBeParsedFromString()
    {
        // Act & Assert
        Assert.True(System.Enum.TryParse(nameof(IssueType.Base), out IssueType type));
        Assert.Equal(IssueType.Base, type);
    }
}

public class VoteTypeEnumTests
{
    [Fact]
    public void VoteType_HasUpAndDownVote()
    {
        // Assert
        Assert.True(System.Enum.IsDefined(typeof(VoteType), VoteType.Up));
        Assert.True(System.Enum.IsDefined(typeof(VoteType), VoteType.Down));
    }

    [Fact]
    public void VoteType_UpIsOne()
    {
        // Assert
        Assert.Equal(1, (int)VoteType.Up);
    }

    [Fact]
    public void VoteType_DownIsNegativeOne()
    {
        // Assert
        Assert.Equal(-1, (int)VoteType.Down);
    }
}

public class MediaTypeEnumTests
{
    [Fact]
    public void MediaType_HasAllRequiredValues()
    {
        // Assert - valid enum values should exist
        Assert.True(System.Enum.GetNames(typeof(MediaType)).Length > 0);
    }
}
