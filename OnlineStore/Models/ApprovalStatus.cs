namespace OnlineStore.Models;

// Comments wait in Pending until the product manager approves or rejects them.
public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected
}
