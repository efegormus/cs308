using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineStore.Models;

namespace OnlineStore.Data.Configurations;

public sealed class InvoiceEmailDeliveryConfiguration : IEntityTypeConfiguration<InvoiceEmailDelivery>
{
    public void Configure(EntityTypeBuilder<InvoiceEmailDelivery> b)
    {
        b.ToTable("InvoiceEmailDeliveries", t =>
        {
            t.HasCheckConstraint("CK_InvoiceEmailDelivery_Attempts", "`AttemptCount` >= 0");
            t.HasCheckConstraint("CK_InvoiceEmailDelivery_Recipient", "CHAR_LENGTH(`RecipientSnapshot`) > 0");
            t.HasCheckConstraint("CK_InvoiceEmailDelivery_Lease", "(`LeaseUntil` IS NULL) = (`LeaseToken` IS NULL)");
            t.HasCheckConstraint("CK_InvoiceEmailDelivery_Stopped", "`StoppedAt` IS NULL OR (`SentAt` IS NULL AND `LeaseUntil` IS NULL AND `LastErrorCode` IS NOT NULL)");
        });
        b.HasKey(x => x.OrderId);
        b.Property(x => x.OrderId).ValueGeneratedNever();
        b.Property(x => x.RecipientSnapshot).HasMaxLength(254).IsRequired()
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        b.Property(x => x.CreatedAt).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        b.Property(x => x.LeaseToken).HasMaxLength(32);
        b.Property(x => x.LastErrorCode).HasMaxLength(40);
        foreach (var name in new[] { "CreatedAt", "NextAttemptAt", "LastAttemptAt", "LeaseUntil", "SentAt", "StoppedAt" })
            b.Property(name).HasColumnType("datetime(6)").HasPrecision(6);
        b.HasIndex(x => new { x.SentAt, x.StoppedAt, x.NextAttemptAt, x.LeaseUntil });
        b.HasOne<Order>().WithOne().HasForeignKey<InvoiceEmailDelivery>(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
