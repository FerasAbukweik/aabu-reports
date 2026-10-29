using System.Security.Claims;
using AabuReports.Core.Enums;

namespace AabuReports.Webapi.ExtensionMethods;

public static class UserExtensionMethods
{
    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(nameof(AppRole.Admin));

    /// <summary> the id of the logged in user (read from the access token) </summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}