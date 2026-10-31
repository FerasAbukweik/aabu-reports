namespace AabuReports.Core.Abstractions.Services;

public interface IEmailService
{
    Task SendEmailAsync(string email, string subject, string body, CancellationToken cancellationToken = default);

    Task SendVerificationCodeAsync(string email, string code, CancellationToken cancellationToken = default);
}
