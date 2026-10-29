using AabuReports.Core.Enums;

namespace AabuReports.Core.Dtos.Notifications;

public class NotificationDto
{
    public required Guid Id { get; set; }
    public required NotificationType Type { get; set; }
    public required string Message { get; set; }

    /// <summary> e.g. the title of the location of the report </summary>
    public string? Subject { get; set; }

    public required DateTime CreatedAt { get; set; }
    public required bool IsRead { get; set; }

    /// <summary> null for notifications that are not about one report </summary>
    public Guid? ReportId { get; set; }
}
