using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineStore.Models;

namespace OnlineStore.Data.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasKey(invoice => invoice.Id);
        builder.Property(invoice => invoice.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(invoice => invoice.OrderId).HasColumnType("int");
        builder.Property(invoice => invoice.InvoiceNumber)
            .HasColumnType("varchar(40)").HasMaxLength(40).IsRequired();
        builder.Property(invoice => invoice.CustomerNameSnapshot)
            .HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
        builder.Property(invoice => invoice.TaxIdSnapshot)
            .HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(invoice => invoice.AddressSnapshot)
            .HasColumnType("varchar(1000)").HasMaxLength(1000).IsRequired();
        builder.Property(invoice => invoice.ProtectedDocumentKey)
            .HasColumnType("varchar(200)").HasMaxLength(200);
        builder.Property(invoice => invoice.IssuedAt).HasColumnType("datetime(6)").HasPrecision(6);
        builder.Property(invoice => invoice.EmailedAt).HasColumnType("datetime(6)").HasPrecision(6);

        builder.HasIndex(invoice => invoice.InvoiceNumber).IsUnique();
        builder.HasIndex(invoice => invoice.IssuedAt);

        // One invoice per order; a paid order's invoice must outlive any cleanup.
        builder.HasOne(invoice => invoice.Order)
            .WithOne(order => order.Invoice)
            .HasForeignKey<Invoice>(invoice => invoice.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
