using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using aabu_reports.core.Entities.Identity;

namespace aabu_reports.core.Entities;

public class RefreshToken
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column(TypeName = "varchar(100)")]
    [Required]
    public required string RefreshTokenString { get; set; }
    
    [Required]
    public required DateTime ExpiresAt { get; set; }
    
    
    // relations
    
    [Required]
    public required Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }
    
    // methods
    
    public bool IsExpired => ExpiresAt < DateTime.UtcNow;
}
