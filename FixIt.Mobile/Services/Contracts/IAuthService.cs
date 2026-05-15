using FixIt.Mobile.Models;

namespace FixIt.Mobile.Services.Contracts;

public interface IAuthService
{
    event EventHandler<bool>? LoginStateChanged;

    bool IsLoggedIn { get; }
    string CurrentDisplayName { get; }

    Task InitializeAsync(CancellationToken ct = default);
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
    Task<string?> GetTokenAsync();
    Task<bool> TryRefreshAsync(CancellationToken ct = default);
    string GetCurrentInitials();
}
