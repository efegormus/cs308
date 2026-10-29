using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineStore.Models;

namespace OnlineStore.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", table => table.HasCheckConstraint(
            "CK_Orders_Status",
            "`Status` IN ('Pending', 'Paid', 'Shipped', 'Completed', 'Cancelled')"));

        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(order => order.UserId).HasColumnType("int");
        builder.Property(order => order.CreatedAt).HasColumnType("datetime(6)").HasPrecision(6);
        builder.Property(order => order.Status)
            .HasConversion<string>()
            .HasColumnType("varchar(20)").HasMaxLength(20)
            .UseCollation("utf8mb4_bin")
            .HasDefaultValue(OrderStatus.Pending);

        builder.HasIndex(order => new { order.UserId, order.CreatedAt });

        builder.HasOne(order => order.User)
            .WithMany(user => user.Orders)
            .HasForeignKey(order => order.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
