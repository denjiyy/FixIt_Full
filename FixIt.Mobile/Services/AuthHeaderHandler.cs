using System.Net;
using System.Net.Http.Headers;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.Services;

public class AuthHeaderHandler : DelegatingHandler
{
    private readonly IAuthService _auth;

    public AuthHeaderHandler(IAuthService auth)
    {
        _auth = auth;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // Production note: configure certificate pinning with HttpClientHandler.ServerCertificateCustomValidationCallback
        // only when validating against the known Railway certificate fingerprint. Keep default validation otherwise.
        var credential = await _auth.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(credential) && request.Headers.Authorization == null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        }

        using var retryRequest = await CloneRequestAsync(request, ct);
        var response = await base.SendAsync(request, ct);

        if (ShouldHandleUnauthorized(request, response))
        {
            response.Dispose();

            var refreshed = await _auth.TryRefreshAsync(ct);
            if (refreshed)
            {
                credential = await _auth.GetTokenAsync();
                if (!string.IsNullOrWhiteSpace(credential))
                {
                    retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
                }

                response = await base.SendAsync(retryRequest, ct);
                if (ShouldHandleUnauthorized(request, response))
                {
                    response.Dispose();
                    await HandleUnauthorizedAsync(request, ct);
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    {
                        RequestMessage = request
                    };
                }
            }
            else
            {
                await HandleUnauthorizedAsync(request, ct);
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    RequestMessage = request
                };
            }
        }

        return response;
    }

    private static bool ShouldHandleUnauthorized(HttpRequestMessage request, HttpResponseMessage response)
    {
        if (IsAuthEndpoint(request.RequestUri))
        {
            return false;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return true;
        }

        if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.RedirectKeepVerb or HttpStatusCode.RedirectMethod)
        {
            var location = response.Headers.Location?.ToString() ?? string.Empty;
            return location.Contains("Identity/Account/Login", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private async Task HandleUnauthorizedAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (IsAuthEndpoint(request.RequestUri))
        {
            return;
        }

        await _auth.LogoutAsync(ct);
        if (Shell.Current != null)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.GoToAsync(AppConstants.RouteAccountTabAbsolute);
            });
        }
    }

    private static bool IsAuthEndpoint(Uri? requestUri)
    {
        var path = requestUri?.AbsolutePath ?? string.Empty;
        return path.Contains("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/api/auth/refresh", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/api/auth/logout", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content != null)
        {
            // FIX B-02: buffer once and give the original request and retry clone independent seekable streams.
            var memory = new MemoryStream();
            await request.Content.CopyToAsync(memory, ct);
            var buffer = memory.ToArray();

            var sourceHeaders = request.Content.Headers;
            request.Content = CreateBufferedContent(buffer, sourceHeaders);
            clone.Content = CreateBufferedContent(buffer, sourceHeaders);
        }

        return clone;
    }

    private static StreamContent CreateBufferedContent(byte[] buffer, HttpContentHeaders sourceHeaders)
    {
        var streamContent = new StreamContent(new MemoryStream(buffer));
        foreach (var header in sourceHeaders)
        {
            streamContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return streamContent;
    }
}
