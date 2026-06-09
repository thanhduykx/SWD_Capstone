using System.Net;
using System.Net.Mail;
using CPMS.Core.Exceptions;

namespace CPMS.Api.Services;

public interface IReviewEmailSender
{
    Task SendAsync(string recipientEmail, string subject, string body, CancellationToken cancellationToken);
}

public sealed class SmtpReviewEmailSender(IConfiguration configuration) : IReviewEmailSender
{
    public async Task SendAsync(
        string recipientEmail,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var host = configuration["Smtp:Host"];
        var fromEmail = configuration["Smtp:FromEmail"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new BusinessRuleException("SMTP is not configured. Set Smtp:Host and Smtp:FromEmail before publishing review schedules.");
        }

        var port = configuration.GetValue("Smtp:Port", 587);
        var enableSsl = configuration.GetValue("Smtp:EnableSsl", true);
        var fromName = configuration["Smtp:FromName"] ?? "CPMS";
        var userName = configuration["Smtp:UserName"];
        var password = configuration["Smtp:Password"];

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(recipientEmail);

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl
        };

        if (!string.IsNullOrWhiteSpace(userName))
        {
            client.Credentials = new NetworkCredential(userName, password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
