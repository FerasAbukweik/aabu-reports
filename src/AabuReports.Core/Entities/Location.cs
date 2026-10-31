using System.ComponentModel.DataAnnotations;
using AabuReports.Core.ValueObjects;
using AabuReports.Core.Dtos.Locations;
using AabuReports.Core.Enums;

namespace AabuReports.Core.Entities;

public class Location
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(500)]
    public required string Title { get; set; }
    
    public required MapCoordinates Coordinates { get; set; }
    
    [Required]
    public required LocationType Type { get; set; }
    

    // not recognized by EF
    public ReportStateEnum? State { get; set; }
    public int? ActiveReportsCount { get; set; }


    // relations
    public List<Report> Reports { get; set; } = new();
    
    
    // methods
    
    // to DTO
    public LocationDto ToDto()
    {
        if(this.State == null || this.ActiveReportsCount == null) throw new Exception("State or report count is null");
        return new LocationDto
        {
            Id = this.Id,
            Coordinates = this.Coordinates,
            Status = this.State.Value,
            Title = this.Title,
            Type = this.Type,
            ActiveReportsCount = this.ActiveReportsCount.Value
        };
    }
    
    
    // override

    public override string ToString()
    {
        return $"Id: {this.Id}, Title: {this.Title}, Coordinates: {this.Coordinates},  Type: {this.Type}";
    }

    public override int GetHashCode() => Id.GetHashCode();

    public override bool Equals(object? obj)
    {
        if(obj is not Location other) return false;

        var precision = 6;
        
        return Id == other.Id &&
               Title == other.Title &&
               Math.Round(Coordinates.Lat, precision) == Math.Round(other.Coordinates.Lat, precision) &&
               Math.Round(Coordinates.Lng, precision) == Math.Round(other.Coordinates.Lng, precision) &&
               Type == other.Type;
    }
}
