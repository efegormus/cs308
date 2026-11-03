using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineStore.Models;

namespace OnlineStore.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", table =>
        {
            table.HasCheckConstraint("CK_Payments_Amount", "`Amount` >= 0");
            table.HasCheckConstraint("CK_Payments_Status", "`Status` IN ('Pending', 'Confirmed', 'Failed')");
            table.HasCheckConstraint("CK_Payments_ConfirmedAt", "`Status` <> 'Confirmed' OR `ConfirmedAt` IS NOT NULL");
        });

        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(payment => payment.OrderId).HasColumnType("int");
        builder.Property(payment => payment.IdempotencyKey)
            .HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(payment => payment.ProviderReference)
            .HasColumnType("varchar(100)").HasMaxLength(100);
        builder.Property(payment => payment.Amount).HasColumnType("decimal(18,2)").HasPrecision(18, 2);
        builder.Property(payment => payment.Status)
            .HasConversion<string>()
            .HasColumnType("varchar(20)").HasMaxLength(20)
            .UseCollation("utf8mb4_bin")
            .HasDefaultValue(PaymentStatus.Pending);
        builder.Property(payment => payment.CreatedAt).HasColumnType("datetime(6)").HasPrecision(6);
        builder.Property(payment => payment.ConfirmedAt).HasColumnType("datetime(6)").HasPrecision(6);

        builder.HasIndex(payment => payment.IdempotencyKey).IsUnique();
        builder.HasIndex(payment => payment.ProviderReference).IsUnique();
        builder.HasIndex(payment => payment.OrderId);

        // A paid order's history must survive: orders cannot be deleted while payments reference them.
        builder.HasOne(payment => payment.Order)
            .WithMany(order => order.Payments)
            .HasForeignKey(payment => payment.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
