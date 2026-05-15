using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.Services;

public class AuthService : IAuthService
{
    private const string TokenKey = AppConstants.TokenKey;
    private const string RefreshTokenKey = AppConstants.RefreshTokenKey;

    private readonly HttpClient _authClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public AuthService(IHttpClientFactory httpClientFactory, JsonSerializerOptions jsonOptions)
    {
        _authClient = httpClientFactory.CreateClient(AppConstants.AuthClientName);
        _jsonOptions = jsonOptions;
    }

    public event EventHandler<bool>? LoginStateChanged;

    public bool IsLoggedIn { get; private set; }

    public string CurrentDisplayName { get; private set; } = string.Empty;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var accessToken = await GetTokenAsync();
        var refreshToken = await SecureGetAsync(RefreshTokenKey);
        CurrentDisplayName = ExtractDisplayNameFromJwt(accessToken);
        SetLoginState(!string.IsNullOrWhiteSpace(accessToken) && !string.IsNullOrWhiteSpace(refreshToken));
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        try
        {
            Console.WriteLine($"[Auth] Login attempt for user: {RedactEmail(email)}");
            using var response = await _authClient.PostAsJsonAsync($"{AppConstants.ApiAuth}/login", new
            {
                email,
                password
            }, _jsonOptions, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await ExtractApiErrorAsync(response, Localization.LocalizationService.Get("Login_Error_Invalid"), ct);
                return new AuthResult(false, error);
            }

            var envelope = await DeserializeEnvelopeAsync<TokenPayload>(response, ct);
            var accessToken = envelope?.Data?.AccessToken;
            var refreshToken = envelope?.Data?.RefreshToken;

            if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
            {
                return new AuthResult(false, Localization.LocalizationService.Get("Common_Error_Generic"));
            }

            await SecureSetAsync(TokenKey, accessToken);
            await SecureSetAsync(RefreshTokenKey, refreshToken);

            CurrentDisplayName = envelope?.Data?.User?.DisplayName ?? ExtractDisplayNameFromJwt(accessToken);
            SetLoginState(true);
            return new AuthResult(true);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[AUTH] Network error: {ex.Message}");
            return new AuthResult(false, Localization.LocalizationService.Get("Common_Error_Network"));
        }
        catch (TaskCanceledException)
        {
            return new AuthResult(false, Localization.LocalizationService.Get("Common_Error_Generic"));
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[AUTH] Parse error: {ex.Message}");
            return new AuthResult(false, Localization.LocalizationService.Get("Common_Error_Generic"));
        }
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        try
        {
            var credential = await GetTokenAsync();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{AppConstants.ApiAuth}/logout");
            if (!string.IsNullOrWhiteSpace(credential))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
            }

