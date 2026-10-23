using Microsoft.AspNetCore.Identity;

namespace aabu_reports.core.Entities.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public override Guid Id { get; set; } = Guid.NewGuid();
}