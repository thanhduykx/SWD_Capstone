using System.Net;
using System.Net.Mail;
using System.Text;
using CPMS.BLL.Abstractions.Messaging;
using CPMS.BLL.Exceptions;
using Microsoft.Extensions.Configuration;

namespace CPMS.DAL.ExternalServices;

public sealed class SmtpEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(
        string recipientEmail, string subject, string body, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        string? fromEmail = configuration["Smtp:FromEmail"];
        if (string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new BusinessRuleException("SMTP is not configured. Set Smtp:FromEmail before sending CPMS emails.");
        }

        string? host = configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new BusinessRuleException("SMTP is not configured. Set Smtp:Host before sending CPMS emails.");
        }

        string? userName = configuration["Smtp:UserName"];
        string? password = configuration["Smtp:Password"];
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            throw new BusinessRuleException(
                "SMTP credentials are not configured. Set Smtp:UserName and Smtp:Password in a secret store.");
        }

        if (!configuration.GetValue("Smtp:EnableSsl", true))
        {
            throw new BusinessRuleException("SMTP TLS must be enabled.");
        }

        int port = configuration.GetValue("Smtp:Port", 587);
        if (port is < 1 or > 65535)
        {
            throw new BusinessRuleException("Smtp:Port must be between 1 and 65535.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, configuration["Smtp:FromName"] ?? "CPMS"),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(recipientEmail);
        using var client = new SmtpClient(host, port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(userName, password)
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}
