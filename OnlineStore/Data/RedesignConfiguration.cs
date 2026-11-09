using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OnlineStore.Models;
namespace OnlineStore.Data;
public static class RedesignConfiguration
{
    public static void Configure(ModelBuilder model, PersonalDataProtector crypto)
    {
        model.UseCollation("utf8mb4_0900_ai_ci");
        {
            var b = model.Entity<Address>();
            b.ToTable("Addresses");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<long>("UserId").HasColumnName("UserId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<string>("Label").HasColumnName("Label").HasColumnType("varchar(50)").HasMaxLength(50).IsRequired();
            b.Property<string>("Text").HasColumnName("AddressEnc").HasColumnType("varbinary(2048)").HasMaxLength(2048).HasConversion(new ValueConverter<string, byte[]>(v => crypto.Encrypt(v), v => crypto.Decrypt(v))).IsRequired();
            b.Property<int>("DataKeyVersion").HasColumnName("DataKeyVersion").HasColumnType("smallint unsigned").HasConversion<ushort>().IsRequired();
            b.Property<bool>("IsDefault").HasColumnName("IsDefault").HasColumnType("tinyint(1)").IsRequired().HasDefaultValueSql("0").ValueGeneratedNever();
            b.Property<bool>("IsActive").HasColumnName("IsActive").HasColumnType("tinyint(1)").IsRequired().HasDefaultValueSql("1").ValueGeneratedNever();
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<DateTime>("UpdatedAt").HasColumnName("UpdatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<long?>("DefaultOwnerId").HasColumnName("DefaultOwnerId").HasColumnType("bigint unsigned").HasConversion<ulong>().HasComputedColumnSql("if(((`IsDefault` = 1) and (`IsActive` = 1)),`UserId`,NULL)", stored: false);
            b.HasKey("Id");
            b.HasIndex(new[] { "DefaultOwnerId" }).HasDatabaseName("UX_Addresses_OneDefault").IsUnique();
            b.HasIndex(new[] { "UserId" }).HasDatabaseName("IX_Addresses_User");
            b.HasOne<User>().WithMany("Addresses").HasForeignKey("UserId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_Addresses_Users");
            b.ToTable(t => t.HasCheckConstraint("CK_Addresses_KeyVersion", "(`DataKeyVersion` > 0)"));
        }
        {
            var b = model.Entity<AttributeDefinition>();
            b.ToTable("AttributeDefinitions");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<long>("CategoryId").HasColumnName("CategoryId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<string>("Code").HasColumnName("Code").HasColumnType("varchar(50)").HasMaxLength(50).IsRequired();
            b.Property<string>("Label").HasColumnName("Label").HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
            b.Property<string>("DataType").HasColumnName("DataType").HasColumnType("varchar(10)").HasMaxLength(10).UseCollation("utf8mb4_bin").IsRequired();
            b.Property<string?>("Unit").HasColumnName("Unit").HasColumnType("varchar(20)").HasMaxLength(20);
            b.Property<bool>("IsFilterable").HasColumnName("IsFilterable").HasColumnType("tinyint(1)").IsRequired().HasDefaultValueSql("0").ValueGeneratedNever();
            b.Property<int>("SortOrder").HasColumnName("SortOrder").HasColumnType("smallint unsigned").HasConversion<ushort>().IsRequired().HasDefaultValueSql("0");
            b.HasKey("Id");
            b.HasIndex(new[] { "CategoryId", "Code" }).HasDatabaseName("UX_AttributeDefinitions_Code").IsUnique();
            b.HasAlternateKey(new[] { "Id", "DataType" }).HasName("UX_AttributeDefinitions_Typed");
            b.HasOne<Category>("Category").WithMany().HasForeignKey("CategoryId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_AttributeDefinitions_Categories");
            b.ToTable(t => t.HasCheckConstraint("CK_AttributeDefinitions_DataType", "(`DataType` in (_utf8mb4'Text',_utf8mb4'Number',_utf8mb4'Bool'))"));
        }
        {
            var b = model.Entity<Brand>();
            b.ToTable("Brands");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<string>("Name").HasColumnName("Name").HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
            b.Property<bool>("IsActive").HasColumnName("IsActive").HasColumnType("tinyint(1)").IsRequired().HasDefaultValueSql("1").ValueGeneratedNever();
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.HasKey("Id");
            b.HasIndex(new[] { "Name" }).HasDatabaseName("UX_Brands_Name").IsUnique();
        }
        {
            var b = model.Entity<CartItem>();
            b.ToTable("CartItems");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<long>("CartId").HasColumnName("CartId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long>("ProductId").HasColumnName("ProductId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<int>("Quantity").HasColumnName("Quantity").HasColumnType("int unsigned").HasConversion<uint>().IsRequired();
            b.Property<DateTime>("AddedAt").HasColumnName("AddedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<DateTime>("UpdatedAt").HasColumnName("UpdatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.HasKey("Id");
            b.HasIndex(new[] { "CartId", "ProductId" }).HasDatabaseName("UX_CartItems_CartProduct").IsUnique();
            b.HasIndex(new[] { "ProductId" }).HasDatabaseName("IX_CartItems_Product");
            b.HasOne<Cart>("Cart").WithMany("Items").HasForeignKey("CartId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_CartItems_Carts");
            b.HasOne<Product>("Product").WithMany().HasForeignKey("ProductId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_CartItems_Products");
            b.ToTable(t => t.HasCheckConstraint("CK_CartItems_Quantity", "(`Quantity` > 0)"));
        }
        {
            var b = model.Entity<Cart>();
            b.ToTable("Carts");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<long?>("UserId").HasColumnName("UserId").HasColumnType("bigint unsigned").HasConversion<ulong>();
            b.Property<string?>("GuestTokenHash").HasColumnName("GuestTokenHash").HasColumnType("char(64)").HasMaxLength(64);
            b.Property<CartState>("State").HasColumnName("State").HasColumnType("varchar(20)").HasMaxLength(20).HasConversion<string>().UseCollation("utf8mb4_bin").IsRequired().HasDefaultValueSql("'Open'").ValueGeneratedNever();
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<DateTime>("UpdatedAt").HasColumnName("UpdatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<DateTime>("RowVersion").HasColumnName("RowVersion").HasColumnType("datetime(6)").IsRequired().IsRowVersion();
            b.Property<long?>("OpenUserId").HasColumnName("OpenUserId").HasColumnType("bigint unsigned").HasConversion<ulong>().HasComputedColumnSql("if((`State` = _utf8mb4'Open'),`UserId`,NULL)", stored: false);
            b.HasKey("Id");
            b.HasIndex(new[] { "GuestTokenHash" }).HasDatabaseName("UX_Carts_GuestToken").IsUnique();
            b.HasIndex(new[] { "Id", "UserId" }).HasDatabaseName("UX_Carts_IdUser").IsUnique();
            b.HasIndex(new[] { "OpenUserId" }).HasDatabaseName("UX_Carts_OneOpenPerUser").IsUnique();
            b.HasIndex(new[] { "UserId", "State" }).HasDatabaseName("IX_Carts_UserState");
            b.HasOne<User>().WithMany().HasForeignKey("UserId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_Carts_Users");
            b.ToTable(t => t.HasCheckConstraint("CK_Carts_Owner", "(((`UserId` is not null) and (`GuestTokenHash` is null)) or ((`UserId` is null) and (`GuestTokenHash` is not null)))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Carts_State", "(`State` in (_utf8mb4'Open',_utf8mb4'CheckedOut',_utf8mb4'Abandoned'))"));
        }
        {
            var b = model.Entity<Category>();
            b.ToTable("Categories");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<long?>("ParentId").HasColumnName("ParentId").HasColumnType("bigint unsigned").HasConversion<ulong>();
            b.Property<string>("Name").HasColumnName("Name").HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
            b.Property<string>("Slug").HasColumnName("Slug").HasColumnType("varchar(120)").HasMaxLength(120).IsRequired();
            b.Property<string?>("Description").HasColumnName("Description").HasColumnType("text");
            b.Property<int>("SortOrder").HasColumnName("SortOrder").HasColumnType("smallint unsigned").HasConversion<ushort>().IsRequired().HasDefaultValueSql("0");
            b.Property<bool>("IsActive").HasColumnName("IsActive").HasColumnType("tinyint(1)").IsRequired().HasDefaultValueSql("1").ValueGeneratedNever();
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<long?>("ParentKey").HasColumnName("ParentKey").HasColumnType("bigint unsigned").HasConversion<ulong>().HasComputedColumnSql("ifnull(`ParentId`,0)", stored: false);
            b.HasKey("Id");
            b.HasIndex(new[] { "Slug" }).HasDatabaseName("UX_Categories_Slug").IsUnique();
            b.HasIndex(new[] { "ParentKey", "Name" }).HasDatabaseName("UX_Categories_ParentName").IsUnique();
            b.HasIndex(new[] { "ParentId" }).HasDatabaseName("FK_Categories_Parent");
            b.HasOne<Category>("Parent").WithMany().HasForeignKey("ParentId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Categories_Parent");
        }
        {
            var b = model.Entity<ChatAttachment>();
            b.ToTable("ChatAttachments");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<long>("ChatMessageId").HasColumnName("ChatMessageId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<string>("ProtectedStorageKey").HasColumnName("StorageKey").HasColumnType("varchar(512)").HasMaxLength(512).IsRequired();
            b.Property<string>("OriginalFileName").HasColumnName("OriginalFileName").HasColumnType("varchar(255)").HasMaxLength(255).IsRequired();
            b.Property<string>("ContentType").HasColumnName("ContentType").HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
            b.Property<long>("SizeBytes").HasColumnName("SizeBytes").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<byte[]>("Sha256").HasColumnName("Sha256").HasColumnType("binary(32)").HasMaxLength(32).IsRequired();
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.HasKey("Id");
            b.HasIndex(new[] { "ChatMessageId" }).HasDatabaseName("IX_ChatAttachments_Message");
            b.HasOne<ChatMessage>("ChatMessage").WithMany("Attachments").HasForeignKey("ChatMessageId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_ChatAttachments_Messages");
            b.ToTable(t => t.HasCheckConstraint("CK_ChatAttachments_Size", "(`SizeBytes` between 1 and 104857600)"));
            b.ToTable(t => t.HasCheckConstraint("CK_ChatAttachments_Type", "((`ContentType` = _utf8mb4'application/pdf') or (`ContentType` like _utf8mb4'image/%') or (`ContentType` like _utf8mb4'video/%'))"));
        }
        {
            var b = model.Entity<ChatMessage>();
            b.ToTable("ChatMessages");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<long>("ConversationId").HasColumnName("ConversationId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<string>("SenderKind").HasColumnName("SenderKind").HasColumnType("varchar(10)").HasMaxLength(10).UseCollation("utf8mb4_bin").IsRequired();
            b.Property<long?>("SenderUserId").HasColumnName("SenderUserId").HasColumnType("bigint unsigned").HasConversion<ulong>();
            b.Property<string?>("Body").HasColumnName("Body").HasColumnType("text");
            b.Property<DateTime>("SentAt").HasColumnName("SentAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<DateTime?>("ReadAt").HasColumnName("ReadAt").HasColumnType("datetime(6)");
            b.HasKey("Id");
            b.HasIndex(new[] { "ConversationId", "Id" }).HasDatabaseName("IX_ChatMessages_Conversation");
            b.HasIndex(new[] { "SenderUserId" }).HasDatabaseName("IX_ChatMessages_Sender");
            b.HasOne<Conversation>("Conversation").WithMany("Messages").HasForeignKey("ConversationId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_ChatMessages_Conversations");
            b.HasOne<User>().WithMany().HasForeignKey("SenderUserId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ChatMessages_Users");
            b.ToTable(t => t.HasCheckConstraint("CK_ChatMessages_Sender", "((`SenderKind` in (_utf8mb4'Customer',_utf8mb4'Guest',_utf8mb4'Agent',_utf8mb4'System')) and ((`SenderKind` in (_utf8mb4'Customer',_utf8mb4'Agent')) = (`SenderUserId` is not null)))"));
        }
        {
            var b = model.Entity<Comment>();
            b.ToTable("Comments");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<long>("UserId").HasColumnName("UserId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long>("ProductId").HasColumnName("ProductId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long>("OrderItemId").HasColumnName("OrderItemId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<string>("Body").HasColumnName("Body").HasColumnType("varchar(2000)").HasMaxLength(2000).IsRequired();
            b.Property<ApprovalStatus>("ApprovalStatus").HasColumnName("Status").HasColumnType("varchar(20)").HasMaxLength(20).HasConversion<string>().UseCollation("utf8mb4_bin").IsRequired().HasDefaultValueSql("'Pending'").ValueGeneratedNever();
            b.Property<long?>("ModeratedByUserId").HasColumnName("ModeratedByUserId").HasColumnType("bigint unsigned").HasConversion<ulong>();
            b.Property<DateTime?>("ModeratedAt").HasColumnName("ModeratedAt").HasColumnType("datetime(6)");
            b.Property<string?>("ModerationNote").HasColumnName("ModerationNote").HasColumnType("varchar(500)").HasMaxLength(500);
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<DateTime>("UpdatedAt").HasColumnName("UpdatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.HasKey("Id");
            b.HasIndex(new[] { "UserId", "ProductId" }).HasDatabaseName("UX_Comments_UserProduct").IsUnique();
            b.HasIndex(new[] { "ProductId", "ApprovalStatus", "CreatedAt" }).HasDatabaseName("IX_Comments_ProductVisible");
            b.HasIndex(new[] { "ApprovalStatus", "CreatedAt" }).HasDatabaseName("IX_Comments_Queue");
            b.HasIndex(new[] { "OrderItemId", "UserId", "ProductId" }).HasDatabaseName("IX_Comments_OrderItem");
            b.HasIndex(new[] { "ModeratedByUserId" }).HasDatabaseName("IX_Comments_Moderator");
            b.HasOne<User>().WithMany().HasForeignKey("ModeratedByUserId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Comments_Moderator");
            b.HasOne<OrderItem>("OrderItem").WithMany().HasForeignKey("OrderItemId", "UserId", "ProductId").HasPrincipalKey("Id", "UserId", "ProductId").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Comments_OrderItems");
            b.ToTable(t => t.HasCheckConstraint("CK_Comments_Body", "(char_length(trim(`Body`)) > 0)"));
            b.ToTable(t => t.HasCheckConstraint("CK_Comments_Moderation", "((`Status` = _utf8mb4'Pending') = ((`ModeratedByUserId` is null) and (`ModeratedAt` is null)))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Comments_Status", "(`Status` in (_utf8mb4'Pending',_utf8mb4'Approved',_utf8mb4'Rejected'))"));
            b.HasOne<Product>("Product").WithMany().HasForeignKey("ProductId").OnDelete(DeleteBehavior.Restrict);
        }
        {
            var b = model.Entity<Conversation>();
            b.ToTable("Conversations");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<Guid>("PublicId").HasColumnName("PublicId").HasColumnType("char(36)").HasMaxLength(36).IsRequired();
            b.Property<long?>("CustomerUserId").HasColumnName("CustomerUserId").HasColumnType("bigint unsigned").HasConversion<ulong>();
            b.Property<string?>("GuestTokenHash").HasColumnName("GuestTokenHash").HasColumnType("char(64)").HasMaxLength(64);
            b.Property<ConversationStatus>("Status").HasColumnName("Status").HasColumnType("varchar(20)").HasMaxLength(20).HasConversion<string>().UseCollation("utf8mb4_bin").IsRequired().HasDefaultValueSql("'Waiting'");
            b.Property<long?>("ClaimedByUserId").HasColumnName("ClaimedByUserId").HasColumnType("bigint unsigned").HasConversion<ulong>();
            b.Property<DateTime?>("ClaimedAt").HasColumnName("ClaimedAt").HasColumnType("datetime(6)");
            b.Property<DateTime?>("ClosedAt").HasColumnName("ClosedAt").HasColumnType("datetime(6)");
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<DateTime>("RowVersion").HasColumnName("RowVersion").HasColumnType("datetime(6)").IsRequired().IsRowVersion();
            b.HasKey("Id");
            b.HasIndex(new[] { "PublicId" }).HasDatabaseName("UX_Conversations_PublicId").IsUnique();
            b.HasIndex(new[] { "Status", "CreatedAt" }).HasDatabaseName("IX_Conversations_Queue");
            b.HasIndex(new[] { "CustomerUserId", "CreatedAt" }).HasDatabaseName("IX_Conversations_Customer");
            b.HasIndex(new[] { "GuestTokenHash" }).HasDatabaseName("IX_Conversations_Guest");
            b.HasIndex(new[] { "ClaimedByUserId", "Status" }).HasDatabaseName("IX_Conversations_Agent");
            b.HasOne<User>().WithMany().HasForeignKey("ClaimedByUserId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Conversations_Agent");
            b.HasOne<User>().WithMany().HasForeignKey("CustomerUserId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Conversations_Customer");
            b.ToTable(t => t.HasCheckConstraint("CK_Conversations_Claim", "(((`Status` = _utf8mb4'Waiting') and (`ClaimedByUserId` is null) and (`ClaimedAt` is null)) or ((`Status` = _utf8mb4'Active') and (`ClaimedByUserId` is not null) and (`ClaimedAt` is not null)) or ((`Status` = _utf8mb4'Closed') and (`ClosedAt` is not null)))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Conversations_Owner", "(((`CustomerUserId` is not null) and (`GuestTokenHash` is null)) or ((`CustomerUserId` is null) and (`GuestTokenHash` is not null)))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Conversations_Status", "(`Status` in (_utf8mb4'Waiting',_utf8mb4'Active',_utf8mb4'Closed'))"));
        }
        {
            var b = model.Entity<Delivery>();
            b.ToTable("Deliveries");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<long>("OrderId").HasColumnName("OrderId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long>("UserId").HasColumnName("UserId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<OrderStatus>("Status").HasColumnName("Status").HasColumnType("varchar(20)").HasMaxLength(20).HasConversion<string>().UseCollation("utf8mb4_bin").IsRequired().HasDefaultValueSql("'Processing'").ValueGeneratedNever();
            b.Property<string>("AddressSnapshot").HasColumnName("ShippingAddressEnc").HasColumnType("varbinary(2048)").HasMaxLength(2048).HasConversion(new ValueConverter<string, byte[]>(v => crypto.Encrypt(v), v => crypto.Decrypt(v))).IsRequired();
            b.Property<int>("DataKeyVersion").HasColumnName("DataKeyVersion").HasColumnType("smallint unsigned").HasConversion<ushort>().IsRequired();
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<DateTime?>("DeliveredAt").HasColumnName("CompletedAt").HasColumnType("datetime(6)");
            b.Property<DateTime>("RowVersion").HasColumnName("RowVersion").HasColumnType("datetime(6)").IsRequired().IsRowVersion();
            b.Property<bool?>("IsCompleted").HasColumnName("IsCompleted").HasColumnType("tinyint(1)").HasComputedColumnSql("(`Status` = _utf8mb4'Delivered')", stored: false);
            b.HasKey("Id");
            b.HasAlternateKey(new[] { "Id", "OrderId" }).HasName("UX_Deliveries_IdOrder");
            b.HasIndex(new[] { "Status", "Id" }).HasDatabaseName("IX_Deliveries_Queue");
            b.HasIndex(new[] { "OrderId", "UserId" }).HasDatabaseName("IX_Deliveries_Order");
            b.HasOne<Order>("Order").WithMany("Deliveries").HasForeignKey("OrderId", "UserId").HasPrincipalKey("Id", "UserId").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Deliveries_Orders");
            b.ToTable(t => t.HasCheckConstraint("CK_Deliveries_Completed", "((`Status` = _utf8mb4'Delivered') = (`CompletedAt` is not null))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Deliveries_KeyVersion", "(`DataKeyVersion` > 0)"));
            b.ToTable(t => t.HasCheckConstraint("CK_Deliveries_Status", "(`Status` in (_utf8mb4'Processing',_utf8mb4'InTransit',_utf8mb4'Delivered',_utf8mb4'Cancelled'))"));
        }
        {
            var b = model.Entity<DeliveryItem>();
            b.ToTable("DeliveryItems");
            b.Property<long>("DeliveryId").HasColumnName("DeliveryId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long>("OrderItemId").HasColumnName("OrderItemId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long>("OrderId").HasColumnName("OrderId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<int>("Quantity").HasColumnName("Quantity").HasColumnType("int unsigned").HasConversion<uint>().IsRequired();
            b.Property<decimal>("TotalPriceSnapshot").HasColumnName("TotalPriceSnapshot").HasColumnType("decimal(18,2)").IsRequired();
            b.HasKey("DeliveryId", "OrderItemId");
            b.HasIndex(new[] { "OrderItemId", "OrderId" }).HasDatabaseName("IX_DeliveryItems_OrderItem");
            b.HasIndex(new[] { "DeliveryId", "OrderId" }).HasDatabaseName("IX_DeliveryItems_Delivery");
            b.HasOne<Delivery>("Delivery").WithMany("Items").HasForeignKey("DeliveryId", "OrderId").HasPrincipalKey("Id", "OrderId").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_DeliveryItems_Deliveries");
            b.HasOne<OrderItem>("OrderItem").WithMany().HasForeignKey("OrderItemId", "OrderId").HasPrincipalKey("Id", "OrderId").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_DeliveryItems_OrderItems");
            b.ToTable(t => t.HasCheckConstraint("CK_DeliveryItems_Price", "(`TotalPriceSnapshot` >= 0)"));
            b.ToTable(t => t.HasCheckConstraint("CK_DeliveryItems_Quantity", "(`Quantity` > 0)"));
        }
        {
            var b = model.Entity<DiscountProduct>();
            b.ToTable("DiscountProducts");
            b.Property<long>("DiscountId").HasColumnName("DiscountId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long>("ProductId").HasColumnName("ProductId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.HasKey("DiscountId", "ProductId");
            b.HasIndex(new[] { "ProductId" }).HasDatabaseName("IX_DiscountProducts_Product");
            b.HasOne<Discount>("Discount").WithMany("Products").HasForeignKey("DiscountId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_DiscountProducts_Discounts");
            b.HasOne<Product>("Product").WithMany().HasForeignKey("ProductId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_DiscountProducts_Products");
        }
        {
            var b = model.Entity<Discount>();
            b.ToTable("Discounts");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<string>("Name").HasColumnName("Name").HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
            b.Property<decimal>("Rate").HasColumnName("Rate").HasColumnType("decimal(5,4)").IsRequired();
            b.Property<DateTime>("StartsAt").HasColumnName("StartsAt").HasColumnType("datetime(6)").IsRequired();
            b.Property<DateTime>("EndsAt").HasColumnName("EndsAt").HasColumnType("datetime(6)").IsRequired();
            b.Property<long>("CreatedByUserId").HasColumnName("CreatedByUserId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<DateTime?>("CancelledAt").HasColumnName("CancelledAt").HasColumnType("datetime(6)");
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.HasKey("Id");
            b.HasIndex(new[] { "StartsAt", "EndsAt" }).HasDatabaseName("IX_Discounts_Active");
            b.HasIndex(new[] { "CreatedByUserId" }).HasDatabaseName("IX_Discounts_CreatedBy");
            b.HasOne<User>().WithMany().HasForeignKey("CreatedByUserId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Discounts_Users");
            b.ToTable(t => t.HasCheckConstraint("CK_Discounts_Period", "(`EndsAt` > `StartsAt`)"));
            b.ToTable(t => t.HasCheckConstraint("CK_Discounts_Rate", "((`Rate` > 0) and (`Rate` <= 1))"));
        }
        {
            var b = model.Entity<Distributor>();
            b.ToTable("Distributors");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<string>("Name").HasColumnName("Name").HasColumnType("varchar(150)").HasMaxLength(150).IsRequired();
            b.Property<string>("ContactInfo").HasColumnName("ContactInfo").HasColumnType("varchar(500)").HasMaxLength(500).IsRequired();
            b.Property<bool>("IsActive").HasColumnName("IsActive").HasColumnType("tinyint(1)").IsRequired().HasDefaultValueSql("1").ValueGeneratedNever();
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.HasKey("Id");
            b.HasIndex(new[] { "Name" }).HasDatabaseName("UX_Distributors_Name").IsUnique();
        }
        {
            var b = model.Entity<InvoiceEmailDelivery>();
            b.ToTable("EmailOutbox");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<string>("DedupKey").HasColumnName("DedupKey").HasColumnType("varchar(120)").HasMaxLength(120).IsRequired();
            b.Property<string>("Kind").HasColumnName("Kind").HasColumnType("varchar(20)").HasMaxLength(20).UseCollation("utf8mb4_bin").IsRequired();
            b.Property<long>("RecipientUserId").HasColumnName("RecipientUserId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<string>("RecipientSnapshot").HasColumnName("RecipientEmailEnc").HasColumnType("varbinary(512)").HasMaxLength(512).HasConversion(new ValueConverter<string, byte[]>(v => crypto.Encrypt(v), v => crypto.Decrypt(v))).IsRequired();
            b.Property<int>("DataKeyVersion").HasColumnName("DataKeyVersion").HasColumnType("smallint unsigned").HasConversion<ushort>().IsRequired();
            b.Property<long?>("InvoiceId").HasColumnName("InvoiceId").HasColumnType("bigint unsigned").HasConversion<ulong>();
            b.Property<long?>("RefundId").HasColumnName("RefundId").HasColumnType("bigint unsigned").HasConversion<ulong>();
            b.Property<string>("Status").HasColumnName("Status").HasColumnType("varchar(20)").HasMaxLength(20).UseCollation("utf8mb4_bin").IsRequired().HasDefaultValueSql("'Pending'");
            b.Property<int>("AttemptCount").HasColumnName("AttemptCount").HasColumnType("smallint unsigned").HasConversion<ushort>().IsRequired().HasDefaultValueSql("0");
            b.Property<DateTime>("NextAttemptAt").HasColumnName("NextAttemptAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<string?>("LeaseToken").HasColumnName("LeaseOwner").HasColumnType("varchar(64)").HasMaxLength(64);
            b.Property<DateTime?>("LeaseUntil").HasColumnName("LeaseExpiresAt").HasColumnType("datetime(6)");
            b.Property<string?>("LastErrorCode").HasColumnName("LastError").HasColumnType("varchar(500)").HasMaxLength(500);
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<DateTime?>("SentAt").HasColumnName("SentAt").HasColumnType("datetime(6)");
            b.HasKey("Id");
            b.HasIndex(new[] { "DedupKey" }).HasDatabaseName("UX_EmailOutbox_Dedup").IsUnique();
            b.HasIndex(new[] { "Status", "NextAttemptAt" }).HasDatabaseName("IX_EmailOutbox_Dequeue");
            b.HasIndex(new[] { "RecipientUserId" }).HasDatabaseName("IX_EmailOutbox_Recipient");
            b.HasIndex(new[] { "InvoiceId" }).HasDatabaseName("IX_EmailOutbox_Invoice");
            b.HasIndex(new[] { "RefundId" }).HasDatabaseName("IX_EmailOutbox_Refund");
            b.HasOne<Invoice>("Invoice").WithMany("EmailDeliveries").HasForeignKey("InvoiceId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_EmailOutbox_Invoices");
            b.HasOne<Refund>().WithMany().HasForeignKey("RefundId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_EmailOutbox_Refunds");
            b.HasOne<User>().WithMany().HasForeignKey("RecipientUserId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_EmailOutbox_Users");
            b.ToTable(t => t.HasCheckConstraint("CK_EmailOutbox_KeyVersion", "(`DataKeyVersion` > 0)"));
            b.ToTable(t => t.HasCheckConstraint("CK_EmailOutbox_Kind", "(((`Kind` = _utf8mb4'InvoiceEmail') and (`InvoiceId` is not null) and (`RefundId` is null)) or ((`Kind` = _utf8mb4'RefundNotice') and (`RefundId` is not null) and (`InvoiceId` is null)))"));
            b.ToTable(t => t.HasCheckConstraint("CK_EmailOutbox_Sent", "((`Status` = _utf8mb4'Sent') = (`SentAt` is not null))"));
            b.ToTable(t => t.HasCheckConstraint("CK_EmailOutbox_Status", "(`Status` in (_utf8mb4'Pending',_utf8mb4'Sending',_utf8mb4'Sent',_utf8mb4'Failed'))"));
        }
        {
            var b = model.Entity<Invoice>();
            b.ToTable("Invoices");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<long>("OrderId").HasColumnName("OrderId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<string>("InvoiceNumber").HasColumnName("InvoiceNumber").HasColumnType("varchar(24)").HasMaxLength(24).IsRequired();
            b.Property<string>("CustomerNameSnapshot").HasColumnName("CustomerName").HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
            b.Property<string>("TaxIdSnapshot").HasColumnName("CustomerTaxIdEnc").HasColumnType("varbinary(256)").HasMaxLength(256).HasConversion(new ValueConverter<string, byte[]>(v => crypto.Encrypt(v), v => crypto.Decrypt(v))).IsRequired();
            b.Property<string>("AddressSnapshot").HasColumnName("BillingAddressEnc").HasColumnType("varbinary(2048)").HasMaxLength(2048).HasConversion(new ValueConverter<string, byte[]>(v => crypto.Encrypt(v), v => crypto.Decrypt(v))).IsRequired();
            b.Property<int>("DataKeyVersion").HasColumnName("DataKeyVersion").HasColumnType("smallint unsigned").HasConversion<ushort>().IsRequired();
            b.Property<string>("CurrencyCode").HasColumnName("CurrencyCode").HasColumnType("char(3)").HasMaxLength(3).UseCollation("utf8mb4_bin").IsRequired().HasDefaultValueSql("'TRY'");
            b.Property<decimal>("Subtotal").HasColumnName("Subtotal").HasColumnType("decimal(18,2)").IsRequired();
            b.Property<decimal>("DiscountTotal").HasColumnName("DiscountTotal").HasColumnType("decimal(18,2)").IsRequired();
            b.Property<decimal>("ShippingTotal").HasColumnName("ShippingTotal").HasColumnType("decimal(18,2)").IsRequired();
            b.Property<decimal>("GrandTotal").HasColumnName("GrandTotal").HasColumnType("decimal(18,2)").IsRequired();
            b.Property<string?>("ProtectedDocumentKey").HasColumnName("PdfStorageKey").HasColumnType("varchar(255)").HasMaxLength(255).IsRequired();
            b.Property<byte[]>("PdfSha256").HasColumnName("PdfSha256").HasColumnType("binary(32)").HasMaxLength(32).IsRequired();
            b.Property<DateTime>("IssuedAt").HasColumnName("IssuedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.HasKey("Id");
            b.HasIndex(new[] { "OrderId" }).HasDatabaseName("UX_Invoices_Order").IsUnique();
            b.HasIndex(new[] { "InvoiceNumber" }).HasDatabaseName("UX_Invoices_Number").IsUnique();
            b.HasIndex(new[] { "IssuedAt" }).HasDatabaseName("IX_Invoices_IssuedAt");
            b.HasOne<Order>("Order").WithOne("Invoice").HasForeignKey<Invoice>("OrderId").HasPrincipalKey<Order>("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Invoices_Orders");
            b.ToTable(t => t.HasCheckConstraint("CK_Invoices_Currency", "(`CurrencyCode` = _utf8mb4'TRY')"));
            b.ToTable(t => t.HasCheckConstraint("CK_Invoices_KeyVersion", "(`DataKeyVersion` > 0)"));
            b.ToTable(t => t.HasCheckConstraint("CK_Invoices_Money", "((`Subtotal` >= 0) and (`DiscountTotal` >= 0) and (`ShippingTotal` >= 0) and (`GrandTotal` = ((`Subtotal` - `DiscountTotal`) + `ShippingTotal`)))"));
        }
        {
            var b = model.Entity<Notification>();
            b.ToTable("Notifications");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<long>("UserId").HasColumnName("UserId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<NotificationKind>("Kind").HasColumnName("Kind").HasColumnType("varchar(20)").HasMaxLength(20).HasConversion<string>().UseCollation("utf8mb4_bin").IsRequired();
            b.Property<long?>("ProductId").HasColumnName("ProductId").HasColumnType("bigint unsigned").HasConversion<ulong>();
            b.Property<long?>("DiscountId").HasColumnName("DiscountId").HasColumnType("bigint unsigned").HasConversion<ulong>();
            b.Property<string>("Title").HasColumnName("Title").HasColumnType("varchar(200)").HasMaxLength(200).IsRequired();
            b.Property<string>("Body").HasColumnName("Body").HasColumnType("varchar(1000)").HasMaxLength(1000).IsRequired();
            b.Property<string>("DeduplicationKey").HasColumnName("DedupKey").HasColumnType("varchar(160)").HasMaxLength(160).IsRequired();
            b.Property<DateTime?>("ReadAt").HasColumnName("ReadAt").HasColumnType("datetime(6)");
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.HasKey("Id");
            b.HasIndex(new[] { "DeduplicationKey" }).HasDatabaseName("UX_Notifications_Dedup").IsUnique();
            b.HasIndex(new[] { "UserId", "ReadAt", "CreatedAt" }).HasDatabaseName("IX_Notifications_Inbox");
            b.HasIndex(new[] { "ProductId" }).HasDatabaseName("IX_Notifications_Product");
            b.HasIndex(new[] { "DiscountId" }).HasDatabaseName("IX_Notifications_Discount");
            b.HasOne<Discount>().WithMany().HasForeignKey("DiscountId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Notifications_Discounts");
            b.HasOne<Product>("Product").WithMany().HasForeignKey("ProductId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Notifications_Products");
            b.HasOne<User>().WithMany().HasForeignKey("UserId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_Notifications_Users");
            b.ToTable(t => t.HasCheckConstraint("CK_Notifications_Discount", "((`Kind` <> _utf8mb4'WishlistDiscount') or ((`ProductId` is not null) and (`DiscountId` is not null)))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Notifications_Kind", "(`Kind` in (_utf8mb4'WishlistDiscount',_utf8mb4'RefundApproved',_utf8mb4'ReturnRejected'))"));
        }
        {
            var b = model.Entity<OrderItem>();
            b.ToTable("OrderItems");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<long>("OrderId").HasColumnName("OrderId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long>("UserId").HasColumnName("UserId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long>("ProductId").HasColumnName("ProductId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<string>("ProductName").HasColumnName("ProductName").HasColumnType("varchar(200)").HasMaxLength(200).IsRequired();
            b.Property<string>("Sku").HasColumnName("Sku").HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
            b.Property<int>("Quantity").HasColumnName("Quantity").HasColumnType("int unsigned").HasConversion<uint>().IsRequired();
            b.Property<decimal>("ListUnitPrice").HasColumnName("ListUnitPrice").HasColumnType("decimal(18,2)").IsRequired();
            b.Property<decimal>("UnitPrice").HasColumnName("PaidUnitPrice").HasColumnType("decimal(18,2)").IsRequired();
            b.Property<decimal?>("UnitCost").HasColumnName("UnitCost").HasColumnType("decimal(18,2)").IsRequired();
            b.Property<long?>("DiscountId").HasColumnName("DiscountId").HasColumnType("bigint unsigned").HasConversion<ulong>();
            b.Property<decimal?>("DiscountRate").HasColumnName("DiscountRate").HasColumnType("decimal(5,4)");
            b.Property<decimal>("LineTotal").HasColumnName("LineTotal").HasColumnType("decimal(18,2)").IsRequired();
            b.Property<int>("DeliveryAllocatedQuantity").HasColumnName("DeliveryAllocatedQuantity").HasColumnType("int unsigned").HasConversion<uint>().IsRequired().HasDefaultValueSql("0");
            b.Property<int>("DeliveredQuantity").HasColumnName("DeliveredQuantity").HasColumnType("int unsigned").HasConversion<uint>().IsRequired().HasDefaultValueSql("0");
            b.Property<int>("ReturnRequestedQuantity").HasColumnName("ReturnRequestedQuantity").HasColumnType("int unsigned").HasConversion<uint>().IsRequired().HasDefaultValueSql("0");
            b.Property<int>("RefundedQuantity").HasColumnName("RefundedQuantity").HasColumnType("int unsigned").HasConversion<uint>().IsRequired().HasDefaultValueSql("0");
            b.Property<DateTime>("RowVersion").HasColumnName("RowVersion").HasColumnType("datetime(6)").IsRequired().IsRowVersion();
            b.HasKey("Id");
            b.HasIndex(new[] { "OrderId", "ProductId" }).HasDatabaseName("UX_OrderItems_OrderProduct").IsUnique();
            b.HasAlternateKey(new[] { "Id", "OrderId" }).HasName("UX_OrderItems_IdOrder");
            b.HasAlternateKey(new[] { "Id", "UserId", "ProductId" }).HasName("UX_OrderItems_IdUserProduct");
            b.HasIndex(new[] { "ProductId", "OrderId" }).HasDatabaseName("IX_OrderItems_ProductSales");
            b.HasIndex(new[] { "DiscountId" }).HasDatabaseName("IX_OrderItems_Discount");
            b.HasIndex(new[] { "OrderId", "UserId" }).HasDatabaseName("FK_OrderItems_Orders");
            b.HasOne<Discount>("Discount").WithMany("OrderItems").HasForeignKey("DiscountId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_OrderItems_Discounts");
            b.HasOne<Order>("Order").WithMany("OrderItems").HasForeignKey("OrderId", "UserId").HasPrincipalKey("Id", "UserId").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_OrderItems_Orders");
            b.HasOne<Product>("Product").WithMany("OrderItems").HasForeignKey("ProductId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_OrderItems_Products");
            b.ToTable(t => t.HasCheckConstraint("CK_OrderItems_Counters", "((`DeliveryAllocatedQuantity` <= `Quantity`) and (`DeliveredQuantity` <= `DeliveryAllocatedQuantity`) and (`ReturnRequestedQuantity` <= `DeliveredQuantity`) and (`RefundedQuantity` <= `ReturnRequestedQuantity`))"));
            b.ToTable(t => t.HasCheckConstraint("CK_OrderItems_Discount", "(((`DiscountId` is null) and (`DiscountRate` is null) and (`PaidUnitPrice` = `ListUnitPrice`)) or ((`DiscountId` is not null) and (`DiscountRate` is not null) and (`DiscountRate` > 0) and (`DiscountRate` <= 1) and (`PaidUnitPrice` = round((`ListUnitPrice` * (1 - `DiscountRate`)),2))))"));
            b.ToTable(t => t.HasCheckConstraint("CK_OrderItems_LineTotal", "(`LineTotal` = (`PaidUnitPrice` * `Quantity`))"));
            b.ToTable(t => t.HasCheckConstraint("CK_OrderItems_Money", "((`ListUnitPrice` >= 0) and (`PaidUnitPrice` >= 0) and (`UnitCost` >= 0) and (`PaidUnitPrice` <= `ListUnitPrice`))"));
            b.ToTable(t => t.HasCheckConstraint("CK_OrderItems_Quantity", "(`Quantity` > 0)"));
        }
        {
            var b = model.Entity<Order>();
            b.ToTable("Orders");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<string>("OrderNumber").HasColumnName("OrderNumber").HasColumnType("varchar(24)").HasMaxLength(24).IsRequired();
            b.Property<long>("UserId").HasColumnName("UserId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long?>("SourceCartId").HasColumnName("SourceCartId").HasColumnType("bigint unsigned").HasConversion<ulong>();
            b.Property<OrderStatus>("Status").HasColumnName("Status").HasColumnType("varchar(20)").HasMaxLength(20).HasConversion<string>().UseCollation("utf8mb4_bin").IsRequired().HasDefaultValueSql("'Processing'").ValueGeneratedNever();
            b.Property<PaymentStatus>("PaymentStatus").HasColumnName("PaymentStatus").HasColumnType("varchar(20)").HasMaxLength(20).HasConversion<string>().UseCollation("utf8mb4_bin").IsRequired().HasDefaultValueSql("'Pending'").ValueGeneratedNever();
            b.Property<string>("CurrencyCode").HasColumnName("CurrencyCode").HasColumnType("char(3)").HasMaxLength(3).UseCollation("utf8mb4_bin").IsRequired().HasDefaultValueSql("'TRY'");
            b.Property<string?>("CustomerNameSnapshot").HasColumnName("CustomerName").HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
            b.Property<string>("EmailSnapshot").HasColumnName("CustomerEmailEnc").HasColumnType("varbinary(512)").HasMaxLength(512).HasConversion(new ValueConverter<string, byte[]>(v => crypto.Encrypt(v), v => crypto.Decrypt(v))).IsRequired();
            b.Property<string>("TaxIdSnapshot").HasColumnName("CustomerTaxIdEnc").HasColumnType("varbinary(256)").HasMaxLength(256).HasConversion(new ValueConverter<string, byte[]>(v => crypto.Encrypt(v), v => crypto.Decrypt(v))).IsRequired();
            b.Property<string>("AddressSnapshot").HasColumnName("ShippingAddressEnc").HasColumnType("varbinary(2048)").HasMaxLength(2048).HasConversion(new ValueConverter<string, byte[]>(v => crypto.Encrypt(v), v => crypto.Decrypt(v))).IsRequired();
            b.Property<int>("DataKeyVersion").HasColumnName("DataKeyVersion").HasColumnType("smallint unsigned").HasConversion<ushort>().IsRequired();
            b.Property<decimal>("Subtotal").HasColumnName("Subtotal").HasColumnType("decimal(18,2)").IsRequired();
            b.Property<decimal>("DiscountTotal").HasColumnName("DiscountTotal").HasColumnType("decimal(18,2)").IsRequired().HasDefaultValueSql("0.00");
            b.Property<decimal>("ShippingTotal").HasColumnName("ShippingTotal").HasColumnType("decimal(18,2)").IsRequired().HasDefaultValueSql("0.00");
            b.Property<decimal>("GrandTotal").HasColumnName("GrandTotal").HasColumnType("decimal(18,2)").IsRequired();
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<DateTime?>("PurchasedAt").HasColumnName("PurchasedAt").HasColumnType("datetime(6)");
            b.Property<DateTime?>("DeliveredAt").HasColumnName("DeliveredAt").HasColumnType("datetime(6)");
            b.Property<DateTime?>("CancelledAt").HasColumnName("CancelledAt").HasColumnType("datetime(6)");
            b.Property<string?>("CancelReason").HasColumnName("CancelReason").HasColumnType("varchar(500)").HasMaxLength(500);
            b.Property<DateTime>("RowVersion").HasColumnName("RowVersion").HasColumnType("datetime(6)").IsRequired().IsRowVersion();
            b.HasKey("Id");
            b.HasIndex(new[] { "OrderNumber" }).HasDatabaseName("UX_Orders_Number").IsUnique();
            b.HasAlternateKey(new[] { "Id", "UserId" }).HasName("UX_Orders_IdUser");
            b.HasIndex(new[] { "SourceCartId" }).HasDatabaseName("UX_Orders_SourceCart").IsUnique();
            b.HasIndex(new[] { "UserId", "CreatedAt" }).HasDatabaseName("IX_Orders_UserHistory");
            b.HasIndex(new[] { "PurchasedAt" }).HasDatabaseName("IX_Orders_PurchasedAt");
            b.HasOne<Cart>().WithMany().HasForeignKey("SourceCartId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Orders_Carts");
            b.HasOne<User>("User").WithMany("Orders").HasForeignKey("UserId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Orders_Users");
            b.ToTable(t => t.HasCheckConstraint("CK_Orders_CancelledAt", "((`Status` = _utf8mb4'Cancelled') = (`CancelledAt` is not null))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Orders_Currency", "(`CurrencyCode` = _utf8mb4'TRY')"));
            b.ToTable(t => t.HasCheckConstraint("CK_Orders_DeliveredAt", "((`Status` = _utf8mb4'Delivered') = (`DeliveredAt` is not null))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Orders_FulfillmentPayment", "(((`Status` <> _utf8mb4'InTransit') or (`PaymentStatus` = _utf8mb4'Confirmed')) and ((`Status` <> _utf8mb4'Delivered') or (`PaymentStatus` in (_utf8mb4'Confirmed',_utf8mb4'Refunded'))))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Orders_GrandTotal", "(`GrandTotal` = ((`Subtotal` - `DiscountTotal`) + `ShippingTotal`))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Orders_KeyVersion", "(`DataKeyVersion` > 0)"));
            b.ToTable(t => t.HasCheckConstraint("CK_Orders_Money", "((`Subtotal` >= 0) and (`DiscountTotal` >= 0) and (`ShippingTotal` >= 0) and (`DiscountTotal` <= `Subtotal`))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Orders_PaymentStatus", "(`PaymentStatus` in (_utf8mb4'Pending',_utf8mb4'Confirmed',_utf8mb4'Failed',_utf8mb4'Refunded'))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Orders_PurchasedWhenPaid", "((`PaymentStatus` not in (_utf8mb4'Confirmed',_utf8mb4'Refunded')) or (`PurchasedAt` is not null))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Orders_Status", "(`Status` in (_utf8mb4'Processing',_utf8mb4'InTransit',_utf8mb4'Delivered',_utf8mb4'Cancelled'))"));
        }
        {
            var b = model.Entity<Payment>();
            b.ToTable("Payments");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<long>("UserId").HasColumnName("UserId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long>("CartId").HasColumnName("CartId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long?>("OrderId").HasColumnName("OrderId").HasColumnType("bigint unsigned").HasConversion<ulong>();
            b.Property<string>("IdempotencyKey").HasColumnName("IdempotencyKeyHash").HasColumnType("binary(32)").HasMaxLength(32).HasConversion(new ValueConverter<string, byte[]>(v => Convert.FromHexString(v), v => Convert.ToHexString(v))).IsRequired();
            b.Property<string?>("ProviderReference").HasColumnName("ProviderReference").HasColumnType("varchar(64)").HasMaxLength(64);
            b.Property<PaymentStatus>("Status").HasColumnName("Status").HasColumnType("varchar(20)").HasMaxLength(20).HasConversion<string>().UseCollation("utf8mb4_bin").IsRequired().HasDefaultValueSql("'Pending'").ValueGeneratedNever();
            b.Property<decimal>("Amount").HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
            b.Property<string>("CurrencyCode").HasColumnName("CurrencyCode").HasColumnType("char(3)").HasMaxLength(3).UseCollation("utf8mb4_bin").IsRequired().HasDefaultValueSql("'TRY'");
            b.Property<string>("CardBrand").HasColumnName("CardBrand").HasColumnType("varchar(20)").HasMaxLength(20).UseCollation("utf8mb4_bin").IsRequired();
            b.Property<string>("CardLast4").HasColumnName("CardLast4").HasColumnType("char(4)").HasMaxLength(4).IsRequired();
            b.Property<string?>("FailureCode").HasColumnName("FailureCode").HasColumnType("varchar(50)").HasMaxLength(50);
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<DateTime?>("ConfirmedAt").HasColumnName("ConfirmedAt").HasColumnType("datetime(6)");
            b.HasKey("Id");
            b.HasIndex(new[] { "UserId", "IdempotencyKey" }).HasDatabaseName("UX_Payments_UserKey").IsUnique();
            b.HasIndex(new[] { "ProviderReference" }).HasDatabaseName("UX_Payments_Provider").IsUnique();
            b.HasIndex(new[] { "Id", "OrderId" }).HasDatabaseName("UX_Payments_IdOrder").IsUnique();
            b.HasIndex(new[] { "OrderId" }).HasDatabaseName("IX_Payments_Order");
            b.HasIndex(new[] { "CartId", "UserId" }).HasDatabaseName("IX_Payments_Cart");
            b.HasIndex(new[] { "OrderId", "UserId" }).HasDatabaseName("FK_Payments_Orders");
            // Nullable composite cart ownership is enforced by the SQL migration, not an EF alternate key.
            b.HasOne<Order>("Order").WithMany("Payments").HasForeignKey("OrderId", "UserId").HasPrincipalKey("Id", "UserId").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Payments_Orders");
            b.ToTable(t => t.HasCheckConstraint("CK_Payments_Amount", "(`Amount` >= 0)"));
            b.ToTable(t => t.HasCheckConstraint("CK_Payments_Card", "((`CardBrand` in (_utf8mb4'Visa',_utf8mb4'Mastercard',_utf8mb4'Amex',_utf8mb4'Troy',_utf8mb4'Other')) and regexp_like(`CardLast4`,_utf8mb4'^[0-9]{4}$'))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Payments_Confirmed", "((`Status` = _utf8mb4'Confirmed') = ((`OrderId` is not null) and (`ConfirmedAt` is not null)))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Payments_Currency", "(`CurrencyCode` = _utf8mb4'TRY')"));
            b.ToTable(t => t.HasCheckConstraint("CK_Payments_Status", "(`Status` in (_utf8mb4'Pending',_utf8mb4'Confirmed',_utf8mb4'Failed'))"));
        }
        {
            var b = model.Entity<ProductAttributeValue>();
            b.ToTable("ProductAttributeValues");
            b.Property<long>("ProductId").HasColumnName("ProductId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long>("AttributeDefinitionId").HasColumnName("AttributeDefinitionId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<string>("DataType").HasColumnName("DataType").HasColumnType("varchar(10)").HasMaxLength(10).UseCollation("utf8mb4_bin").IsRequired();
            b.Property<string?>("ValueText").HasColumnName("ValueText").HasColumnType("varchar(255)").HasMaxLength(255);
            b.Property<decimal?>("ValueNumber").HasColumnName("ValueNumber").HasColumnType("decimal(18,4)");
            b.Property<bool?>("ValueBool").HasColumnName("ValueBool").HasColumnType("tinyint(1)");
            b.HasKey("ProductId", "AttributeDefinitionId");
            b.HasIndex(new[] { "AttributeDefinitionId", "ValueNumber" }).HasDatabaseName("IX_ProductAttributeValues_Number");
            b.HasIndex(new[] { "AttributeDefinitionId", "ValueText" }).HasDatabaseName("IX_ProductAttributeValues_Text");
            b.HasIndex(new[] { "AttributeDefinitionId", "DataType" }).HasDatabaseName("FK_ProductAttributeValues_Definitions");
            b.HasOne<AttributeDefinition>("Definition").WithMany().HasForeignKey("AttributeDefinitionId", "DataType").HasPrincipalKey("Id", "DataType").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ProductAttributeValues_Definitions");
            b.HasOne<Product>().WithMany("Attributes").HasForeignKey("ProductId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ProductAttributeValues_Products");
            b.ToTable(t => t.HasCheckConstraint("CK_ProductAttributeValues_Typed", "(((`DataType` = _utf8mb4'Text') and (`ValueText` is not null) and (`ValueNumber` is null) and (`ValueBool` is null)) or ((`DataType` = _utf8mb4'Number') and (`ValueNumber` is not null) and (`ValueText` is null) and (`ValueBool` is null)) or ((`DataType` = _utf8mb4'Bool') and (`ValueBool` is not null) and (`ValueText` is null) and (`ValueNumber` is null)))"));
        }
        {
            var b = model.Entity<ProductImage>();
            b.ToTable("ProductImages");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<long>("ProductId").HasColumnName("ProductId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<string>("StorageKey").HasColumnName("StorageKey").HasColumnType("varchar(512)").HasMaxLength(512).IsRequired();
            b.Property<string?>("AltText").HasColumnName("AltText").HasColumnType("varchar(200)").HasMaxLength(200);
            b.Property<int>("SortOrder").HasColumnName("SortOrder").HasColumnType("smallint unsigned").HasConversion<ushort>().IsRequired().HasDefaultValueSql("0");
            b.Property<bool>("IsPrimary").HasColumnName("IsPrimary").HasColumnType("tinyint(1)").IsRequired().HasDefaultValueSql("0").ValueGeneratedNever();
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<long?>("PrimaryProductId").HasColumnName("PrimaryProductId").HasColumnType("bigint unsigned").HasConversion<ulong>().HasComputedColumnSql("if((`IsPrimary` = 1),`ProductId`,NULL)", stored: false);
            b.HasKey("Id");
            b.HasIndex(new[] { "PrimaryProductId" }).HasDatabaseName("UX_ProductImages_OnePrimary").IsUnique();
            b.HasIndex(new[] { "ProductId", "SortOrder" }).HasDatabaseName("IX_ProductImages_Product");
            b.HasOne<Product>().WithMany("Images").HasForeignKey("ProductId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_ProductImages_Products");
        }
        {
            var b = model.Entity<Product>();
            b.ToTable("Products");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<long>("CategoryId").HasColumnName("CategoryId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long?>("BrandId").HasColumnName("BrandId").HasColumnType("bigint unsigned").HasConversion<ulong>();
            b.Property<long>("DistributorId").HasColumnName("DistributorId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<string>("Sku").HasColumnName("Sku").HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
            b.Property<string>("Name").HasColumnName("Name").HasColumnType("varchar(200)").HasMaxLength(200).IsRequired();
            b.Property<string>("Model").HasColumnName("Model").HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
            b.Property<string>("SerialNumber").HasColumnName("SerialNumber").HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
            b.Property<string?>("Description").HasColumnName("Description").HasColumnType("text").IsRequired();
            b.Property<string>("WarrantyStatus").HasColumnName("WarrantyStatus").HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
            b.Property<decimal>("Price").HasColumnName("Price").HasColumnType("decimal(18,2)").IsRequired();
            b.Property<decimal?>("Cost").HasColumnName("Cost").HasColumnType("decimal(18,2)");
            b.Property<int>("StockQuantity").HasColumnName("StockQuantity").HasColumnType("int unsigned").HasConversion<uint>().IsRequired().HasDefaultValueSql("0");
            b.Property<bool>("IsActive").HasColumnName("IsActive").HasColumnType("tinyint(1)").IsRequired().HasDefaultValueSql("1").ValueGeneratedNever();
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<DateTime>("UpdatedAt").HasColumnName("UpdatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<DateTime>("RowVersion").HasColumnName("RowVersion").HasColumnType("datetime(6)").IsRequired().IsRowVersion();
            b.Property<string?>("SearchText").HasColumnName("SearchText").HasColumnType("text").HasComputedColumnSql("replace(replace(concat_ws(_utf8mb4' ',`Name`,`Model`,`Description`),_utf8mb4'Ä±',_utf8mb4'i'),_utf8mb4'Ä°',_utf8mb4'i')", stored: true);
            b.HasKey("Id");
            b.HasIndex(new[] { "Sku" }).HasDatabaseName("UX_Products_Sku").IsUnique();
            b.HasIndex(new[] { "CategoryId", "IsActive", "Name" }).HasDatabaseName("IX_Products_CategoryList");
            b.HasIndex(new[] { "IsActive", "Price" }).HasDatabaseName("IX_Products_PriceSort");
            b.HasIndex(new[] { "BrandId" }).HasDatabaseName("IX_Products_Brand");
            b.HasIndex(new[] { "DistributorId" }).HasDatabaseName("IX_Products_Distributor");
            b.HasIndex(new[] { "SearchText" }).HasDatabaseName("FT_Products_Search").IsFullText();
            b.HasOne<Brand>("Brand").WithMany().HasForeignKey("BrandId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Products_Brands");
            b.HasOne<Category>("Category").WithMany("Products").HasForeignKey("CategoryId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Products_Categories");
            b.HasOne<Distributor>("Distributor").WithMany().HasForeignKey("DistributorId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Products_Distributors");
            b.ToTable(t => t.HasCheckConstraint("CK_Products_ActiveDetails", "((`IsActive` = 0) or ((char_length(trim(`Sku`)) > 0) and (char_length(trim(`Name`)) > 0) and (char_length(trim(`Model`)) > 0) and (char_length(trim(`SerialNumber`)) > 0) and (char_length(trim(`Description`)) > 0) and (char_length(trim(`WarrantyStatus`)) > 0)))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Products_Cost", "((`Cost` is null) or (`Cost` >= 0))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Products_Price", "(`Price` >= 0)"));
        }
        {
            var b = model.Entity<Rating>();
            b.ToTable("Ratings");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<long>("UserId").HasColumnName("UserId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long>("ProductId").HasColumnName("ProductId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long>("OrderItemId").HasColumnName("OrderItemId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<byte>("Stars").HasColumnName("Stars").HasColumnType("tinyint unsigned").HasConversion<byte>().IsRequired();
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<DateTime>("UpdatedAt").HasColumnName("UpdatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.HasKey("Id");
            b.HasIndex(new[] { "UserId", "ProductId" }).HasDatabaseName("UX_Ratings_UserProduct").IsUnique();
            b.HasIndex(new[] { "ProductId" }).HasDatabaseName("IX_Ratings_Product");
            b.HasIndex(new[] { "OrderItemId", "UserId", "ProductId" }).HasDatabaseName("IX_Ratings_OrderItem");
            b.HasOne<OrderItem>("OrderItem").WithMany().HasForeignKey("OrderItemId", "UserId", "ProductId").HasPrincipalKey("Id", "UserId", "ProductId").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Ratings_OrderItems");
            b.ToTable(t => t.HasCheckConstraint("CK_Ratings_Stars", "(`Stars` between 1 and 5)"));
            b.HasOne<Product>("Product").WithMany().HasForeignKey("ProductId").OnDelete(DeleteBehavior.Restrict);
        }
        {
            var b = model.Entity<Refund>();
            b.ToTable("Refunds");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<long>("OrderId").HasColumnName("OrderId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long>("PaymentId").HasColumnName("PaymentId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long?>("ReturnRequestId").HasColumnName("ReturnRequestId").HasColumnType("bigint unsigned").HasConversion<ulong>();
            b.Property<string>("Reason").HasColumnName("Reason").HasColumnType("varchar(20)").HasMaxLength(20).UseCollation("utf8mb4_bin").IsRequired();
            b.Property<string>("IdempotencyKey").HasColumnName("IdempotencyKeyHash").HasColumnType("binary(32)").HasMaxLength(32).HasConversion(new ValueConverter<string, byte[]>(v => Convert.FromHexString(v), v => Convert.ToHexString(v))).IsRequired();
            b.Property<decimal>("Amount").HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
            b.Property<string>("CurrencyCode").HasColumnName("CurrencyCode").HasColumnType("char(3)").HasMaxLength(3).UseCollation("utf8mb4_bin").IsRequired().HasDefaultValueSql("'TRY'");
            b.Property<string>("Status").HasColumnName("Status").HasColumnType("varchar(20)").HasMaxLength(20).UseCollation("utf8mb4_bin").IsRequired().HasDefaultValueSql("'Pending'");
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<DateTime?>("RefundedAt").HasColumnName("CompletedAt").HasColumnType("datetime(6)");
            b.Property<long?>("CancelOrderId").HasColumnName("CancelOrderId").HasColumnType("bigint unsigned").HasConversion<ulong>().HasComputedColumnSql("if((`Reason` = _utf8mb4'Cancellation'),`OrderId`,NULL)", stored: false);
            b.HasKey("Id");
            b.HasIndex(new[] { "IdempotencyKey" }).HasDatabaseName("UX_Refunds_Key").IsUnique();
            b.HasIndex(new[] { "ReturnRequestId" }).HasDatabaseName("UX_Refunds_ReturnRequest").IsUnique();
            b.HasIndex(new[] { "CancelOrderId" }).HasDatabaseName("UX_Refunds_OneCancellation").IsUnique();
            b.HasIndex(new[] { "OrderId" }).HasDatabaseName("IX_Refunds_Order");
            b.HasIndex(new[] { "PaymentId", "OrderId" }).HasDatabaseName("IX_Refunds_Payment");
            b.HasIndex(new[] { "RefundedAt" }).HasDatabaseName("IX_Refunds_Completed");
            b.HasOne<Order>().WithOne().HasForeignKey<Refund>("OrderId").HasPrincipalKey<Order>("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Refunds_Orders");
            b.HasOne<Payment>("Payment").WithMany().HasForeignKey("PaymentId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Refunds_Payments");
            b.HasOne<ReturnRequest>("ReturnRequest").WithOne("Refund").HasForeignKey<Refund>("ReturnRequestId").HasPrincipalKey<ReturnRequest>("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Refunds_ReturnRequests");
            b.ToTable(t => t.HasCheckConstraint("CK_Refunds_Amount", "(`Amount` > 0)"));
            b.ToTable(t => t.HasCheckConstraint("CK_Refunds_Completed", "((`Status` = _utf8mb4'Completed') = (`CompletedAt` is not null))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Refunds_Currency", "(`CurrencyCode` = _utf8mb4'TRY')"));
            b.ToTable(t => t.HasCheckConstraint("CK_Refunds_Reason", "((`Reason` in (_utf8mb4'Return',_utf8mb4'Cancellation')) and ((`Reason` = _utf8mb4'Return') = (`ReturnRequestId` is not null)))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Refunds_Status", "(`Status` in (_utf8mb4'Pending',_utf8mb4'Completed',_utf8mb4'Failed'))"));
        }
        {
            var b = model.Entity<ReturnRequest>();
            b.ToTable("ReturnRequests");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<long>("OrderItemId").HasColumnName("OrderItemId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long>("UserId").HasColumnName("UserId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long>("ProductId").HasColumnName("ProductId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<int>("Quantity").HasColumnName("Quantity").HasColumnType("int unsigned").HasConversion<uint>().IsRequired();
            b.Property<string>("Reason").HasColumnName("Reason").HasColumnType("varchar(500)").HasMaxLength(500).IsRequired();
            b.Property<ReturnStatus>("Status").HasColumnName("Status").HasColumnType("varchar(20)").HasMaxLength(20).HasConversion<string>().UseCollation("utf8mb4_bin").IsRequired().HasDefaultValueSql("'Requested'").ValueGeneratedNever();
            b.Property<DateTime>("RequestedAt").HasColumnName("RequestedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<long?>("ReviewedByUserId").HasColumnName("ReviewedByUserId").HasColumnType("bigint unsigned").HasConversion<ulong>();
            b.Property<DateTime?>("ReviewedAt").HasColumnName("ReviewedAt").HasColumnType("datetime(6)");
            b.Property<string?>("ReviewNote").HasColumnName("ReviewNote").HasColumnType("varchar(500)").HasMaxLength(500);
            b.Property<DateTime?>("ReceivedAt").HasColumnName("ReceivedAt").HasColumnType("datetime(6)");
            b.Property<DateTime>("RowVersion").HasColumnName("RowVersion").HasColumnType("datetime(6)").IsRequired().IsRowVersion();
            b.HasKey("Id");
            b.HasIndex(new[] { "OrderItemId", "UserId", "ProductId" }).HasDatabaseName("IX_ReturnRequests_Item");
            b.HasIndex(new[] { "UserId", "RequestedAt" }).HasDatabaseName("IX_ReturnRequests_UserHistory");
            b.HasIndex(new[] { "Status", "RequestedAt" }).HasDatabaseName("IX_ReturnRequests_Queue");
            b.HasIndex(new[] { "ReviewedByUserId" }).HasDatabaseName("IX_ReturnRequests_Reviewer");
            b.HasOne<OrderItem>("OrderItem").WithMany().HasForeignKey("OrderItemId", "UserId", "ProductId").HasPrincipalKey("Id", "UserId", "ProductId").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ReturnRequests_OrderItems");
            b.HasOne<User>().WithMany().HasForeignKey("ReviewedByUserId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_ReturnRequests_Reviewer");
            b.ToTable(t => t.HasCheckConstraint("CK_ReturnRequests_Quantity", "(`Quantity` > 0)"));
            b.ToTable(t => t.HasCheckConstraint("CK_ReturnRequests_Review", "(((`Status` = _utf8mb4'Requested') and (`ReviewedByUserId` is null) and (`ReviewedAt` is null) and (`ReceivedAt` is null)) or ((`Status` = _utf8mb4'Rejected') and (`ReviewedByUserId` is not null) and (`ReviewedAt` is not null) and (`ReceivedAt` is null)) or ((`Status` in (_utf8mb4'Received',_utf8mb4'Approved')) and (`ReviewedByUserId` is not null) and (`ReviewedAt` is not null) and (`ReceivedAt` is not null)))"));
            b.ToTable(t => t.HasCheckConstraint("CK_ReturnRequests_Status", "(`Status` in (_utf8mb4'Requested',_utf8mb4'Received',_utf8mb4'Approved',_utf8mb4'Rejected'))"));
        }
        {
            var b = model.Entity<User>();
            b.ToTable("Users");
            b.Property<long>("Id").HasColumnName("Id").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired().ValueGeneratedOnAdd();
            b.Property<Guid>("PublicId").HasColumnName("PublicId").HasColumnType("char(36)").HasMaxLength(36).IsRequired();
            b.Property<string>("Name").HasColumnName("Name").HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
            b.Property<string>("Email").HasColumnName("EmailEnc").HasColumnType("varbinary(512)").HasMaxLength(512).HasConversion(new ValueConverter<string, byte[]>(v => crypto.Encrypt(v), v => crypto.Decrypt(v))).IsRequired();
            b.Property<byte[]>("EmailBlindIndex").HasColumnName("EmailBlindIndex").HasColumnType("binary(32)").HasMaxLength(32).IsRequired();
            b.Property<string>("TaxId").HasColumnName("TaxIdEnc").HasColumnType("varbinary(256)").HasMaxLength(256).HasConversion(new ValueConverter<string, byte[]>(v => crypto.Encrypt(v), v => crypto.Decrypt(v)));
            b.Property<int>("DataKeyVersion").HasColumnName("DataKeyVersion").HasColumnType("smallint unsigned").HasConversion<ushort>().IsRequired();
            b.Property<string>("PasswordHash").HasColumnName("PasswordHash").HasColumnType("varchar(255)").HasMaxLength(255).IsRequired();
            b.Property<UserRole>("Role").HasColumnName("Role").HasColumnType("varchar(20)").HasMaxLength(20).HasConversion<string>().UseCollation("utf8mb4_bin").IsRequired().HasDefaultValueSql("'Customer'").ValueGeneratedNever();
            b.Property<string>("SecurityStamp").HasColumnName("SecurityStamp").HasColumnType("char(32)").HasMaxLength(32).IsRequired();
            b.Property<bool>("IsActive").HasColumnName("IsActive").HasColumnType("tinyint(1)").IsRequired().HasDefaultValueSql("1").ValueGeneratedNever();
            b.Property<int>("FailedLoginCount").HasColumnName("FailedLoginCount").HasColumnType("smallint unsigned").HasConversion<ushort>().IsRequired().HasDefaultValueSql("0");
            b.Property<DateTime?>("LockoutUntil").HasColumnName("LockoutUntil").HasColumnType("datetime(6)");
            b.Property<DateTime>("CreatedAt").HasColumnName("CreatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<DateTime>("UpdatedAt").HasColumnName("UpdatedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.Property<DateTime>("RowVersion").HasColumnName("RowVersion").HasColumnType("datetime(6)").IsRequired().IsRowVersion();
            b.HasKey("Id");
            b.HasIndex(new[] { "PublicId" }).HasDatabaseName("UX_Users_PublicId").IsUnique();
            b.HasIndex(new[] { "EmailBlindIndex" }).HasDatabaseName("UX_Users_EmailBlindIndex").IsUnique();
            b.ToTable(t => t.HasCheckConstraint("CK_Users_CustomerTaxId", "((`Role` <> _utf8mb4'Customer') or (`TaxIdEnc` is not null))"));
            b.ToTable(t => t.HasCheckConstraint("CK_Users_KeyVersion", "(`DataKeyVersion` > 0)"));
            b.ToTable(t => t.HasCheckConstraint("CK_Users_Name", "(char_length(trim(`Name`)) > 0)"));
            b.ToTable(t => t.HasCheckConstraint("CK_Users_Role", "(`Role` in (_utf8mb4'Customer',_utf8mb4'SalesManager',_utf8mb4'ProductManager',_utf8mb4'SupportAgent'))"));
        }
        {
            var b = model.Entity<WishlistItem>();
            b.ToTable("WishlistItems");
            b.Property<long>("UserId").HasColumnName("UserId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<long>("ProductId").HasColumnName("ProductId").HasColumnType("bigint unsigned").HasConversion<ulong>().IsRequired();
            b.Property<DateTime>("AddedAt").HasColumnName("AddedAt").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("utc_timestamp(6)");
            b.HasKey("UserId", "ProductId");
            b.HasIndex(new[] { "ProductId" }).HasDatabaseName("IX_WishlistItems_Product");
            b.HasOne<Product>("Product").WithMany().HasForeignKey("ProductId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WishlistItems_Products");
            b.HasOne<User>().WithMany().HasForeignKey("UserId").HasPrincipalKey("Id").OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_WishlistItems_Users");
        }
        model.Entity<User>().Navigation(u => u.Addresses).AutoInclude();
        model.Entity<Product>().Navigation(p => p.Distributor).AutoInclude();
        model.Entity<Product>().Navigation(p => p.Images).AutoInclude();
        model.Entity<Invoice>().Navigation(i => i.EmailDeliveries).AutoInclude();
        model.Entity<InvoiceEmailDelivery>().Property(x => x.LastAttemptAt).HasColumnType("datetime(6)");
    }
}
