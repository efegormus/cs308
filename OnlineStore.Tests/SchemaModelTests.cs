using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Tests;

// Checks the rules the full e-commerce schema (SCRUM-97) declares. They read the EF model only,
// so they run in CI without MySQL. SchemaMySqlTests proves that MySQL really enforces them.
public class SchemaModelTests
{
    private static readonly IModel Model = BuildModel();

    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseMySql("Server=localhost;Database=model_only;User ID=none;Password=none", new MySqlServerVersion(new Version(8, 0, 16)))
            .Options;
        using var db = new StoreDbContext(options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType Entity(string name) => Model.GetEntityTypes().Single(type => type.ClrType.Name == name);

    [Fact]
    public void Migrations_AreUpToDateWithTheModel()
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseMySql("Server=localhost;Database=model_only;User ID=none;Password=none", new MySqlServerVersion(new Version(8, 0, 16)))
            .Options;
        using var db = new StoreDbContext(options);
        var services = db.GetInfrastructure();
        var snapshot = services.GetRequiredService<IMigrationsAssembly>().ModelSnapshot!.Model;
        var snapshotModel = services.GetRequiredService<IModelRuntimeInitializer>()
            .Initialize((IModel)snapshot, designTime: true, validationLogger: null);
        var differences = services.GetRequiredService<IMigrationsModelDiffer>()
            .GetDifferences(snapshotModel.GetRelationalModel(), db.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        // A failure here means the entities changed without a new migration (dotnet ef migrations add).
        Assert.Empty(differences);
    }

    [Fact]
    public void TheSchemaHasTheExpectedTables()
    {
        var expected = new[]
        {
            "Users", "Categories", "Products", "Orders", "OrderItems", "Discounts", "DiscountProducts", "Payments",
            "Carts", "CartItems", "WishlistItems", "Invoices", "EmailOutbox", "Deliveries", "DeliveryItems", "Ratings", "Comments",
            "Addresses", "Brands", "Distributors", "AttributeDefinitions", "ProductAttributeValues", "ProductImages",
            "ReturnRequests", "Refunds", "Notifications", "Conversations", "ChatMessages", "ChatAttachments"
        };

        Assert.Equal(expected.Order(), Model.GetEntityTypes().Select(type => type.GetTableName()!).Order());
    }

    // ---- Statuses are stored as text and the database lists every allowed value ----

    [Theory]
    [InlineData(nameof(Order), nameof(Order.Status), "CK_Orders_Status", typeof(OrderStatus))]
    [InlineData(nameof(Delivery), nameof(Delivery.Status), "CK_Deliveries_Status", typeof(OrderStatus))]
    [InlineData(nameof(Cart), nameof(Cart.State), "CK_Carts_State", typeof(CartState))]
    [InlineData(nameof(Comment), nameof(Comment.ApprovalStatus), "CK_Comments_Status", typeof(ApprovalStatus))]
    [InlineData(nameof(ReturnRequest), nameof(ReturnRequest.Status), "CK_ReturnRequests_Status", typeof(ReturnStatus))]
    [InlineData(nameof(Conversation), nameof(Conversation.Status), "CK_Conversations_Status", typeof(ConversationStatus))]
    [InlineData(nameof(Notification), nameof(Notification.Kind), "CK_Notifications_Kind", typeof(NotificationKind))]
    public void StatusColumns_AreTextAndTheCheckListsEveryEnumValue(string entity, string property, string check, Type enumType)
    {
        var column = Entity(entity).FindProperty(property)!;
        Assert.Equal(typeof(string), column.GetProviderClrType());
        Assert.Equal("utf8mb4_bin", column.GetCollation());

        var constraint = Entity(entity).GetCheckConstraints().Single(candidate => candidate.Name == check);
        foreach (var value in Enum.GetNames(enumType))
        {
            Assert.Contains($"'{value}'", constraint.Sql);
        }
        Assert.Equal(Enum.GetNames(enumType).Length, constraint.Sql.Count(character => character == '\''  ) / 2);
    }

    [Fact]
    public void Payments_AcceptOnlyTheStatesOfASinglePaymentAttempt()
    {
        // Refunded and Unknown describe the whole order (Order.PaymentStatus); a refund is its own row in Refunds.
        var column = Entity(nameof(Payment)).FindProperty(nameof(Payment.Status))!;
        Assert.Equal(typeof(string), column.GetProviderClrType());
        var constraint = Entity(nameof(Payment)).GetCheckConstraints().Single(check => check.Name == "CK_Payments_Status");
        foreach (var state in new[] { "Pending", "Confirmed", "Failed" }) Assert.Contains($"'{state}'", constraint.Sql);
    }

    [Fact]
    public void OrderStatus_MatchesTheCourseStatuses()
    {
        Assert.Equal(new[] { "Processing", "InTransit", "Delivered", "Cancelled" }, Enum.GetNames<OrderStatus>());
    }

    // ---- Check constraints ----

    [Fact]
    public void TheSchemaDeclaresTheExpectedCheckConstraints()
    {
        var names = Model.GetEntityTypes().SelectMany(type => type.GetCheckConstraints()).Select(check => check.Name).ToList();

        foreach (var expected in new[]
        {
            "CK_Orders_PurchasedWhenPaid", "CK_OrderItems_Money", "CK_Payments_Confirmed", "CK_Deliveries_Completed",
            "CK_Carts_Owner", "CK_Comments_Moderation", "CK_ReturnRequests_Review", "CK_Conversations_Claim",
            "CK_ChatMessages_Sender", "CK_ChatAttachments_Size", "CK_DeliveryItems_Quantity", "CK_DeliveryItems_Price",
            "CK_OrderItems_Counters", "CK_ProductAttributeValues_Typed", "CK_Users_KeyVersion"
        })
        {
            Assert.Contains(expected, names);
        }
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    // ---- Duplicates are rejected by the database, so a retried request is safe ----

    [Theory]
    [InlineData(nameof(Order), nameof(Order.SourceCartId))]
    [InlineData(nameof(Refund), nameof(Refund.IdempotencyKey))]
    [InlineData(nameof(Refund), nameof(Refund.ReturnRequestId))]
    [InlineData(nameof(Invoice), nameof(Invoice.OrderId))]
    [InlineData(nameof(Invoice), nameof(Invoice.InvoiceNumber))]
    [InlineData(nameof(Cart), nameof(Cart.GuestTokenHash))]
    public void TheseColumnsAreUnique(string entity, string property)
    {
        Assert.Contains(Entity(entity).GetIndexes(),
            index => index.IsUnique && index.Properties.Select(column => column.Name).SequenceEqual([property]));
    }

    [Theory]
    [InlineData(nameof(Rating), nameof(Rating.UserId), nameof(Rating.ProductId))]
    [InlineData(nameof(Comment), nameof(Comment.UserId), nameof(Comment.ProductId))]
    [InlineData(nameof(WishlistItem), nameof(WishlistItem.UserId), nameof(WishlistItem.ProductId))]
    public void OneRowPerUserAndProduct(string entity, string first, string second)
    {
        var key = Entity(entity).FindPrimaryKey()!.Properties.Select(column => column.Name);
        var unique = Entity(entity).GetIndexes().Where(index => index.IsUnique).Select(index => index.Properties.Select(c => c.Name));
        Assert.True(key.SequenceEqual([first, second]) || unique.Any(columns => columns.SequenceEqual([first, second])),
            $"{entity} must allow one row per {first} and {second}");
    }

    // ---- Deleting a parent must never silently destroy paid history ----

    [Theory]
    [InlineData(nameof(Order), nameof(Order.User), DeleteBehavior.Restrict)]
    [InlineData(nameof(OrderItem), nameof(OrderItem.Product), DeleteBehavior.Restrict)]
    [InlineData(nameof(OrderItem), nameof(OrderItem.Discount), DeleteBehavior.Restrict)]
    [InlineData(nameof(Payment), nameof(Payment.Order), DeleteBehavior.Restrict)]
    [InlineData(nameof(Invoice), nameof(Invoice.Order), DeleteBehavior.Restrict)]
    [InlineData(nameof(Delivery), nameof(Delivery.Order), DeleteBehavior.Restrict)]
    [InlineData(nameof(DeliveryItem), nameof(DeliveryItem.OrderItem), DeleteBehavior.Restrict)]
    [InlineData(nameof(DeliveryItem), nameof(DeliveryItem.Delivery), DeleteBehavior.Restrict)]
    [InlineData(nameof(Rating), nameof(Rating.OrderItem), DeleteBehavior.Restrict)]
    [InlineData(nameof(Rating), nameof(Rating.Product), DeleteBehavior.Restrict)]
    [InlineData(nameof(Comment), nameof(Comment.OrderItem), DeleteBehavior.Restrict)]
    [InlineData(nameof(Comment), nameof(Comment.Product), DeleteBehavior.Restrict)]
    [InlineData(nameof(ReturnRequest), nameof(ReturnRequest.OrderItem), DeleteBehavior.Restrict)]
    [InlineData(nameof(Refund), nameof(Refund.ReturnRequest), DeleteBehavior.Restrict)]
    [InlineData(nameof(Refund), nameof(Refund.Payment), DeleteBehavior.Restrict)]
    [InlineData(nameof(CartItem), nameof(CartItem.Cart), DeleteBehavior.Cascade)]
    [InlineData(nameof(ChatMessage), nameof(ChatMessage.Conversation), DeleteBehavior.Cascade)]
    [InlineData(nameof(ChatAttachment), nameof(ChatAttachment.ChatMessage), DeleteBehavior.Cascade)]
    [InlineData(nameof(DiscountProduct), nameof(DiscountProduct.Discount), DeleteBehavior.Restrict)]
    public void DeleteBehavior_IsAsDesigned(string entity, string navigation, DeleteBehavior expected)
    {
        Assert.Equal(expected, Entity(entity).FindNavigation(navigation)!.ForeignKey.DeleteBehavior);
    }

    [Fact]
    public void NothingThatHoldsPaidHistoryCascadesFromProductsPaymentsOrReturns()
    {
        var protectedParents = new[] { nameof(Product), nameof(Payment), nameof(OrderItem), nameof(ReturnRequest), nameof(Order), nameof(User) };
        var cascades = Model.GetEntityTypes()
            .SelectMany(type => type.GetForeignKeys())
            .Where(key => key.DeleteBehavior == DeleteBehavior.Cascade && protectedParents.Contains(key.PrincipalEntityType.ClrType.Name))
            .Select(key => $"{key.DeclaringEntityType.ClrType.Name}->{key.PrincipalEntityType.ClrType.Name}")
            .ToList();

        // Only owned drafts may disappear with a parent: order lines go with their order (the order itself cannot be deleted
        // once it has payments, deliveries or an invoice), cart lines, the wishlist and the notifications.
        var allowed = new[] { "Address->User", "ProductImage->Product", "OrderItem->Order", "CartItem->Product", "DiscountProduct->Product", "WishlistItem->Product", "WishlistItem->User", "Cart->User", "Notification->User" };
        Assert.Empty(cascades.Except(allowed));
    }

    [Fact]
    public void ADeletedCartKeepsTheSupportChat()
    {
        Assert.Null(Entity(nameof(Conversation)).FindProperty(nameof(Conversation.CartId)));
        Assert.NotNull(Entity(nameof(Conversation)).FindProperty(nameof(Conversation.CustomerUserId)));
    }

    // ---- Concurrency, money, time and privacy ----

    [Theory]
    [InlineData(nameof(Order))]
    [InlineData(nameof(ReturnRequest))]
    [InlineData(nameof(Conversation))]
    public void RowVersion_IsAConcurrencyTokenMySqlUpdatesItself(string entity)
    {
        var rowVersion = Entity(entity).FindProperty("RowVersion")!;
        Assert.True(rowVersion.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
    }

    [Fact]
    public void MoneyColumnsAreExactDecimals()
    {
        var money = Model.GetEntityTypes()
            .SelectMany(type => type.GetProperties())
            .Where(column => column.ClrType == typeof(decimal) || column.ClrType == typeof(decimal?))
            .ToList();

        Assert.NotEmpty(money);
        foreach (var column in money)
        {
            var expected = column.Name is "Rate" or "DiscountRate" ? "decimal(5,4)" : column.Name == "ValueNumber" ? "decimal(18,4)" : "decimal(18,2)";
            Assert.True(column.GetColumnType() == expected, $"{column.DeclaringType.ClrType.Name}.{column.Name} is {column.GetColumnType()}");
        }
    }

    [Fact]
    public void TimestampsAreStoredWithMicrosecondsAsUtc()
    {
        var times = Model.GetEntityTypes()
            .SelectMany(type => type.GetProperties())
            .Where(column => column.ClrType == typeof(DateTime) || column.ClrType == typeof(DateTime?));

        foreach (var column in times)
        {
            Assert.True(column.GetColumnType() == "datetime(6)", $"{column.DeclaringType.ClrType.Name}.{column.Name} is {column.GetColumnType()}");
        }
    }

    [Fact]
    public void PaymentsAndRefundsStoreNoCardData()
    {
        var forbidden = new[] { "CardNumber", "pan", "cvv", "cvc", "expiry", "holder" };
        foreach (var entity in new[] { nameof(Payment), nameof(Refund) })
        {
            foreach (var column in Entity(entity).GetProperties())
            {
                Assert.DoesNotContain(forbidden, word => column.Name.Contains(word, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    // ---- Customer snapshots must fit whatever the profile allows ----

    [Theory]
    [InlineData(nameof(Order), "CustomerNameSnapshot", nameof(User.Name))]
    [InlineData(nameof(Order), "TaxIdSnapshot", nameof(User.TaxId))]
    [InlineData(nameof(Order), "EmailSnapshot", nameof(User.Email))]
    [InlineData(nameof(Order), "AddressSnapshot", nameof(User.HomeAddress))]
    [InlineData(nameof(Invoice), "CustomerNameSnapshot", nameof(User.Name))]
    [InlineData(nameof(Invoice), "TaxIdSnapshot", nameof(User.TaxId))]
    [InlineData(nameof(Invoice), "AddressSnapshot", nameof(User.HomeAddress))]
    [InlineData(nameof(Delivery), "AddressSnapshot", nameof(User.HomeAddress))]
    public void Snapshot_IsAtLeastAsLongAsTheProfileField(string entity, string snapshot, string profileField)
    {
        var snapshotLength = Entity(entity).FindProperty(snapshot)!.GetMaxLength();
        var profileLength = profileField == nameof(User.HomeAddress)
            ? Entity(nameof(Address)).FindProperty(nameof(Address.Text))!.GetMaxLength()
            : Entity(nameof(User)).FindProperty(profileField)!.GetMaxLength();

        // If the profile field is made longer later, this fails until the snapshot is widened too.
        Assert.True(snapshotLength >= profileLength, $"{entity}.{snapshot} is shorter than Users.{profileField}");
    }
}
