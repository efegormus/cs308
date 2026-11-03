using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineStore.Models;

namespace OnlineStore.Data.Configurations;

public class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> builder)
    {
        builder.ToTable("Ratings", table =>
            table.HasCheckConstraint("CK_Ratings_Stars", "`Stars` BETWEEN 1 AND 5"));

        builder.HasKey(rating => rating.Id);
        builder.Property(rating => rating.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(rating => rating.UserId).HasColumnType("int");
        builder.Property(rating => rating.ProductId).HasColumnType("int");
        builder.Property(rating => rating.OrderItemId).HasColumnType("int");
        builder.Property(rating => rating.Stars).HasColumnType("tinyint unsigned");
        builder.Property(rating => rating.CreatedAt).HasColumnType("datetime(6)").HasPrecision(6);

        // One rating per user and product; the product page averages these.
        builder.HasIndex(rating => new { rating.UserId, rating.ProductId }).IsUnique();
        builder.HasIndex(rating => rating.ProductId);
        builder.HasIndex(rating => rating.OrderItemId);

        builder.HasOne<User>().WithMany()
            .HasForeignKey(rating => rating.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(rating => rating.Product).WithMany()
            .HasForeignKey(rating => rating.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(rating => rating.OrderItem).WithMany()
            .HasForeignKey(rating => rating.OrderItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
