using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineStore.Models;

namespace OnlineStore.Data.Configurations;

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments", table =>
        {
            table.HasCheckConstraint("CK_Comments_ApprovalStatus",
                "`ApprovalStatus` IN ('Pending', 'Approved', 'Rejected')");
            // Pending comments have no moderator yet; decided comments must say who and when.
            table.HasCheckConstraint("CK_Comments_Moderation",
                "(`ApprovalStatus` = 'Pending' AND `ModeratedAt` IS NULL AND `ModeratedByUserId` IS NULL) "
                + "OR (`ApprovalStatus` <> 'Pending' AND `ModeratedAt` IS NOT NULL AND `ModeratedByUserId` IS NOT NULL)");
        });

        builder.HasKey(comment => comment.Id);
        builder.Property(comment => comment.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(comment => comment.UserId).HasColumnType("int");
        builder.Property(comment => comment.ProductId).HasColumnType("int");
        builder.Property(comment => comment.OrderItemId).HasColumnType("int");
        builder.Property(comment => comment.ModeratedByUserId).HasColumnType("int");
        builder.Property(comment => comment.Body)
            .HasColumnType("varchar(2000)").HasMaxLength(2000).IsRequired();
        builder.Property(comment => comment.ApprovalStatus)
            .HasConversion<string>()
            .HasColumnType("varchar(20)").HasMaxLength(20)
            .UseCollation("utf8mb4_bin")
            .HasDefaultValue(ApprovalStatus.Pending);
        builder.Property(comment => comment.CreatedAt).HasColumnType("datetime(6)").HasPrecision(6);
        builder.Property(comment => comment.ModeratedAt).HasColumnType("datetime(6)").HasPrecision(6);

        // One comment per user and product.
        builder.HasIndex(comment => new { comment.UserId, comment.ProductId }).IsUnique();
        // Product page: approved comments of one product, newest first.
        builder.HasIndex(comment => new { comment.ProductId, comment.ApprovalStatus, comment.CreatedAt });
        // Moderation queue for the product manager.
        builder.HasIndex(comment => new { comment.ApprovalStatus, comment.CreatedAt });
        builder.HasIndex(comment => comment.OrderItemId);

        builder.HasOne<User>().WithMany()
            .HasForeignKey(comment => comment.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>().WithMany()
            .HasForeignKey(comment => comment.ModeratedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(comment => comment.Product).WithMany()
            .HasForeignKey(comment => comment.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(comment => comment.OrderItem).WithMany()
            .HasForeignKey(comment => comment.OrderItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