            using var _ = await _authClient.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[AUTH] Network error: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("[AUTH] Logout request timed out.");
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[AUTH] Parse error: {ex.Message}");
        }
        finally
        {
            await ClearTokensAsync();
            SetLoginState(false);
            CurrentDisplayName = string.Empty;
        }
    }

    public async Task<string?> GetTokenAsync()
    {
        return await SecureGetAsync(TokenKey);
    }

    public async Task<bool> TryRefreshAsync(CancellationToken ct = default)
    {
        var refreshCredential = await SecureGetAsync(RefreshTokenKey);
        if (string.IsNullOrWhiteSpace(refreshCredential))
        {
            await ClearTokensAsync();
            SetLoginState(false);
            return false;
        }

        try
        {
            var refreshed = await TryRefreshWithPostAsync(refreshCredential, ct) || await TryRefreshWithGetAsync(refreshCredential, ct);
            if (refreshed)
            {
                SetLoginState(true);
                return true;
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[AUTH] Network error: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("[AUTH] Refresh request timed out.");
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[AUTH] Parse error: {ex.Message}");
        }

        await ClearTokensAsync();
        SetLoginState(false);
        CurrentDisplayName = string.Empty;
        return false;
    }

    public string GetCurrentInitials()
    {
        if (string.IsNullOrWhiteSpace(CurrentDisplayName))
        {
            return "👤";
        }

        var parts = CurrentDisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return "👤";
        }

        if (parts.Length == 1)
        {
            return parts[0][0].ToString().ToUpperInvariant();
        }

        return string.Concat(parts[0][0], parts[1][0]).ToUpperInvariant();
    }

    private async Task<bool> TryRefreshWithGetAsync(string refreshCredential, CancellationToken ct)
    {
        var path = $"{AppConstants.ApiAuth}/refresh?refreshToken={Uri.EscapeDataString(refreshCredential)}";
        using var response = await _authClient.GetAsync(path, ct);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var envelope = await DeserializeEnvelopeAsync<RefreshPayload>(response, ct);
        return await PersistRefreshPayloadAsync(envelope?.Data);
    }

    private async Task<bool> TryRefreshWithPostAsync(string refreshCredential, CancellationToken ct)
    {
        using var response = await _authClient.PostAsJsonAsync($"{AppConstants.ApiAuth}/refresh", new
        {
            refreshToken = refreshCredential
        }, _jsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var envelope = await DeserializeEnvelopeAsync<RefreshPayload>(response, ct);
        return await PersistRefreshPayloadAsync(envelope?.Data);
    }

    private async Task<bool> PersistRefreshPayloadAsync(RefreshPayload? payload)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.AccessToken) || string.IsNullOrWhiteSpace(payload.RefreshToken))
        {
            return false;
        }

        await SecureSetAsync(TokenKey, payload.AccessToken);
        await SecureSetAsync(RefreshTokenKey, payload.RefreshToken);
        CurrentDisplayName = ExtractDisplayNameFromJwt(payload.AccessToken);
        return true;
    }

    private async Task ClearTokensAsync()
    {
        try
        {
            SecureStorage.Default.Remove(TokenKey);
            SecureStorage.Default.Remove(RefreshTokenKey);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AUTH] Failed to clear secure storage: {ex.Message}");
        }
    }

    private void SetLoginState(bool isLoggedIn)
    {
        if (IsLoggedIn == isLoggedIn)
        {
            return;
        }

        IsLoggedIn = isLoggedIn;
        LoginStateChanged?.Invoke(this, IsLoggedIn);
    }

    private static async Task<string?> SecureGetAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AUTH] Failed to read secure storage: {ex.Message}");
            return null;
        }
    }

    private static async Task SecureSetAsync(string key, string value)
    {
        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AUTH] Failed to write secure storage: {ex.Message}");
        }
    }

    private async Task<ApiEnvelope<T>?> DeserializeEnvelopeAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<ApiEnvelope<T>>(stream, _jsonOptions, ct);
    }

    private async Task<string> ExtractApiErrorAsync(HttpResponseMessage response, string fallbackMessage, CancellationToken ct)
    {
        try
        {
            var envelope = await DeserializeEnvelopeAsync<object>(response, ct);
            if (!string.IsNullOrWhiteSpace(envelope?.Message))
            {
                return envelope.Message;
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[AUTH] Parse error: {ex.Message}");
        }

        return fallbackMessage;
    }

    private static string ExtractDisplayNameFromJwt(string? credential)
    {
        if (string.IsNullOrWhiteSpace(credential))
        {
            return string.Empty;
        }

        var segments = credential.Split('.');
        if (segments.Length < 2)
        {
            return string.Empty;
        }

        try
        {
            var payload = segments[1]
                .Replace('-', '+')
                .Replace('_', '/');

            var padLength = 4 - payload.Length % 4;
            if (padLength is > 0 and < 4)
            {
                payload = payload.PadRight(payload.Length + padLength, '=');
            }

            var jsonBytes = Convert.FromBase64String(payload);
            var json = Encoding.UTF8.GetString(jsonBytes);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            return GetClaimValue(root, "DisplayName")
                ?? GetClaimValue(root, "displayName")
                ?? GetClaimValue(root, "name")
                ?? string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AUTH] Failed to parse credential payload: {ex.Message}");
            return string.Empty;
        }
    }

    private static string? GetClaimValue(JsonElement root, string claimName)
    {
        if (root.TryGetProperty(claimName, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static string RedactEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "unknown";
        }

        var visible = email.Length >= 3 ? email[..3] : email[..1];
        return $"{visible}***";
    }
}
