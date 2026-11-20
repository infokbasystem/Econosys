using System.Globalization;
using Econosys.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static Econosys.Api.Services.DocumentPdfLayout;
using PdfUnit = QuestPDF.Infrastructure.Unit;

namespace Econosys.Api.Services;

public static class CustomerOrderPdfDocument
{
    public sealed record CostLine(string Description, string Amount);
    public sealed record Content(string PriceHeading, string Quantity, string Price, string Delivery,
        bool ShowToolRetentionText, IReadOnlyList<CostLine> Costs);

    public static Content BuildContent(CustomerOrder order, DocumentPdfTranslations texts,
        PalletFormat? palletFormat = null, Calculation? calculation = null, bool quotationIncludesPallet = false)
    {
        calculation ??= order.SelectedCalculationRow?.Calculation;
        var currency = order.SalesCurrency ?? calculation?.SalesCurrency;
        var unit = order.Unit ?? calculation?.Unit;
        var currencyName = texts.Text(currency?.TranslationCode ?? 0, currency?.Name ?? currency?.SpcsKey ?? "SEK");
        var specialUnit = order.Customer?.SpecialUnitHandling == true;
        var unitName = specialUnit ? texts.Text(-101) : texts.Text(unit?.TranslationCode ?? 0, unit?.Name ?? texts.Text(-101));
        var decimals = specialUnit ? 3 : Math.Clamp(calculation?.NrOfCalcDecimalsSales ?? unit?.NrOfCalcDecimals ?? 2, 0, 15);
        var price = specialUnit ? order.SalesPrice / 1000d : order.SalesPrice;
        var delivery = order.DeliveryDate.HasValue
            ? order.DeliveryDateWeekMode
                ? $"{ISOWeek.GetYear(order.DeliveryDate.Value)} {texts.Text(DocumentPdfTranslations.WeekLabel)} {ISOWeek.GetWeekOfYear(order.DeliveryDate.Value):00}"
                : order.DeliveryDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : order.TimeOfDelivery ?? "";
        if (!string.IsNullOrWhiteSpace(delivery) && (order.IsCallOff || order.SupplierOrder?.Inventory?.IsInventory == true))
        {
            delivery += " " + texts.Text(DocumentPdfTranslations.CustomerCallOff);
        }
        var costs = new List<CostLine>();
        var unitPriceSeparator = $"{texts.Text(-101)}. *";
        if (order.EurPallet && quotationIncludesPallet)
        {
            const string fallback = "Exkl. EUR-pall";
            var palletCode = palletFormat?.TranslationCode ?? 0;
            var known = order.EurPalletValue.HasValue && order.NrOfEurPallet.HasValue;
            var description = palletCode == 0 ? fallback
                : $"{texts.Text(-99)} {texts.Text(palletCode, palletFormat?.Name ?? fallback)}"
                    + (known ? "" : $" {texts.Text(-100)}");
            var amount = "";
            if (order.NrOfEurPallet is short count && count != 0
                && order.EurPalletValue is double unitPrice && unitPrice != 0)
            {
                amount = $"{count.ToString(texts.Culture)} {unitPriceSeparator} {unitPrice.ToString(texts.Culture)}"
                    + $" = {(unitPrice * count).ToString(texts.Culture)} {currencyName}";
            }
            costs.Add(new CostLine(description, amount));
        }

        foreach (var cost in PrintableCosts(order))
        {
            var known = UsesKnownCostText(cost);
            var fallback = $"{texts.Text(-99)} {cost.Cost?.Name}".TrimEnd()
                + (known ? "" : $" {texts.Text(-100)}");
            var isDmt = cost.Cost?.DmtFixed.HasValue == true || cost.Cost?.DmtPercent.HasValue == true;
            var amount = "";
            if (!isDmt && cost.OutPrice is decimal outPrice)
            {
                amount = $"{(outPrice * (cost.NrOf ?? 1m)).ToString("0", texts.Culture)} {currencyName}";
                if (cost.Cost?.IsNrOf == true)
                {
                    amount = $"{cost.NrOf.GetValueOrDefault().ToString("0", texts.Culture)} {unitPriceSeparator}"
                        + $" {outPrice.ToString("0", texts.Culture)} = {amount}";
                }
            }
            costs.Add(new CostLine(texts.Text(CostTranslationCode(cost) ?? 0, fallback), amount));
        }

        return new Content($"{currencyName}/{unitName}", texts.Number(order.Edition), texts.Number(price, decimals), delivery,
            order.OrderCosts.Any(cost => cost.DoDebit && cost.Cost?.DoPrintScrapToolsTextOnCustomerOrder == true), costs);
    }

    internal static IEnumerable<OrderCost> PrintableCosts(CustomerOrder order) =>
        order.OrderCosts.Where(cost => cost.DoDebit && cost.DoPrintOnCustomerOrder).OrderBy(cost => cost.Id);

    private static bool UsesKnownCostText(OrderCost cost) => cost.OutPrice.GetValueOrDefault() != 0;

    internal static int? CostTranslationCode(OrderCost cost) =>
        UsesKnownCostText(cost) ? cost.Cost?.TranslationCodeCustomerOrderKnown : cost.Cost?.TranslationCodeCustomerOrderUnknown;

