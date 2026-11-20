using Econosys.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using PdfUnit = QuestPDF.Infrastructure.Unit;

namespace Econosys.Api.Services;

public static class DocumentPdfLayout
{
    private static readonly byte[] LogoBytes = File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "Assets", "Econopack_Logo_Sage.png"));
    private static readonly Lazy<byte[]> BlackLogoBytes = new(CreateBlackLogo);
    private static readonly byte[] WatermarkBytes = File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "Assets", "Box_Sage.png"));

    public static void Configure(PageDescriptor page, bool printForScanning = false)
    {
        page.Size(PageSizes.A4);
        page.MarginHorizontal(0.85f, PdfUnit.Centimetre);
        page.MarginVertical(1.5f, PdfUnit.Centimetre);
        page.DefaultTextStyle(style => style.FontFamily("DM Sans").FontSize(9)
            .FontColor(printForScanning ? "#000000" : "#111827"));
        if (!printForScanning)
        {
            page.Background().AlignBottom().AlignRight().OffsetX(150).OffsetY(-25)
                .Width(320).Image(WatermarkBytes).FitWidth();
        }
    }

    public static void Logo(IContainer container) => Logo(container, printForScanning: false);

    public static void Logo(IContainer container, bool printForScanning) =>
        container.Height(40).AlignLeft().Image(printForScanning ? BlackLogoBytes.Value : LogoBytes).FitHeight();

    private static byte[] CreateBlackLogo()
    {
        using var source = SKBitmap.Decode(LogoBytes)
            ?? throw new InvalidOperationException("Could not decode the company logo for scanning.");
        using var blackLogo = new SKBitmap(source.Width, source.Height);
        using var canvas = new SKCanvas(blackLogo);
        using var ink = SKColorFilter.CreateBlendMode(SKColors.Black, SKBlendMode.SrcIn);
        using var paint = new SKPaint { ColorFilter = ink };
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(source, 0, 0, paint);
        canvas.Flush();
        using var png = blackLogo.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Could not encode the company logo for scanning.");
        return png.ToArray();
    }

    public static void Field(IContainer container, string label, string? value, float labelWidth = 72)
    {
        container.Row(row =>
        {
            row.ConstantItem(labelWidth).PaddingRight(5).Text(label).Bold();
            row.RelativeItem().Text(value ?? "");
        });
    }

    public static void Address(IContainer container, string? heading, params string?[] lines)
    {
        container.Column(column =>
        {
            if (!string.IsNullOrWhiteSpace(heading))
            {
                column.Item().PaddingBottom(3).Text(heading).Bold();
            }
            foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
            {
                column.Item().Text(line!);
            }
        });
    }

    public static IContainer HeaderCell(IContainer container) =>
        container.Background("#f1f4f3").Padding(5).DefaultTextStyle(style => style.Bold().FontSize(8));

    private const float PriceHeaderCornerRadius = 6;

    public static IContainer LeftPriceHeaderCell(IContainer container) =>
        HeaderCell(container.CornerRadiusTopLeft(PriceHeaderCornerRadius).CornerRadiusBottomLeft(PriceHeaderCornerRadius));

    public static IContainer RightPriceHeaderCell(IContainer container) =>
        HeaderCell(container.CornerRadiusTopRight(PriceHeaderCornerRadius).CornerRadiusBottomRight(PriceHeaderCornerRadius));

    public static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(0.4f).BorderColor("#dbe4e2").PaddingVertical(3).PaddingHorizontal(5);

    public static (string Address, string Contact) ContactLines(CompanyInfo? company,
        Func<int, string, string>? translate = null)
    {
        if (company == null)
        {
            return ("", "");
        }

        var postalAddress = string.Join(" ", NonEmpty(company.ZipCode, company.PostalAddress));
        var address = string.Join(", ", NonEmpty(company.Address, postalAddress));
        var telephone = string.IsNullOrWhiteSpace(company.Telephone2) ? company.Telephone1 : company.Telephone2;
        var firstLine = string.Join(" \u2022 ", NonEmpty(address,
            translate?.Invoke(DocumentPdfTranslations.CompanyCountry, "Sverige") ?? "Sverige",
            string.IsNullOrWhiteSpace(telephone) ? null : "T " + telephone));
        var contacts = string.Join(" \u2022 ", NonEmpty(company.Email, company.Web));
        return (firstLine, string.IsNullOrWhiteSpace(contacts) ? "" : "\u2022 " + contacts);
    }

    public static void Footer(IContainer container, CompanyInfo? company,
        Func<int, string, string>? translate = null)
    {
        var lines = ContactLines(company, translate);
        container.DefaultTextStyle(style => style.FontSize(8)).Column(column =>
        {
            column.Item().Text(lines.Address);
            column.Item().Text(lines.Contact);
        });
    }

    public static void InvoiceFooter(IContainer container, CompanyInfo? company, string? vatInfo,
        Func<int, string, string>? translate = null, bool printForScanning = false)
    {
        string Label(int code, string fallback) => translate?.Invoke(code, fallback) ?? fallback;
        var lines = ContactLines(company, translate);

        container.DefaultTextStyle(style => style.FontColor(printForScanning ? "#000000" : "#4B5563")).Row(row =>
        {
            row.RelativeItem().DefaultTextStyle(style => style.FontSize(8)).Column(column =>
            {
                if (lines.Address.Length > 0)
                    column.Item().Text(lines.Address);
                if (lines.Contact.Length > 0)
                    column.Item().Text(lines.Contact);
                if (!string.IsNullOrWhiteSpace(vatInfo))
                {
                    column.Item().Text(vatInfo).Italic();
                }
            });
            row.ConstantItem(270).AlignRight().Column(column =>
            {
                column.Item().AlignRight().Text(text =>
                {
                    text.Span(Label(-24, "Bank") + ": ").Bold();
                    text.Span(company?.Bank ?? "");
                });
                column.Item().AlignRight().Text(text =>
                {
                    text.Span(Label(-25, "Bankgiro") + ": ").Bold();
                    text.Span(company?.BG ?? "");
                });
                column.Item().AlignRight().Row(line =>
                {
                    line.AutoItem().Text(text =>
                    {
                        text.Span(Label(DocumentPdfTranslations.VatNumber, "VAT-nr") + ": ").Bold();
                        text.Span(company?.VATNr ?? "");
                    });
                    line.ConstantItem(18);
                    line.AutoItem().Text(text =>
                    {
                        text.Span("BIC: ").Bold();
                        text.Span(company?.BIC ?? "");
                    });
                });
                column.Item().AlignRight().Text(text =>
                {
                    text.Span("IBAN: ").Bold();
                    text.Span(company?.IBAN ?? "");
                });
            });
        });
    }

    private static IEnumerable<string> NonEmpty(params string?[] values) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!);
}