namespace Econosys.Api.DTOs
{
    public class DeliveryDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public int? SupplierOrderId { get; set; }
        public int? CustomerOrderId { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public double? NrOfItems { get; set; }
        public bool? ToInvoice { get; set; }
        public int? NrOfPallets { get; set; }
        public string? CallOff { get; set; }
        public string? DeliveryNr { get; set; }
        public int? InventoryId { get; set; }
        public int? OldDbId { get; set; }
        public string? SpcsRefNr { get; set; }
        public int? SupplierInvoiceNr { get; set; }
        public decimal? SupplierInvoiceCost { get; set; }
        public int? DeliveryStatus { get; set; }
        public int? PalletFormatId { get; set; }
        public bool PalletIsStackable { get; set; }
        public int? PalletWidth { get; set; }
        public int? PalletHeight { get; set; }
        public int? PalletLength { get; set; }
        public int? EditionPerPallet { get; set; }
        public decimal? PalletCalcFactor { get; set; }
        public bool IsSlattPallet { get; set; }
        public int? ParentDeliveryId { get; set; }
        public bool? DoNotInvoice { get; set; }
        public bool InfoOk { get; set; }
        public DateTime? WeightMissingEmailSentDateTime { get; set; }
        public DateTime? CostMissingEmailSentDateTime { get; set; }
        public bool? IsPalletInvoicedSeparately { get; set; }
        public bool? IsAdjustment { get; set; }
        public int? AdjustedFromDeliveryId { get; set; }
        public int? DeliveryToStockId { get; set; }
        public int? NrOfBunt { get; set; }
        public int? NrOfYtterforpackning { get; set; }

        // Joined display/filter fields (customer/supplier order, inventory, transport order, call-off)
        public int? CustomerId { get; set; }
        public string? ProductName { get; set; }
        public string? CustomerName { get; set; }
        public string? SupplierOrderNr { get; set; }
        public string? CustomersOrderNr { get; set; }
        public string? InventoryName { get; set; }
        public int? TransportOrderId { get; set; }
        public int? TransportOrderNr { get; set; }
        public int? CallOffNr { get; set; }
    }

    public sealed record DeliveryEditResponse
    {
        public required DeliveryDto Delivery { get; init; }
        public required string Version { get; init; }
        public int? OrderedQty { get; init; }
        public bool AutoFreightCalc { get; init; }
        public string PackagingType { get; init; } = string.Empty;
        public int? UnitsPerPackage { get; init; }
        public double? AvailableItems { get; init; }
        public double? AvailablePallets { get; init; }
        public DateTime? LatestStockTakingDate { get; init; }
        public IReadOnlyList<DeliveryInventoryOption> Inventories { get; init; } = [];
        public IReadOnlyList<int> RelatedDeliveryIds { get; init; } = [];
    }

    public sealed record DeliveryInventoryOption(int Id, string Name);

    public sealed record UpdateDeliveryRequest
    {
        public string Version { get; init; } = string.Empty;
        public DateTime? DeliveryDate { get; init; }
        public double? NrOfItems { get; init; }
        public int? NrOfPallets { get; init; }
        public int? NrOfBunt { get; init; }
        public int? NrOfYtterforpackning { get; init; }
        public string? CallOff { get; init; }
        public int? InventoryId { get; init; }
        public int? PalletLength { get; init; }
        public int? PalletWidth { get; init; }
        public int? PalletHeight { get; init; }
        public bool PalletIsStackable { get; init; }
        public decimal? PalletCalcFactor { get; init; }
        public bool IsDelivered { get; init; }
        public string? SpcsRefNr { get; init; }
        public int? SupplierInvoiceNr { get; init; }
        public DateTime? ConfirmedStockTakingDate { get; init; }
    }

    public sealed record DeliveryEditError(string Code, string Message, DateTime? StockTakingDate = null);

    public class SearchDeliveriesRequestDto
    {
        public FilterRequest? Filter { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }
}