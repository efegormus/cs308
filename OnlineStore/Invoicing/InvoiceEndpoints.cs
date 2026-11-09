using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Accounts;
using OnlineStore.Data;
using OnlineStore.Purchasing;

namespace OnlineStore.Invoicing;

public static class InvoiceEndpoints
{
    public static void MapInvoiceEndpoints(this WebApplication app)
    {
        // The owning customer only. Another customer's order answers 404, so order IDs cannot be probed.
        app.MapCustomerApi().AddEndpointFilter<PurchaseErrorFilter>().MapGet("/orders/{id:long}/invoice",
            async (long id, ClaimsPrincipal user, StoreDbContext db, InvoiceService invoices, CancellationToken ct) =>
            {
                var owner = long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
                if (!await db.Orders.AnyAsync(order => order.Id == id && order.UserId == owner, ct)) return Results.NotFound();
                return Pdf(await invoices.GetPdfAsync(id, ct));
            });

        // Sales managers (invoice lists, SCRUM-75) and product managers (delivery department) may read any invoice.
        foreach (var staff in new[] { app.MapSalesManagerApi(), app.MapProductManagerApi() })
        {
            staff.AddEndpointFilter<PurchaseErrorFilter>().MapGet("/invoices/{orderId:long}",
                async (long orderId, InvoiceService invoices, CancellationToken ct) => Pdf(await invoices.GetPdfAsync(orderId, ct)));
        }
    }

    private static IResult Pdf(InvoiceFile? file) =>
        file is null ? Results.NotFound() : Results.File(file.Content, "application/pdf", file.FileName);
}
