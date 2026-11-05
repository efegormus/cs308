using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OnlineStore.Invoicing;

public sealed record InvoiceLine(string ProductName, int Quantity, decimal UnitPrice)
{
    public decimal Total => UnitPrice * Quantity;
}

// Everything printed on an invoice. Built only from purchase-time snapshots, never from current products or profiles.
public sealed record InvoiceData(string InvoiceNumber, int OrderId, DateTime PurchasedAt, DateTime IssuedAt,
    string CustomerName, string TaxId, string Address, string Currency, IReadOnlyList<InvoiceLine> Lines)
{
    public decimal Total => Lines.Sum(line => line.Total);
}

public static class InvoiceDocument
{
    private static readonly CultureInfo Format = CultureInfo.InvariantCulture;

    // QuestPDF's free Community license covers this course project.
    static InvoiceDocument() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Render(InvoiceData data)
    {
        if (data.Lines.Count == 0) throw new ArgumentException("An invoice needs at least one line.", nameof(data));
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(text => text.FontSize(10));

            page.Header().Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("Online Store").FontSize(20).Bold();
                    column.Item().Text("CS 308 Team");
                });
                row.RelativeItem().AlignRight().Column(column =>
                {
                    column.Item().Text("INVOICE").FontSize(20).Bold();
                    column.Item().Text($"Invoice no: {data.InvoiceNumber}");
                    column.Item().Text($"Order no: {data.OrderId}");
                    column.Item().Text($"Purchase date: {Date(data.PurchasedAt)}");
                    column.Item().Text($"Issued: {Date(data.IssuedAt)}");
                });
            });

            page.Content().PaddingVertical(20).Column(column =>
            {
                column.Spacing(16);
                column.Item().Column(customer =>
                {
                    customer.Item().Text("Bill to").Bold();
                    customer.Item().Text(data.CustomerName);
                    customer.Item().Text($"Tax ID: {data.TaxId}");
                    customer.Item().Text(data.Address);
                });

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(5);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });
                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Product");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Qty");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Unit price");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Total");
                    });
                    foreach (var line in data.Lines)
                    {
                        table.Cell().Element(BodyCell).Text(line.ProductName);
                        table.Cell().Element(BodyCell).AlignRight().Text(line.Quantity.ToString(Format));
                        table.Cell().Element(BodyCell).AlignRight().Text(Money(line.UnitPrice, data.Currency));
                        table.Cell().Element(BodyCell).AlignRight().Text(Money(line.Total, data.Currency));
                    }
                });

                column.Item().AlignRight().Text($"Total: {Money(data.Total, data.Currency)}").FontSize(14).Bold();
            });

            page.Footer().AlignCenter().Text("Prices are the amounts paid at purchase time, including any discount.")
                .FontSize(8).FontColor(Colors.Grey.Darken1);
        })).GeneratePdf();
    }

    public static string Money(decimal amount, string currency) => $"{amount.ToString("N2", Format)} {currency}";

    private static string Date(DateTime value) => value.ToString("yyyy-MM-dd HH:mm 'UTC'", Format);

    private static IContainer HeaderCell(IContainer cell) =>
        cell.BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingVertical(4).DefaultTextStyle(text => text.Bold());

    private static IContainer BodyCell(IContainer cell) =>
        cell.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4);
}
