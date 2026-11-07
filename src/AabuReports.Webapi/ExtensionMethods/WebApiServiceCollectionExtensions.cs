using System.Text;
using System.Threading.RateLimiting;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Options;
using AabuReports.Infrastructure.Decorators.Logging;
using AabuReports.Webapi.Services;
using AabuReports.Webapi.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AabuReports.Webapi.ExtensionMethods;

/// <summary>
/// Every DI registration for the web api layer lives here.
/// Only <see cref="WebApiDependencyInjectionExtension.AddWebApi"/> is public; the other methods are internal building blocks.
/// </summary>
public static class WebApiServiceCollectionExtensions
{
    internal static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddOpenApi();

        return services;
    }

    internal static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("verification", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    internal static IServiceCollection AddRealTimeNotifications(this IServiceCollection services)
    {
        // signalR (real time notifications), the service in Infrastructure pushes through INotificationPusher
        services.AddSignalR();
        services.AddScoped<SignalRNotificationPusher>();
        services.AddScoped<INotificationPusher>(provider => provider.GetRequiredService<SignalRNotificationPusher>());
        services.Decorate<INotificationPusher, LoggingNotificationPusher>();

        return services;
    }

    internal static IServiceCollection AddWebApiOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection("JWT"));
        services.Configure<AuthCookieSettings>(configuration.GetSection("AuthCookies"));

        return services;
    }

    internal static IServiceCollection AddCookieServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICookieService, CookieService>();

        return services;
    }

    internal static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        // credentials are required for cookies, so origins must be listed explicitly, no wildcard
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
        });

        return services;
    }

    internal static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // validation settings are built from JwtOptions when the first request comes in,
        // so tools like "dotnet ef" can still start the app without the signing key
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, IOptions<AuthCookieSettings>>((bearerOptions, jwtOptions, cookieSettings) =>
            {
                var jwt = jwtOptions.Value;

                if (jwt.Key.Length < 32)
                    throw new InvalidOperationException(
                        "JWT:Key is missing or shorter than 32 characters. Set it with user-secrets or an environment variable (JWT__Key).");

                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateLifetime = true,
                };

                // read the access token from the cookie (an Authorization header still works and wins)
                bearerOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrEmpty(context.Request.Headers.Authorization))
                            context.Token = context.Request.Cookies[cookieSettings.Value.AccessTokenCookieName];

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }
}
