using Econosys.Api.DTOs;
using Econosys.Api.Models;

namespace Econosys.Api.Services;

public static class CalculationUpdateMapper
{
    public static void Apply(Calculation calculation, UpdateCalculationRequest request, int editedBy, DateTime editedAt)
    {
        calculation.IsArchived = request.IsArchived;
        calculation.CustomerId = request.CustomerId;
        calculation.SupplierId = request.SupplierId;
        calculation.UnitId = request.UnitId;
        calculation.PurchaseCurrencyId = request.PurchaseCurrencyId;
        calculation.SalesCurrencyId = request.SalesCurrencyId;
        calculation.PurchaseCurrencyRate = request.PurchaseCurrencyRate;
        calculation.SalesCurrencyRate = request.SalesCurrencyRate;
        calculation.CurrencyRate = request.CurrencyRate;
        calculation.NrOfCalcDecimalsPurchase = request.PurchaseDecimals;
        calculation.NrOfCalcDecimalsSales = request.SalesDecimals;
        calculation.LockOutPrice = request.LockOutPrice;
        calculation.CalculationFollowUp = request.CalculationFollowUp;
        calculation.OtherCostNote = request.OtherCostNote;
        calculation.FreightNote = request.FreightNote;
        calculation.LoadingInstruction = request.LoadingInstruction;
        calculation.AutomaticLoadingInstruction = request.AutomaticLoadingInstruction;
        calculation.AutoFreightCalc = request.AutoFreightCalc;
        calculation.AutoFreightPrice = request.AutoFreightPrice;
        calculation.DeactivateFreightCalculation = request.DeactivateFreightCalculation;
        calculation.SupplierFactoryId = request.SupplierFactoryId;
        calculation.CustomerDeliveryAddressId = request.CustomerDeliveryAddressId;
        calculation.FreightPerPallet = request.FreightPerPallet;
        calculation.FreightPerVehicle = request.FreightPerVehicle;
        calculation.LoadingPerDelivery = request.LoadingPerDelivery;
        calculation.DeliveryAddressCountryCode = request.DeliveryAddressCountryCode;
        calculation.DeliveryAddressPostalNr = request.DeliveryAddressPostalNr;
        calculation.PalletFormatId = request.PalletFormatId;
        calculation.PalletLength = request.PalletLength;
        calculation.PalletWidth = request.PalletWidth;
        calculation.PalletHeight = request.PalletHeight;
        calculation.EditionPerPallet = request.EditionPerPallet;
        calculation.PalletIsStackable = request.PalletIsStackable;
        calculation.PalletsPerTruck = request.PalletsPerTruck;
        calculation.PalletCalcFactor = request.PalletCalcFactor;
        calculation.PalletPriceIn = request.PalletPriceIn;
        calculation.PalletPriceOut = request.PalletPriceOut;
        calculation.DoDebitPalletPrice = request.DoDebitPalletPrice;
        calculation.DoBuntas = request.DoBuntas;
        calculation.NrOfPerBundle = request.NrOfPerBundle;
        calculation.IsYtterforpackning = request.IsYtterforpackning;
        calculation.NrOfPerOuterPackaging = request.NrOfPerOuterPackaging;
        calculation.CalcStorage = request.CalcStorage;
        calculation.LoadingPerPallet = request.LoadingPerPallet;
        calculation.StoragePerM2 = request.StoragePerM2;
        calculation.Interest = request.Interest;
        calculation.FreightFromStockPerPallet = request.FreightFromStockPerPallet;
        calculation.EditedAt = editedAt;
        calculation.EditedBy = editedBy;
    }

    public static void ApplyApprovals(Calculation calculation, UpdateCalculationRequest request, string? approvedBy, DateTime approvedAt)
    {
        if (request.ApprovePackaging)
        {
            calculation.PackagingApprovedBy = approvedBy;
            calculation.PackagingApprovedDateTime = approvedAt;
        }
        if (request.ApproveFreight)
        {
            calculation.FreightApprovedBy = approvedBy;
            calculation.FreightApprovedDateTime = approvedAt;
        }
        if (request.ApproveOtherCosts)
        {
            calculation.OtherCostApprovedBy = approvedBy;
            calculation.OtherCostApprovedDateTime = approvedAt;
        }
        if (request.ApproveStorage)
        {
            calculation.StorageApprovedBy = approvedBy;
            calculation.StorageApprovedDateTime = approvedAt;
        }
    }

    public static void ApplyRow(
        CalculationRow row,
        UpdateCalculationRowRequest request,
        Calculation calculation,
        int unitMultiplier,
        int salesDecimals)
    {
        row.CompanyId = calculation.CompanyId;
        row.Edition = request.Edition;
        row.PurchasePrice = request.PurchasePrice;
        row.Freight = request.Freight;
        row.Other = request.Other;
        row.Storage = request.Storage;
        row.PackagingCost = request.PackagingCost;
        row.PercentAddition = request.PercentAddition;
        row.SalesPrice = request.SalesPrice;
        row.Info = request.Info;
        row.NrOfPalletsPopup = request.NrOfPallets;
        row.NrOfVehiclesPopup = request.NrOfVehicles;
        row.PalletCalcFactor = request.PalletCalcFactor;
        row.FreightCostPerPalletDomesticPopup = request.FreightDomesticPerPallet;
        row.FreightCostPerPalletInternationalPopup = request.FreightInternationalPerPallet;
        row.FreightCostPerFtlInternationalPopup = request.FreightPerVehicle;
        row.FreightUnloadingPopup = request.FreightUnloading;
        row.FreightPopup = request.FreightTotal;
        row.FreightTotalPopupPerUnit = request.FreightPerUnit;
        row.PreCalcAdditionPercent = request.PreCalcAdditionPercent;
        row.CalcPalletPriceUsed = request.CalcPalletPriceUsed;
        row.CalcFtlPriceUsed = request.CalcFtlPriceUsed;
        row.CustomerDeliveryAddressId = request.CustomerDeliveryAddressId;
        row.StorageNrOfPallets = request.StorageNrOfPallets;
        row.NrOfStorageMonths = request.StorageMonths;

        if (row.Edition is not > 0 || unitMultiplier <= 0 || calculation.SalesCurrencyRate is not > 0)
        {
            return;
        }

        var result = CalculationPricingService.CalculateSalesRow(
            ToDecimal(row.PurchasePrice),
            ToDecimal(calculation.CurrencyRate),
            ToDecimal(row.Freight),
            ToDecimal(row.Other),
            ToDecimal(row.Storage),
            row.PackagingCost ?? 0,
            ToDecimal(row.SalesPrice),
            ToDecimal(row.PercentAddition),
            calculation.LockOutPrice,
            row.Edition.Value,
            unitMultiplier,
            (decimal)calculation.SalesCurrencyRate.Value,
            salesDecimals);
        row.SalesPrice = (double)result.SalesPrice;
        row.PercentAddition = (double)result.AdditionPercent;
    }

    private static decimal ToDecimal(double? value) => value.HasValue
        && double.IsFinite(value.Value)
        && value.Value >= (double)decimal.MinValue
        && value.Value <= (double)decimal.MaxValue
            ? (decimal)value.Value
            : 0;
}