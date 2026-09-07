using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;
using SRS_backend.Configuration;

namespace SRS_backend.Services.Email;

public sealed class AzureCommunicationServicesEmailSender(IOptions<EmailOptions> emailOptions) : IEmailSender
{
    private readonly EmailOptions _emailOptions = emailOptions.Value;
    private readonly EmailClient _emailClient = new(emailOptions.Value.AzureCommunicationServices.ConnectionString);

    public async Task SendAsync(EmailDeliveryMessage message, CancellationToken cancellationToken)
    {
        var operation = await _emailClient.SendAsync(
            WaitUntil.Completed,
            _emailOptions.SenderAddress,
            message.RecipientAddress,
            message.Subject,
            message.HtmlContent,
            message.NotificationId,
            message.PlainTextContent,
            cancellationToken);

        if (operation.Value.Status != EmailSendStatus.Succeeded)
        {
            throw new InvalidOperationException("The email provider did not accept the message.");
        }
    }
}
