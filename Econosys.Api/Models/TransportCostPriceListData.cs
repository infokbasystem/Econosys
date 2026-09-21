using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    // Pallet-count price tiers (P1..P32) plus a full-truck-load tier (PFTL), keyed by country/postal range.
    [Table("TransportCostPriceListData")]
    public class TransportCostPriceListData
    {
        [Key]
        public int Id { get; set; }

        public int? TransportCostPriceListId { get; set; }

        [Column(TypeName = "varchar(10)")]
        public string? CountryCode { get; set; }

        public int? PostalNrFrom { get; set; }

        public int? PostalNrTo { get; set; }

        [Column(TypeName = "varchar(100)")]
        public string? Transhipment { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? AdditionSekPerPallet { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P1 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P2 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P3 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P4 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P5 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P6 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P7 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P8 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P9 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P10 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P11 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P12 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P13 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P14 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P15 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P16 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P17 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P18 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P19 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P20 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P21 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P22 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P23 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P24 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P25 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P26 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P27 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P28 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P29 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P30 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P31 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? P32 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? PFTL { get; set; }

        [ForeignKey(nameof(TransportCostPriceListId))]
        public virtual TransportCostPriceList? TransportCostPriceList { get; set; }
    }
}
