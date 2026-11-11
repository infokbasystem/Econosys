using System.Globalization;
using Econosys.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Econosys.Api.Services;

public static class QuotationPdfDocument
{
    public sealed record PriceLine(string Quantity, string Price, string Note);
    public sealed record CostLine(string Description, string Amount, string EditionInfo);
    public sealed record Content(string Currency, string PriceHeading,
        IReadOnlyList<PriceLine> Prices, IReadOnlyList<CostLine> Costs);

    public static Content BuildContent(Quotation quotation, DocumentPdfTranslations texts,
        IEnumerable<OrderCost>? orderCosts = null)
    {
        var rows = quotation.QuotationRows.OrderBy(row => row.Id).ToList();
        var calculation = quotation.Calculation ?? rows.Select(row => row.CalculationRow?.Calculation)
            .FirstOrDefault(value => value != null);
        var currency = quotation.SalesCurrency ?? calculation?.SalesCurrency;
        var unit = quotation.Unit ?? calculation?.Unit;
        var currencyName = texts.Text(currency?.TranslationCode ?? 0, currency?.Name ?? currency?.SpcsKey ?? "SEK");
        var unitName = texts.Text(unit?.TranslationCode ?? 0, unit?.Name ?? texts.Text(-101));
        var decimals = Math.Clamp(calculation?.NrOfCalcDecimalsSales ?? unit?.NrOfCalcDecimals ?? 2, 0, 15);
        var prices = rows.Select(row => new PriceLine(
            texts.Number(row.Edition), texts.Number(row.Price, decimals), row.CalculationRow?.Info ?? "")).ToList();
        var costs = new List<CostLine>();

        if (quotation.EurPallet)
        {
            var palletName = texts.Text(quotation.PalletFormat?.TranslationCode ?? 0,
                quotation.PalletFormat?.Name ?? "EUR-pall");
            var known = quotation.NrOfEurPallet.HasValue && quotation.EurPalletValue.HasValue;
            costs.Add(new CostLine(
                $"{texts.Text(-99)} {palletName}" + (known ? "" : $" {texts.Text(-100)}"),
                known ? texts.Money((decimal)quotation.EurPalletValue!.Value * quotation.NrOfEurPallet!.Value, currencyName) : "",
                ""));
        }

        foreach (var cost in (orderCosts ?? quotation.OrderCosts)
            .Where(cost => cost.DoDebit && cost.DoPrintOnQuotation && cost.SupplierOrderId == null)
            .DistinctBy(cost => cost.Id)
            .OrderBy(cost => cost.CalculationRowId).ThenBy(cost => cost.Id))
        {
            var known = cost.OutPrice.HasValue;
            var code = known ? cost.Cost?.TranslationCodeQuotationKnown : cost.Cost?.TranslationCodeQuotationUnknown;
            var fallback = $"{texts.Text(-99)} {cost.Cost?.Name}".TrimEnd()
                + (known ? "" : $" {texts.Text(-100)}");
            var description = texts.Text(code ?? 0, fallback);
            var amount = texts.Money(cost.OutPrice * (cost.NrOf ?? 1m), currencyName);
            if (known && cost.Cost?.IsNrOf == true && cost.NrOf.HasValue)
            {
                amount = $"{cost.NrOf.Value.ToString("0.#####", texts.Culture)} {texts.Text(-101)}"
                    + $" \u00d7 {texts.Money(cost.OutPrice, currencyName)} = {amount}";
            }

            var linkedRow = rows.FirstOrDefault(row => cost.QuotationRowId == row.Id
                || (cost.QuotationRowId == null && cost.CalculationRowId.HasValue
                    && cost.CalculationRowId == row.CalculationRowId));
            var editionInfo = linkedRow == null ? ""
                : $"{texts.Text(-97)}: {texts.Number(linkedRow.Edition)} {texts.Text(-101)}, "
                    + $"{texts.Text(-98)} {texts.Number(linkedRow.Price, decimals)} {currencyName}/{unitName}";
            costs.Add(new CostLine(description, amount, editionInfo));
        }

        return new Content(currencyName, $"{currencyName}/{unitName}", prices, costs);
    }

    public static byte[] Generate(Quotation quotation, CompanyInfo? company, DocumentPdfTranslations texts,
        IEnumerable<OrderCost>? orderCosts = null) =>
        CreateDocument(quotation, company, texts, orderCosts).GeneratePdf();

