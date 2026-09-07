namespace SRS_backend.Services.Email;

public interface IEmailSender
{
    Task SendAsync(EmailDeliveryMessage message, CancellationToken cancellationToken);
}

public sealed record EmailDeliveryMessage(
    Guid NotificationId,
    string RecipientAddress,
    string Subject,
    string PlainTextContent,
    string HtmlContent);
