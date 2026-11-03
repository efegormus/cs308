namespace OnlineStore.Models;

public enum PaymentStatus
{
    Pending,
    Confirmed,
    Failed,
    Refunded,
    // Legacy cancelled orders do not reveal whether payment was ever collected.
    Unknown
}
