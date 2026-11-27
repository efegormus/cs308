using System.Net;
using System.Net.Mail;

namespace OnlineStore.Pricing;

// Same Email:From and Email:Smtp:* settings as SmtpInvoiceEmailSender.
public sealed class SmtpDiscountEmailSender(IConfiguration config) : IDiscountEmailSender
{
    public async Task SendAsync(DiscountEmailMessage message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!MailAddress.TryCreate(message.Recipient, out var recipient)
            || !string.Equals(recipient.Address, message.Recipient, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The recipient is not a valid email address.");
        if (!MailAddress.TryCreate(config["Email:From"], out var from))
            throw new InvalidOperationException("Email:From is not configured.");
        var host = config["Email:Smtp:Host"];
        var port = config.GetValue("Email:Smtp:Port", 587);
        if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535)
            throw new InvalidOperationException("Email:Smtp is not configured.");
        var username = config["Email:Smtp:Username"];
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = config.GetValue("Email:Smtp:EnableSsl", true), UseDefaultCredentials = false,
            Credentials = string.IsNullOrEmpty(username) ? null : new NetworkCredential(username, config["Email:Smtp:Password"])
        };
        using var mail = new MailMessage(from, recipient) { Subject = message.Subject, Body = message.Body };
        mail.Headers.Add("Message-ID", message.MessageId);
        await client.SendMailAsync(mail, ct);
    }
}
