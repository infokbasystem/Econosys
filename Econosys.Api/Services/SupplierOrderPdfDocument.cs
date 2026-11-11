using System.Globalization;
using Econosys.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static Econosys.Api.Services.DocumentPdfLayout;

namespace Econosys.Api.Services;

public static class SupplierOrderPdfDocument
{
    public sealed record CostLine(string Description, string Amount);
    public sealed record Content(string PriceHeading, string Quantity, string Price, string GoodsMarking,
        string DesiredDelivery, string Confirmation, string PalletInfo, IReadOnlyList<CostLine> Costs);

    public static Content BuildContent(SupplierOrder order, DocumentPdfTranslations texts, PalletFormat? palletFormat = null)
    {
        var calculation = order.SelectedCalculationRow?.Calculation;
        var currency = order.PurchaseCurrency ?? calculation?.PurchaseCurrency;
        var unit = order.Unit ?? calculation?.Unit;
        var currencyName = texts.Text(currency?.TranslationCode ?? 0, currency?.Name ?? currency?.SpcsKey ?? "SEK");
        var unitName = texts.Text(unit?.TranslationCode ?? 0, unit?.Name ?? texts.Text(-101));
        var decimals = Math.Clamp(calculation?.NrOfCalcDecimalsPurchase ?? unit?.NrOfCalcDecimals ?? 2, 0, 15);
        var automaticMarking = order.HideProductNameOnPrint || order.HideCustomerInfoOnPrint || order.HideCustomerNameOnPrint;
        var marking = automaticMarking
            ? string.Join("\n", NonEmpty(order.HideProductNameOnPrint ? order.Product : null,
                order.HideCustomerInfoOnPrint && order.Supplier?.SupplierOrderTemplateNr != 1 ? order.CustomerOrderNr : null,
                order.HideCustomerNameOnPrint ? order.Customer?.Name : null))
            : order.GoodsMarking ?? "";
        var delivery = order.DeliveryDate.HasValue
            ? Date(order.DeliveryDate.Value, order.DeliveryDateWeekMode, texts)
            : order.TimeOfDelivery ?? "";
        if (!string.IsNullOrWhiteSpace(delivery) && order.Inventory?.NoInventoryValue == true)
        {
            delivery = texts.Text(DocumentPdfTranslations.CallOffFrom) + " " + delivery;
        }
        var confirmation = order.Confirmed ? texts.Text(DocumentPdfTranslations.ConfirmedStatus) : "";
        if (order.Confirmed && order.ConfirmedDeliveryDate.HasValue)
        {
            confirmation += "\n" + Date(order.ConfirmedDeliveryDate.Value, order.ConfirmedDeliveryDateWeekMode, texts);
        }
        var costs = order.OrderCosts.Where(cost => cost.DoPrintOnSupplierOrder)
            .OrderBy(cost => cost.Id).Select(cost =>
            {
                var known = cost.InPrice.HasValue;
                var code = known ? cost.Cost?.TranslationCodeSupplierOrderKnown : cost.Cost?.TranslationCodeSupplierOrderUnknown;
                var fallback = $"{texts.Text(-99)} {cost.Cost?.Name}".TrimEnd()
                    + (known ? "" : $" {texts.Text(-100)}");
                var costCurrency = texts.Text(cost.InPriceCurrency?.TranslationCode ?? 0,
                    cost.InPriceCurrency?.Name ?? cost.InPriceCurrency?.SpcsKey ?? currencyName);
                var amount = texts.Money(cost.InPrice, costCurrency);
                if (known && cost.Cost?.IsNrOf == true && cost.NrOf.HasValue)
                {
                    amount = $"{cost.NrOf.Value.ToString("0.#####", texts.Culture)} {texts.Text(-101)} \u00d7 {amount}";
                }
                return new CostLine(texts.Text(code ?? 0, fallback), amount);
            }).ToList();
        var palletInfo = palletFormat != null
            ? texts.Text(palletFormat.TranslationCode ?? 0, palletFormat.Name ?? "")
            : string.Join("\n", NonEmpty(order.EurPallet ? texts.Text(-66) : null,
                order.OneWayPallet ? texts.Text(-78) : null, order.IsNonStopPallet ? texts.Text(-79) : null));
        if (order.IsParcelDelivery)
        {
            palletInfo = string.Join("\n", NonEmpty(palletInfo, texts.Text(-95)));
        }

        return new Content($"{currencyName}/{unitName}", texts.Number(order.Edition),
            texts.Number(order.PurchasePrice, decimals), marking, delivery, confirmation, palletInfo, costs);
    }

    public static byte[] Generate(SupplierOrder order, CompanyInfo? company, DocumentPdfTranslations texts,
        PalletFormat? palletFormat = null, string? previousOrderNumber = null) =>
        CreateDocument(order, company, texts, palletFormat, previousOrderNumber).GeneratePdf();

