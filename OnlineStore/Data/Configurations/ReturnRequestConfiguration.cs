using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineStore.Models;

namespace OnlineStore.Data.Configurations;

public class ReturnRequestConfiguration : IEntityTypeConfiguration<ReturnRequest>
{
    public void Configure(EntityTypeBuilder<ReturnRequest> builder)
    {
        builder.ToTable("ReturnRequests", table =>
        {
            table.HasCheckConstraint("CK_ReturnRequests_Quantity", "`Quantity` > 0");
            table.HasCheckConstraint("CK_ReturnRequests_Status",
                "`Status` IN ('Requested', 'Received', 'Approved', 'Rejected')");
            // Each status needs its proof: receipt time for Received/Approved, a reviewer for decisions.
            table.HasCheckConstraint("CK_ReturnRequests_Progress",
                "(`Status` = 'Requested' AND `ReceivedAt` IS NULL AND `ReviewedByUserId` IS NULL) "
                + "OR (`Status` = 'Received' AND `ReceivedAt` IS NOT NULL) "
                + "OR (`Status` = 'Approved' AND `ReceivedAt` IS NOT NULL AND `ReviewedByUserId` IS NOT NULL) "
                + "OR (`Status` = 'Rejected' AND `ReviewedByUserId` IS NOT NULL)");
        });

        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(request => request.OrderItemId).HasColumnType("int");
        builder.Property(request => request.UserId).HasColumnType("int");
        builder.Property(request => request.ReviewedByUserId).HasColumnType("int");
        builder.Property(request => request.Quantity).HasColumnType("int");
        builder.Property(request => request.Status)
            .HasConversion<string>()
            .HasColumnType("varchar(20)").HasMaxLength(20)
            .UseCollation("utf8mb4_bin")
            .HasDefaultValue(ReturnStatus.Requested);
        builder.Property(request => request.RequestedAt).HasColumnType("datetime(6)").HasPrecision(6);
        builder.Property(request => request.ReceivedAt).HasColumnType("datetime(6)").HasPrecision(6);
        builder.Property(request => request.RowVersion).IsRowVersion();

        // The sales manager's queue.
        builder.HasIndex(request => new { request.Status, request.RequestedAt });
        builder.HasIndex(request => request.OrderItemId);
        builder.HasIndex(request => request.UserId);

        builder.HasOne(request => request.OrderItem).WithMany()
            .HasForeignKey(request => request.OrderItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>().WithMany()
            .HasForeignKey(request => request.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>().WithMany()
            .HasForeignKey(request => request.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds", table =>
            table.HasCheckConstraint("CK_Refunds_Amount", "`Amount` > 0"));

        builder.HasKey(refund => refund.Id);
        builder.Property(refund => refund.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(refund => refund.ReturnRequestId).HasColumnType("int");
        builder.Property(refund => refund.PaymentId).HasColumnType("int");
        builder.Property(refund => refund.IdempotencyKey)
            .HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(refund => refund.Amount).HasColumnType("decimal(18,2)").HasPrecision(18, 2);
        builder.Property(refund => refund.RefundedAt).HasColumnType("datetime(6)").HasPrecision(6);

        builder.HasIndex(refund => refund.IdempotencyKey).IsUnique();
        builder.HasIndex(refund => refund.PaymentId);

        // One refund per return request, so a request can never be refunded twice.
        builder.HasOne(refund => refund.ReturnRequest)
            .WithOne(request => request.Refund)
            .HasForeignKey<Refund>(refund => refund.ReturnRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(refund => refund.Payment).WithMany()
            .HasForeignKey(refund => refund.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
