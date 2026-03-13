using Xunit;
using Moq;
using FixIt.Services;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Issues;
using System.Linq.Expressions;

namespace FixIt.Tests.Services;

public class TagServiceTests
{
    private readonly Mock<IRepository<Tag>> _tagRepoMock;
    private readonly TagService _tagService;

    public TagServiceTests()
    {
        _tagRepoMock = new Mock<IRepository<Tag>>();
        _tagService = new TagService(_tagRepoMock.Object);
    }

    [Fact]
    public async Task CreateTagAsync_WithValidName_CreatesTagSuccessfully()
    {
        // Arrange
        const string tagName = "Pothole";
        const string category = "Infrastructure";
        const string description = "Road damage with holes";

        Tag? capturedTag = null;

        _tagRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Tag, bool>>>()))
            .ReturnsAsync(new List<Tag>());

        _tagRepoMock.Setup(r => r.InsertAsync(It.IsAny<Tag>()))
            .Callback<Tag>(t => capturedTag = t)
            .ReturnsAsync((Tag t) => t);

        // Act
        var result = await _tagService.CreateTagAsync(tagName, category, description);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("pothole", result.Name); // Normalized to lowercase
        Assert.Equal(category, result.Category);
        Assert.Equal(description, result.Description);
        Assert.True(result.IsApproved);
        Assert.Equal(0, result.UsageCount);
        _tagRepoMock.Verify(r => r.InsertAsync(It.IsAny<Tag>()), Times.Once);
    }

    [Fact]
    public async Task CreateTagAsync_WithExistingTag_ReturnsExistingTag()
    {
        // Arrange
        const string tagName = "Pothole";
        var existingTag = new Tag { Id = "tag1", Name = "pothole" };

        _tagRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Tag, bool>>>()))
            .ReturnsAsync(new List<Tag> { existingTag });

        // Act
        var result = await _tagService.CreateTagAsync(tagName);

        // Assert
        Assert.Equal("tag1", result.Id);
        Assert.Equal("pothole", result.Name);
        _tagRepoMock.Verify(r => r.InsertAsync(It.IsAny<Tag>()), Times.Never);
    }

    [Fact]
    public async Task CreateTagAsync_WithNullName_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _tagService.CreateTagAsync(null!));
        Assert.Contains("Tag name is required", ex.Message);
    }

    [Fact]
    public async Task CreateTagAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _tagService.CreateTagAsync("   "));
        Assert.Contains("Tag name is required", ex.Message);
    }

    [Fact]
    public async Task CreateTagAsync_WithNameTooLong_ThrowsArgumentException()
    {
        // Arrange
        var longName = new string('a', 51);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _tagService.CreateTagAsync(longName));
        Assert.Contains("50 characters or less", ex.Message);
    }

    [Fact]
    public async Task AutocompleteTagsAsync_WithValidPrefix_ReturnsMatchingTags()
    {
        // Arrange
        const string prefix = "pot";
        var tags = new List<Tag>
        {
            new Tag { Id = "tag1", Name = "pothole", IsApproved = true },
            new Tag { Id = "tag2", Name = "potted-plant", IsApproved = true },
            new Tag { Id = "tag3", Name = "bridge", IsApproved = true }
        };

        _tagRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Tag, bool>>>()))
            .ReturnsAsync(new List<Tag>
            {
                tags[0], // pothole matches prefix
                tags[1]  // potted-plant matches prefix
            });

        // Act
        var result = await _tagService.AutocompleteTagsAsync(prefix);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains(result, t => t.Name == "pothole");
        Assert.Contains(result, t => t.Name == "potted-plant");
    }

    [Fact]
    public async Task AutocompleteTagsAsync_WithEmptyPrefix_ReturnsEmptyList()
    {
        // Act
        var result = await _tagService.AutocompleteTagsAsync("");

        // Assert
        Assert.Empty(result);
        _tagRepoMock.Verify(r => r.FindAsync(It.IsAny<Expression<Func<Tag, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task AutocompleteTagsAsync_RespectLimitParameter()
    {
        // Arrange
        const string prefix = "road";
        var tags = Enumerable.Range(1, 15)
            .Select(i => new Tag { Id = $"tag{i}", Name = $"road{i}", IsApproved = true })
            .ToList();

        _tagRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Tag, bool>>>()))
            .ReturnsAsync(tags);

        // Act
        var result = await _tagService.AutocompleteTagsAsync(prefix, limit: 5);

        // Assert
        Assert.Equal(5, result.Count());
    }

    [Fact]
    public async Task GetPopularTagsAsync_ReturnsTagsOrderedByUsageCount()
    {
        // Arrange
        var tags = new List<Tag>
        {
            new Tag { Id = "tag1", Name = "pothole", UsageCount = 100, IsApproved = true, CreatedAt = DateTime.UtcNow },
            new Tag { Id = "tag2", Name = "road-damage", UsageCount = 50, IsApproved = true, CreatedAt = DateTime.UtcNow },
            new Tag { Id = "tag3", Name = "traffic-light", UsageCount = 75, IsApproved = true, CreatedAt = DateTime.UtcNow }
        };

        _tagRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Tag, bool>>>()))
            .ReturnsAsync(tags);

        // Act
        var result = await _tagService.GetPopularTagsAsync();

        // Assert
        var tagList = result.ToList();
        Assert.Equal("pothole", tagList[0].Name); // 100 usages
        Assert.Equal("traffic-light", tagList[1].Name); // 75 usages
        Assert.Equal("road-damage", tagList[2].Name); // 50 usages
    }

    [Fact]
    public async Task GetTagByNameAsync_WithExistingTag_ReturnsTag()
    {
        // Arrange
        const string tagName = "Pothole";
        var expectedTag = new Tag { Id = "tag1", Name = "pothole" };

        _tagRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Tag, bool>>>()))
            .ReturnsAsync(new List<Tag> { expectedTag });

        // Act
        var result = await _tagService.GetTagByNameAsync(tagName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("tag1", result.Id);
        Assert.Equal("pothole", result.Name);
    }

    [Fact]
    public async Task GetTagByNameAsync_WithNonexistentTag_ReturnsNull()
    {
        // Arrange
        const string tagName = "NonExistent";

        _tagRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Tag, bool>>>()))
            .ReturnsAsync(new List<Tag>());

        // Act
        var result = await _tagService.GetTagByNameAsync(tagName);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTagByIdAsync_ReturnsTagById()
    {
        // Arrange
        const string tagId = "tag1";
        var expectedTag = new Tag { Id = tagId, Name = "pothole" };

        _tagRepoMock.Setup(r => r.GetByIdAsync(tagId))
            .ReturnsAsync(expectedTag);

        // Act
        var result = await _tagService.GetTagByIdAsync(tagId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(tagId, result.Id);
    }

    [Fact]
    public async Task IncrementUsageCountAsync_IncrementsCountAndUpdatesTime()
    {
        // Arrange
        const string tagId = "tag1";
        var tag = new Tag 
        { 
            Id = tagId, 
            Name = "pothole", 
            UsageCount = 5,
            UpdatedAt = DateTime.UtcNow.AddHours(-1)
        };

        Tag? updatedTag = null;

        _tagRepoMock.Setup(r => r.GetByIdAsync(tagId))
            .ReturnsAsync(tag);

        _tagRepoMock.Setup(r => r.ReplaceAsync(tagId, It.IsAny<Tag>()))
            .Callback<string, Tag>((_, t) => updatedTag = t)
            .Returns((string _, Tag t) => Task.FromResult(t));

        // Act
        await _tagService.IncrementUsageCountAsync(tagId);

        // Assert
        Assert.NotNull(updatedTag);
        Assert.Equal(6, updatedTag.UsageCount);
        Assert.True(updatedTag.UpdatedAt > tag.UpdatedAt);
    }
}
