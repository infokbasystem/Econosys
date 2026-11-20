using Econosys.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Econosys.Api.Services;

public static class InvoicePdfDocument
{
    private const decimal VatRate = 0.25m;
    private const int RoundingPriceTypeId = 12;
    private const int VatPriceTypeId = 13;
    private const string DefaultCurrency = "SEK";
    private const float HeaderCornerRadius = 6;
    private const string HeaderBackground = "#f1f4f3";
    private const float AmountRightInset = 6;

    public static byte[] Generate(Invoice invoice, CompanyInfo? company, string? vatInfo) =>
        CreateDocument(invoice, company, vatInfo).GeneratePdf();

    public static IDocument CreateDocument(Invoice invoice, CompanyInfo? company, string? vatInfo)
    {
        var printForScanning = invoice.Customer?.PrintForDocumentScanning == true;
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
                DocumentPdfLayout.Configure(page, printForScanning);

                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Element(container => ComposeHeader(container, invoice, printForScanning));
                    column.Item().Element(container => ComposeCustomerSection(container, invoice));
                    column.Item().Element(container => ComposeLines(container, lines, currency, printForScanning));
                    column.Item().Element(container => ComposeTotals(container, subtotal, vat, rounding, total, currency));
                    column.Item().PaddingTop(34).Element(container => ComposeTerms(container, invoice));
                });

                page.Footer()
                    .PaddingTop(14)
                    .Element(container => DocumentPdfLayout.InvoiceFooter(container, company, vatInfo, printForScanning: printForScanning));
            });
        });
    }

    private static void ComposeHeader(IContainer container, Invoice invoice, bool printForScanning)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Element(item => DocumentPdfLayout.Logo(item, printForScanning));
            });

            row.ConstantItem(190).Column(column =>
            {
                column.Item().PaddingTop(14).AlignRight().Text(text =>
                {
                    text.Span("Fakturadatum: ").Bold();
                    text.Span(FormatDate(invoice.InvoiceDate));
                });
                column.Item().PaddingTop(5).AlignRight().Text(text =>
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
                row.ConstantItem(190).Column(title =>
                {
                    title.Item().AlignRight().Text("Faktura").FontSize(16).SemiBold();
                    title.Item().AlignRight().Text(Value(invoice.InvoiceNumber?.ToString())).FontSize(16).SemiBold();
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

    private static void ComposeLines(IContainer container, IReadOnlyList<InvoiceLine> lines, string currency, bool printForScanning)
    {
        IContainer StyleHeader(IContainer cell) => HeaderCell(printForScanning ? cell : cell.Background(HeaderBackground));
        IContainer StyleBody(IContainer cell) => BodyCell(cell, printForScanning);

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
                header.Cell().CornerRadiusTopLeft(HeaderCornerRadius).CornerRadiusBottomLeft(HeaderCornerRadius)
                    .Element(StyleHeader).AlignRight().Text("Antal");
                header.Cell().Element(StyleHeader).Text("Enhet");
                header.Cell().Element(StyleHeader).AlignRight().Text("Pris/Enhet");
                header.Cell().CornerRadiusTopRight(HeaderCornerRadius).CornerRadiusBottomRight(HeaderCornerRadius)
                    .Element(StyleHeader).AlignRight().Text("Total");
            });

            foreach (var line in lines)
            {
                table.Cell().Element(StyleBody).Column(column =>
                {
                    column.Item().Text(Value(line.Description)).Bold();
                    if (!string.IsNullOrWhiteSpace(line.Description2))
                    {
                        column.Item().Text(line.Description2!);
                    }
                });
                table.Cell().Element(StyleBody).AlignRight().Text(FormatNumber(line.Quantity));
                table.Cell().Element(StyleBody).Text(Value(line.Unit));
                table.Cell().Element(StyleBody).AlignRight().Text(FormatMoney(line.UnitPrice, currency));
                table.Cell().Element(StyleBody).AlignRight().Text(FormatMoney(line.Total, currency));
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
                SummaryAmount(row.ConstantItem(105)).Text(FormatMoney(subtotal, currency)).Bold();
            });
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Moms 25%");
                SummaryAmount(row.ConstantItem(105)).Text(FormatMoney(vat, currency));
            });
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Summa inkl. moms");
                SummaryAmount(row.ConstantItem(105)).Text(FormatMoney(total, currency));
            });
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Öresutjämning");
                SummaryAmount(row.ConstantItem(105)).Text(FormatMoney(rounding, currency));
            });
        });
    }

    private static IContainer SummaryAmount(IContainer container) =>
        container.PaddingRight(AmountRightInset).AlignRight();

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

    private static IContainer HeaderCell(IContainer container) => container
        .PaddingVertical(7)
        .PaddingLeft(6)
        .PaddingRight(AmountRightInset)
        .DefaultTextStyle(style => style.Bold());

    private static IContainer EmptyHeaderCell(IContainer container) => container
        .PaddingVertical(7)
        .PaddingHorizontal(6);

    private static IContainer BodyCell(IContainer container, bool printForScanning) => container
        .BorderBottom(0.5f)
        .BorderColor(printForScanning ? "#000000" : "#dbe4e2")
        .PaddingVertical(6)
        .PaddingRight(AmountRightInset);

    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "" : value;

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