    public static IDocument CreateDocument(Quotation quotation, CompanyInfo? company, DocumentPdfTranslations texts,
        IEnumerable<OrderCost>? orderCosts = null)
    {
        var content = BuildContent(quotation, texts, orderCosts);
        return Document.Create(document => document.Page(page =>
        {
            DocumentPdfLayout.Configure(page);
            page.Header().PaddingBottom(24).Row(row =>
            {
                row.RelativeItem().Element(DocumentPdfLayout.Logo);
                row.ConstantItem(190).PaddingTop(14).AlignRight().Text(
                    $"{texts.Text(-28)}: {(quotation.Date ?? quotation.CreatedAt)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
            });
            page.Content().Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(container => Address(container, texts.Text(-90),
                        quotation.CustomerName, quotation.Address, quotation.Address2,
                        Join(quotation.PostalNr, quotation.PostalAddress), quotation.Country));
                    if (!quotation.HideDeliveryAddressOnPrint)
                    {
                        row.RelativeItem().Element(container => Address(container, texts.Text(-49),
                            quotation.DeliveryAddressName, quotation.DeliveryAddress,
                            Join(quotation.DeliveryPostalNr, quotation.DeliveryPostalAddress), quotation.DeliveryCountry));
                    }
                    else
                    {
                        row.RelativeItem();
                    }
                });

                column.Item().PaddingTop(30).BorderBottom(0.5f).BorderColor("#dbe4e2").PaddingBottom(5).Row(row =>
                {
                    row.RelativeItem().Column(references =>
                    {
                        references.Spacing(5);
                        references.Item().Element(container => Field(container, texts.Text(-10), quotation.OurReference));
                        references.Item().Element(container => Field(container, texts.Text(-8), quotation.YourReference));
                    });
                    row.ConstantItem(190).AlignRight().Column(title =>
                    {
                        title.Item().AlignRight().Text(texts.Text(-46)).FontSize(16);
                        title.Item().AlignRight().Text(quotation.Id.ToString(CultureInfo.InvariantCulture)).FontSize(16);
                    });
                });

                column.Item().PaddingTop(12).Row(row =>
                {
                    row.Spacing(18);
                    row.RelativeItem(1.15f).Column(product =>
                    {
                        product.Spacing(6);
                        product.Item().Element(container => Field(container, texts.Text(-1), quotation.Product));
                        product.Item().Element(container => Field(container, texts.Text(-2), quotation.Format));
                        product.Item().Element(container => Field(container, texts.Text(-4), quotation.Construction));
                        var material = Join(quotation.Material, quotation.MaterialThickness);
                        if (quotation.IsFsc)
                        {
                            material += "\n" + texts.Text(-96);
                        }
                        product.Item().Element(container => Field(container, texts.Text(-3), material));
                        product.Item().Element(container => Field(container, texts.Text(-5), quotation.Color));
                        if (!string.IsNullOrWhiteSpace(quotation.ProductMessage))
                        {
                            product.Item().Element(container => Field(container, texts.Text(DocumentPdfTranslations.Note), quotation.ProductMessage));
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
                            header.Cell().Element(HeaderCell).Text(texts.Text(DocumentPdfTranslations.Note));
                        });
                        foreach (var price in content.Prices)
                        {
                            table.Cell().Element(BodyCell).Text(price.Quantity);
                            table.Cell().Element(BodyCell).Text(price.Price);
                            table.Cell().Element(BodyCell).Text(price.Note);
                        }
                    });
                });

                column.Item().PaddingTop(20).Column(additions =>
                {
                    additions.Spacing(6);
                    additions.Item().Text(texts.Text(DocumentPdfTranslations.Additions)).Bold();
                    foreach (var cost in content.Costs)
                    {
                        additions.Item().Column(costBlock =>
                        {
                            costBlock.Item().Row(row =>
                            {
                                row.RelativeItem(3).Text(cost.Description);
                                row.RelativeItem(2).AlignRight().Text(cost.Amount);
                            });
                            if (!string.IsNullOrWhiteSpace(cost.EditionInfo))
                            {
                                costBlock.Item().PaddingTop(2).Text(cost.EditionInfo).FontSize(8).Italic();
                            }
                        });
                    }
                    additions.Item().Text(texts.Text(DocumentPdfTranslations.ToolingDisclaimer)).Italic();
                    if (!string.IsNullOrWhiteSpace(quotation.Message))
                    {
                        additions.Item().PaddingTop(6).Text(quotation.Message);
                    }
                });

                column.Item().ExtendVertical().AlignBottom().PaddingTop(24).Column(terms =>
                {
                    terms.Spacing(6);
                    terms.Item().Element(container => Field(container, texts.Text(-37), quotation.TimeOfDelivery, 100));
                    terms.Item().Element(container => Field(container, texts.Text(-38), quotation.TermsOfDelivery, 100));
                    terms.Item().Element(container => Field(container, texts.Text(-39), quotation.TermsOfPayment, 100));
                    terms.Item().PaddingLeft(100).Text(texts.Text(DocumentPdfTranslations.PaymentDisclaimer)).Italic();
                    terms.Item().Text(texts.Text(-50)).FontSize(8);
                    var validity = texts.Text(-93);
                    if (!string.IsNullOrWhiteSpace(validity))
                    {
                        terms.Item().Text(validity).FontSize(8);
                    }
                });
            });
            page.Footer().PaddingTop(14).Element(container => DocumentPdfLayout.Footer(container, company, texts.Text));
        }));
    }

    private static void Field(IContainer container, string label, string? value, float labelWidth = 72) =>
        DocumentPdfLayout.Field(container, label, value, labelWidth);

    private static void Address(IContainer container, string heading, params string?[] lines) =>
        DocumentPdfLayout.Address(container, heading, lines);

    private static string Join(params string?[] values) =>
        string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static IContainer HeaderCell(IContainer container) => DocumentPdfLayout.HeaderCell(container);

    private static IContainer BodyCell(IContainer container) => DocumentPdfLayout.BodyCell(container);
}