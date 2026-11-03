namespace OnlineStore.Models;

// A support chat. It belongs either to a logged-in customer or to a guest (identified by a hashed token).
public class Conversation
{
    public int Id { get; set; }
    public int? CustomerUserId { get; set; }
    public string? GuestTokenHash { get; set; }

    // The support agent who claimed the chat; null while it waits in the queue.
    public int? ClaimedByUserId { get; set; }

    // The customer's cart, so the agent can see it (logged-in customers only).
    public int? CartId { get; set; }
    public ConversationStatus Status { get; set; } = ConversationStatus.Waiting;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Updated by MySQL on every change, so two agents cannot both claim the same chat.
    public DateTime RowVersion { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public class ChatMessage
{
    public int Id { get; set; }
    public int ConversationId { get; set; }

    // Null when the guest customer wrote it.
    public int? SenderUserId { get; set; }

    // Null when the message is only an attachment.
    public string? Body { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public Conversation Conversation { get; set; } = null!;
    public ICollection<ChatAttachment> Attachments { get; set; } = new List<ChatAttachment>();
}

// A file sent in chat (PDF, image or video). The file itself is stored outside wwwroot.
public class ChatAttachment
{
    public int Id { get; set; }
    public int ChatMessageId { get; set; }
    public required string ProtectedStorageKey { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }

    public ChatMessage ChatMessage { get; set; } = null!;
}
