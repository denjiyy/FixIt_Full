using Xunit;
using FixIt.Models.Users;
using FixIt.Models.Enums;

namespace FixIt.Tests.Models;

public class ApplicationUserModelTests
{
    [Fact]
    public void ApplicationUser_CanBeCreated()
    {
        // Arrange & Act
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            DisplayName = "Test User"
        };

        // Assert
        Assert.Equal("testuser", user.UserName);
        Assert.Equal("test@example.com", user.Email);
        Assert.Equal("Test User", user.DisplayName);
    }

    [Fact]
    public void ApplicationUser_DefaultRole_IsUser()
    {
        // Arrange & Act
        var user = new ApplicationUser();

        // Assert
        Assert.Equal(UserRole.User, user.Role);
    }

    [Fact]
    public void ApplicationUser_RoleCanBeChangedToModerator()
    {
        // Arrange
        var user = new ApplicationUser { Role = UserRole.User };

        // Act
        user.Role = UserRole.Moderator;

        // Assert
        Assert.Equal(UserRole.Moderator, user.Role);
    }

    [Fact]
    public void ApplicationUser_RoleCanBeChangedToAdmin()
    {
        // Arrange
        var user = new ApplicationUser { Role = UserRole.User };

        // Act
        user.Role = UserRole.Admin;

        // Assert
        Assert.Equal(UserRole.Admin, user.Role);
    }

    [Fact]
    public void ApplicationUser_Bio_CanBeNull()
    {
        // Arrange & Act
        var user = new ApplicationUser { Bio = null };

        // Assert
        Assert.Null(user.Bio);
    }

    [Fact]
    public void ApplicationUser_Bio_CanBeSet()
    {
        // Arrange
        var bio = "City enthusiast and photographer";
        var user = new ApplicationUser();

        // Act
        user.Bio = bio;

        // Assert
        Assert.Equal(bio, user.Bio);
    }

    [Fact]
    public void ApplicationUser_AnonymousReportingEnabled_DefaultIsTrue()
    {
        // Arrange & Act
        var user = new ApplicationUser();

        // Assert
        Assert.True(user.AnonymousReportingEnabled);
    }

    [Fact]
    public void ApplicationUser_AnonymousReportingEnabled_CanBeDisabled()
    {
        // Arrange
        var user = new ApplicationUser { AnonymousReportingEnabled = true };

        // Act
        user.AnonymousReportingEnabled = false;

        // Assert
        Assert.False(user.AnonymousReportingEnabled);
    }

    [Fact]
    public void ApplicationUser_ReputationScore_DefaultIsZero()
    {
        // Arrange & Act
        var user = new ApplicationUser();

        // Assert
        Assert.Equal(0, user.ReputationScore);
    }

    [Fact]
    public void ApplicationUser_ReputationScore_CanBeUpdated()
    {
        // Arrange
        var user = new ApplicationUser { ReputationScore = 0 };

        // Act
        user.ReputationScore = 150;

        // Assert
        Assert.Equal(150, user.ReputationScore);
    }

    [Fact]
    public void ApplicationUser_TrustLevel_DefaultIsZero()
    {
        // Arrange & Act
        var user = new ApplicationUser();

        // Assert
        Assert.Equal(0, user.TrustLevel);
    }

    [Fact]
    public void ApplicationUser_HasPasswordAuth_DefaultIsTrue()
    {
        // Arrange & Act
        var user = new ApplicationUser();

        // Assert
        Assert.True(user.HasPasswordAuth);
    }

    [Fact]
    public void ApplicationUser_IsVerifiedOfficial_DefaultIsFalse()
    {
        // Arrange & Act
        var user = new ApplicationUser();

        // Assert
        Assert.False(user.IsVerifiedOfficial);
    }

    [Fact]
    public void ApplicationUser_OfficialDepartment_CanBeSet()
    {
        // Arrange
        var department = "Public Works";
        var user = new ApplicationUser();

        // Act
        user.OfficialDepartment = department;

        // Assert
        Assert.Equal(department, user.OfficialDepartment);
    }

    [Fact]
    public void ApplicationUser_OfficialTitle_CanBeSet()
    {
        // Arrange
        var title = "Street Manager";
        var user = new ApplicationUser();

        // Act
        user.OfficialTitle = title;

        // Assert
        Assert.Equal(title, user.OfficialTitle);
    }

    [Fact]
    public void ApplicationUser_PreferredCityId_CanBeSet()
    {
        // Arrange
        var cityId = "507f1f77bcf86cd799439011";
        var user = new ApplicationUser();

        // Act
        user.PreferredCityId = cityId;

        // Assert
        Assert.Equal(cityId, user.PreferredCityId);
    }

    [Fact]
    public void ApplicationUser_ExternalIdentities_InitializedAsEmptyList()
    {
        // Arrange & Act
        var user = new ApplicationUser();

        // Assert
        Assert.NotNull(user.ExternalIdentities);
        Assert.Empty(user.ExternalIdentities);
    }

    [Fact]
    public void ApplicationUser_ExternalIdentities_CanBeAdded()
    {
        // Arrange
        var user = new ApplicationUser();
        var externalIdentity = new ExternalIdentity
        {
            Provider = "Google",
            ProviderId = "123456",
            ProviderDisplayName = "Test User"
        };

        // Act
        user.ExternalIdentities.Add(externalIdentity);

        // Assert
        Assert.Single(user.ExternalIdentities);
        Assert.Equal("Google", user.ExternalIdentities.First().Provider);
    }
}
