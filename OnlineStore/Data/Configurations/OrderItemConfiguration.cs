using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineStore.Models;

namespace OnlineStore.Data.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", table =>
        {
            table.HasCheckConstraint("CK_OrderItems_UnitPrice", "`UnitPrice` >= 0");
            table.HasCheckConstraint("CK_OrderItems_Quantity", "`Quantity` > 0");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(item => item.OrderId).HasColumnType("int");
        builder.Property(item => item.ProductId).HasColumnType("int");
        builder.Property(item => item.ProductName)
            .HasColumnType("varchar(200)").HasMaxLength(200).IsRequired();
        builder.Property(item => item.UnitPrice).HasColumnType("decimal(18,2)").HasPrecision(18, 2);
        builder.Property(item => item.Quantity).HasColumnType("int");

        builder.HasIndex(item => new { item.OrderId, item.ProductId }).IsUnique();
        builder.HasIndex(item => item.ProductId);

        builder.HasOne(item => item.Order)
            .WithMany(order => order.OrderItems)
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.Product)
            .WithMany(product => product.OrderItems)
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
