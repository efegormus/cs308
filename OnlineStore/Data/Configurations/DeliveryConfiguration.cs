using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineStore.Models;

namespace OnlineStore.Data.Configurations;

public class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.ToTable("Deliveries", table =>
        {
            table.HasCheckConstraint("CK_Deliveries_Status",
                "`Status` IN ('Processing', 'InTransit', 'Delivered', 'Cancelled')");
            table.HasCheckConstraint("CK_Deliveries_DeliveredAt",
                "`Status` <> 'Delivered' OR `DeliveredAt` IS NOT NULL");
        });

        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(delivery => delivery.OrderId).HasColumnType("int");
        builder.Property(delivery => delivery.AddressSnapshot)
            .HasColumnType("varchar(1000)").HasMaxLength(1000).IsRequired();
        builder.Property(delivery => delivery.Status)
            .HasConversion<string>()
            .HasColumnType("varchar(20)").HasMaxLength(20)
            .UseCollation("utf8mb4_bin")
            .HasDefaultValue(OrderStatus.Processing);
        builder.Property(delivery => delivery.CreatedAt).HasColumnType("datetime(6)").HasPrecision(6);
        builder.Property(delivery => delivery.DeliveredAt).HasColumnType("datetime(6)").HasPrecision(6);

        builder.HasIndex(delivery => new { delivery.Status, delivery.CreatedAt });
        builder.HasIndex(delivery => delivery.OrderId);

        builder.HasOne(delivery => delivery.Order)
            .WithMany(order => order.Deliveries)
            .HasForeignKey(delivery => delivery.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DeliveryItemConfiguration : IEntityTypeConfiguration<DeliveryItem>
{
    public void Configure(EntityTypeBuilder<DeliveryItem> builder)
    {
        builder.ToTable("DeliveryItems", table =>
        {
            table.HasCheckConstraint("CK_DeliveryItems_Quantity", "`Quantity` > 0");
            table.HasCheckConstraint("CK_DeliveryItems_TotalPrice", "`TotalPriceSnapshot` >= 0");
        });

        builder.HasKey(item => new { item.DeliveryId, item.OrderItemId });
        builder.Property(item => item.DeliveryId).HasColumnType("int");
        builder.Property(item => item.OrderItemId).HasColumnType("int");
        builder.Property(item => item.Quantity).HasColumnType("int");
        builder.Property(item => item.TotalPriceSnapshot).HasColumnType("decimal(18,2)").HasPrecision(18, 2);

        builder.HasIndex(item => item.OrderItemId);

        builder.HasOne(item => item.Delivery)
            .WithMany(delivery => delivery.Items)
            .HasForeignKey(item => item.DeliveryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.OrderItem).WithMany()
            .HasForeignKey(item => item.OrderItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
