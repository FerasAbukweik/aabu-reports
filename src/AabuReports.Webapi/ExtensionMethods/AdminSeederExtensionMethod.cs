using AabuReports.Core.Entities.Identity;
using AabuReports.Core.Enums;
using Microsoft.AspNetCore.Identity;

namespace AabuReports.Webapi.ExtensionMethods;

public static class AdminSeeder
{
    public static async Task SeedAdminAsync(this IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        var roleName = nameof(AppRole.Admin);
        if (!await roleManager.RoleExistsAsync(roleName))
            Check(await roleManager.CreateAsync(new ApplicationRole { Name = roleName }));

        var studentId = config["SeedAdmin:StudentId"];
        var password  = config["SeedAdmin:Password"];
        var email     = config["SeedAdmin:Email"];
        if (string.IsNullOrWhiteSpace(studentId) ||
            string.IsNullOrWhiteSpace(password)  ||
            string.IsNullOrWhiteSpace(email)) return;

        var fullName = config["SeedAdmin:FullName"] ?? "Admin";
        var admin = await userManager.FindByNameAsync(studentId);

        if (admin is null)
        {
            admin = new ApplicationUser
            {
                StudentId = studentId,
                UserName = studentId,
                FullName = fullName,
                Email = email,
                EmailConfirmed = true
            };
            Check(await userManager.CreateAsync(admin, password));
        }
        else if (admin.FullName != fullName || admin.Email != email)
        {
            admin.FullName = fullName;
            admin.Email = email;
            Check(await userManager.UpdateAsync(admin));
        }

        if (!await userManager.IsInRoleAsync(admin, roleName))
            Check(await userManager.AddToRoleAsync(admin, roleName));
    }

    private static void Check(IdentityResult r)
    {
        if (!r.Succeeded)
            throw new InvalidOperationException("Admin seed failed: " +
                                                string.Join("; ", r.Errors.Select(e => e.Description)));
    }
}