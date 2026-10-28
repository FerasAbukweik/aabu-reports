using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace AabuReports.Core.Entities.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    [Key]
    public override Guid Id { get; set; } = Guid.NewGuid();
    
    [Column(TypeName = "varchar(15)")]
    [Required]
    public required string StudentId { get; set; }
    
    [Column(TypeName = "nvarchar(150)")]
    [Required]
    public required string FullName { get; set; }
    
    
    // relations
    public List<RefreshToken> RefreshTokens { get; set; } = new();

    public List<Report> Reports { get; set; } = new();

}