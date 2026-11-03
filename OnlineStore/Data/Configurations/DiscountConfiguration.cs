using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineStore.Models;

namespace OnlineStore.Data.Configurations;

public class DiscountConfiguration : IEntityTypeConfiguration<Discount>
{
    public void Configure(EntityTypeBuilder<Discount> builder)
    {
        builder.ToTable("Discounts", table =>
        {
            table.HasCheckConstraint("CK_Discounts_Rate", "`Rate` > 0 AND `Rate` <= 1");
            table.HasCheckConstraint("CK_Discounts_Period", "`EndsAt` > `StartsAt`");
        });

        builder.HasKey(discount => discount.Id);
        builder.Property(discount => discount.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(discount => discount.Rate).HasColumnType("decimal(5,4)").HasPrecision(5, 4);
        builder.Property(discount => discount.StartsAt).HasColumnType("datetime(6)").HasPrecision(6);
        builder.Property(discount => discount.EndsAt).HasColumnType("datetime(6)").HasPrecision(6);

        builder.HasIndex(discount => new { discount.StartsAt, discount.EndsAt });
    }
}

public class DiscountProductConfiguration : IEntityTypeConfiguration<DiscountProduct>
{
    public void Configure(EntityTypeBuilder<DiscountProduct> builder)
    {
        builder.ToTable("DiscountProducts");
        builder.HasKey(link => new { link.DiscountId, link.ProductId });
        builder.Property(link => link.DiscountId).HasColumnType("int");
        builder.Property(link => link.ProductId).HasColumnType("int");

        builder.HasIndex(link => link.ProductId);

        // Removing a discount removes its product links; a product can never be deleted through them.
        builder.HasOne(link => link.Discount)
            .WithMany(discount => discount.Products)
            .HasForeignKey(link => link.DiscountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(link => link.Product)
            .WithMany()
            .HasForeignKey(link => link.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
