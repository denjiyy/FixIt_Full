using Xunit;
using FixIt.Models.Engagement;
using System;

namespace FixIt.Tests.Models;

public class CommentModelTests
{
    [Fact]
    public void Comment_CanBeCreated()
    {
        // Arrange
        var issueId = "507f1f77bcf86cd799439011";
        var authorId = "507f1f77bcf86cd799439012";
        var text = "I can help fix this!";

        // Act
        var comment = new Comment
        {
            IssueId = issueId,
            AuthorId = authorId,
            Text = text,
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(issueId, comment.IssueId);
        Assert.Equal(authorId, comment.AuthorId);
        Assert.Equal(text, comment.Text);
    }

    [Fact]
    public void Comment_Id_CanBeSet()
    {
        // Arrange
        var id = "507f1f77bcf86cd799439011";
        var comment = new Comment();

        // Act
        comment.Id = id;

        // Assert
        Assert.Equal(id, comment.Id);
    }

    [Fact]
    public void Comment_Text_CanBeUpdated()
    {
        // Arrange
        var comment = new Comment { Text = "Original text" };
        var newText = "Updated text";

        // Act
        comment.Text = newText;

        // Assert
        Assert.Equal(newText, comment.Text);
    }

    [Fact]
    public void Comment_CreatedAt_IsUtcDateTime()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var comment = new Comment { CreatedAt = now };

        // Act & Assert
        Assert.Equal(now.Kind, comment.CreatedAt.Kind);
        Assert.Equal(now, comment.CreatedAt);
    }

    [Fact]
    public void Comment_LikedBy_InitializedAsEmptySet()
    {
        // Arrange & Act
        var comment = new Comment();

        // Assert
        Assert.NotNull(comment.LikedBy);
        Assert.Empty(comment.LikedBy);
    }

    [Fact]
    public void Comment_LikedBy_CanAddUserIds()
    {
        // Arrange
        var comment = new Comment();
        var userId = "507f1f77bcf86cd799439011";

        // Act
        comment.LikedBy.Add(userId);

        // Assert
        Assert.Contains(userId, comment.LikedBy);
        Assert.Single(comment.LikedBy);
    }

    [Fact]
    public void Comment_DislikedBy_InitializedAsEmptySet()
    {
        // Arrange & Act
        var comment = new Comment();

        // Assert
        Assert.NotNull(comment.DislikedBy);
        Assert.Empty(comment.DislikedBy);
    }

    [Fact]
    public void Comment_DislikedBy_CanAddUserIds()
    {
        // Arrange
        var comment = new Comment();
        var userId = "507f1f77bcf86cd799439011";

        // Act
        comment.DislikedBy.Add(userId);

        // Assert
        Assert.Contains(userId, comment.DislikedBy);
        Assert.Single(comment.DislikedBy);
    }

    [Fact]
    public void Comment_IsAnonymous_DefaultIsFalse()
    {
        // Arrange & Act
        var comment = new Comment();

        // Assert
        Assert.False(comment.IsAnonymous);
    }

    [Fact]
    public void Comment_IsAnonymous_CanBeSet()
    {
        // Arrange
        var comment = new Comment();

        // Act
        comment.IsAnonymous = true;

        // Assert
        Assert.True(comment.IsAnonymous);
    }
}
