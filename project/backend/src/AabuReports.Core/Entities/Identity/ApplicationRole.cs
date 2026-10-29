using Microsoft.AspNetCore.Identity;

namespace AabuReports.Core.Entities.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public override Guid Id { get; set; } = Guid.NewGuid();
}