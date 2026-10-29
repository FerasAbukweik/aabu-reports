using System.ComponentModel.DataAnnotations;
using AabuReports.Core.Dtos.Reports;
using AabuReports.Core.Entities.Identity;
using AabuReports.Core.Enums;

namespace AabuReports.Core.Entities;

public class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public required DateTime ReportDate { get; set; } = DateTime.UtcNow;
    
    public DateTime? FixDate { get; set; }
    
    [Required]
    public required ReportCategory Category { get; set; }
    
    [Required]
    public required ReportStateEnum State { get; set; }
    
    [Required]
    public required ReportSeverity Severity { get; set; }
    
    [Required]
    [MaxLength(500)]
    public required string Description { get; set; }

    /// <summary> how the user rated the fix, only has a value while the state is Fixed </summary>
    [Range(1, 5)]
    public int? Rate { get; private set; }


    // relations

    /// <summary> the building / pin the report is about (a location has many reports) </summary>
    [Required]
    public required Guid LocationId { get; set; }
    public Location? Location { get; set; }

    /// <summary> the images attached to the report </summary>
    public List<Image> Images { get; set; } = new();

    /// <summary> the user who sent the report </summary>
    [Required]
    public required Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }


    // methods

    /// <summary> changes the state, sets FixDate when the report becomes fixed, and clears the rate and FixDate if it is no longer fixed </summary>
    public void ChangeState(ReportStateEnum newStateEnum)
    {
        if (newStateEnum == ReportStateEnum.Fixed)
        {
            // keep the old FixDate if it was already fixed
            if (State != ReportStateEnum.Fixed) FixDate = DateTime.UtcNow;
        }
        else
        {
            Rate = null;
            FixDate = null;
        }

        State = newStateEnum;
    }

    /// <summary> sets the rate </summary>
    /// <returns> false (and nothing changes) if the report is not fixed or the rate is out of range </returns>
    public bool TryRate(int rate)
    {
        if (State != ReportStateEnum.Fixed) return false;
        if (rate < 1 || rate > 5) return false;

        Rate = rate;
        return true;
    }

    // to DTO
    public ReportDto ToDto()
    {
        return new ReportDto
        {
            Id = this.Id,
            ReportDate = this.ReportDate,
            Category = this.Category,
            State = this.State,
            Severity = this.Severity,
            Description = this.Description,
            Coordinates = this.Location!.Coordinates,
            ImageUrls = this.Images.Select(i => i.Url).ToList(),
            LocationTitle = this.Location!.Title,
            Rate = this.Rate,
        };
    }

    public ReportForMapDto ToMapDto()
    {
        return new ReportForMapDto
        {
            Id = this.Id,
            ReportDate = this.ReportDate,
            Category = this.Category,
            State = this.State,
            Severity = this.Severity,
            Description = this.Description,
            LocationId = this.LocationId
        };
    }


    // override

    public override string ToString()
    {
        return $"Id: {this.Id}, ReportDate: {this.ReportDate}, Category: {this.Category}, State: {this.State}, Rate: {this.Rate}, LocationId: {this.LocationId}, UserId: {this.UserId}";
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Report other) return false;

        return Id == other.Id &&
               ReportDate == other.ReportDate &&
               Category == other.Category &&
               State == other.State &&
               Rate == other.Rate &&
               Severity == other.Severity &&
               Description == other.Description &&
               LocationId == other.LocationId &&
               UserId == other.UserId;
    }
}
