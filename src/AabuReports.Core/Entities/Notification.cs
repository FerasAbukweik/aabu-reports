using System.ComponentModel.DataAnnotations;
using AabuReports.Core.Dtos.Notifications;
using AabuReports.Core.Entities.Identity;
using AabuReports.Core.Enums;

namespace AabuReports.Core.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary> what the notification is about, the frontend picks its label, icon and color from it </summary>
    [Required]
    public required NotificationType Type { get; set; }

    /// <summary> the text of the notification </summary>
    [Required]
    [MaxLength(500)]
    public required string Message { get; set; }

    /// <summary> the thing it is about, e.g. the title of the location of the report (a copy, so it stays if the location is renamed or deleted) </summary>
    [MaxLength(500)]
    public string? Subject { get; set; }

    [Required]
    public required DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary> true once the user saw it </summary>
    public bool IsRead { get; private set; }


    // relations

    /// <summary> the user who gets the notification </summary>
    [Required]
    public required Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    /// <summary> the report it is about, null for notifications that are not about one report </summary>
    public Guid? ReportId { get; set; }
    public Report? Report { get; set; }


    // methods

    /// <summary> marks the notification as seen </summary>
    /// <returns> false (and nothing changes) if it was already seen </returns>
    public bool MarkAsRead()
    {
        if (IsRead) return false;

        IsRead = true;
        return true;
    }

    // to DTO
    public NotificationDto ToDto()
    {
        return new NotificationDto
        {
            Id = this.Id,
            Type = this.Type,
            Message = this.Message,
            Subject = this.Subject,
            CreatedAt = this.CreatedAt,
            IsRead = this.IsRead,
            ReportId = this.ReportId
        };
    }


    // override

    public override string ToString()
    {
        return $"Id: {this.Id}, Type: {this.Type}, CreatedAt: {this.CreatedAt}, IsRead: {this.IsRead}, UserId: {this.UserId}, ReportId: {this.ReportId}";
    }
}
