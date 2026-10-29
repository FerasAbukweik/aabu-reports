namespace AabuReports.Core.Dtos.Messages;

public class MessageDto
{
    public required Guid SenderId { get; set; }
    public required string SenderName { get; set; }
    public required Guid ReportId { get; set; }
    public required DateTime SentAt { get; set; }
    public required string Content { get; set; }
}
