using System.Globalization;
using Econosys.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SkiaSharp;
using static Econosys.Api.Services.DocumentPdfLayout;

namespace Econosys.Api.Services;

public static class TransportOrderPdfDocument
{
    public const string Transporter = "TRANSPORTER";
    public const string Supplier = "SUPPLIER";
    public const string Receiver = "RECEIVER";

    private const int TruckWidth = 240;
    private const int TruckLength = 1360;

    private static readonly SKColor[] PalletColors =
    {
        new(47, 78, 115, 136),
        new(186, 171, 207, 136),
        new(248, 181, 0, 136),
        new(252, 60, 60, 136),
        new(0, 173, 181, 136),
        new(0, 178, 81, 136),
        new(140, 91, 63, 136),
        new(218, 148, 30, 136),
    };

    public sealed record DeliveryLine(int Id, string SupplierOrderNumber, string ProductName,
        string CustomerNameAddress, string DeliveryAddressFreeText, string PalletInfo,
        string LoadingPlace, string Note, string PalletSizeInfo, decimal? TotalWeight);

    public sealed record Content(string SupplierFactoryText, IReadOnlyList<DeliveryLine> Deliveries,
        byte[] PalletScheme);

    public static Content BuildContent(string receiverType, int? receiverId,
        IReadOnlyList<TransportOrderDelivery> deliveries,
        IReadOnlyDictionary<int, SupplierFactory> supplierFactories,
        IReadOnlyList<TransportOrderDeliveryPallet> palletPlan)
    {
        var normalizedReceiver = receiverType.Trim().ToUpperInvariant();
        var matchingDeliveries = deliveries
            .Where(delivery => normalizedReceiver switch
            {
                Transporter => true,
                Supplier => delivery.SupplierOrder?.SupplierFactoryId == receiverId,
                Receiver => delivery.DeliveryToStock?.InventoryId == receiverId,
                _ => false,
            })
            .OrderBy(delivery => delivery.SortOrder ?? int.MaxValue)
            .ThenBy(delivery => delivery.Id)
            .ToList();

        var lines = matchingDeliveries.Select(delivery => MapDelivery(delivery, supplierFactories,
            normalizedReceiver == Transporter)).ToList();

        var factoryIds = deliveries
            .Where(delivery => delivery.SupplierOrder?.SupplierFactoryId.HasValue == true)
            .Select(delivery => delivery.SupplierOrder!.SupplierFactoryId!.Value)
            .Where(id => normalizedReceiver != Supplier || id == receiverId)
            .Distinct()
            .ToList();
        var factoryText = string.Join(Environment.NewLine, factoryIds
            .Where(supplierFactories.ContainsKey)
            .Select(id => supplierFactories[id])
            .OrderBy(factory => factory.Name)
            .Select(factory => string.Join(", ", NonEmpty(factory.Name, factory.Address, factory.City))));

        var deliveryLabels = deliveries.ToDictionary(delivery => delivery.Id, delivery =>
            delivery.SupplierOrder?.SupplierOrderNr ?? delivery.Id.ToString(CultureInfo.InvariantCulture));
        var diagram = RenderPalletScheme(palletPlan, deliveryLabels);
        return new Content(factoryText, lines, diagram);
    }

    public static byte[] Generate(TransportOrder order, CompanyInfo? company, DocumentPdfTranslations texts,
        string receiverType, int? receiverId, IReadOnlyList<TransportOrderDelivery> deliveries,
        IReadOnlyDictionary<int, SupplierFactory> supplierFactories,
        IReadOnlyList<TransportOrderDeliveryPallet> palletPlan) =>
        CreateDocument(order, company, texts, receiverType, receiverId, deliveries, supplierFactories, palletPlan)
            .GeneratePdf();