    public static byte[] Generate(CustomerOrder order, CompanyInfo? company, DocumentPdfTranslations texts,
        PalletFormat? palletFormat = null, Calculation? calculation = null, bool quotationIncludesPallet = false) =>
        CreateDocument(order, company, texts, palletFormat, calculation, quotationIncludesPallet).GeneratePdf();

    public static IDocument CreateDocument(CustomerOrder order, CompanyInfo? company, DocumentPdfTranslations texts,
        PalletFormat? palletFormat = null, Calculation? calculation = null, bool quotationIncludesPallet = false)
    {
        var content = BuildContent(order, texts, palletFormat, calculation, quotationIncludesPallet);
        return Document.Create(document => document.Page(page =>
        {
            Configure(page);
            page.Header().PaddingBottom(24).Row(row =>
            {
                row.RelativeItem().Element(Logo);
                row.ConstantItem(190).PaddingTop(14).AlignRight().Text(
                    $"{texts.Text(-28)}: {(order.Date ?? order.Created)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
            });
            page.Content().Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(container => Address(container, texts.Text(-90),
                        order.CustomerName, order.Address, order.Address2,
                        Join(order.PostalNr, order.PostalAddress), order.Country));
                    row.RelativeItem().Element(container => Address(container, texts.Text(-49),
                        order.DeliveryAddressName, order.DeliveryAddress,
                        Join(order.DeliveryPostalNr, order.DeliveryPostalAddress), order.DeliveryCountry));
                });

                column.Item().PaddingTop(30).BorderBottom(0.5f).BorderColor("#dbe4e2").PaddingBottom(6).Row(row =>
                {
                    row.RelativeItem().Column(references =>
                    {
                        references.Spacing(5);
                        references.Item().Element(container => Field(container, texts.Text(-10), order.OurReference));
                        references.Item().Element(container => Field(container, texts.Text(-8), order.YourReference));
                        references.Item().Element(container => Field(container, texts.Text(-9), order.YourOrderNr));
                    });
                    row.ConstantItem(190).AlignRight().Column(title =>
                    {
                        title.Item().AlignRight().Text(texts.Text(-27)).FontSize(16);
                        title.Item().AlignRight().Text(order.CustomerOrderNr ?? order.Id.ToString(CultureInfo.InvariantCulture)).FontSize(16);
                    });
                });

                column.Item().PaddingTop(12).Row(row =>
                {
                    row.Spacing(18);
                    row.RelativeItem(1.15f).Column(product =>
                    {
                        product.Spacing(6);
                        product.Item().Element(container => Field(container, texts.Text(-1), order.Product));
                        product.Item().Element(container => Field(container, texts.Text(DocumentPdfTranslations.SupplierFormat), order.Format));
                        product.Item().Element(container => Field(container, texts.Text(-4), order.Construction));
                        var material = Join(order.Material, order.MaterialThickness);
                        if (order.IsFSC)
                        {
                            material += "\n" + texts.Text(-96);
                        }
                        product.Item().Element(container => Field(container, texts.Text(-3), material));
                        product.Item().Element(container => Field(container, texts.Text(-5), order.Color));
                        product.Item().Element(container => Field(container, texts.Text(DocumentPdfTranslations.Note), order.ProductMessage));
                    });
                    row.RelativeItem().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });
                        table.Header(header =>
                        {
                            header.Cell().Element(LeftPriceHeaderCell).Text(texts.Text(-41));
                            header.Cell().Element(RightPriceHeaderCell).Text(content.PriceHeading);
                        });
                        table.Cell().Element(BodyCell).Text(content.Quantity);
                        table.Cell().Element(BodyCell).Text(content.Price);
                    });
                });

                column.Item().PaddingTop(24).Width(300).BorderTop(0.5f).BorderColor("#dbe4e2").PaddingTop(10).Column(additions =>
                {
                    additions.Spacing(6);
                    additions.Item().Text(texts.Text(DocumentPdfTranslations.Additions)).Bold();
                    foreach (var cost in content.Costs)
                    {
                        additions.Item().Row(row =>
                        {
                            row.RelativeItem(2).Text(cost.Description);
                            row.RelativeItem().AlignRight().Text(cost.Amount);
                        });
                    }
                    if (content.ShowToolRetentionText)
                    {
                        additions.Item().Text(texts.Text(-94)).Italic();
                    }
                    if (!string.IsNullOrWhiteSpace(order.Message))
                    {
                        additions.Item().PaddingTop(4).Text(order.Message);
                    }
                });

                column.Item().ExtendVertical().AlignBottom().PaddingTop(24).PaddingBottom(2, PdfUnit.Centimetre).Column(terms =>
                {
                    terms.Spacing(6);
                    terms.Item().Element(container => Field(container, texts.Text(-37), content.Delivery, 95));
                    terms.Item().Element(container => Field(container, texts.Text(-38), order.TermsOfDelivery, 95));
                    terms.Item().Element(container => Field(container, texts.Text(-39), order.TermsOfPayment, 95));
                    terms.Item().PaddingLeft(95).Text(texts.Text(DocumentPdfTranslations.PaymentDisclaimer)).Italic();
                });
            });
            page.Footer().PaddingTop(14).Element(container => Footer(container, company, texts.Text));
        }));
    }

    private static string Join(params string?[] values) =>
        string.Join(" ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
}