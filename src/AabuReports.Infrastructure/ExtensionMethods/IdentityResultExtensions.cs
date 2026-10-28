using Microsoft.AspNetCore.Identity;

namespace AabuReports.Infrastructure.ExtensionMethods;

public static class IdentityResultExtensions
{
    public static string DescribeErrors(this IdentityResult identityResult)
    {
        return string.Join("; ", identityResult.Errors.Select(e => e.Description));
    }
}
