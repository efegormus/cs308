using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineStore.Models;

namespace OnlineStore.Data.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations", table =>
        {
            table.HasCheckConstraint("CK_Conversations_Status", "`Status` IN ('Waiting', 'Active', 'Closed')");
            // Exactly one customer identity: an account or a guest token.
            table.HasCheckConstraint("CK_Conversations_Customer",
                "(`CustomerUserId` IS NOT NULL AND `GuestTokenHash` IS NULL) "
                + "OR (`CustomerUserId` IS NULL AND `GuestTokenHash` IS NOT NULL)");
            // A waiting chat has no agent; an active chat must have one.
            table.HasCheckConstraint("CK_Conversations_Claim",
                "(`Status` = 'Waiting' AND `ClaimedByUserId` IS NULL) "
                + "OR (`Status` = 'Active' AND `ClaimedByUserId` IS NOT NULL) OR `Status` = 'Closed'");
        });

        builder.HasKey(conversation => conversation.Id);
        builder.Property(conversation => conversation.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(conversation => conversation.CustomerUserId).HasColumnType("int");
        builder.Property(conversation => conversation.GuestTokenHash).HasColumnType("varchar(64)").HasMaxLength(64);
        builder.Property(conversation => conversation.ClaimedByUserId).HasColumnType("int");
        builder.Property(conversation => conversation.CartId).HasColumnType("int");
        builder.Property(conversation => conversation.Status)
            .HasConversion<string>()
            .HasColumnType("varchar(20)").HasMaxLength(20)
            .UseCollation("utf8mb4_bin")
            .HasDefaultValue(ConversationStatus.Waiting);
        builder.Property(conversation => conversation.CreatedAt).HasColumnType("datetime(6)").HasPrecision(6);
        builder.Property(conversation => conversation.RowVersion).IsRowVersion();

        // The agent's queue of waiting chats.
        builder.HasIndex(conversation => new { conversation.Status, conversation.CreatedAt });
        builder.HasIndex(conversation => conversation.CustomerUserId);
        builder.HasIndex(conversation => conversation.ClaimedByUserId);
        builder.HasIndex(conversation => conversation.GuestTokenHash);
        builder.HasIndex(conversation => conversation.CartId);

        builder.HasOne<User>().WithMany()
            .HasForeignKey(conversation => conversation.CustomerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>().WithMany()
            .HasForeignKey(conversation => conversation.ClaimedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // If a cart is cleaned up, the chat stays; it just loses the cart link.
        builder.HasOne<Cart>().WithMany()
            .HasForeignKey(conversation => conversation.CartId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages", table =>
            table.HasCheckConstraint("CK_ChatMessages_Body", "`Body` IS NULL OR CHAR_LENGTH(`Body`) > 0"));

        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(message => message.ConversationId).HasColumnType("int");
        builder.Property(message => message.SenderUserId).HasColumnType("int");
        builder.Property(message => message.Body).HasColumnType("varchar(4000)").HasMaxLength(4000);
        builder.Property(message => message.SentAt).HasColumnType("datetime(6)").HasPrecision(6);

        builder.HasIndex(message => new { message.ConversationId, message.SentAt });
        builder.HasIndex(message => message.SenderUserId);

        builder.HasOne(message => message.Conversation)
            .WithMany(conversation => conversation.Messages)
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>().WithMany()
            .HasForeignKey(message => message.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ChatAttachmentConfiguration : IEntityTypeConfiguration<ChatAttachment>
{
    public void Configure(EntityTypeBuilder<ChatAttachment> builder)
    {
        builder.ToTable("ChatAttachments", table =>
            table.HasCheckConstraint("CK_ChatAttachments_Size", "`SizeBytes` > 0"));

        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(attachment => attachment.ChatMessageId).HasColumnType("int");
        builder.Property(attachment => attachment.ProtectedStorageKey)
            .HasColumnType("varchar(200)").HasMaxLength(200).IsRequired();
        builder.Property(attachment => attachment.OriginalFileName)
            .HasColumnType("varchar(255)").HasMaxLength(255).IsRequired();
        builder.Property(attachment => attachment.ContentType)
            .HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
        builder.Property(attachment => attachment.SizeBytes).HasColumnType("bigint");

        // The stored file name is random and unique; the original name is only shown to people.
        builder.HasIndex(attachment => attachment.ProtectedStorageKey).IsUnique();
        builder.HasIndex(attachment => attachment.ChatMessageId);

        builder.HasOne(attachment => attachment.ChatMessage)
            .WithMany(message => message.Attachments)
            .HasForeignKey(attachment => attachment.ChatMessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
