using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class TransportCostPriceListOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public abstract class TransportCostPriceListHeaderBase
    {
        private const double MaxHeaderDecimal = 99999.99999;

        [Required]
        [MaxLength(100)]
        public string? Name { get; set; }

        public int? CurrencyId { get; set; }

        [Range(-MaxHeaderDecimal, MaxHeaderDecimal)]
        public decimal? SecaMarpolPallet { get; set; }

        [Range(-MaxHeaderDecimal, MaxHeaderDecimal)]
        public decimal? DmtPercentPallet { get; set; }

        [Range(-MaxHeaderDecimal, MaxHeaderDecimal)]
        public decimal? AdditionCurrencyPercentPallet { get; set; }

        [Range(-MaxHeaderDecimal, MaxHeaderDecimal)]
        public decimal? OtherPallet { get; set; }

        [Range(-MaxHeaderDecimal, MaxHeaderDecimal)]
        public decimal? OtherPercentPallet { get; set; }

        [Range(-MaxHeaderDecimal, MaxHeaderDecimal)]
        public decimal? AdditionTotalPercentPallet { get; set; }

        [Range(-MaxHeaderDecimal, MaxHeaderDecimal)]
        public decimal? SecaMarpolFtl { get; set; }

        [Range(-MaxHeaderDecimal, MaxHeaderDecimal)]
        public decimal? DmtPercentFtl { get; set; }

        [Range(-MaxHeaderDecimal, MaxHeaderDecimal)]
        public decimal? AdditionCurrencyPercentFtl { get; set; }

        [Range(-MaxHeaderDecimal, MaxHeaderDecimal)]
        public decimal? OtherFtl { get; set; }

        [Range(-MaxHeaderDecimal, MaxHeaderDecimal)]
        public decimal? OtherPercentFtl { get; set; }

        [Range(-MaxHeaderDecimal, MaxHeaderDecimal)]
        public decimal? AdditionTotalPercentFtl { get; set; }

        public bool IsStafflad { get; set; }

        [Range(-MaxHeaderDecimal, MaxHeaderDecimal)]
        public decimal? LoadingCost { get; set; }

        [Range(-MaxHeaderDecimal, MaxHeaderDecimal)]
        public decimal? UnloadingCost { get; set; }

        [Range(-MaxHeaderDecimal, MaxHeaderDecimal)]
        public decimal? PreCalcAdditionPercent { get; set; }
    }

    public abstract class TransportCostPriceListDataRowBase
    {
        private const double MaxRowDecimal = 99999.99999;
        private const double MaxPriceDecimal = 9999999999999.99999;

        [MaxLength(10)]
        public string? CountryCode { get; set; }

        public int? PostalNrFrom { get; set; }

        public int? PostalNrTo { get; set; }

        [MaxLength(100)]
        public string? Transhipment { get; set; }

        [Range(-MaxRowDecimal, MaxRowDecimal)]
        public decimal? AdditionSekPerPallet { get; set; }

        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P1 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P2 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P3 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P4 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P5 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P6 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P7 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P8 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P9 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P10 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P11 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P12 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P13 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P14 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P15 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P16 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P17 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P18 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P19 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P20 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P21 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P22 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P23 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P24 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P25 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P26 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P27 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P28 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P29 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P30 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P31 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? P32 { get; set; }
        [Range(-MaxPriceDecimal, MaxPriceDecimal)] public decimal? PFTL { get; set; }
    }

    public class TransportCostPriceListDto : TransportCostPriceListHeaderBase
    {
        public int Id { get; set; }
        public string? ImportName { get; set; }
        public DateTime? LastImportDateTime { get; set; }
        public string? CurrencyName { get; set; }
        public List<TransportCostPriceListDataDto> PriceListData { get; set; } = new();
        public List<TransportCostPriceListSupplierFactoryLinkDto> SupplierFactories { get; set; } = new();
        public List<TransportCostPriceListInventoryLinkDto> Inventories { get; set; } = new();
    }

    public class TransportCostPriceListDataDto : TransportCostPriceListDataRowBase
    {
        public int Id { get; set; }
        public int? TransportCostPriceListId { get; set; }
    }

    public class TransportCostPriceListSupplierFactoryLinkDto
    {
        public int Id { get; set; }
        public int SupplierFactoryId { get; set; }
        public string? SupplierName { get; set; }
        public string? FactoryName { get; set; }
    }

    public class TransportCostPriceListInventoryLinkDto
    {
        public int Id { get; set; }
        public int InventoryId { get; set; }
        public string? InventoryName { get; set; }
    }

    public class SaveTransportCostPriceListRequest : TransportCostPriceListHeaderBase
    {
        public List<SaveTransportCostPriceListDataRow> Rows { get; set; } = new();
        public List<int> SupplierFactoryIds { get; set; } = new();
        public List<int> InventoryIds { get; set; } = new();
    }

    public class SaveTransportCostPriceListDataRow : TransportCostPriceListDataRowBase
    {
        // Null or 0 means a new row.
        public int? Id { get; set; }
    }

    public class TransportCostPriceListFormOptionsDto
    {
        public List<TransportCostPriceListCurrencyOptionDto> Currencies { get; set; } = new();
        public List<TransportCostPriceListSupplierFactoryOptionDto> SupplierFactories { get; set; } = new();
        public List<TransportCostPriceListInventoryOptionDto> Inventories { get; set; } = new();
    }

    public class TransportCostPriceListCurrencyOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class TransportCostPriceListSupplierFactoryOptionDto
    {
        public int Id { get; set; }
        public string? SupplierName { get; set; }
        public string? FactoryName { get; set; }
    }

    public class TransportCostPriceListInventoryOptionDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }
}
