using Econosys.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfUnit = QuestPDF.Infrastructure.Unit;

namespace Econosys.Api.Services;

public static class InvoicePdfDocument
{
    private const decimal VatRate = 0.25m;
    private const int RoundingPriceTypeId = 12;
    private const int VatPriceTypeId = 13;
    private const string DefaultCurrency = "SEK";
    private static readonly byte[] LogoBytes = File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "Assets", "Econopack_Logo_Sage.png"));
    private static readonly byte[] WatermarkBytes = File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "Assets", "Box_Sage.png"));

    public static byte[] Generate(Invoice invoice, CompanyInfo? company)
    {
        var invoiceRows = invoice.InvoiceRows
            .OrderBy(row => row.SortOrder ?? int.MaxValue)
            .ThenBy(row => row.Id)
            .ToList();

        var lines = invoiceRows
            .Where(row => row.PriceTypeId != VatPriceTypeId && row.PriceTypeId != RoundingPriceTypeId)
            .Select(row => new InvoiceLine(
                row.Text,
                row.Text2,
                row.NrOf,
                row.Unit?.Name,
                row.UnitPrice,
                row.Sum,
                row.VatGround))
            .ToList();

        var subtotal = lines.Sum(line => (decimal)(line.Total ?? 0d));
        var vatRow = invoiceRows.FirstOrDefault(row => row.PriceTypeId == VatPriceTypeId);
        var roundingRow = invoiceRows.FirstOrDefault(row => row.PriceTypeId == RoundingPriceTypeId);
        var vat = vatRow?.Sum is double vatValue
            ? (decimal)vatValue
            : lines.Where(line => line.VatGround).Sum(line => (decimal)((line.Total ?? 0d) * (double)VatRate));
        var rounding = roundingRow?.Sum is double roundingValue ? (decimal)roundingValue : 0m;
        var total = subtotal + vat + rounding;
        var currency = string.IsNullOrWhiteSpace(invoice.SalesCurrency?.SpcsKey)
            ? invoice.SalesCurrency?.Name ?? DefaultCurrency
            : invoice.SalesCurrency.SpcsKey;

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(1.7f, PdfUnit.Centimetre);
                page.MarginVertical(1.5f, PdfUnit.Centimetre);
                page.DefaultTextStyle(style => style
                    .FontFamily("DM Sans")
                    .FontSize(9)
                    .FontColor("#111827"));

                page.Background()
                    .AlignBottom()
                    .AlignRight()
                    .OffsetX(150)
                    .OffsetY(-25)
                    .Width(320)
                    .Image(WatermarkBytes)
                    .FitWidth();

                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Element(container => ComposeHeader(container, invoice));
                    column.Item().Element(container => ComposeCustomerSection(container, invoice));
                    column.Item().Element(container => ComposeLines(container, lines, currency));
                    column.Item().Element(container => ComposeTotals(container, subtotal, vat, rounding, total, currency));
                    column.Item().PaddingTop(34).Element(container => ComposeTerms(container, invoice));
                });

                page.Footer()
                    .PaddingTop(14)
                    .Element(container => ComposeFooter(container, company));
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, Invoice invoice)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Height(40).AlignLeft().Image(LogoBytes).FitHeight();
            });

            row.ConstantItem(190).AlignRight().Column(column =>
            {
                column.Item().PaddingTop(14).Text(text =>
                {
                    text.Span("Fakturadatum: ").Bold();
                    text.Span(FormatDate(invoice.InvoiceDate));
                });
                column.Item().PaddingTop(5).Text(text =>
                {
                    text.Span("Förfallodag: ").Bold();
                    text.Span(FormatDate(invoice.InvoiceDate?.AddDays(invoice.InvoiceDays ?? 0)));
                });
            });
        });
    }

    private static void ComposeCustomerSection(IContainer container, Invoice invoice)
    {
        container.Column(column =>
        {
            column.Spacing(14);
            column.Item().PaddingTop(30).Row(row =>
            {
                row.RelativeItem().Element(c => ComposeAddress(c, "Kund", invoice.CustomerName, invoice.Address, invoice.PostalNr, invoice.PostalAddress, invoice.Country));
                row.RelativeItem().Element(c => ComposeAddress(c, "Leveransadress", invoice.CustomerName, invoice.Address2 ?? invoice.Address, invoice.PostalNr, invoice.PostalAddress, invoice.Country));
            });

            column.Item().Row(row =>
            {
                row.RelativeItem().Column(details =>
                {
                    details.Item().Text(text => { text.Span("Vår referens    ").Bold(); text.Span(Value(invoice.OurReference)); });
                    details.Item().Text(text => { text.Span("Er referens      ").Bold(); text.Span(Value(invoice.YourReference)); });
                    details.Item().Text(text => { text.Span("Ert ordernr.     ").Bold(); text.Span(Value(invoice.YourOrderNr)); });
                });
                row.ConstantItem(190).AlignRight().Column(title =>
                {
                    title.Item().Text("Faktura").FontSize(16).SemiBold();
                    title.Item().Text(Value(invoice.InvoiceNumber?.ToString())).FontSize(16).SemiBold();
                });
            });
        });
    }

    private static void ComposeAddress(IContainer container, string heading, params string?[] values)
    {
        container.Column(column =>
        {
            column.Item().Text(heading).Bold();
            foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                column.Item().Text(value!);
            }
        });
    }

    private static void ComposeLines(IContainer container, IReadOnlyList<InvoiceLine> lines, string currency)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(4.8f);
                columns.RelativeColumn(1.35f);
                columns.RelativeColumn(1.35f);
                columns.RelativeColumn(1.8f);
                columns.RelativeColumn(1.8f);
            });

            table.Header(header =>
            {
                header.Cell().Element(EmptyHeaderCell).Text("");
                header.Cell().Element(HeaderCell).AlignRight().Text("Antal");
                header.Cell().Element(HeaderCell).Text("Enhet");
                header.Cell().Element(HeaderCell).AlignRight().Text("Pris/Enhet");
                header.Cell().Element(HeaderCell).AlignRight().Text("Total");
            });

            foreach (var line in lines)
            {
                table.Cell().Element(BodyCell).Column(column =>
                {
                    column.Item().Text(Value(line.Description)).Bold();
                    if (!string.IsNullOrWhiteSpace(line.Description2))
                    {
                        column.Item().Text(line.Description2!);
                    }
                });
                table.Cell().Element(BodyCell).AlignRight().Text(FormatNumber(line.Quantity));
                table.Cell().Element(BodyCell).Text(Value(line.Unit));
                table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(line.UnitPrice, currency));
                table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(line.Total, currency));
            }
        });
    }

    private static void ComposeTotals(IContainer container, decimal subtotal, decimal vat, decimal rounding, decimal total, string currency)
    {
        container.AlignRight().Width(270).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Summa exkl. moms").Bold();
                row.ConstantItem(105).AlignRight().Text(FormatMoney(subtotal, currency)).Bold();
            });
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Moms 25%");
                row.ConstantItem(105).AlignRight().Text(FormatMoney(vat, currency));
            });
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Summa inkl. moms");
                row.ConstantItem(105).AlignRight().Text(FormatMoney(total, currency));
            });
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Öresutjämning");
                row.ConstantItem(105).AlignRight().Text(FormatMoney(rounding, currency));
            });
        });
    }

    private static void ComposeTerms(IContainer container, Invoice invoice)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Row(line =>
                {
                    line.ConstantItem(112).Text("Leveransdatum").Bold();
                    line.RelativeItem().Text(FormatDate(invoice.AccountDate ?? invoice.InvoiceDate));
                });
                row.RelativeItem().Row(line =>
                {
                    line.ConstantItem(112).Text("Leveransvillkor").Bold();
                    line.RelativeItem().Text(Value(invoice.TimeOfDelivery));
                });
            });

            column.Item().PaddingTop(10).Row(line =>
            {
                line.ConstantItem(112).Text("Betalningsvillkor").Bold();
                line.RelativeItem().Column(payment =>
                {
                    payment.Item().Text(Value(invoice.TermsOfPayment ?? (invoice.InvoiceDays.HasValue ? $"{invoice.InvoiceDays} dagar från fakturadatum" : null)));
                    payment.Item().Text("Efter förfallodagen debiteras dröjsmålsränta med årsränta 8%.").Italic();
                    payment.Item().Text("Fakturerad dröjsmålsränta förfaller till omedelbar betalning.").Italic();
                });
            });

            column.Item().PaddingTop(30).Text(
                "Econopack är anslutet till Näringslivets Producentansvar och betalar förpackningsavgift för det emballage som används vid transport för att skydda våra produkter. I de fall där avgift är betald för förpackningen i sig så är det specificerat på fakturan. I alla andra fall så har fyllaren producentansvar och ska därmed betala förpackningsavgift."
            ).Italic();
        });
    }

    private static void ComposeFooter(IContainer container, CompanyInfo? company)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(string.Join(" ", NonEmpty(company?.Address, company?.ZipCode, company?.PostalAddress, "•", company?.Telephone1)));
                column.Item().Text(string.Join(" ", NonEmpty("•", company?.Email, "•", company?.Web)));
                column.Item().Text(text =>
                {
                    text.Span(Value(company?.CompanyName));
                    text.Span(" är godkänd för F-skatt.").Italic();
                });
            });
            row.ConstantItem(270).AlignRight().Column(column =>
            {
                column.Item().AlignRight().Text(text => { text.Span("Bank: ").Bold(); text.Span(Value(company?.Bank)); });
                column.Item().AlignRight().Text(text => { text.Span("Bankgiro: ").Bold(); text.Span(Value(company?.BG)); });
                column.Item().AlignRight().Row(line =>
                {
                    line.AutoItem().Text(text => { text.Span("VAT-nr: ").Bold(); text.Span(Value(company?.VATNr)); });
                    line.ConstantItem(18);
                    line.AutoItem().Text(text => { text.Span("BIC: ").Bold(); text.Span(Value(company?.BIC)); });
                });
                column.Item().AlignRight().Text(text => { text.Span("IBAN: ").Bold(); text.Span(Value(company?.IBAN)); });
            });
        });
    }

    private static IContainer HeaderCell(IContainer container) => container
        .Background("#f1f4f3")
        .PaddingVertical(7)
        .PaddingHorizontal(6)
        .DefaultTextStyle(style => style.Bold());

    private static IContainer EmptyHeaderCell(IContainer container) => container
        .PaddingVertical(7)
        .PaddingHorizontal(6);

    private static IContainer BodyCell(IContainer container) => container
        .BorderBottom(0.5f)
        .BorderColor("#dbe4e2")
        .PaddingVertical(6)
        .PaddingRight(6);

    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "" : value;

    private static IEnumerable<string> NonEmpty(params string?[] values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!);

    private static string FormatDate(DateTime? date) => date?.ToString("yyyy-MM-dd") ?? "";

    private static string FormatNumber(double? value) => value?.ToString("0.##") ?? "";

    private static string FormatMoney(double? value, string currency) => value.HasValue
        ? $"{value.Value:N2} {currency}"
        : "";

    private static string FormatMoney(decimal value, string currency) => $"{value:N2} {currency}";

    private sealed record InvoiceLine(
        string? Description,
        string? Description2,
        double? Quantity,
        string? Unit,
        double? UnitPrice,
        double? Total,
        bool VatGround);
}
