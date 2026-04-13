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
    }

    public class SearchDeliveriesRequestDto
    {
        public FilterRequest? Filter { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }
}