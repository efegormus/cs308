using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineStore.Models;

namespace OnlineStore.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", table =>
        {
            table.HasCheckConstraint("CK_Products_Price", "`Price` >= 0");
            table.HasCheckConstraint("CK_Products_StockQuantity", "`StockQuantity` >= 0");
        });

        builder.HasKey(product => product.Id);
        builder.Property(product => product.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(product => product.CategoryId).HasColumnType("int");
        builder.Property(product => product.Sku)
            .HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(product => product.Name)
            .HasColumnType("varchar(200)").HasMaxLength(200).IsRequired();
        builder.Property(product => product.Description).HasColumnType("text");
        builder.Property(product => product.Price).HasColumnType("decimal(18,2)").HasPrecision(18, 2);
        builder.Property(product => product.StockQuantity).HasColumnType("int").HasDefaultValue(0);
        builder.Property(product => product.ImageUrl).HasColumnType("varchar(2048)").HasMaxLength(2048);
        builder.Property(product => product.IsActive).HasColumnType("tinyint(1)").HasDefaultValue(true);

        builder.HasIndex(product => product.Sku).IsUnique();
        builder.HasIndex(product => new { product.CategoryId, product.IsActive, product.Name });

        builder.HasOne(product => product.Category)
            .WithMany(category => category.Products)
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
