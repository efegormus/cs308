namespace OnlineStore.Models;

// Requested -> Received (the product is back in the store) -> Approved (refund) or Rejected.
public enum ReturnStatus
{
    Requested,
    Received,
    Approved,
    Rejected
}
