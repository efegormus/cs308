using System.Net.Mail;
using System.Net.Sockets;
using System.Security.Authentication;

namespace OnlineStore.Invoicing;

// Only these categories, never exception text or inner exceptions, reach delivery state/logs.
public sealed record InvoiceEmailFailure(string Code, bool Retryable)
{
    public static InvoiceEmailFailure Classify(Exception error) => error switch
    {
        InvoiceEmailConfigurationException => new("smtp_configuration", false),
        InvoiceEmailRecipientException => new("recipient_invalid", false),
        AuthenticationException => new("smtp_authentication", false),
        SmtpFailedRecipientsException recipients => recipients.InnerExceptions.Any(x => (int)x.StatusCode >= 500)
            ? new("recipient_rejected", false) : new("smtp_transient", true),
        SmtpFailedRecipientException recipient => (int)recipient.StatusCode >= 500
            ? new("recipient_rejected", false) : new("smtp_transient", true),
        SmtpException smtp when (int)smtp.StatusCode is 530 or 534 or 535 or 538 => new("smtp_authentication", false),
        SmtpException { InnerException: AuthenticationException } => new("smtp_authentication", false),
        SmtpException smtp when (int)smtp.StatusCode >= 500 => new("smtp_permanent", false),
        SmtpException smtp when (int)smtp.StatusCode is >= 400 and < 500 => new("smtp_transient", true),
        SmtpException { InnerException: IOException or SocketException or TimeoutException } => new("smtp_transient", true),
        // GeneralFailure without a transport cause can mean unsupported TLS or invalid client setup.
        SmtpException => new("smtp_configuration", false),
        OperationCanceledException or TimeoutException => new("send_timeout", true),
        SocketException => new("network_unavailable", true),
        IOException => new("document_or_network_unavailable", true),
        _ => new("unexpected_failure", false)
    };
}

public sealed class InvoiceEmailConfigurationException() : Exception("SMTP configuration unavailable.");
public sealed class InvoiceEmailRecipientException() : Exception("Invalid invoice recipient.");
