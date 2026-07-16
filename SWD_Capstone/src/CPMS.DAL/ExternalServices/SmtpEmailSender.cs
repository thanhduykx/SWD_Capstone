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
        var fromEmail = configuration["Smtp:FromEmail"];
        if (string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new BusinessRuleException("SMTP is not configured. Set Smtp:FromEmail before sending CPMS emails.");
        }
        var host = configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new BusinessRuleException("SMTP is not configured. Set Smtp:Host before sending CPMS emails.");
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
        using var client = new SmtpClient(host, configuration.GetValue("Smtp:Port", 587))
        {
            EnableSsl = true
        };
        var userName = configuration["Smtp:UserName"];
        if (!string.IsNullOrWhiteSpace(userName))
        {
            client.Credentials = new NetworkCredential(userName, configuration["Smtp:Password"]);
        }
        await client.SendMailAsync(message, cancellationToken);
    }
}
