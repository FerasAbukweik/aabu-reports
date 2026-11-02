using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Entities.Identity;
using AabuReports.Infrastructure.BackGroundServices;
using AabuReports.Infrastructure.Repositories;
using AabuReports.Infrastructure.Services;
using AabuReports.Infrastructure.Services.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AabuReports.Infrastructure;

public static class InfrastructureDependencyInjectionExtension
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // link with sql server
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseSqlServer(configuration.GetConnectionString("Default")));
        
        // add identity core
        services.AddIdentityCore<ApplicationUser>(opt =>
            {
                opt.Password.RequiredUniqueChars = 1;
                opt.Password.RequiredLength = 8;
                opt.Password.RequireDigit = true;
                opt.Password.RequireLowercase = true;
                opt.Password.RequireUppercase = true;

                opt.User.RequireUniqueEmail = true;
            }
        )
        .AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>();
        
        
        // redis: one shared connection, used by IDistributedCache and by RedisService (to clear keys by prefix).
        // AbortOnConnectFail=false so the app (and tools like "dotnet ef") can start while Redis is down, it reconnects in the background
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisOptions = ConfigurationOptions.Parse(configuration.GetConnectionString("Redis") ?? "localhost:6379");
            redisOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisOptions);
        });
        services.AddStackExchangeRedisCache(opt => opt.InstanceName = configuration["Redis:InstanceName"]);
        services.AddOptions<RedisCacheOptions>()
            .Configure<IConnectionMultiplexer>((opt, multiplexer) =>
                opt.ConnectionMultiplexerFactory = () => Task.FromResult(multiplexer));
        services.AddSingleton<IRedisService, RedisService>();

        // add services
        // every service with failure logging is registered with the "inner" key, and its logging decorator takes its place.
        // a service that is also cached is registered with the "core" key, then logging -> "inner", then the cached decorator takes its place
        services.AddKeyedScoped<IAccessTokenService, AccessTokenService>("inner");
        services.AddScoped<IAccessTokenService, LoggingAccessTokenService>();
        services.AddKeyedScoped<IRefreshTokenService, RefreshTokenService>("inner");
        services.AddScoped<IRefreshTokenService, LoggingRefreshTokenService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddKeyedScoped<IAuthService, AuthService>("inner");
        services.AddScoped<IAuthService, LoggingAuthService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();
        services.AddKeyedScoped<ILocationService, LocationService>("core");
        services.AddKeyedScoped<ILocationService, LoggingLocationService>("inner");
        services.AddScoped<ILocationService, CachedLocationService>();
        services.AddKeyedScoped<IReportService, ReportService>("core");
        services.AddKeyedScoped<IReportService, LoggingReportService>("inner");
        services.AddScoped<IReportService, CachedReportService>();
        services.AddScoped<IImageService, ImageService>();
        services.AddScoped<ICloudinaryService, CloudinaryService>();
        services.AddKeyedScoped<INotificationService, NotificationService>("inner");
        services.AddScoped<INotificationService, LoggingNotificationService>();
        services.AddKeyedScoped<IMessageService, MessageService>("inner");
        services.AddScoped<IMessageService, LoggingMessageService>();
        
        // add repositories
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IImageRepository, ImageRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        
        // add background services
        services.AddHostedRemoveExpiredRefreshTokens();
        services.AddHostedRemoveOrphanedImages();
        
        
        return services;
    }
}
