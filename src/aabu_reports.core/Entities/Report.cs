using System.ComponentModel.DataAnnotations;
using aabu_reports.core.DTOs.Report;
using aabu_reports.core.Entities.Identity;
using aabu_reports.core.Enums;

namespace aabu_reports.core.Entities;

public class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public required DateTime ReportDate { get; set; } = DateTime.UtcNow;
    
    [Required]
    public required ReportCategory Category { get; set; }
    
    [Required]
    public required ReportState State { get; set; }
    
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

    /// <summary> the user who sent the report </summary>
    [Required]
    public required Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }


    // methods

    /// <summary> changes the state, and clears the rate if the report is no longer fixed </summary>
    public void ChangeState(ReportState newState)
    {
        State = newState;

        if (newState != ReportState.Fixed) Rate = null;
    }

    /// <summary> sets the rate </summary>
    /// <returns> false (and nothing changes) if the report is not fixed or the rate is out of range </returns>
    public bool TryRate(int rate)
    {
        if (State != ReportState.Fixed) return false;
        if (rate < 1 || rate > 5) return false;

        Rate = rate;
        return true;
    }

    // to DTO
    public ReportDTO ToDTO()
    {
        return new ReportDTO
        {
            Id = this.Id,
            ReportDate = this.ReportDate,
            Category = this.Category,
            State = this.State,
            Rate = this.Rate,
            Severity = this.Severity,
            Description = this.Description,
            LocationId = this.LocationId,
            UserId = this.UserId,
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
