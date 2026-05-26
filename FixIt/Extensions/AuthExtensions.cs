using FixIt.Services.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;

namespace FixIt.Extensions;

public static class AuthExtensions
{
    public sealed record AuthenticationConfigResult(bool GoogleConfigured);

    public static AuthenticationConfigResult AddFixItAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var isProduction = environment.IsProduction();

        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.LoginPath = "/Identity/Account/Login";
            options.LogoutPath = "/Identity/Account/Logout";
            options.AccessDeniedPath = "/access-denied";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.Cookie.SecurePolicy = isProduction ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(12);
        });

        var authConfig = configuration.GetSection("Authentication");
        var googleSection = authConfig.GetSection("Google");
        var googleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? googleSection["ClientId"];
        var googleClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? googleSection["ClientSecret"];
        var hasGoogle = !string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret);

        if (hasGoogle)
        {
            authBuilder.AddGoogle(options =>
            {
                options.ClientId = googleClientId!.Trim();
                options.ClientSecret = googleClientSecret!.Trim();
                options.CallbackPath = "/signin-google";
            });
        }
        else if (isProduction)
        {
            throw new InvalidOperationException("Google OAuth credentials are not configured. Set GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET environment variables.");
        }

        var jwtConfig = configuration.GetSection("Jwt");
        var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? jwtConfig["SecretKey"];

        if (string.IsNullOrWhiteSpace(jwtSecretKey) || jwtSecretKey.Trim().Length < 32)
        {
            throw new InvalidOperationException(
                "JWT secret key is not configured or too weak. Set Jwt:SecretKey or JWT_SECRET_KEY to a strong random secret with at least 32 characters.");
        }

        var key = System.Text.Encoding.ASCII.GetBytes(jwtSecretKey.Trim());
        var issuer = jwtConfig["Issuer"] ?? "FixIt";
        var audience = jwtConfig["Audience"] ?? "FixItClients";

        authBuilder.AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(0)
            };
        });

        services.AddScoped<ITokenService, JwtTokenService>();

        return new AuthenticationConfigResult(hasGoogle);
    }
}