    public static IDocument CreateDocument(TransportOrder order, CompanyInfo? company, DocumentPdfTranslations texts,
        string receiverType, int? receiverId, IReadOnlyList<TransportOrderDelivery> deliveries,
        IReadOnlyDictionary<int, SupplierFactory> supplierFactories,
        IReadOnlyList<TransportOrderDeliveryPallet> palletPlan)
    {
        var content = BuildContent(receiverType, receiverId, deliveries, supplierFactories, palletPlan);
        var normalizedReceiver = receiverType.Trim().ToUpperInvariant();

        return Document.Create(document => document.Page(page =>
        {
            Configure(page);
            page.Header().PaddingBottom(18).Row(row =>
            {
                row.RelativeItem().Element(Logo);
                row.ConstantItem(190).PaddingTop(14).AlignRight().Text(
                    $"{texts.Text(-28)}: {order.DateCreated?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
            });
            page.Content().Column(column =>
            {
                column.Spacing(9);
                column.Item().Row(row =>
                {
                    row.ConstantItem(110).Text(texts.Text(DocumentPdfTranslations.TransportOrderNumber, "Transportorder"))
                        .FontSize(15).Bold();
                    row.RelativeItem().Text(order.TransportOrderNr.ToString(CultureInfo.InvariantCulture))
                        .FontSize(15).Bold();
                });
                column.Item().PaddingTop(8).Row(row =>
                {
                    row.ConstantItem(300).Element(container => Field(container,
                        texts.Text(DocumentPdfTranslations.TransporterLabel, "Transportor"), order.Shipper?.Name));
                    row.RelativeItem().Element(container => Field(container,
                        texts.Text(DocumentPdfTranslations.SenderReferenceLabel, "Sender ref."), order.SenderReference));
                });
                column.Item().Row(row =>
                {
                    row.ConstantItem(300).Element(container => Field(container,
                        texts.Text(DocumentPdfTranslations.AttentionLabel, "Att."), content.SupplierFactoryText));
                    row.RelativeItem().Column(dates =>
                    {
                        dates.Item().Element(container => Field(container,
                            texts.Text(DocumentPdfTranslations.LoadingDateLabel, "Date of loading"), FormatDate(order.DateLoading), 90));
                        dates.Item().Element(container => Field(container,
                            texts.Text(DocumentPdfTranslations.DeliveryDateLabel, "Date of delivery"), FormatDate(order.DateDelivery), 90));
                    });
                });
                if (!string.IsNullOrWhiteSpace(order.Note))
                {
                    column.Item().Element(container => Field(container,
                        texts.Text(DocumentPdfTranslations.Note, "Note"), order.Note));
                }

                column.Item().PaddingTop(20).Row(row =>
                {
                    row.Spacing(80);
                    row.ConstantItem(100).Column(scheme =>
                    {
                        // scheme.Item().AlignCenter().Text(texts.Text(DocumentPdfTranslations.PalletSchemeLabel, "Pallet scheme"))
                        //     .FontSize(8).Bold();
                        scheme.Item().Image(content.PalletScheme).FitArea();
                    });
                    row.RelativeItem().Column(deliveryColumn =>
                    {
                        // deliveryColumn.Item().BorderBottom(0.5f).BorderColor("#a8b2af").PaddingBottom(4)
                        //     .Text(texts.Text(DocumentPdfTranslations.PlacesOfDeliveryLabel, "Places of delivery"))
                        //     .Bold();
                        deliveryColumn.Item().Column(places =>
                        {
                            foreach (var delivery in content.Deliveries)
                            {
                                places.Item().Column(place =>
                                {
                                    place.Item().PaddingTop(4).Row(placeHeader =>
                                    {
                                        placeHeader.Spacing(8);
                                        placeHeader.RelativeItem(1.35f).Column(address =>
                                        {
                                            address.Item().Text(delivery.CustomerNameAddress);
                                            if (!string.IsNullOrWhiteSpace(delivery.DeliveryAddressFreeText))
                                            {
                                                address.Item().Text(delivery.DeliveryAddressFreeText).Italic();
                                            }
                                            if (!string.IsNullOrWhiteSpace(delivery.Note))
                                            {
                                                address.Item().Text(delivery.Note).Italic();
                                            }
                                            if (delivery.TotalWeight.HasValue)
                                            {
                                                address.Item().Text($"{delivery.TotalWeight.Value.ToString("0.##", texts.Culture)} kg");
                                            }
                                            if (normalizedReceiver == Transporter && !string.IsNullOrWhiteSpace(delivery.PalletSizeInfo))
                                            {
                                                address.Item().Text(delivery.PalletSizeInfo);
                                            }
                                        });
                                        placeHeader.RelativeItem().Column(details =>
                                        {
                                            details.Item().AlignRight().Text(delivery.SupplierOrderNumber).Bold();
                                            if (!string.IsNullOrWhiteSpace(delivery.ProductName))
                                            {
                                                details.Item().AlignRight().Text(delivery.ProductName);
                                            }
                                        });
                                    });
                                    place.Item().PaddingBottom(4).Row(placeFooter =>
                                    {
                                        placeFooter.RelativeItem().Text(delivery.LoadingPlace).Bold();
                                        placeFooter.RelativeItem().AlignRight().Text(delivery.PalletInfo);
                                    });
                                });
                                places.Item().BorderBottom(0.5f).BorderColor("#a8b2af");
                            }
                        });
                    });
                });
            });
            page.Footer().PaddingTop(14).Element(container => Footer(container, company, texts.Text));
        }));
    }

    public static byte[] RenderPalletScheme(IReadOnlyList<TransportOrderDeliveryPallet> palletPlan,
        IReadOnlyDictionary<int, string> deliveryLabels)
    {
        using var surface = SKSurface.Create(new SKImageInfo(TruckWidth, TruckLength));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(211, 211, 211));

        using var borderPaint = new SKPaint { Color = new SKColor(72, 81, 89), Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
        using var fillPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        using var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

        var colorIndexByDelivery = new Dictionary<int, int>();
        foreach (var pallet in palletPlan.OrderBy(item => item.PosCmY).ThenBy(item => item.PosCmX))
        {
            var x = Math.Clamp(pallet.PosCmX, 0, TruckWidth);
            var y = Math.Clamp(pallet.PosCmY, 0, TruckLength);
            var width = Math.Clamp(pallet.Width, 0, TruckWidth - x);
            var height = Math.Clamp(pallet.Height, 0, TruckLength - y);
            if (width <= 0 || height <= 0)
            {
                continue;
            }

            var deliveryId = pallet.TransportOrderDeliveryId ?? 0;
            if (!colorIndexByDelivery.TryGetValue(deliveryId, out var colorIndex))
            {
                colorIndex = colorIndexByDelivery.Count % PalletColors.Length;
                colorIndexByDelivery[deliveryId] = colorIndex;
            }

            var bounds = new SKRect(x, y, x + width, y + height);
            fillPaint.Color = PalletColors[colorIndex];
            canvas.DrawRect(bounds, fillPaint);
            canvas.DrawRect(bounds, borderPaint);

            var label = deliveryLabels.GetValueOrDefault(deliveryId, string.Empty);
            if (string.IsNullOrWhiteSpace(label) || width < 18 || height < 18)
            {
                continue;
            }

            var fontSize = Math.Clamp(Math.Min(width, height) * 0.12f, 5f, 12f);
            using var font = new SKFont(SKTypeface.Default, fontSize);
            canvas.Save();
            canvas.ClipRect(bounds);
            canvas.DrawText(Shorten(label, 10), x + width / 2f, y + height / 2f, SKTextAlign.Center, font, textPaint);
            canvas.Restore();
        }

        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static DeliveryLine MapDelivery(TransportOrderDelivery delivery,
        IReadOnlyDictionary<int, SupplierFactory> supplierFactories, bool includePalletSize)
    {
        var supplierOrder = delivery.SupplierOrder;
        var customerOrder = delivery.DeliveryFromStock?.CustomerOrder ?? delivery.DeliveryToCustomer?.CustomerOrder;
        var customerName = customerOrder?.CustomerName ?? supplierOrder?.Customer?.Name ?? supplierOrder?.Customer2 ?? string.Empty;
        var isOmlast = delivery.ForcedOmlastStatus == 1
            || delivery.OmlastInventory?.IsOmlast == true
            || delivery.DeliveryFromStock?.Inventory?.IsOmlast == true
            || delivery.DeliveryToStock?.Inventory?.IsOmlast == true;
        var inventory = isOmlast && delivery.OmlastInventory != null
            ? delivery.OmlastInventory
            : delivery.DeliveryToStock?.Inventory;

        var addressName = inventory?.Name ?? customerOrder?.DeliveryAddressName ?? supplierOrder?.DeliveryAddressName;
        var street = inventory?.Address ?? customerOrder?.DeliveryAddress ?? supplierOrder?.DeliveryAddress;
        var postalCode = inventory?.PostalNrText ?? inventory?.PostalNr?.ToString(CultureInfo.InvariantCulture)
            ?? customerOrder?.DeliveryPostalNr ?? supplierOrder?.DeliveryPostalNr;
        var city = inventory?.PostalAddress ?? customerOrder?.DeliveryPostalAddress ?? supplierOrder?.DeliveryPostalAddress;
        var address = string.Join(Environment.NewLine, NonEmpty(customerName, addressName, street,
            string.Join(" ", NonEmpty(postalCode, city))));

        var source = delivery.DeliveryToStock != null
            ? (delivery.DeliveryToStock.PalletWidth, delivery.DeliveryToStock.PalletLength, delivery.DeliveryToStock.PalletHeight,
                delivery.DeliveryToStock.NrOfPallets, delivery.DeliveryToStock.PalletIsStackable)
            : delivery.DeliveryToCustomer != null
                ? (delivery.DeliveryToCustomer.PalletWidth, delivery.DeliveryToCustomer.PalletLength, delivery.DeliveryToCustomer.PalletHeight,
                    delivery.DeliveryToCustomer.NrOfPallets, delivery.DeliveryToCustomer.PalletIsStackable)
                : (delivery.DeliveryFromStock?.PalletWidth, delivery.DeliveryFromStock?.PalletLength, delivery.DeliveryFromStock?.PalletHeight,
                    delivery.DeliveryFromStock?.NrOfPallets, delivery.DeliveryFromStock?.PalletIsStackable);
        var palletCount = source.NrOfPallets;
        var palletInfo = palletCount.HasValue
            ? source.PalletIsStackable == true
                ? $"{palletCount.Value}/{Math.Ceiling(palletCount.Value / 2d).ToString(CultureInfo.InvariantCulture)} pallets, 2p i height"
                : $"{palletCount.Value} pallets"
            : string.Empty;
        var palletSize = includePalletSize && source.PalletWidth.HasValue && source.PalletLength.HasValue && source.PalletHeight.HasValue
            ? $"{source.PalletLength}x{source.PalletWidth}x{source.PalletHeight}"
            : string.Empty;

        var factoryName = supplierOrder?.SupplierFactoryId is int factoryId
            && supplierFactories.TryGetValue(factoryId, out var factory)
                ? factory.Name ?? string.Empty
                : string.Empty;

        return new DeliveryLine(delivery.Id,
            supplierOrder?.SupplierOrderNr ?? string.Empty,
            customerOrder?.Product ?? supplierOrder?.Product ?? string.Empty,
            address,
            delivery.DeliveryAddressFreeText ?? string.Empty,
            palletInfo,
            factoryName,
            delivery.Note ?? string.Empty,
            palletSize,
            delivery.TotalWeight);
    }

    private static string FormatDate(DateTime? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Shorten(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static IEnumerable<string> NonEmpty(params string?[] values) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!);
}