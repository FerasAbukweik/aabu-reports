using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AabuReports.Core.Entities.Identity;

namespace AabuReports.Core.Entities;

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
    
    
    // override

    public override bool Equals(object? obj)
    {
        if (obj is not RefreshToken other) return false;
        
        return Id == other.Id &&
               RefreshTokenString == other.RefreshTokenString &&
               ExpiresAt == other.ExpiresAt &&
               UserId == other.UserId;
    }
}
