using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Accounts;
using OnlineStore.Data;
using OnlineStore.Invoicing;

namespace OnlineStore.Purchasing;

public static class PurchaseEndpoints
{
    public static void MapPurchaseEndpoints(this WebApplication app)
    {
        var customer = app.MapCustomerApi().AddEndpointFilter<PurchaseErrorFilter>();
        customer.MapGet("/checkout", async (ClaimsPrincipal user, CheckoutService service, CancellationToken ct) =>
            Results.Ok(await service.QuoteAsync(Owner(user), ct)));
        customer.MapPost("/checkout", async (ClaimsPrincipal user, CheckoutRequest input,
            CheckoutService service, InvoiceService invoices, ILogger<InvoiceService> log, CancellationToken ct) =>
        {
            var result = await service.CompleteAsync(Owner(user), input, ct);
            // The purchase is already committed. An invoice failure leaves it "Pending" and a later read retries.
            var receipt = await WithInvoiceAsync(result.Receipt, invoices, log, ct);
            return Results.Json(receipt, statusCode: result.Replayed ? 200 : 201);
        }).AddEndpointFilter<CsrfFilter>();
        customer.MapGet("/checkout/attempts/{key:guid}", async (Guid key, ClaimsPrincipal user, StoreDbContext db, CancellationToken ct) =>
        {
            var owner = Owner(user);
            var attemptKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{owner}:{key:D}")));
            var payment = await db.Payments.AsNoTracking().Where(payment => payment.IdempotencyKey == attemptKey
                    && payment.Order.UserId == owner)
                .Include(payment => payment.Order).ThenInclude(order => order.OrderItems)
                .Include(payment => payment.Order).ThenInclude(order => order.Invoice).SingleOrDefaultAsync(ct);
            return payment is null ? Results.NotFound() : Results.Ok(PurchaseViews.Receipt(payment.Order));
        });
        var management = app.MapProductManagerApi().AddEndpointFilter<PurchaseErrorFilter>();
        management.MapGet("/deliveries", async (int? afterId, DeliveryService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(afterId ?? 0, ct)));
        management.MapPost("/deliveries/{id:int}/status", async (int id, DeliveryTransition input,
            DeliveryService service, CancellationToken ct) => Results.Ok(await service.TransitionAsync(id, input, ct)))
            .AddEndpointFilter<CsrfFilter>();
        // The confirmation screen and SCRUM-76 share this owner-only snapshot response.
        customer.MapGet("/orders/{id:int}", async (int id, ClaimsPrincipal user, StoreDbContext db,
            InvoiceService invoices, ILogger<InvoiceService> log, CancellationToken ct) =>
        {
            var owner = Owner(user);
            var order = await db.Orders.AsNoTracking().Include(order => order.OrderItems).Include(order => order.Invoice)
                .SingleOrDefaultAsync(order => order.Id == id && order.UserId == owner, ct);
            return order is null ? Results.NotFound()
                : Results.Ok(await WithInvoiceAsync(PurchaseViews.Receipt(order), invoices, log, ct));
        });
    }

    // Issues the invoice of a paid order if it is still missing (SCRUM-74). Never fails the purchase response.
    private static async Task<PurchaseReceipt> WithInvoiceAsync(PurchaseReceipt receipt, InvoiceService invoices,
        ILogger log, CancellationToken ct)
    {
        if (receipt.InvoiceStatus == "Ready" || receipt.PaymentStatus != "Confirmed") return receipt;
        try
        {
            if (!await invoices.EnsureAsync(receipt.OrderId, ct)) return receipt;
            return receipt with { InvoiceStatus = "Ready", InvoiceUrl = $"/api/customer/orders/{receipt.OrderId}/invoice" };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            log.LogWarning(exception, "Invoice for order {OrderId} is still pending.", receipt.OrderId);
            return receipt;
        }
    }

    private static int Owner(ClaimsPrincipal user) => int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public sealed class PurchaseErrorFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        try { return await next(context); }
        catch (PurchaseException exception) { return Results.Problem(statusCode: exception.Status, title: exception.Message); }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(statusCode: 409, title: "The order changed. Reload before trying again.");
        }
        catch (Exception exception) when (DatabaseErrors.IsUnavailable(exception))
        {
            return Results.Problem(statusCode: 503, title: "The purchase service is temporarily unavailable. Retry the same request.");
        }
    }
}
