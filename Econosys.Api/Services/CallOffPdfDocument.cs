using System.Globalization;
using Econosys.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static Econosys.Api.Services.DocumentPdfLayout;

namespace Econosys.Api.Services;

public static class CallOffPdfDocument
{
    public sealed record DeliveryLine(int Id, string CustomerOrderNumber, string Note, string ProductName,
        string InventoryName, string DeliveryAddressFreeText, int NumberOfPallets, double NumberOfItems,
        decimal NumberOfPalletPlaces, string PalletFormatName, string PackageFormat,
        string UnitPrice, string Price, string Currency, decimal? LineTotal);

    public sealed record Content(string DeliveryAddress, string DebitFreight, decimal TotalNumberOfPallets,
        decimal TotalNumberOfPalletPlaces, IReadOnlyList<DeliveryLine> Deliveries, decimal? TotalPrice,
        string TotalPriceCurrency);

    public static Content BuildContent(CallOff callOff, IReadOnlyList<CallOffDelivery> callOffDeliveries,
        IReadOnlyDictionary<int, string> palletFormatNames, DocumentPdfTranslations texts)
    {
        var lines = callOffDeliveries
            .OrderBy(item => item.SortOrder ?? int.MaxValue)
            .ThenBy(item => item.Id)
            .Select(item => MapDelivery(item, palletFormatNames, texts))
            .ToList();
        var address = callOff.CustomerDeliveryAddress;
        var deliveryAddress = address == null
            ? string.Empty
            : string.Join(Environment.NewLine, NonEmpty(address.Customer?.Name, address.Name, address.Address,
                string.Join(" ", NonEmpty(address.PostalNr, address.PostalAddress))));
        var debitFreight = callOff.DoDebitFreight
            ? $"JA: {(callOff.FreightCostToDebit ?? 0m).ToString("N2", texts.Culture)} kr"
            : string.Empty;

        var pricedLines = lines.Where(item => item.LineTotal.HasValue).ToList();
        var currencies = pricedLines.Select(item => item.Currency).Distinct(StringComparer.Ordinal).ToList();
        decimal? totalPrice = pricedLines.Count > 0 && currencies.Count == 1
            ? pricedLines.Sum(item => item.LineTotal!.Value)
            : null;

        return new Content(deliveryAddress, debitFreight,
            lines.Sum(item => item.NumberOfPallets),
            lines.Sum(item => item.NumberOfPalletPlaces),
            lines,
            totalPrice,
            currencies.Count == 1 ? currencies[0] : string.Empty);
    }

    public static byte[] GenerateFreightOrder(CallOff callOff, CompanyInfo? company, DocumentPdfTranslations texts,
        IReadOnlyList<CallOffDelivery> deliveries, IReadOnlyDictionary<int, string> palletFormatNames) =>
        CreateFreightOrderDocument(callOff, company, texts, deliveries, palletFormatNames).GeneratePdf();

    public static IDocument CreateFreightOrderDocument(CallOff callOff, CompanyInfo? company,
        DocumentPdfTranslations texts, IReadOnlyList<CallOffDelivery> deliveries,
        IReadOnlyDictionary<int, string> palletFormatNames)
    {
        var content = BuildContent(callOff, deliveries, palletFormatNames, texts);
        return Document.Create(document =>
        {
            document.Page(page => ComposePage(page, callOff, content, company, texts,
                "Fraktbeställning / avrop från lager", showPrices: false));
            document.Page(page => ComposePage(page, callOff, content, company, texts,
                "Följesedel", showPrices: false));
        });
    }

    public static byte[] GenerateConfirmation(CallOff callOff, CompanyInfo? company, DocumentPdfTranslations texts,
        IReadOnlyList<CallOffDelivery> deliveries, IReadOnlyDictionary<int, string> palletFormatNames) =>
        CreateConfirmationDocument(callOff, company, texts, deliveries, palletFormatNames).GeneratePdf();

