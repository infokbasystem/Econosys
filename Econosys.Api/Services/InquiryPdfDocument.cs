using System.Globalization;
using Econosys.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static Econosys.Api.Services.DocumentPdfLayout;

namespace Econosys.Api.Services;

public static class InquiryPdfDocument
{
    public sealed record InquiryLine(string Quantity, string Info);
    public sealed record Content(string PriceHeading, string ProductNote, IReadOnlyList<InquiryLine> Lines);

    public static string? RecipientLanguageCode(Inquiry inquiry)
    {
        var recipientCode = inquiry.SelectedInquiryRecipient?.Language?.LanguageCode;
        return string.IsNullOrWhiteSpace(recipientCode)
            ? inquiry.SelectedInquiryRecipient?.Supplier?.Language?.LanguageCode
            : recipientCode;
    }

    public static Content BuildContent(Inquiry inquiry, DocumentPdfTranslations texts)
    {
        var rows = inquiry.InquiryRows.OrderBy(row => row.Id).ToList();
        var calculation = inquiry.Calculation ?? rows.Select(row => row.CalculationRow?.Calculation)
            .FirstOrDefault(value => value != null);
        var currency = calculation?.PurchaseCurrency;
        var unit = calculation?.Unit;
        var currencyName = texts.Text(currency?.TranslationCode ?? 0, currency?.Name ?? currency?.SpcsKey ?? "SEK");
        var unitName = texts.Text(unit?.TranslationCode ?? 0, unit?.Name ?? texts.Text(-101));
        var productNote = calculation?.Product?.NoteExternal ?? "";
        if (calculation is { PalletLength: > 0, PalletWidth: > 0, PalletHeight: > 0, EditionPerPallet: > 0 })
        {
            var packing = $"{calculation.PalletLength}x{calculation.PalletWidth}x{calculation.PalletHeight}mm "
                + $"{texts.Number(calculation.EditionPerPallet)}{texts.Text(-101)}/{texts.Text(DocumentPdfTranslations.PalletUnit)}";
            if (!productNote.Contains(packing, StringComparison.OrdinalIgnoreCase))
            {
                productNote = string.IsNullOrWhiteSpace(productNote) ? packing : packing + "\n" + productNote;
            }
        }

        return new Content($"{currencyName}/{unitName}", productNote,
            rows.Select(row => new InquiryLine(texts.Number(row.Edition), row.CalculationRow?.Info ?? "")).ToList());
    }

    public static byte[] Generate(Inquiry inquiry, CompanyInfo? company, DocumentPdfTranslations texts) =>
        CreateDocument(inquiry, company, texts).GeneratePdf();

