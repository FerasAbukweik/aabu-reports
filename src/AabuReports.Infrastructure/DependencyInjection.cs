using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Entities.Identity;
using AabuReports.Infrastructure.BackGroundServices;
using AabuReports.Infrastructure.Repositories;
using AabuReports.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        
        
        // add services
        services.AddScoped<IAccessTokenService, AccessTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IImageService, ImageService>();
        services.AddScoped<ICloudinaryService, CloudinaryService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IMessageService, MessageService>();
        
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
