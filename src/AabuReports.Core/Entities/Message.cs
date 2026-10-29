using System.ComponentModel.DataAnnotations;
using AabuReports.Core.Dtos.Messages;
using AabuReports.Core.Entities.Identity;

namespace AabuReports.Core.Entities;

/// <summary> one chat message inside the chat of a report </summary>
public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(500)]
    public required string Content { get; set; }

    [Required]
    public required DateTime SentAt { get; set; } = DateTime.UtcNow;


    // relations

    /// <summary> the user who wrote the message </summary>
    [Required]
    public required Guid SenderId { get; set; }
    public ApplicationUser? Sender { get; set; }

    /// <summary> the report whose chat the message belongs to </summary>
    [Required]
    public required Guid ReportId { get; set; }
    public Report? Report { get; set; }


    // to DTO (the Sender must be loaded)
    public MessageDto ToDto()
    {
        return new MessageDto
        {
            SenderId = this.SenderId,
            SenderName = this.Sender!.FullName,
            ReportId = this.ReportId,
            SentAt = this.SentAt,
            Content = this.Content
        };
    }


    // override

    public override string ToString()
    {
        return $"Id: {this.Id}, SenderId: {this.SenderId}, ReportId: {this.ReportId}, SentAt: {this.SentAt}";
    }
}
