using CPMS.BLL.Exceptions;
using CPMS.DAL.ExternalServices;
using Microsoft.Extensions.Configuration;

namespace CPMS.IntegrationTests;

public sealed class SmtpEmailSenderTests
{
    [Fact]
    public async Task SendAsync_WhenPasswordIsMissing_FailsBeforeOpeningNetworkConnection()
    {
        SmtpEmailSender sender = CreateSender(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.example.test",
            ["Smtp:Port"] = "587",
            ["Smtp:EnableSsl"] = "true",
            ["Smtp:FromEmail"] = "sender@example.test",
            ["Smtp:UserName"] = "sender@example.test",
            ["Smtp:Password"] = ""
        });

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.SendAsync("recipient@example.test", "Subject", "Body", CancellationToken.None));

        Assert.Equal(
            "SMTP credentials are not configured. Set Smtp:UserName and Smtp:Password in a secret store.",
            exception.Message);
    }

    [Fact]
    public async Task SendAsync_WhenTlsIsDisabled_FailsBeforeOpeningNetworkConnection()
    {
        SmtpEmailSender sender = CreateSender(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.example.test",
            ["Smtp:Port"] = "587",
            ["Smtp:EnableSsl"] = "false",
            ["Smtp:FromEmail"] = "sender@example.test",
            ["Smtp:UserName"] = "sender@example.test",
            ["Smtp:Password"] = "test-only-password"
        });

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.SendAsync("recipient@example.test", "Subject", "Body", CancellationToken.None));

        Assert.Equal("SMTP TLS must be enabled.", exception.Message);
    }

    private static SmtpEmailSender CreateSender(Dictionary<string, string?> values)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new SmtpEmailSender(configuration);
    }
}
