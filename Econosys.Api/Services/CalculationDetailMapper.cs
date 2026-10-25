using Econosys.Api.DTOs;
using Econosys.Api.Models;

namespace Econosys.Api.Services;

public static class CalculationDetailMapper
{
    private static readonly TimeZoneInfo SwedenTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    public static CalculationDetailDto Map(Calculation calculation, IReadOnlyDictionary<int, string?> userNames)
    {
        var product = calculation.Product;
        var purchaseDecimals = Math.Clamp(calculation.NrOfCalcDecimalsPurchase ?? calculation.Unit?.NrOfCalcDecimals ?? 0, 0, 10);
        var salesDecimals = Math.Clamp(calculation.NrOfCalcDecimalsSales ?? calculation.Unit?.NrOfCalcDecimals ?? 0, 0, 10);
        return new CalculationDetailDto
        {
            Id = calculation.Id,
            IsArchived = calculation.IsArchived,
            Product = product is null ? null : new CalculationProductDto(product.Id, product.Name,
                product.Material?.Name, product.Construction?.Name, product.ProductCode,
                product.LengthMm, product.WidthMm, product.HeightMm, product.Format,
                product.NoteInternal, product.NoteExternal, product.NrOfColors,
                product.Varnish?.Name, product.VarnishOther, product.NetWeightPer1000),
            Supplier = calculation.Supplier is null ? null : new(calculation.Supplier.Id, calculation.Supplier.Name),
            Customer = calculation.Customer is null ? null : new(calculation.Customer.Id, calculation.Customer.Name),
            UnitId = calculation.UnitId,
            UnitName = calculation.Unit?.Name,
            UnitMultiplier = calculation.Unit?.Multiplicator,
            PurchaseCurrencyId = calculation.PurchaseCurrencyId,
            PurchaseCurrencyName = calculation.PurchaseCurrency?.Name,
            PurchaseCurrencyRate = calculation.PurchaseCurrencyRate,
            SalesCurrencyId = calculation.SalesCurrencyId,
            SalesCurrencyName = calculation.SalesCurrency?.Name,
            SalesCurrencyRate = calculation.SalesCurrencyRate,
            CurrencyRate = calculation.CurrencyRate,
            PurchaseDecimals = purchaseDecimals,
            SalesDecimals = salesDecimals,
            LockOutPrice = calculation.LockOutPrice,
            CalculationFollowUp = calculation.CalculationFollowUp,
            OtherCostNote = calculation.OtherCostNote,
            FreightNote = calculation.FreightNote,
            LoadingInstruction = calculation.LoadingInstruction,
            AutomaticLoadingInstruction = calculation.AutomaticLoadingInstruction,
            AutoFreightCalc = calculation.AutoFreightCalc,
            AutoFreightPrice = calculation.AutoFreightPrice,
            DeactivateFreightCalculation = calculation.DeactivateFreightCalculation,
            SupplierFactoryId = calculation.SupplierFactoryId,
            CustomerDeliveryAddressId = calculation.CustomerDeliveryAddressId,
            FreightPerPallet = calculation.FreightPerPallet,
            FreightPerVehicle = calculation.FreightPerVehicle,
            LoadingPerDelivery = calculation.LoadingPerDelivery,
            DeliveryAddressCountryCode = calculation.DeliveryAddressCountryCode,
            DeliveryAddressPostalNr = calculation.DeliveryAddressPostalNr,
            PalletFormatId = calculation.PalletFormatId,
            PalletFormatName = calculation.PalletFormat?.Name,
            PalletLength = calculation.PalletLength,
            PalletWidth = calculation.PalletWidth,
            PalletHeight = calculation.PalletHeight,
            EditionPerPallet = calculation.EditionPerPallet,
            PalletIsStackable = calculation.PalletIsStackable,
            PalletsPerTruck = calculation.PalletsPerTruck,
            PalletCalcFactor = calculation.PalletCalcFactor,
            PalletPriceIn = calculation.PalletPriceIn,
            PalletPriceOut = calculation.PalletPriceOut,
            DoDebitPalletPrice = calculation.DoDebitPalletPrice,
            DoBuntas = calculation.DoBuntas,
            NrOfPerBundle = calculation.NrOfPerBundle,
            IsYtterforpackning = calculation.IsYtterforpackning,
            NrOfPerOuterPackaging = calculation.NrOfPerOuterPackaging,
            CalcStorage = calculation.CalcStorage,
            LoadingPerPallet = calculation.LoadingPerPallet,
            StoragePerM2 = calculation.StoragePerM2,
            Interest = calculation.Interest,
            FreightFromStockPerPallet = calculation.FreightFromStockPerPallet,
            CreatedAt = ToOffset(calculation.CreatedAt),
            CreatedByName = Name(calculation.CreatedBy, userNames),
            EditedAt = ToOffset(calculation.EditedAt),
            EditedByName = Name(calculation.EditedBy, userNames),
            Approvals = [
                new("packaging", calculation.PackagingApprovedBy, ToOffset(calculation.PackagingApprovedDateTime)),
                new("freight", calculation.FreightApprovedBy, ToOffset(calculation.FreightApprovedDateTime)),
                new("other", calculation.OtherCostApprovedBy, ToOffset(calculation.OtherCostApprovedDateTime)),
                new("storage", calculation.StorageApprovedBy, ToOffset(calculation.StorageApprovedDateTime)),
            ],
            Rows = calculation.CalculationRows.OrderBy(row => row.Edition).ThenBy(row => row.Id)
                .Select(row => MapRow(calculation, row, salesDecimals)).ToArray(),
        };
    }

