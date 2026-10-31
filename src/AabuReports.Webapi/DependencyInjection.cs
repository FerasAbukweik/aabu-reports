using System.Text;
using System.Threading.RateLimiting;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Options;
using AabuReports.Webapi.Services;
using AabuReports.Webapi.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AabuReports.Webapi;

public static class WebApiDependencyInjectionExtension
{
    public static IServiceCollection AddWebApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
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
        services.AddOpenApi();

        // signalR (real time notifications), the service in Infrastructure pushes through INotificationPusher
        services.AddSignalR();
        services.AddScoped<INotificationPusher, SignalRNotificationPusher>();
        
        // add configs
        services.Configure<JwtOptions>(configuration.GetSection("JWT"));
        services.Configure<AuthCookieSettings>(configuration.GetSection("AuthCookies"));

        // cookies
        services.AddHttpContextAccessor();
        services.AddScoped<ICookieService, CookieService>();

        // cors (credentials are required for cookies, so origins must be listed explicitly, no wildcard)
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
        });
        
        // authentication (JWT bearer)
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
