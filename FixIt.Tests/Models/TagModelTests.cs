using Xunit;
using FixIt.Models.Issues;

namespace FixIt.Tests.Models;

public class TagModelTests
{
    [Fact]
    public void Tag_Constructor_PropertiesCanBeSet()
    {
        // Arrange
        var name = "pothole";
        var category = "Road Damage";

        // Act
        var tag = new Tag
        {
            Name = name,
            Category = category,
            UsageCount = 0,
            CreatedAt = System.DateTime.UtcNow
        };

        // Assert
        Assert.Equal(name, tag.Name);
        Assert.Equal(category, tag.Category);
        Assert.Equal(0, tag.UsageCount);
    }

    [Fact]
    public void Tag_UsageCount_CanBeIncremented()
    {
        // Arrange
        var tag = new Tag { UsageCount = 5 };

        // Act
        tag.UsageCount++;

        // Assert
        Assert.Equal(6, tag.UsageCount);
    }

    [Fact]
    public void Tag_UsageCount_CanBeDecremented()
    {
        // Arrange
        var tag = new Tag { UsageCount = 5 };

        // Act
        tag.UsageCount--;

        // Assert
        Assert.Equal(4, tag.UsageCount);
    }

    [Fact]
    public void Tag_Id_CanBeSet()
    {
        // Arrange
        var id = "507f1f77bcf86cd799439011";
        var tag = new Tag();

        // Act
        tag.Id = id;

        // Assert
        Assert.Equal(id, tag.Id);
    }

    [Fact]
    public void Tag_Name_CanBeChanged()
    {
        // Arrange
        var tag = new Tag { Name = "old-name" };
        var newName = "new-name";

        // Act
        tag.Name = newName;

        // Assert
        Assert.Equal(newName, tag.Name);
    }

    [Fact]
    public void Tag_CreatedAt_IsUtcDateTime()
    {
        // Arrange
        var now = System.DateTime.UtcNow;
        var tag = new Tag { CreatedAt = now };

        // Act & Assert
        Assert.Equal(now.Kind, tag.CreatedAt.Kind);
    }

    [Fact]
    public void Tag_Description_CanBeNull()
    {
        // Arrange & Act
        var tag = new Tag { Description = null };

        // Assert
        Assert.Null(tag.Description);
    }

    [Fact]
    public void Tag_Description_CanBeSet()
    {
        // Arrange
        var description = "Damage to road surface";
        var tag = new Tag();

        // Act
        tag.Description = description;

        // Assert
        Assert.Equal(description, tag.Description);
    }

    [Fact]
    public void Tag_Category_CanBeNull()
    {
        // Arrange & Act
        var tag = new Tag { Category = null };

        // Assert
        Assert.Null(tag.Category);
    }
}
