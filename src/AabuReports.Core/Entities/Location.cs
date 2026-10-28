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
    public ReportState? State { get; set; }
    public int? ReportsCount { get; set; }


    // relations
    public List<Report> Reports { get; set; } = new();
    
    
    // methods
    
    // to DTO
    public LocationDto ToDto()
    {
        if(this.State == null || this.ReportsCount == null) throw new Exception("State or report count is null");
        return new LocationDto
        {
            Id = this.Id,
            Coordinates = this.Coordinates,
            Status = this.State.Value,
            Title = this.Title,
            Type = this.Type,
            ReportsCount = this.ReportsCount.Value
        };
    }
    
    
    // override

    public override string ToString()
    {
        return $"Id: {this.Id}, Title: {this.Title}, Coordinates: {this.Coordinates},  Type: {this.Type}";
    }

    public override bool Equals(object? obj)
    {
        if(obj is not Location other) return false;

        return Id == other.Id &&
               Title == other.Title &&
               Coordinates.Lat == other.Coordinates.Lat &&
               Coordinates.Lng == other.Coordinates.Lng &&
               Type == other.Type;
    }
}
