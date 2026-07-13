namespace CPMS.BLL.Abstractions.Messaging;

public interface IEmailSender
{
    Task SendAsync(string recipientEmail, string subject, string body, CancellationToken cancellationToken);
}