    public static IDocument CreateDocument(SupplierOrder order, CompanyInfo? company, DocumentPdfTranslations texts,
        PalletFormat? palletFormat = null, string? previousOrderNumber = null)
    {
        var content = BuildContent(order, texts, palletFormat);
        var number = order.SupplierOrderNr ?? order.Id.ToString(CultureInfo.InvariantCulture);
        if (order.Supplier?.SupplierOrderTemplateNr == 1 && !string.IsNullOrWhiteSpace(order.CustomerOrderNr))
        {
            number += " / " + order.CustomerOrderNr;
        }

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
                if (!order.HideCustomerInfoOnPrint)
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Element(container => Address(container, texts.Text(-90),
                            order.HideCustomerNameOnPrint ? null : order.Customer?.Name,
                            order.Customer?.Address, order.Customer?.Address2,
                            Join(order.Customer?.PostalNr, order.Customer?.PostalAddress), order.Customer?.Country));
                        row.RelativeItem().Element(container => Address(container, texts.Text(-49),
                            order.HideCustomerNameOnPrint ? null : order.DeliveryAddressName, order.DeliveryAddress,
                            Join(order.DeliveryPostalNr, order.DeliveryPostalAddress), order.DeliveryCountry));
                    });
                }

                column.Item().PaddingTop(30).BorderBottom(0.5f).BorderColor("#dbe4e2").PaddingBottom(8).Row(row =>
                {
                    row.Spacing(8);
                    row.RelativeItem(1.25f).Column(references =>
                    {
                        references.Spacing(6);
                        references.Item().Element(container => Field(container, texts.Text(-10), order.OurReference));
                        references.Item().Element(container => Field(container, texts.Text(-8), order.YourReference));
                    });
                    row.RelativeItem(1.1f).Column(links =>
                    {
                        links.Spacing(6);
                        links.Item().Element(container => Field(container, texts.Text(DocumentPdfTranslations.InquiryReference),
                            order.InquiryId?.ToString(CultureInfo.InvariantCulture), 88));
                        links.Item().Element(container => Field(container, texts.Text(-43), previousOrderNumber, 88));
                    });
                    row.RelativeItem(0.85f).AlignRight().Column(title =>
                    {
                        title.Item().AlignRight().Text(texts.Text(-45)).FontSize(16);
                        title.Item().AlignRight().Text(number).FontSize(16);
                    });
                });

                column.Item().PaddingTop(12).Row(row =>
                {
                    row.Spacing(18);
                    row.RelativeItem(1.15f).Column(product =>
                    {
                        product.Spacing(6);
                        product.Item().Element(container => Field(container, texts.Text(-1), order.HideProductNameOnPrint ? null : order.Product));
                        product.Item().Element(container => Field(container, texts.Text(DocumentPdfTranslations.SupplierFormat), order.Format));
                        product.Item().Element(container => Field(container, texts.Text(-4), order.Construction));
                        var material = Join(order.Material, order.MaterialThickness);
                        if (order.IsFSC)
                        {
                            material += "\n" + texts.Text(-96);
                        }
                        product.Item().Element(container => Field(container, texts.Text(-3), material));
                        product.Item().Element(container => Field(container, texts.Text(-5), order.Color));
                        product.Item().Element(container => Field(container, texts.Text(DocumentPdfTranslations.Note), order.Message));
                        product.Item().PaddingTop(4).EnsureSpace(90).CornerRadius(6).Background("#f1f4f3").Padding(12).Column(shipment =>
                        {
                            shipment.Spacing(7);
                            shipment.Item().Element(container => Field(container, texts.Text(DocumentPdfTranslations.DesiredDelivery), content.DesiredDelivery, 95));
                            shipment.Item().PaddingTop(3).Text(texts.Text(-88) + ":").Bold();
                            if (!string.IsNullOrWhiteSpace(order.LoadingInstruction))
                            {
                                shipment.Item().Text(order.LoadingInstruction);
                            }
                            if (!string.IsNullOrWhiteSpace(content.PalletInfo))
                            {
                                shipment.Item().Text(content.PalletInfo);
                            }
                            shipment.Item().PaddingTop(3).Text(texts.Text(-40) + ":").Bold();
                            shipment.Item().Text(content.GoodsMarking);
                        });
                    });
                    row.RelativeItem().Column(pricing =>
                    {
                        pricing.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text(texts.Text(-41));
                                header.Cell().Element(HeaderCell).Text(content.PriceHeading);
                            });
                            table.Cell().Element(BodyCell).Text(content.Quantity);
                            table.Cell().Element(BodyCell).Text(content.Price);
                        });
                        pricing.Item().PaddingTop(18).CornerRadius(6).Background("#f1f4f3").Padding(10).Column(confirmation =>
                        {
                            confirmation.Item().Text(texts.Text(-48) + ":").Bold();
                            confirmation.Item().PaddingTop(4).MinHeight(20).Text(content.Confirmation);
                        });
                    });
                });

                column.Item().PaddingTop(14).Width(280).Column(additions =>
                {
                    additions.Spacing(6);
                    additions.Item().Text(texts.Text(DocumentPdfTranslations.Additions)).Bold();
                    if (!string.IsNullOrWhiteSpace(order.Preparation))
                    {
                        additions.Item().Element(container => Field(container, texts.Text(-44), order.Preparation));
                    }
                    foreach (var cost in content.Costs)
                    {
                        additions.Item().Row(row =>
                        {
                            row.RelativeItem(2).Text(cost.Description);
                            row.RelativeItem().AlignRight().Text(cost.Amount);
                        });
                    }
                    additions.Item().PaddingTop(8).BorderBottom(0.5f).BorderColor("#dbe4e2");
                    additions.Item().PaddingTop(8).Element(container => Field(container, texts.Text(-38), order.TermsOfDelivery, 95));
                    additions.Item().Element(container => Field(container, texts.Text(-39), order.TermsOfPayment, 95));
                });
            });
            page.Footer().PaddingTop(14).Element(container => Footer(container, company, texts.Text));
        }));
    }

    private static string Date(DateTime value, bool weekMode, DocumentPdfTranslations texts) =>
        weekMode ? $"{ISOWeek.GetYear(value)} {texts.Text(DocumentPdfTranslations.WeekLabel)} {ISOWeek.GetWeekOfYear(value):00}"
            : value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static IEnumerable<string> NonEmpty(params string?[] values) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!);

    private static string Join(params string?[] values) => string.Join(" ", NonEmpty(values));
}