    public static IDocument CreateDocument(Inquiry inquiry, CompanyInfo? company, DocumentPdfTranslations texts)
    {
        var content = BuildContent(inquiry, texts);
        var recipient = inquiry.SelectedInquiryRecipient;
        return Document.Create(document => document.Page(page =>
        {
            Configure(page);
            page.Header().PaddingBottom(24).Row(row =>
            {
                row.RelativeItem().Element(Logo);
                row.ConstantItem(190).PaddingTop(14).AlignRight().Column(header =>
                {
                    header.Item().AlignRight().Text($"{texts.Text(-28)}: {Date(inquiry.CreatedAt)}");
                    if (inquiry.AnswerDueDate.HasValue)
                    {
                        header.Item().PaddingTop(5).AlignRight().Text($"{texts.Text(-47)}: {Date(inquiry.AnswerDueDate)}");
                    }
                });
            });
            page.Content().Column(column =>
            {
                if (!inquiry.HideCustomerInfoOnPrint)
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Element(container => Address(container, texts.Text(-90),
                            inquiry.CustomerName, inquiry.Customer?.Address, inquiry.Customer?.Address2,
                            Join(inquiry.Customer?.PostalNr, inquiry.Customer?.PostalAddress), inquiry.Customer?.Country));
                        row.RelativeItem().Element(container => Address(container, texts.Text(-49),
                            inquiry.DeliveryAddressName, inquiry.DeliveryAddress,
                            Join(inquiry.DeliveryPostalNr, inquiry.DeliveryPostalAddress), inquiry.DeliveryCountry));
                    });
                }

                column.Item().PaddingTop(30).BorderBottom(0.5f).BorderColor("#dbe4e2").PaddingBottom(5).Row(row =>
                {
                    row.RelativeItem().Column(references =>
                    {
                        references.Spacing(5);
                        references.Item().Element(container => Field(container, texts.Text(-10), inquiry.OurRef));
                        references.Item().Element(container => Field(container, texts.Text(-8), recipient?.YourRef));
                    });
                    row.ConstantItem(190).AlignRight().Column(title =>
                    {
                        title.Item().AlignRight().Text(texts.Text(-42)).FontSize(16);
                        title.Item().AlignRight().Text(inquiry.Id.ToString(CultureInfo.InvariantCulture)).FontSize(16);
                    });
                });

                column.Item().PaddingTop(12).Row(row =>
                {
                    row.Spacing(18);
                    row.RelativeItem(1.15f).Column(product =>
                    {
                        product.Spacing(6);
                        product.Item().Element(container => Field(container, texts.Text(-1), inquiry.Product));
                        product.Item().Element(container => Field(container, texts.Text(-2), inquiry.Format));
                        product.Item().Element(container => Field(container, texts.Text(-4), inquiry.Construction));
                        var material = Join(inquiry.Material, inquiry.MaterialThickness);
                        if (inquiry.IsFSC)
                        {
                            material += "\n" + texts.Text(-96);
                        }
                        product.Item().Element(container => Field(container, texts.Text(-3), material));
                        product.Item().Element(container => Field(container, texts.Text(-5), inquiry.Color));
                        if (!string.IsNullOrWhiteSpace(content.ProductNote))
                        {
                            product.Item().Element(container => Field(container, texts.Text(DocumentPdfTranslations.Note), content.ProductNote));
                        }
                    });
                    row.RelativeItem().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(0.9f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.2f);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text(texts.Text(-41));
                            header.Cell().Element(HeaderCell).Text(content.PriceHeading);
                            header.Cell().Element(HeaderCell).Text(texts.Text(DocumentPdfTranslations.InquiryInfo));
                        });
                        foreach (var line in content.Lines)
                        {
                            table.Cell().Element(BodyCell).Text(line.Quantity);
                            table.Cell().Element(BodyCell).Text("");
                            table.Cell().Element(BodyCell).Text(line.Info);
                        }
                    });
                });

                column.Item().PaddingTop(20).Column(additions =>
                {
                    additions.Spacing(6);
                    additions.Item().Text(texts.Text(DocumentPdfTranslations.Additions)).Bold();
                    additions.Item().Element(container => Field(container,
                        texts.Text(DocumentPdfTranslations.InquiryToolingCost) + ":", "?", 170));
                    if (!string.IsNullOrWhiteSpace(inquiry.Preparation))
                    {
                        additions.Item().Element(container => Field(container, texts.Text(-44), inquiry.Preparation));
                    }
                    if (!string.IsNullOrWhiteSpace(recipient?.Note))
                    {
                        additions.Item().Text(recipient.Note);
                    }
                });

                column.Item().ExtendVertical().AlignBottom().PaddingTop(24).Column(terms =>
                {
                    terms.Spacing(6);
                    terms.Item().Element(container => Field(container, texts.Text(-37), inquiry.TimeOfDelivery, 100));
                    terms.Item().Element(container => Field(container, texts.Text(-38), recipient?.TermsOfDelivery, 100));
                    terms.Item().Element(container => Field(container, texts.Text(-39), recipient?.TermsOfPayment, 100));
                    terms.Item().PaddingLeft(100).Text(texts.Text(DocumentPdfTranslations.PaymentDisclaimer)).Italic();
                });
            });
            page.Footer().PaddingTop(14).Element(container => Footer(container, company, texts.Text));
        }));
    }

    private static string Date(DateTime? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

    private static string Join(params string?[] values) =>
        string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
}