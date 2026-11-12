using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using OnlineStore.Accounts;
using OnlineStore.Dashboards;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Purchasing;

namespace OnlineStore.Tests;

public class DashboardApiTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTime PurchasedAt = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MySqlConnectionString_PreservesCarriageReturnFromExistingCrlfSecret()
    {
        var password = new string('a', 64) + "\r";
        var connectionString =
            $"Server=127.0.0.1;Port=33306;Database=onlinestore;User ID=onlinestore;Password=\"{password}\"";

        var parsed = new MySqlConnectionStringBuilder(connectionString);

        Assert.True(parsed.Password.EndsWith('\r'));
    }

    [Fact]
    public async Task DashboardEndpoints_Return401And403ForEveryRoleGuard()
    {
        await using var server = await DashboardTestServer.CreateAsync();
        var routes = new[]
        {
            (Path: "/api/customer/dashboard", AllowedRoles: new[] { RolePolicies.Customer }),
            (Path: "/api/sales/dashboard?from=2026-11-01T00:00:00Z&to=2026-11-11T00:00:00Z",
                AllowedRoles: new[] { RolePolicies.SalesManager }),
            (Path: "/api/product-management/dashboard", AllowedRoles: new[] { RolePolicies.ProductManager }),
            (Path: "/api/management/users", AllowedRoles: new[] { RolePolicies.SalesManager, RolePolicies.ProductManager })
        };
        var allRoles = new[] { RolePolicies.Customer, RolePolicies.SalesManager, RolePolicies.ProductManager, RolePolicies.SupportAgent };

        foreach (var route in routes)
        {
            using var anonymousResponse = await server.Client.GetAsync(route.Path);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
            Assert.Equal("no-store", anonymousResponse.Headers.CacheControl!.ToString());
            foreach (var role in allRoles)
            {
                using var response = await server.GetAsync(route.Path, role, server.CustomerId);
                var expected = route.AllowedRoles.Contains(role) ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
                Assert.Equal(expected, response.StatusCode);
                Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
            }
        }
    }

    [Fact]
    public async Task CustomerSalesAndProductDashboards_ReturnEmptyStates()
    {
        await using var server = await DashboardTestServer.CreateAsync();

        using var customerResponse = await server.GetAsync(
            "/api/customer/dashboard", RolePolicies.Customer, server.CustomerId);
        var customer = (await customerResponse.Content.ReadFromJsonAsync<CustomerDashboardResponse>())!;
        Assert.Empty(customer.RecentConfirmedOrders);
        Assert.Equal(0, customer.WishlistCount);
        Assert.Empty(customer.WishlistItems);

        using var salesResponse = await server.GetAsync(
            "/api/sales/dashboard?from=2026-11-09T00:00:00Z&to=2026-11-11T00:00:00Z",
            RolePolicies.SalesManager, server.CustomerId);
        var sales = (await salesResponse.Content.ReadFromJsonAsync<SalesDashboardResponse>())!;
        Assert.Equal(0m, sales.GrossRevenue);
        Assert.Equal(0m, sales.Refunds);
        Assert.Equal(0m, sales.NetRevenue);
        Assert.Empty(sales.RecentInvoices);
        Assert.Empty(sales.ActiveDiscounts);

        using var productResponse = await server.GetAsync(
            "/api/product-management/dashboard", RolePolicies.ProductManager, server.CustomerId);
        var product = (await productResponse.Content.ReadFromJsonAsync<ProductManagementDashboardResponse>())!;
        Assert.Empty(product.LowStockProducts);
        Assert.Empty(product.PendingDeliveries);
        Assert.Empty(product.CommentsWaitingForApproval);
    }

    [Fact]
    public async Task CustomerDashboard_UsesOnlyTheSignedInCustomersData()
    {
        await using var server = await DashboardTestServer.CreateAsync();
        long otherId;
        long ownOrderId;
        await using (var scope = server.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
            var customer = await db.Users.SingleAsync(user => user.Id == server.CustomerId);
            var other = NewUser("other@example.com", "Other Customer");
            var product = NewProduct("CUSTOMER-OWNED", 100m, 4);
            db.Users.Add(other);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            otherId = other.Id;

            var ownOrder = PaidOrder(customer, product, 2, 45m);
            var otherOrder = PaidOrder(other, product, 1, 100m);
            var unpaidOrder = UnpaidOrder(customer, product);
            db.Orders.AddRange(ownOrder, otherOrder, unpaidOrder);
            db.WishlistItems.AddRange(
                new WishlistItem { UserId = customer.Id, ProductId = product.Id, Product = product },
                new WishlistItem { UserId = other.Id, ProductId = product.Id, Product = product });
            await db.SaveChangesAsync();
            ownOrderId = ownOrder.Id;
        }

        using var response = await server.GetAsync(
            $"/api/customer/dashboard?userId={otherId}", RolePolicies.Customer, server.CustomerId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = (await response.Content.ReadFromJsonAsync<CustomerDashboardResponse>())!;
        Assert.Equal(server.CustomerId, dashboard.Profile.Id);
        Assert.Equal("Ada Customer", dashboard.Profile.Name);
        Assert.Equal(1, dashboard.WishlistCount);
        Assert.Equal(["CUSTOMER-OWNED product"], dashboard.WishlistItems.Select(item => item.Name));
        Assert.Equal([ownOrderId], dashboard.RecentConfirmedOrders.Select(order => order.OrderId));
        Assert.Equal(90m, dashboard.RecentConfirmedOrders.Single().Total);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("TAX-", body, StringComparison.Ordinal);
        Assert.DoesNotContain("PasswordHash", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CustomerDashboard_ReturnsFiveNewestOrdersInStableOrder()
    {
        await using var server = await DashboardTestServer.CreateAsync();
        long[] orderIds;
        await using (var scope = server.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
            var customer = await db.Users.SingleAsync(user => user.Id == server.CustomerId);
            var product = NewProduct("ORDER-LIMIT", 10m, 20);
            db.Products.Add(product);
            await db.SaveChangesAsync();

            var orders = Enumerable.Range(1, 7)
                .Select(index => PaidOrder(customer, product, 1, index * 10m))
                .ToArray();
            db.Orders.AddRange(orders);
            await db.SaveChangesAsync();
            orderIds = orders.Select(order => order.Id).OrderDescending().Take(5).ToArray();
        }

        using var response = await server.GetAsync(
            "/api/customer/dashboard", RolePolicies.Customer, server.CustomerId);
        var dashboard = (await response.Content.ReadFromJsonAsync<CustomerDashboardResponse>())!;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(orderIds, dashboard.RecentConfirmedOrders.Select(order => order.OrderId));
        Assert.All(dashboard.RecentConfirmedOrders, order => Assert.Equal(1, order.ItemCount));
    }

    [Fact]
    public async Task SalesDashboard_CalculatesCapturedRevenueLessCompletedRefunds()
    {
        await using var server = await DashboardTestServer.CreateAsync();
        await using (var scope = server.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
            var customer = await db.Users.SingleAsync(user => user.Id == server.CustomerId);
            var order = NewOrder(customer, 100m);
            order.ConfirmPayment(PurchasedAt);
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            var payment = new Payment
            {
                OrderId = order.Id, Order = order, UserId = customer.Id, CartId = 1,
                IdempotencyKey = new string('A', 64), Amount = 200m, Status = PaymentStatus.Confirmed,
                CreatedAt = PurchasedAt, ConfirmedAt = PurchasedAt, CardBrand = "Visa", CardLast4 = "1234"
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync();
            db.Refunds.Add(new Refund
            {
                OrderId = order.Id, PaymentId = payment.Id, Payment = payment,
                IdempotencyKey = new string('B', 64), Amount = 30m, Reason = "Cancellation",
                Status = "Completed", CreatedAt = PurchasedAt.AddDays(1), RefundedAt = PurchasedAt.AddDays(1)
            });
            db.Invoices.Add(new Invoice
            {
                OrderId = order.Id, Order = order, InvoiceNumber = "INV-2026-000001",
                CustomerNameSnapshot = "Ada Customer", TaxIdSnapshot = "PRIVATE", AddressSnapshot = "PRIVATE",
                ProtectedDocumentKey = "invoice.pdf", IssuedAt = PurchasedAt.AddDays(1), GrandTotal = 200m
            });
            db.Discounts.Add(new Discount
            {
                Name = "Current", CreatedByUserId = customer.Id, Rate = .1m,
                StartsAt = FixedNow.UtcDateTime.AddDays(-1), EndsAt = FixedNow.UtcDateTime.AddDays(1)
            });
            db.Discounts.Add(new Discount
            {
                Name = "Cancelled", CreatedByUserId = customer.Id, Rate = .2m,
                StartsAt = FixedNow.UtcDateTime.AddDays(-1), EndsAt = FixedNow.UtcDateTime.AddDays(1),
                CancelledAt = FixedNow.UtcDateTime.AddHours(-1)
            });
            await db.SaveChangesAsync();
        }

        using var response = await server.GetAsync(
            "/api/sales/dashboard?from=2026-11-09T00:00:00Z&to=2026-11-11T00:00:00Z",
            RolePolicies.SalesManager, server.CustomerId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = (await response.Content.ReadFromJsonAsync<SalesDashboardResponse>())!;
        Assert.Equal(200m, dashboard.GrossRevenue);
        Assert.Equal(30m, dashboard.Refunds);
        Assert.Equal(170m, dashboard.NetRevenue);
        Assert.Equal("TRY", dashboard.CurrencyCode);
        Assert.Equal("INV-2026-000001", dashboard.RecentInvoices.Single().InvoiceNumber);
        Assert.Equal(["Current"], dashboard.ActiveDiscounts.Select(discount => discount.Name));
    }

    [Fact]
    public async Task SalesDashboard_UsesCapturedAmountAndPartialRefundsWithoutOrderLineJoinDuplication()
    {
        await using var server = await DashboardTestServer.CreateAsync();
        var from = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(3);
        await using (var scope = server.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
            var customer = await db.Users.SingleAsync(user => user.Id == server.CustomerId);
            var firstProduct = NewProduct("HISTORY-ONE", 50m, 5);
            var secondProduct = NewProduct("HISTORY-TWO", 70m, 5);
            db.Products.AddRange(firstProduct, secondProduct);
            await db.SaveChangesAsync();

            var order = NewOrder(customer, 120m);
            order.OrderItems.Add(OrderItem.Snapshot(firstProduct, 1));
            order.OrderItems.Add(OrderItem.Snapshot(secondProduct, 1));
            Assert.True(order.ConfirmPayment(from.AddHours(1)));
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            var payment = new Payment
            {
                OrderId = order.Id, Order = order, UserId = customer.Id, CartId = 1,
                IdempotencyKey = new string('C', 64), Amount = 120m, Status = PaymentStatus.Confirmed,
                CreatedAt = from, ConfirmedAt = from.AddHours(1), CardBrand = "Visa", CardLast4 = "1234"
            };
            var boundaryOrder = NewOrder(customer, 20m);
            boundaryOrder.OrderItems.Add(OrderItem.Snapshot(firstProduct, 1, 20m));
            Assert.True(boundaryOrder.ConfirmPayment(from.AddHours(2)));
            db.Orders.Add(boundaryOrder);
            await db.SaveChangesAsync();
            var boundaryPayment = new Payment
            {
                OrderId = boundaryOrder.Id, Order = boundaryOrder, UserId = customer.Id, CartId = 1,
                IdempotencyKey = new string('D', 64), Amount = 20m, Status = PaymentStatus.Confirmed,
                CreatedAt = to.AddTicks(-1), ConfirmedAt = to.AddTicks(-1), CardBrand = "Visa", CardLast4 = "1234"
            };
            db.Payments.Add(payment);
            db.Payments.Add(boundaryPayment);
            await db.SaveChangesAsync();

            db.Refunds.AddRange(
                NewRefund(order, payment, 10m, "Completed", from),
                NewRefund(boundaryOrder, boundaryPayment, 20m, "Completed", to));
            firstProduct.Price = 500m;
            secondProduct.Price = 700m;
            await db.SaveChangesAsync();
        }

        using var response = await server.GetAsync(
            "/api/sales/dashboard?from=2026-11-09T00:00:00Z&to=2026-11-12T00:00:00Z",
            RolePolicies.SalesManager, server.CustomerId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = (await response.Content.ReadFromJsonAsync<SalesDashboardResponse>())!;
        Assert.Equal(140m, dashboard.GrossRevenue);
        Assert.Equal(10m, dashboard.Refunds);
        Assert.Equal(130m, dashboard.NetRevenue);
    }

    [Fact]
    public async Task SalesDashboard_UsesHalfOpenDatesAndExcludesUnconfirmedPaymentsAndRefunds()
    {
        await using var server = await DashboardTestServer.CreateAsync();
        var from = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(3);
        await using (var scope = server.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
            var customer = await db.Users.SingleAsync(user => user.Id == server.CustomerId);
            var paymentEntries = new[]
            {
                (ConfirmedAt: from, Amount: 100m),
                (ConfirmedAt: from.AddDays(1), Amount: 200m),
                (ConfirmedAt: to, Amount: 300m),
                (ConfirmedAt: from.AddHours(1), Amount: 0m),
                (ConfirmedAt: from.AddHours(2), Amount: 0m)
            };
            var orders = paymentEntries.Select(entry =>
            {
                var order = NewOrder(customer, entry.Amount);
                Assert.True(order.ConfirmPayment(entry.ConfirmedAt));
                return order;
            }).ToArray();
            db.Orders.AddRange(orders);
            await db.SaveChangesAsync();

            var confirmedPayments = paymentEntries.Select((entry, index) => new Payment
            {
                OrderId = orders[index].Id, Order = orders[index], UserId = customer.Id, CartId = 1,
                IdempotencyKey = (index + 1).ToString("X64"), Amount = entry.Amount,
                Status = PaymentStatus.Confirmed, CreatedAt = entry.ConfirmedAt,
                ConfirmedAt = entry.ConfirmedAt, CardBrand = "Visa", CardLast4 = "1234"
            }).ToArray();
            var pendingAttempt = new Payment
            {
                UserId = customer.Id, CartId = 1, IdempotencyKey = new string('A', 64), Amount = 400m,
                Status = PaymentStatus.Pending, CreatedAt = from.AddHours(3), CardBrand = "Visa", CardLast4 = "1234"
            };
            var declinedAttempt = new Payment
            {
                UserId = customer.Id, CartId = 1, IdempotencyKey = new string('B', 64), Amount = 500m,
                Status = PaymentStatus.Failed, CreatedAt = from.AddHours(4), CardBrand = "Visa", CardLast4 = "1234"
            };
            db.Payments.AddRange(confirmedPayments);
            db.Payments.AddRange(pendingAttempt, declinedAttempt);
            await db.SaveChangesAsync();

            db.Refunds.AddRange(
                NewRefund(orders[0], confirmedPayments[0], 10m, "Completed", from),
                NewRefund(orders[1], confirmedPayments[1], 20m, "Completed", from.AddDays(2)),
                NewRefund(orders[2], confirmedPayments[2], 30m, "Completed", to),
                NewRefund(orders[3], confirmedPayments[3], 40m, "Pending", null),
                NewRefund(orders[4], confirmedPayments[4], 50m, "Failed", null));

            var product = NewProduct("RETURN-REJECTED", 10m, 1);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var returnOrder = NewOrder(customer, 10m);
            Assert.True(returnOrder.ConfirmPayment(from));
            var line = OrderItem.Snapshot(product, 1, 10m);
            line.UserId = customer.Id;
            returnOrder.OrderItems.Add(line);
            db.Orders.Add(returnOrder);
            await db.SaveChangesAsync();
            db.ReturnRequests.Add(new ReturnRequest
            {
                OrderItemId = line.Id, UserId = customer.Id, ProductId = product.Id,
                ReviewedByUserId = customer.Id, OrderItem = line,
                Quantity = 1, Status = ReturnStatus.Rejected, RequestedAt = from,
                ReviewedAt = from.AddDays(1)
            });
            await db.SaveChangesAsync();
            Assert.Equal(1, await db.ReturnRequests.CountAsync(request =>
                request.Status == ReturnStatus.Rejected && request.Refund == null));
        }

        using var response = await server.GetAsync(
            "/api/sales/dashboard?from=2026-11-09T00:00:00Z&to=2026-11-12T00:00:00Z",
            RolePolicies.SalesManager, server.CustomerId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = (await response.Content.ReadFromJsonAsync<SalesDashboardResponse>())!;
        Assert.Equal(300m, dashboard.GrossRevenue);
        Assert.Equal(30m, dashboard.Refunds);
        Assert.Equal(270m, dashboard.NetRevenue);
    }

    [Fact]
    public async Task ProductDashboard_UsesSchemaStatusesAndLowStockThreshold()
    {
        await using var server = await DashboardTestServer.CreateAsync();
        await using (var scope = server.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
            var customer = await db.Users.SingleAsync(user => user.Id == server.CustomerId);
            var scarce = NewProduct("SCARCE", 100m, 5);
            var inactive = NewProduct("INACTIVE", 100m, 1);
            inactive.IsActive = false;
            var healthy = NewProduct("HEALTHY", 100m, 6);
            db.Products.AddRange(scarce, inactive, healthy);
            await db.SaveChangesAsync();

            var order = PaidOrder(customer, scarce, 1, 100m);
            Assert.True(order.TransitionTo(OrderStatus.InTransit, PurchasedAt.AddDays(1)));
            var processingOrder = PaidOrder(customer, scarce, 1, 100m);
            var deliveredOrder = PaidOrder(customer, scarce, 1, 100m);
            Assert.True(deliveredOrder.TransitionTo(OrderStatus.InTransit, PurchasedAt.AddDays(1)));
            Assert.True(deliveredOrder.TransitionTo(OrderStatus.Delivered, PurchasedAt.AddDays(2)));
            var cancelledOrder = PaidOrder(customer, scarce, 1, 100m);
            Assert.True(cancelledOrder.TransitionTo(OrderStatus.Cancelled, PurchasedAt.AddDays(1)));
            db.Orders.AddRange(order, processingOrder, deliveredOrder, cancelledOrder);
            await db.SaveChangesAsync();
            var line = order.OrderItems.Single();
            db.Deliveries.AddRange(
                new Delivery
                {
                    OrderId = order.Id, UserId = customer.Id, Order = order, AddressSnapshot = "Snapshot",
                    Status = OrderStatus.InTransit, CreatedAt = PurchasedAt.AddDays(1)
                },
                new Delivery
                {
                    OrderId = processingOrder.Id, UserId = customer.Id, Order = processingOrder, AddressSnapshot = "Snapshot",
                    Status = OrderStatus.Processing, CreatedAt = PurchasedAt.AddDays(2)
                },
                new Delivery
                {
                    OrderId = deliveredOrder.Id, UserId = customer.Id, Order = deliveredOrder, AddressSnapshot = "Snapshot",
                    Status = OrderStatus.Delivered, CreatedAt = PurchasedAt.AddDays(3)
                },
                new Delivery
                {
                    OrderId = cancelledOrder.Id, UserId = customer.Id, Order = cancelledOrder, AddressSnapshot = "Snapshot",
                    Status = OrderStatus.Cancelled, CreatedAt = PurchasedAt.AddDays(4)
                });
            db.Comments.Add(new Comment
            {
                UserId = customer.Id, ProductId = scarce.Id, OrderItemId = line.Id, Product = scarce,
                OrderItem = line, Body = "Waiting for review", CreatedAt = PurchasedAt.AddDays(1)
            });
            db.Comments.Add(new Comment
            {
                UserId = customer.Id, ProductId = scarce.Id, OrderItemId = line.Id, Product = scarce,
                OrderItem = line, Body = new string('x', 1001), CreatedAt = PurchasedAt.AddDays(2)
            });
            db.Comments.Add(new Comment
            {
                UserId = customer.Id, ProductId = scarce.Id, OrderItemId = line.Id, Product = scarce,
                OrderItem = line, Body = "Already approved", ApprovalStatus = ApprovalStatus.Approved
            });
            await db.SaveChangesAsync();
        }

        using var response = await server.GetAsync(
            "/api/product-management/dashboard", RolePolicies.ProductManager, server.CustomerId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = (await response.Content.ReadFromJsonAsync<ProductManagementDashboardResponse>())!;
        Assert.Equal(["SCARCE"], dashboard.LowStockProducts.Select(product => product.Sku));
        Assert.Equal(["Processing", "InTransit"], dashboard.PendingDeliveries.Select(delivery => delivery.Status));
        Assert.Equal(["Waiting for review", new string('x', 1000)],
            dashboard.CommentsWaitingForApproval.Select(comment => comment.Body));
    }

    [Fact]
    public async Task ManagementUsers_ArePagedSearchedAndExcludePrivateFields()
    {
        await using var server = await DashboardTestServer.CreateAsync();
        await using (var scope = server.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
            db.Users.AddRange(
                NewUser("brenda@example.com", "Brenda Buyer"),
                NewUser("brian@example.com", "Brian Buyer"),
                NewUser("cora@example.com", "Cora Buyer"));
            await db.SaveChangesAsync();
        }

        using var pageResponse = await server.GetAsync("/api/management/users?page=1&pageSize=2",
            RolePolicies.SalesManager, server.CustomerId);
        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        var page = (await pageResponse.Content.ReadFromJsonAsync<ManagementUsersResponse>())!;
        Assert.Equal(4, page.TotalCount);
        Assert.Equal(2, page.Users.Count);
        Assert.Equal(1, page.Page);

        using var searchResponse = await server.GetAsync("/api/management/users?search=Br&pageSize=10",
            RolePolicies.ProductManager, server.CustomerId);
        var search = (await searchResponse.Content.ReadFromJsonAsync<ManagementUsersResponse>())!;
        Assert.Equal(["Brenda Buyer", "Brian Buyer"], search.Users.Select(user => user.Name));
        Assert.Equal(2, search.TotalCount);
        var body = await searchResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("email", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("taxId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", body, StringComparison.OrdinalIgnoreCase);

        using var emailSearch = await server.GetAsync("/api/management/users?search=ada@example.com",
            RolePolicies.SalesManager, server.CustomerId);
        var emailSearchResult = (await emailSearch.Content.ReadFromJsonAsync<ManagementUsersResponse>())!;
        Assert.Empty(emailSearchResult.Users);
    }

    [Theory]
    [InlineData("/api/sales/dashboard")]
    [InlineData("/api/sales/dashboard?from=2026-11-10T00:00:00Z&to=2026-11-09T00:00:00Z")]
    [InlineData("/api/sales/dashboard?from=2026-11-09T00:00:00Z&to=2026-11-09T00:00:00Z")]
    [InlineData("/api/sales/dashboard?from=2026-11-09T00:00:00&to=2026-11-10T00:00:00")]
    [InlineData("/api/sales/dashboard?from=2025-11-01T00:00:00Z&to=2026-11-10T00:00:00Z")]
    public async Task SalesDashboard_RejectsMissingOrInvalidDateRanges(string path)
    {
        await using var server = await DashboardTestServer.CreateAsync();
        using var response = await server.GetAsync(path, RolePolicies.SalesManager, server.CustomerId);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SalesDashboard_AcceptsTheMaximumDateRange()
    {
        await using var server = await DashboardTestServer.CreateAsync();
        using var response = await server.GetAsync(
            "/api/sales/dashboard?from=2025-11-09T00:00:00Z&to=2026-11-10T00:00:00Z",
            RolePolicies.SalesManager, server.CustomerId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=10001")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=51")]
    [InlineData("?search=x")]
    [InlineData("?search=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task ManagementUsers_RejectsInvalidPaginationAndSearch(string query)
    {
        await using var server = await DashboardTestServer.CreateAsync();
        using var response = await server.GetAsync("/api/management/users" + query,
            RolePolicies.SalesManager, server.CustomerId);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Dashboards_ReturnSafe503WhenTheDatabaseIsUnavailable()
    {
        await using var server = await DashboardTestServer.CreateAsync(databaseFailure: true);
        using var response = await server.GetAsync(
            "/api/sales/dashboard?from=2026-11-09T00:00:00Z&to=2026-11-11T00:00:00Z",
            RolePolicies.SalesManager, server.CustomerId);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        Assert.DoesNotContain("database", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    private static User NewUser(string email, string name) => new()
    {
        Name = name, Email = email, TaxId = "TAX-PRIVATE", HomeAddress = "Private address",
        PasswordHash = "password-hash", Role = UserRole.Customer
    };

    private static Product NewProduct(string sku, decimal price, int stock)
    {
        var product = new Product
        {
            Sku = sku, Name = sku + " product", Price = price, StockQuantity = stock,
            Category = new Category { Name = sku + " category" }
        };
        CatalogFixture.CompleteDetails(product);
        return product;
    }

    private static Order NewOrder(User user, decimal total) => new()
    {
        User = user, UserId = user.Id, CustomerNameSnapshot = user.Name, EmailSnapshot = user.Email,
        TaxIdSnapshot = user.TaxId, AddressSnapshot = user.HomeAddress,
        Subtotal = total, GrandTotal = total
    };

    private static Order PaidOrder(User user, Product product, int quantity, decimal unitPrice)
    {
        var order = NewOrder(user, quantity * unitPrice);
        var line = OrderItem.Snapshot(product, quantity, unitPrice);
        line.UserId = user.Id;
        order.OrderItems.Add(line);
        order.ConfirmPayment(PurchasedAt);
        return order;
    }

    private static Order UnpaidOrder(User user, Product product)
    {
        var order = NewOrder(user, 100m);
        var line = OrderItem.Snapshot(product, 1, 100m);
        line.UserId = user.Id;
        order.OrderItems.Add(line);
        return order;
    }

    private static Refund NewRefund(Order order, Payment payment, decimal amount, string status, DateTime? refundedAt) =>
        new()
        {
            OrderId = order.Id, PaymentId = payment.Id, Payment = payment,
            IdempotencyKey = Guid.NewGuid().ToString("N").PadRight(64, '0'), Amount = amount,
            Reason = "Cancellation", Status = status, CreatedAt = PurchasedAt, RefundedAt = refundedAt
        };

    private sealed class DashboardTestServer : IAsyncDisposable
    {
        private DashboardTestServer(WebApplication app, HttpClient client, long customerId)
        {
            App = app;
            Client = client;
            CustomerId = customerId;
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }
        public long CustomerId { get; }

        public static async Task<DashboardTestServer> CreateAsync(bool databaseFailure = false)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var databaseName = Guid.NewGuid().ToString();
            builder.Services.AddDbContext<StoreDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
            builder.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedNow));
            if (databaseFailure)
                builder.Services.AddScoped<IDashboardService>(_ => new FailingDashboardService());
            else
                builder.Services.AddScoped<IDashboardService, DashboardService>();
            builder.Services.AddAuthentication("dashboard-test")
                .AddScheme<AuthenticationSchemeOptions, DashboardTestAuthenticationHandler>("dashboard-test", _ => { });
            builder.Services.AddAuthorization(RolePolicies.Configure);

            var app = builder.Build();
            app.UseRouting();
            app.UsePrivateResponseNoStore();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapDashboardEndpoints();
            var customer = NewUser("ada@example.com", "Ada Customer");
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
                db.Users.Add(customer);
                await db.SaveChangesAsync();
            }
            await app.StartAsync();
            return new DashboardTestServer(app, app.GetTestClient(), customer.Id);
        }

        public Task<HttpResponseMessage> GetAsync(string path, string role, long userId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Test-Role", role);
            request.Headers.Add("X-Test-User-Id", userId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }

    private sealed class DashboardTestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
                return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new(ClaimTypes.Role, role.ToString()) };
            if (Request.Headers.TryGetValue("X-Test-User-Id", out var userId))
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "dashboard-test"));
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, "dashboard-test")));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FailingDashboardService : IDashboardService
    {
        public Task<CustomerDashboardResponse?> GetCustomerAsync(long userId, CancellationToken cancellationToken) =>
            throw new SimulatedDatabaseException();

        public Task<SalesDashboardResponse> GetSalesAsync(DateTimeOffset from, DateTimeOffset to,
            CancellationToken cancellationToken) => throw new SimulatedDatabaseException();

        public Task<ProductManagementDashboardResponse> GetProductManagementAsync(CancellationToken cancellationToken) =>
            throw new SimulatedDatabaseException();

        public Task<ManagementUsersResponse> ListUsersAsync(int page, int pageSize, string? search,
            CancellationToken cancellationToken) => throw new SimulatedDatabaseException();
    }

    private sealed class SimulatedDatabaseException : DbException { }
}
