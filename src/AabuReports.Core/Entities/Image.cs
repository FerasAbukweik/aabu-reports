using System.ComponentModel.DataAnnotations;
using AabuReports.Core.Dtos.Images;

namespace AabuReports.Core.Entities;

public class Image
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public required string Url { get; set; }
    
    [Required]
    public required string PublicId { get; set; }
    
    
    // relations

    /// <summary>
    /// the report the image belongs to (a report has many images, an image has one report),
    /// null means the report was removed, so the image is an orphan waiting for the cleanup job
    /// </summary>
    public Guid? ReportId { get; set; }
    public Report? Report { get; set; }
    
    
    // methods

    public ImageDto ToDto()
    {
        return new ImageDto
        {
            Id = Id,
            Url = Url,
        };
    }
    
    
    // override
    
    public override string ToString()
    {
        return $"Id: {this.Id},  Url: {this.Url}, PublicId: {this.PublicId}";
    }

    public override int GetHashCode() => Id.GetHashCode();

    public override bool Equals(object? obj)
    {
        if (obj is not Image other) return false;
        
        return 
            other.Id == this.Id &&
            other.Url == this.Url &&
            other.PublicId == this.PublicId &&
            other.ReportId == this.ReportId;
    }
}