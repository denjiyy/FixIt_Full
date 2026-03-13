using Xunit;
using FixIt.Models.Common;
using FixIt.Data.Repository.Contracts;

namespace FixIt.Tests.Utilities;

public class PagedResultTests
{
    [Fact]
    public void PagedResult_CanBeCreated()
    {
        // Arrange
        var items = new List<string> { "item1", "item2", "item3" };
        var total = 10L;

        // Act
        var pagedResult = new PagedResult<string>
        {
            Items = items,
            Total = total
        };

        // Assert
        Assert.Equal(items, pagedResult.Items);
        Assert.Equal(total, pagedResult.Total);
    }

    [Fact]
    public void PagedResult_Items_CanBeEmpty()
    {
        // Arrange & Act
        var pagedResult = new PagedResult<string>
        {
            Items = new List<string>(),
            Total = 0
        };

        // Assert
        Assert.Empty(pagedResult.Items);
        Assert.Equal(0, pagedResult.Total);
    }

    [Fact]
    public void PagedResult_Items_DefaultIsEmpty()
    {
        // Arrange & Act
        var pagedResult = new PagedResult<string>();

        // Assert
        Assert.NotNull(pagedResult.Items);
        Assert.Empty(pagedResult.Items);
    }

    [Fact]
    public void PagedResult_Total_CanBeSet()
    {
        // Arrange
        var pagedResult = new PagedResult<int>();

        // Act
        pagedResult.Total = 100;

        // Assert
        Assert.Equal(100, pagedResult.Total);
    }

    [Fact]
    public void PagedResult_WithMultipleItems()
    {
        // Arrange
        var items = new List<int> { 1, 2, 3, 4, 5 };

        // Act
        var pagedResult = new PagedResult<int>
        {
            Items = items,
            Total = 50
        };

        // Assert
        Assert.Equal(5, pagedResult.Items.Count());
        Assert.Equal(50, pagedResult.Total);
    }
}

public class UserSummaryTests
{
    [Fact]
    public void UserSummary_CanBeCreated()
    {
        // Arrange
        var id = "507f1f77bcf86cd799439011";
        var displayName = "John Doe";

        // Act
        var summary = new UserSummary
        {
            Id = id,
            DisplayName = displayName
        };

        // Assert
        Assert.Equal(id, summary.Id);
        Assert.Equal(displayName, summary.DisplayName);
    }

    [Fact]
    public void UserSummary_DisplayName_CanBeUpdated()
    {
        // Arrange
        var summary = new UserSummary { DisplayName = "Old Name" };

        // Act
        summary.DisplayName = "New Name";

        // Assert
        Assert.Equal("New Name", summary.DisplayName);
    }

    [Fact]
    public void UserSummary_AvatarUrl_CanBeSet()
    {
        // Arrange
        var url = "https://example.com/avatar.jpg";
        var summary = new UserSummary();

        // Act
        summary.AvatarUrl = url;

        // Assert
        Assert.Equal(url, summary.AvatarUrl);
    }

    [Fact]
    public void UserSummary_AvatarUrl_CanBeNull()
    {
        // Arrange & Act
        var summary = new UserSummary { AvatarUrl = null };

        // Assert
        Assert.Null(summary.AvatarUrl);
    }

    [Fact]
    public void UserSummary_Id_CanBeSet()
    {
        // Arrange
        var summary = new UserSummary();
        var id = "507f1f77bcf86cd799439012";

        // Act
        summary.Id = id;

        // Assert
        Assert.Equal(id, summary.Id);
    }
}
