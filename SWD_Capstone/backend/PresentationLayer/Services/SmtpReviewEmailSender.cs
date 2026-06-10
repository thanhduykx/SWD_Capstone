using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using CPMS.Core.Exceptions;

namespace CPMS.Api.Services;

public interface IReviewEmailSender
{
    Task SendAsync(string recipientEmail, string subject, string body, CancellationToken cancellationToken);

    Task SendHtmlAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        string textBody,
        IReadOnlyCollection<EmailInlineImage> inlineImages,
        CancellationToken cancellationToken);
}

public sealed class SmtpReviewEmailSender(IConfiguration configuration) : IReviewEmailSender
{
    public async Task SendAsync(
        string recipientEmail,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        using var message = CreateMessage(recipientEmail, subject);
        message.Body = body;
        message.IsBodyHtml = false;

        await SendMessageAsync(message, cancellationToken);
    }

    public async Task SendHtmlAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        string textBody,
        IReadOnlyCollection<EmailInlineImage> inlineImages,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlBody);
        ArgumentException.ThrowIfNullOrWhiteSpace(textBody);
        ArgumentNullException.ThrowIfNull(inlineImages);

        using var message = CreateMessage(recipientEmail, subject);
        message.Body = textBody;
        message.IsBodyHtml = false;

        var plainView = AlternateView.CreateAlternateViewFromString(
            textBody,
            Encoding.UTF8,
            MediaTypeNames.Text.Plain);
        var htmlView = AlternateView.CreateAlternateViewFromString(
            htmlBody,
            Encoding.UTF8,
            MediaTypeNames.Text.Html);

        foreach (var image in inlineImages)
        {
            if (!File.Exists(image.FilePath))
            {
                continue;
            }

            var resource = new LinkedResource(image.FilePath, image.ContentType)
            {
                ContentId = image.ContentId,
                TransferEncoding = TransferEncoding.Base64
            };
            resource.ContentType.Name = Path.GetFileName(image.FilePath);
            htmlView.LinkedResources.Add(resource);
        }

        message.AlternateViews.Add(plainView);
        message.AlternateViews.Add(htmlView);

        await SendMessageAsync(message, cancellationToken);
    }

    private MailMessage CreateMessage(string recipientEmail, string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var fromEmail = configuration["Smtp:FromEmail"];
        if (string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new BusinessRuleException("SMTP is not configured. Set Smtp:FromEmail before sending CPMS emails.");
        }

        var fromName = configuration["Smtp:FromName"] ?? "CPMS";
        var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };
        message.To.Add(recipientEmail);
        return message;
    }

    private async Task SendMessageAsync(MailMessage message, CancellationToken cancellationToken)
    {
        var host = configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new BusinessRuleException("SMTP is not configured. Set Smtp:Host before sending CPMS emails.");
        }

        var port = configuration.GetValue("Smtp:Port", 587);
        var enableSsl = configuration.GetValue("Smtp:EnableSsl", true);
        var userName = configuration["Smtp:UserName"];
        var password = configuration["Smtp:Password"];

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

public sealed record EmailInlineImage(string ContentId, string FilePath, string ContentType);
