using Econosys.Api.DTOs;

namespace Econosys.Api.Services
{
    // Shared DTO mapping for TransportOrderCostCalc, used by both the CRUD controller and the recalculation controller.
    public static class TransportOrderCostCalcMapper
    {
        public static TransportOrderCostCalcDto MapToDto(Models.TransportOrderCostCalc costCalc)
        {
            return new TransportOrderCostCalcDto
            {
                Id = costCalc.Id,
                TransportOrderId = costCalc.TransportOrderId,
                CalcCityFrom = costCalc.CalcCityFrom,
                CalcPostalNrTo = costCalc.CalcPostalNrTo,
                ResultInternationalCost = costCalc.ResultInternationalCost,
                Note = costCalc.Note,
                CreatedAtStatus = costCalc.CreatedAtStatus,
                CreatedDateTime = costCalc.CreatedDateTime,
                IsManualCalc = costCalc.IsManualCalc,
                IsLTL = costCalc.IsLTL,
                CostCalcForcePriceListId = costCalc.CostCalcForcePriceListId,
                SupplierOrders = costCalc.TransportOrderCostCalcSupplierOrders.Select(MapToDto).ToList(),
            };
        }

        public static TransportOrderCostCalcSupplierOrderDto MapToDto(Models.TransportOrderCostCalcSupplierOrder supplierOrder)
        {
            return new TransportOrderCostCalcSupplierOrderDto
            {
                Id = supplierOrder.Id,
                TransportOrderCostCalcId = supplierOrder.TransportOrderCostCalcId,
                SupplierOrderId = supplierOrder.SupplierOrderId,
                CaclulationInternationalCost = supplierOrder.CaclulationInternationalCost,
                CaclulationDomesticCost = supplierOrder.CaclulationDomesticCost,
                CaclulationFtl = supplierOrder.CaclulationFtl,
                CaclulationUnloading = supplierOrder.CaclulationUnloading,
                CaclulationTotal = supplierOrder.CaclulationTotal,
                CaclulationUsedTotal = supplierOrder.CaclulationUsedTotal,
                CalculationCalcInfo = supplierOrder.CalculationCalcInfo,
                ToInternationalCost = supplierOrder.ToInternationalCost,
                TransportOrderDomesticCost = supplierOrder.TransportOrderDomesticCost,
                TransportOrderLoading = supplierOrder.TransportOrderLoading,
                TransportOrderUnloading = supplierOrder.TransportOrderUnloading,
                TransportOrderOther = supplierOrder.TransportOrderOther,
                TransportOrderTotal = supplierOrder.TransportOrderTotal,
                TransportOrderCalcInfo = supplierOrder.TransportOrderCalcInfo,
                ResultInternationalCost = supplierOrder.ResultInternationalCost,
                ResultDomesticCost = supplierOrder.ResultDomesticCost,
                ResultTotal = supplierOrder.ResultTotal,
                ResultOther = supplierOrder.ResultOther,
                ResultNote = supplierOrder.ResultNote,
                DiffTransportOrderCaclulation = supplierOrder.DiffTransportOrderCaclulation,
                DiffResultCaclulation = supplierOrder.DiffResultCaclulation,
                DiffResultTransportOrder = supplierOrder.DiffResultTransportOrder,
                TotalNrOfItems = supplierOrder.TotalNrOfItems,
                TotalNrOfPallets = supplierOrder.TotalNrOfPallets,
                TotalNrOfPalletPlaces = supplierOrder.TotalNrOfPalletPlaces,
                TotalPalletArea = supplierOrder.TotalPalletArea,
                PalletFactor = supplierOrder.PalletFactor,
                Deliveries = supplierOrder.TransportOrderCostCalcSupplierOrderDeliveries.Select(MapToDto).ToList(),
            };
        }

        public static TransportOrderCostCalcSupplierOrderDeliveryDto MapToDto(Models.TransportOrderCostCalcSupplierOrderDelivery delivery)
        {
            return new TransportOrderCostCalcSupplierOrderDeliveryDto
            {
                Id = delivery.Id,
                TransportOrderCostCalcSupplierOrderId = delivery.TransportOrderCostCalcSupplierOrderId,
                DeliveryToCustomerId = delivery.DeliveryToCustomerId,
                DeliveryToStockId = delivery.DeliveryToStockId,
                DeliveryFromStockId = delivery.DeliveryFromStockId,
                NrOfItems = delivery.NrOfItems,
                NrOfPallets = delivery.NrOfPallets,
                PalletFormatId = delivery.PalletFormatId,
                PalletIsStackable = delivery.PalletIsStackable,
                PalletWidth = delivery.PalletWidth,
                PalletHeight = delivery.PalletHeight,
                PalletLength = delivery.PalletLength,
                EditionPerPallet = delivery.EditionPerPallet,
                PalletCalcFactor = delivery.PalletCalcFactor,
                IsSlattPallet = delivery.IsSlattPallet,
                ParentDeliveryId = delivery.ParentDeliveryId,
            };
        }
    }
}
