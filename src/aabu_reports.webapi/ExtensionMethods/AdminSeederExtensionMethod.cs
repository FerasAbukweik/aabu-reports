using aabu_reports.core.Constants;
using aabu_reports.core.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace aabu_reports.webapi.ExtensionMethods;

public static class AdminSeeder
{
    public static async Task SeedAdminAsync(this IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        var roleName = nameof(Roles.Admin);
        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new ApplicationRole { Name = roleName });

        var studentId = config["SeedAdmin:StudentId"];
        var password = config["SeedAdmin:Password"];
        if (string.IsNullOrEmpty(studentId) || string.IsNullOrEmpty(password)) return;

        if (await userManager.FindByNameAsync(studentId) is not null) return;

        var admin = new ApplicationUser
        {
            StudentId = studentId,
            UserName = studentId,
            FullName = config["SeedAdmin:FullName"] ?? "Admin",
            Email = config["SeedAdmin:Email"] ?? "admin@gmail.com"
        };

        var result = await userManager.CreateAsync(admin, password);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, roleName);
    }
}