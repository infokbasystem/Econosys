using Econosys.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfUnit = QuestPDF.Infrastructure.Unit;

namespace Econosys.Api.Services;

public static class DocumentPdfLayout
{
    private static readonly byte[] LogoBytes = File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "Assets", "Econopack_Logo_Sage.png"));
    private static readonly byte[] WatermarkBytes = File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "Assets", "Box_Sage.png"));

    public static void Configure(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.MarginHorizontal(1.7f, PdfUnit.Centimetre);
        page.MarginVertical(1.5f, PdfUnit.Centimetre);
        page.DefaultTextStyle(style => style.FontFamily("DM Sans").FontSize(9).FontColor("#111827"));
        page.Background().AlignBottom().AlignRight().OffsetX(150).OffsetY(-25)
            .Width(320).Image(WatermarkBytes).FitWidth();
    }

    public static void Logo(IContainer container) =>
        container.Height(40).AlignLeft().Image(LogoBytes).FitHeight();

    public static void Field(IContainer container, string label, string? value, float labelWidth = 72)
    {
        container.Row(row =>
        {
            row.ConstantItem(labelWidth).PaddingRight(5).Text(label).Bold();
            row.RelativeItem().Text(value ?? "");
        });
    }

    public static void Address(IContainer container, string heading, params string?[] lines)
    {
        container.Column(column =>
        {
            column.Item().PaddingBottom(3).Text(heading).Bold();
            foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
            {
                column.Item().Text(line!);
            }
        });
    }

    public static IContainer HeaderCell(IContainer container) =>
        container.Background("#f1f4f3").Padding(5).DefaultTextStyle(style => style.Bold().FontSize(8));

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

    public static void InvoiceFooter(IContainer container, CompanyInfo? company,
        Func<int, string, string>? translate = null)
    {
        string Label(int code, string fallback) => translate?.Invoke(code, fallback) ?? fallback;

        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(string.Join(" ", NonEmpty(company?.Address, company?.ZipCode, company?.PostalAddress, "\u2022", company?.Telephone1)));
                column.Item().Text(string.Join(" ", NonEmpty("\u2022", company?.Email, "\u2022", company?.Web)));
                column.Item().Text(text =>
                {
                    text.Span(company?.CompanyName ?? "");
                    text.Span(" " + Label(DocumentPdfTranslations.TaxApproval, "\u00e4r godk\u00e4nd f\u00f6r F-skatt.")).Italic();
                });
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

    public static Telerik.Reporting.PageFooterSection ContactReportFooter(CompanyInfo? company,
        Func<int, string, string>? translate = null)
    {
        var lines = ContactLines(company, translate);
        var footer = new Telerik.Reporting.PageFooterSection
        {
            Height = Telerik.Reporting.Drawing.Unit.Cm(1)
        };
        footer.Items.Add(new Telerik.Reporting.TextBox
        {
            Name = "CompanyContactFooter",
            Value = lines.Address + "\n" + lines.Contact,
            Location = new Telerik.Reporting.Drawing.PointU(
                Telerik.Reporting.Drawing.Unit.Cm(0), Telerik.Reporting.Drawing.Unit.Cm(0)),
            Size = new Telerik.Reporting.Drawing.SizeU(
                Telerik.Reporting.Drawing.Unit.Cm(18), Telerik.Reporting.Drawing.Unit.Cm(0.8)),
            CanGrow = true,
            Style = { Font = { Name = "Arial", Size = Telerik.Reporting.Drawing.Unit.Point(8) } }
        });
        return footer;
    }

    private static IEnumerable<string> NonEmpty(params string?[] values) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!);
}