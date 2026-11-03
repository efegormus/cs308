using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineStore.Models;

namespace OnlineStore.Data.Configurations;

public class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.ToTable("WishlistItems");
        builder.HasKey(item => new { item.UserId, item.ProductId });
        builder.Property(item => item.UserId).HasColumnType("int");
        builder.Property(item => item.ProductId).HasColumnType("int");
        builder.Property(item => item.AddedAt).HasColumnType("datetime(6)").HasPrecision(6);

        // Used to find everyone who wished for a product when it is discounted.
        builder.HasIndex(item => item.ProductId);

        builder.HasOne<User>().WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.Product).WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
