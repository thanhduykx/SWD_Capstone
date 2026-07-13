using CPMS.BLL.Abstractions.Messaging;
using CPMS.BLL.Accounts;
using CPMS.BLL.Exceptions;

namespace CPMS.DAL.ExternalServices;

public sealed class AccountWelcomeNotificationSender(IEmailSender emailSender) : IAccountWelcomeNotificationSender
{
    public async Task<AccountNotificationResult> SendAsync(
        string email,
        string fullName,
        string username,
        string initialPassword,
        CancellationToken cancellationToken)
    {
        var body = string.Join(Environment.NewLine,
        [
            $"Hello {fullName},",
            string.Empty,
            "Your CPMS account has been created successfully.",
            "Use the credentials below to sign in:",
            $"Username / Account: {username}",
            $"Initial password: {initialPassword}",
            string.Empty,
            "Keep this account information private and contact the moderator if it is unexpected."
        ]);
        try
        {
            await emailSender.SendAsync(email, "CPMS account login information", body, cancellationToken);
            return new AccountNotificationResult("Sent", null);
        }
        catch (BusinessRuleException exception)
        {
            return new AccountNotificationResult("Skipped", exception.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new AccountNotificationResult("Failed", exception.Message);
        }
    }
}
