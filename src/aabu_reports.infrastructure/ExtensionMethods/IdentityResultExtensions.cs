using Microsoft.AspNetCore.Identity;

namespace aabu_reports.infrastructure.ExtensionMethods;

public static class IdentityResultExtensions
{
    public static string DescribeErrors(this IdentityResult identityResult)
    {
        return string.Join("; ", identityResult.Errors.Select(e => e.Description));
    }
}
