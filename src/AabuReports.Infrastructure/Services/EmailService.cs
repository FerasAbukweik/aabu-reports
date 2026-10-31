using AabuReports.Core.Abstractions.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace AabuReports.Infrastructure.Services;

public class EmailService(IConfiguration configuration) : IEmailService
{
    public Task SendVerificationCodeAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        var lifetimeMinutes = configuration.GetValue("Email:VerificationCodeLifetimeMinutes", 5);
        return SendEmailAsync(email, "AABU Reports verification code",
            $"Your verification code is: {code}\nIt expires in {lifetimeMinutes} minutes. If you did not request this code, ignore this email.",
            cancellationToken);
    }

    public async Task SendEmailAsync(string email, string subject, string body, CancellationToken cancellationToken = default)
    {
        var host = configuration["Email:Host"];
        var from = configuration["Email:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("Configure Email:Host and Email:From before sending emails.");

        var message = new MimeMessage
        {
            Subject = subject,
            Body = new TextPart("plain") { Text = body }
        };
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(email));

        var port = configuration.GetValue("Email:Port", 587);
        var security = !configuration.GetValue("Email:EnableSsl", true)
            ? SecureSocketOptions.None
            : port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, security, cancellationToken);

        var username = configuration["Email:Username"];
        if (!string.IsNullOrWhiteSpace(username))
            await client.AuthenticateAsync(username, configuration["Email:Password"] ?? string.Empty, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