    private static CalculationDetailRowDto MapRow(Calculation calculation, CalculationRow row, int decimals)
    {
        var salesRate = row.Archived ? row.ArchivedSalesCurrencyRate ?? 1 : ToDecimal(calculation.SalesCurrencyRate) ?? 1;
        var purchaseRate = row.Archived ? row.ArchivedPurchaseCurrencyRate ?? 1 : ToDecimal(calculation.PurchaseCurrencyRate) ?? 1;
        var currencyRate = row.Archived
            ? salesRate > 0 ? purchaseRate / salesRate : (decimal?)null
            : ToDecimal(calculation.CurrencyRate);
        var converted = ToDecimal(row.PurchasePrice) * currencyRate;
        var total = currencyRate.HasValue
            ? (converted ?? 0) + (ToDecimal(row.Freight) ?? 0) + (ToDecimal(row.Other) ?? 0)
                + (ToDecimal(row.Storage) ?? 0) + (row.PackagingCost ?? 0)
            : (decimal?)null;
        var contribution = total.HasValue && salesRate > 0
            ? Math.Round(((ToDecimal(row.SalesPrice) ?? 0) - total.Value) * salesRate, decimals)
            : (decimal?)null;
        var multiplier = calculation.Unit?.Multiplicator;
        var totalContribution = contribution.HasValue && multiplier > 0 && row.Edition.HasValue
            ? Math.Round(contribution.Value * row.Edition.Value / multiplier.Value, decimals)
            : (decimal?)null;

        return new CalculationDetailRowDto
        {
            Id = row.Id, Edition = row.Edition, Archived = row.Archived,
            PurchasePrice = row.PurchasePrice, Freight = row.Freight, Other = row.Other,
            Storage = row.Storage, PackagingCost = row.PackagingCost,
            PercentAddition = row.PercentAddition, SalesPrice = row.SalesPrice,
            ConvertedPurchasePrice = converted, TotalCost = total,
            Contribution = contribution, TotalContribution = totalContribution,
            NrOfPallets = row.NrOfPalletsPopup, NrOfVehicles = row.NrOfVehiclesPopup,
            PalletCalcFactor = row.PalletCalcFactor,
            FreightDomesticPerPallet = row.FreightCostPerPalletDomesticPopup,
            FreightInternationalPerPallet = row.FreightCostPerPalletInternationalPopup,
            FreightPerVehicle = row.FreightCostPerFtlInternationalPopup,
            FreightUnloading = row.FreightUnloadingPopup,
            FreightTotal = row.FreightPopup, FreightPerUnit = row.FreightTotalPopupPerUnit,
            PreCalcAdditionPercent = row.PreCalcAdditionPercent,
            CalcPalletPriceUsed = row.CalcPalletPriceUsed, CalcFtlPriceUsed = row.CalcFtlPriceUsed,
            CustomerDeliveryAddressId = row.CustomerDeliveryAddressId,
            StorageNrOfPallets = row.StorageNrOfPallets, StorageMonths = row.NrOfStorageMonths,
            StorageUnloading = row.StorageUnloadingPopup, StorageAmount = row.StoragePopup,
            StorageInterest = row.StorageInterestPopup, StorageFreight = row.StorageFreightToCustomerPopup,
            Info = row.Info, PriceListSummary = row.ArchivedPriceListSummary,
            Costs = row.CalculationRowCosts.OrderBy(cost => cost.Id).Select(cost => new CalculationDetailCostDto(
                cost.Id, cost.Cost?.Name, cost.CostTypeText, cost.NrOf,
                cost.InPrice, cost.InPriceCurrency?.Name, cost.OutPrice, cost.DoDebit,
                cost.CostId, cost.InPriceCurrencyId)).ToArray(),
            Documents = row.SupplierOrders.Select(order => new CalculationDocumentLinkDto("supplierorders", order.Id, order.SupplierOrderNr))
                .Concat(row.CustomerOrders.Select(order => new CalculationDocumentLinkDto("customerorders", order.Id, order.CustomerOrderNr)))
                .Concat(row.QuotationRows.Where(item => item.QuotationId.HasValue)
                    .Select(item => new CalculationDocumentLinkDto("quotations", item.QuotationId!.Value, item.QuotationId.Value.ToString())))
                .Distinct().OrderBy(item => item.Type).ThenBy(item => item.Id).ToArray(),
        };
    }

    private static decimal? ToDecimal(double? value) => value.HasValue && double.IsFinite(value.Value)
        && value.Value > (double)decimal.MinValue && value.Value < (double)decimal.MaxValue
            ? (decimal)value.Value : null;

    private static string? Name(int? id, IReadOnlyDictionary<int, string?> names) =>
        id.HasValue && names.TryGetValue(id.Value, out var name) ? name : null;

    private static DateTimeOffset? ToOffset(DateTime? value)
    {
        if (!value.HasValue) return null;
        var wallClock = DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified);
        return new DateTimeOffset(wallClock, SwedenTimeZone.GetUtcOffset(wallClock));
    }
}