    public static IDocument CreateConfirmationDocument(CallOff callOff, CompanyInfo? company,
        DocumentPdfTranslations texts, IReadOnlyList<CallOffDelivery> deliveries,
        IReadOnlyDictionary<int, string> palletFormatNames)
    {
        var content = BuildContent(callOff, deliveries, palletFormatNames, texts);
        return Document.Create(document => document.Page(page => ComposePage(page, callOff, content, company, texts,
            "Avropsbekräftelse", showPrices: true)));
    }

    private static void ComposePage(PageDescriptor page, CallOff callOff, Content content, CompanyInfo? company,
        DocumentPdfTranslations texts, string title, bool showPrices)
    {
        Configure(page);
        page.Header().PaddingBottom(22).Row(row =>
        {
            row.RelativeItem().Element(Logo);
            row.ConstantItem(190).PaddingTop(14).AlignRight().Text(
                $"{texts.Text(-28)}: {callOff.CreatedDateTime?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        });
        page.Content().Column(column =>
        {
            column.Spacing(10);
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(title).FontSize(16).Bold();
                row.ConstantItem(110).AlignRight().Text(callOff.Id.ToString(CultureInfo.InvariantCulture))
                    .FontSize(16).Bold();
            });
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(fields =>
                {
                    fields.Spacing(5);
                    fields.Item().Element(container => Field(container, "Speditör", callOff.Shipper?.Name, 82));
                    fields.Item().Element(container => Field(container, "Vår referens", callOff.Reference, 82));
                    if (!string.IsNullOrWhiteSpace(callOff.Note))
                    {
                        fields.Item().Element(container => Field(container, "Märkning", callOff.Note, 82));
                    }
                    fields.Item().Element(container => Field(container, "Frakt debit.", content.DebitFreight, 82));
                    fields.Item().Element(container => Field(container, "Lev.datum", FormatDate(callOff.DeliveryDate), 82));
                });
                row.RelativeItem().Element(container => Address(container, "Leveransadress",
                    content.DeliveryAddress.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)));
            });

            column.Item().PaddingTop(18).Element(container => AddDeliveryTable(container, content, texts, showPrices));
        });
        page.Footer().PaddingTop(14).Element(container => Footer(container, company, texts.Text));
    }

    private static void AddDeliveryTable(IContainer container, Content content,
        DocumentPdfTranslations texts, bool showPrices)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(showPrices ? 1.35f : 0.9f);
                columns.RelativeColumn(showPrices ? 2.1f : 2.2f);
                columns.RelativeColumn(showPrices ? 2.6f : 3.2f);
                columns.RelativeColumn(0.8f);
                columns.RelativeColumn(1.15f);
                columns.RelativeColumn(0.8f);
                if (showPrices)
                {
                    columns.RelativeColumn(1.7f);
                    columns.RelativeColumn(1.5f);
                }
            });
            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Ordernr");
                header.Cell().Element(HeaderCell).Text("Notering");
                header.Cell().Element(HeaderCell).Text("Artikel");
                header.Cell().Element(HeaderCell).AlignRight().Text("Antal pall");
                header.Cell().Element(HeaderCell).AlignRight().Text("Antal");
                header.Cell().Element(HeaderCell).AlignRight().Text("ppl");
                if (showPrices)
                {
                    header.Cell().Element(HeaderCell).AlignRight().Text("Pris/enh");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Pris");
                }
            });

            foreach (var delivery in content.Deliveries)
            {
                table.Cell().Element(BodyCell).Text(delivery.CustomerOrderNumber);
                table.Cell().Element(BodyCell).Column(note =>
                {
                    note.Item().Text(delivery.Note);
                    if (!string.IsNullOrWhiteSpace(delivery.DeliveryAddressFreeText))
                    {
                        note.Item().Text(delivery.DeliveryAddressFreeText);
                    }
                });
                table.Cell().Element(BodyCell).Text(delivery.ProductName);
                table.Cell().Element(BodyCell).AlignRight().Text(delivery.NumberOfPallets.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCell).AlignRight().Text(texts.Number(delivery.NumberOfItems, 1));
                table.Cell().Element(BodyCell).AlignRight().Text(texts.Number((double)delivery.NumberOfPalletPlaces, 1));
                if (showPrices)
                {
                    table.Cell().Element(BodyCell).AlignRight().Text(delivery.UnitPrice);
                    table.Cell().Element(BodyCell).AlignRight().Text(delivery.Price);
                }
            }

            table.Cell().ColumnSpan(3).Element(BodyCell).AlignRight().Text("Total").Bold();
            table.Cell().Element(BodyCell).AlignRight().Text(texts.Number((double)content.TotalNumberOfPallets, 1)).Bold();
            table.Cell().Element(BodyCell).Text(string.Empty);
            table.Cell().Element(BodyCell).AlignRight().Text(texts.Number((double)content.TotalNumberOfPalletPlaces, 1)).Bold();
            if (showPrices)
            {
                table.Cell().Element(BodyCell).Text(string.Empty);
                table.Cell().Element(BodyCell).AlignRight().Text(texts.Money(content.TotalPrice,
                    content.TotalPriceCurrency)).Bold();
            }
        });
    }

    private static DeliveryLine MapDelivery(CallOffDelivery item,
        IReadOnlyDictionary<int, string> palletFormatNames, DocumentPdfTranslations texts)
    {
        var stockDelivery = item.DeliveryFromStock;
        var order = stockDelivery?.CustomerOrder;
        var customer = order?.Customer;
        var numberOfPallets = stockDelivery?.NrOfPallets ?? 0;
        var numberOfItems = stockDelivery?.NrOfItems ?? 0;
        var numberOfPalletPlaces = item.NrOfPalletPlaces
            ?? numberOfPallets / (stockDelivery?.PalletIsStackable == true ? 2m : 1m);
        var palletFormatName = order?.PalletFormatId is int palletFormatId
            && palletFormatNames.TryGetValue(palletFormatId, out var palletFormatNameValue)
                ? palletFormatNameValue
                : string.Empty;
        var packageFormat = stockDelivery == null
            ? string.Empty
            : $"{stockDelivery.PalletLength ?? 0} x {stockDelivery.PalletWidth ?? 0} x {stockDelivery.PalletHeight ?? 0}";
        var note = string.Join(Environment.NewLine, NonEmpty(item.Note, palletFormatName, packageFormat));

        var unitPrice = string.Empty;
        var price = string.Empty;
        var currencyName = string.Empty;
        decimal? lineTotal = null;
        if (order?.SalesPrice is double salesPrice)
        {
            var specialUnit = customer?.SpecialUnitHandling == true;
            var normalizedPrice = specialUnit ? salesPrice / 1000d : salesPrice;
            var decimals = specialUnit ? 3 : Math.Clamp((int)(order.Unit?.NrOfCalcDecimals ?? 2), 0, 15);
            var unitMultiplicator = specialUnit ? 1 : Math.Max(1, (int)(order.Unit?.Multiplicator ?? 1));
            currencyName = texts.Text(order.SalesCurrency?.TranslationCode ?? 0,
                order.SalesCurrency?.Name ?? order.SalesCurrency?.SpcsKey ?? "SEK");
            var unitName = specialUnit
                ? texts.Text(-101)
                : texts.Text(order.Unit?.TranslationCode ?? 0, order.Unit?.Name ?? texts.Text(-101));
            unitPrice = $"{texts.Number(normalizedPrice, decimals)} {currencyName}/{unitName}";
            if (numberOfItems != 0)
            {
                lineTotal = (decimal)normalizedPrice * (decimal)numberOfItems / unitMultiplicator;
                price = texts.Money(lineTotal, currencyName);
            }
        }

        return new DeliveryLine(item.Id,
            order?.CustomerOrderNr ?? string.Empty,
            note,
            order?.Product ?? string.Empty,
            stockDelivery?.Inventory?.Name ?? string.Empty,
            item.DeliveryAddressFreeText ?? string.Empty,
            numberOfPallets,
            numberOfItems,
            numberOfPalletPlaces,
            palletFormatName,
            packageFormat,
            unitPrice,
            price,
            currencyName,
            lineTotal);
    }

    private static string FormatDate(DateTime? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    private static IEnumerable<string> NonEmpty(params string?[] values) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!);
}
