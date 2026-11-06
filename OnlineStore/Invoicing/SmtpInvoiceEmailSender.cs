using System.Net;
using System.Net.Mail;
using System.Net.Mime;

namespace OnlineStore.Invoicing;

public sealed class SmtpInvoiceEmailSender(IConfiguration config) : IInvoiceEmailSender
{
    public async Task SendAsync(InvoiceEmailMessage message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!MailAddress.TryCreate(message.Recipient, out var recipient)
            || !string.Equals(recipient.Address, message.Recipient, StringComparison.OrdinalIgnoreCase))
            throw new InvoiceEmailRecipientException();
        using var client = CreateClient();
        var from = config["Email:From"];
        if (!MailAddress.TryCreate(from, out var fromAddress)) throw new InvoiceEmailConfigurationException();
        using var mail = new MailMessage(fromAddress, recipient)
        {
            Subject = $"Invoice for order {message.OrderId}", Body = "Your purchase invoice is attached."
        };
        mail.Headers.Add("Message-ID", message.MessageId);
        mail.Attachments.Add(new Attachment(new MemoryStream(message.Attachment.Content, writable: false),
            message.Attachment.FileName, MediaTypeNames.Application.Pdf));
        await client.SendMailAsync(mail, ct);
    }

    private SmtpClient CreateClient()
    {
        try
        {
            var host = config["Email:Smtp:Host"];
            var port = config.GetValue("Email:Smtp:Port", 587);
            var ssl = config.GetValue("Email:Smtp:EnableSsl", true);
            if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535)
                throw new InvoiceEmailConfigurationException();
            var username = config["Email:Smtp:Username"];
            return new SmtpClient(host, port)
            {
                EnableSsl = ssl, UseDefaultCredentials = false,
                Credentials = string.IsNullOrEmpty(username) ? null : new NetworkCredential(username, config["Email:Smtp:Password"])
            };
        }
        catch (Exception e) when (e is ArgumentException or FormatException or InvalidOperationException)
        {
            throw new InvoiceEmailConfigurationException();
        }
    }
}
