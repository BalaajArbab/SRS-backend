using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;
using SRS_backend.Configuration;

namespace SRS_backend.Services.Email;

public sealed class SmtpEmailSender(IOptions<EmailOptions> emailOptions) : IEmailSender
{
    private readonly EmailOptions _emailOptions = emailOptions.Value;

    public async Task SendAsync(EmailDeliveryMessage message, CancellationToken cancellationToken)
    {
        using var mailMessage = new MailMessage
        {
            From = new MailAddress(_emailOptions.SenderAddress),
            Subject = message.Subject,
            SubjectEncoding = Encoding.UTF8,
            Body = message.PlainTextContent,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };
        mailMessage.To.Add(message.RecipientAddress);
        mailMessage.AlternateViews.Add(
            AlternateView.CreateAlternateViewFromString(message.HtmlContent, Encoding.UTF8, "text/html"));
        mailMessage.Headers.Add("X-Order-Email-Notification-Id", message.NotificationId.ToString("N"));

        using var client = new SmtpClient(_emailOptions.Smtp.Host, _emailOptions.Smtp.Port)
        {
            EnableSsl = _emailOptions.Smtp.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(_emailOptions.Smtp.Username))
        {
            client.Credentials = new NetworkCredential(
                _emailOptions.Smtp.Username,
                _emailOptions.Smtp.Password);
        }

        await client.SendMailAsync(mailMessage, cancellationToken);
    }
}
