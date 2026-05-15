using FluentAssertions;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services.Contracts;
using FixIt.Mobile.Tests.TestSupport;
using FixIt.Mobile.ViewModels;
using Moq;
using Xunit;

namespace FixIt.Mobile.Tests.ViewModels;

public class LoginViewModelTests
{
    [Fact]
    public async Task Login_WhenEmailEmpty_SetsHasError()
    {
        var vm = CreateViewModel(new AuthResult(false));
        vm.Password = "secret";

        await vm.LoginCommand.ExecuteAsync(null);

        vm.HasError.Should().BeTrue();
        vm.HasEmailError.Should().BeTrue();
    }

    [Fact]
    public async Task Login_WhenPasswordEmpty_SetsHasError()
    {
        var vm = CreateViewModel(new AuthResult(false));
        vm.Email = "user@example.com";

        await vm.LoginCommand.ExecuteAsync(null);

        vm.HasError.Should().BeTrue();
        vm.HasPasswordError.Should().BeTrue();
    }

    [Fact]
    public async Task Login_WhenCredentialsValid_NavigatesToHome()
    {
        Shell.Current = new Shell();
        var vm = CreateViewModel(new AuthResult(true));
        vm.Email = "user@example.com";
        vm.Password = "secret";

        await vm.LoginCommand.ExecuteAsync(null);

        Shell.Current.LastRoute.Should().Be(AppConstants.RouteHome);
    }

    [Fact]
    public async Task Login_WhenCredentialsInvalid_SetsErrorMessage()
    {
        var vm = CreateViewModel(new AuthResult(false, "Nope"));
        vm.Email = "user@example.com";
        vm.Password = "bad";

        await vm.LoginCommand.ExecuteAsync(null);

        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Be("Nope");
    }

    [Fact]
    public async Task Login_SetsIsLoading_ThenClearsIt()
    {
        var tcs = new TaskCompletionSource<AuthResult>();
        var auth = new Mock<IAuthService>();
        auth.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(tcs.Task);
        var vm = new LoginViewModel(auth.Object, new NoopAnalyticsService())
        {
            Email = "user@example.com",
            Password = "secret"
        };

        var task = vm.LoginCommand.ExecuteAsync(null);
        vm.IsLoading.Should().BeTrue();
        tcs.SetResult(new AuthResult(false));
        await task;

        vm.IsLoading.Should().BeFalse();
    }

    private static LoginViewModel CreateViewModel(AuthResult result)
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return new LoginViewModel(auth.Object, new NoopAnalyticsService());
    }
}
