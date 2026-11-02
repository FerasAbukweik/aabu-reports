using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Entities.Identity;
using AabuReports.Infrastructure.BackGroundServices;
using AabuReports.Infrastructure.Decorators.Logging;
using AabuReports.Infrastructure.Repositories;
using AabuReports.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AabuReports.Infrastructure.ExtensionMethods;

public static class AddApplicationServicesExtensionMethod
{
    public static IServiceCollection AddServiceDecorators(this IServiceCollection services)
    {
        // logging decorators
        services.Decorate<IAccessTokenService, LoggingAccessTokenService>();
        services.Decorate<IRefreshTokenService, LoggingRefreshTokenService>();
        services.Decorate<IAuthService, LoggingAuthService>();
        services.Decorate<ILocationService, LoggingLocationService>();
        services.Decorate<IReportService, LoggingReportService>();
        services.Decorate<INotificationService, LoggingNotificationService>();
        services.Decorate<IMessageService, LoggingMessageService>();
        services.Decorate<IAccountService, LoggingAccountService>();
        services.Decorate<IEmailVerificationService, LoggingEmailVerificationService>();
        services.Decorate<IImageService, LoggingImageService>();
        services.Decorate<ICloudinaryService, LoggingCloudinaryService>();

        // cache decorators (must come after logging so they sit on the outside)
        services.Decorate<ILocationService, CachedLocationService>();
        services.Decorate<IReportService, CachedReportService>();

        return services;
    }
    
    
    
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        // add services (core implementations only)
        services.AddScoped<IAccessTokenService, AccessTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IImageService, ImageService>();
        services.AddScoped<ICloudinaryService, CloudinaryService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IMessageService, MessageService>();

        return services;
    }
    
    
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    { 
        // add repositories
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IImageRepository, ImageRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();


        return services;
    }


    public static IServiceCollection AddIdentityCore(this IServiceCollection services)
    {
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

        return services;
    }


    public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        // redis: one shared connection, used by IDistributedCache and by RedisService (to clear keys by prefix).
        // AbortOnConnectFail=false so the app (and tools like "dotnet ef") can start while Redis is down, it reconnects in the background
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisOptions = ConfigurationOptions.Parse(configuration.GetConnectionString("Redis") ?? "localhost:6379");
            redisOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisOptions);
        });        // add background services
        services.AddHostedRemoveExpiredRefreshTokens();
        services.AddHostedRemoveOrphanedImages();
        services.AddStackExchangeRedisCache(opt => opt.InstanceName = configuration["Redis:InstanceName"]);
        services.AddOptions<RedisCacheOptions>()
            .Configure<IConnectionMultiplexer>((opt, multiplexer) =>
                opt.ConnectionMultiplexerFactory = () => Task.FromResult(multiplexer));
        services.AddSingleton<IRedisService, RedisService>();

        return services;
    }


    public static IServiceCollection AddSqlServer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseSqlServer(configuration.GetConnectionString("Default")));

        return services;
    }


    public static IServiceCollection AddBackGroundServices(this IServiceCollection services)
    {
        // add background services
        services.AddHostedRemoveExpiredRefreshTokens();
        services.AddHostedRemoveOrphanedImages();

        return services;
    }
}