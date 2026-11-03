using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineStore.Models;

namespace OnlineStore.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", table =>
            table.HasCheckConstraint("CK_Notifications_Kind",
                "`Kind` IN ('WishlistDiscount', 'RefundApproved', 'ReturnRejected')"));

        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(notification => notification.UserId).HasColumnType("int");
        builder.Property(notification => notification.ProductId).HasColumnType("int");
        builder.Property(notification => notification.Kind)
            .HasConversion<string>()
            .HasColumnType("varchar(30)").HasMaxLength(30)
            .UseCollation("utf8mb4_bin");
        builder.Property(notification => notification.DeduplicationKey)
            .HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
        builder.Property(notification => notification.CreatedAt).HasColumnType("datetime(6)").HasPrecision(6);
        builder.Property(notification => notification.SentAt).HasColumnType("datetime(6)").HasPrecision(6);

        builder.HasIndex(notification => notification.DeduplicationKey).IsUnique();
        builder.HasIndex(notification => new { notification.UserId, notification.CreatedAt });
        // The sender job looks for notifications that were not sent yet.
        builder.HasIndex(notification => new { notification.SentAt, notification.CreatedAt });

        builder.HasOne<User>().WithMany()
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(notification => notification.Product).WithMany()
            .HasForeignKey(notification => notification.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
