using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("InvoiceRow")]
    public class InvoiceRow
    {
        [Key]
        [Column("lngInvoiceRow_ID")]
        public int Id { get; set; }

        [Column("lngInvoice_ID")]
        public int? InvoiceId { get; set; }

        [Column("lngSortOrder")]
        public int? SortOrder { get; set; }

        [Column("lngInvoiceRow")]
        public int? InvoiceRowNumber { get; set; }

        [Column("lngDeliveryToCustomer_ID")]
        public int? DeliveryToCustomerId { get; set; }

        [Column("lngDeliveryFromStock_ID")]
        public int? DeliveryFromStockId { get; set; }

        [Column("lngPriceType_ID")]
        public int? PriceTypeId { get; set; }

        [Column("strPriceKey")]
        [MaxLength(50)]
        public string? PriceKey { get; set; }

        [Column("strText")]
        [MaxLength(255)]
        public string? Text { get; set; }

        [Column("strText2")]
        [MaxLength(255)]
        public string? Text2 { get; set; }

        [Column("dblNrOf")]
        public double? NrOf { get; set; }

        [Column("dblUnitPrice")]
        public double? UnitPrice { get; set; }

        [Column("dblSum")]
        public double? Sum { get; set; }

        [Column("bolVATGround")]
        public bool VatGround { get; set; }

        [Column("bolCompareWithOrder")]
        public bool CompareWithOrder { get; set; }

        [Column("bolCalculate")]
        public bool Calculate { get; set; }

        [Column("strAccountNr")]
        [MaxLength(50)]
        public string? AccountNr { get; set; }

        [Column("strCostCenter")]
        [MaxLength(50)]
        public string? CostCenter { get; set; }

        [Column("lngUnit_ID")]
        public int? UnitId { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("DoNotAggregateWithParent")]
        public bool DoNotAggregateWithParent { get; set; }

        [Column("Weight", TypeName = "decimal(10,2)")]
        public decimal? Weight { get; set; }

        [Column("OrderCostId")]
        public int? OrderCostId { get; set; }

        [ForeignKey(nameof(InvoiceId))]
        public Invoice? Invoice { get; set; }

        [ForeignKey(nameof(DeliveryToCustomerId))]
        public DeliveryToCustomer? DeliveryToCustomer { get; set; }

        [ForeignKey(nameof(DeliveryFromStockId))]
        public DeliveryFromStock? DeliveryFromStock { get; set; }

        [ForeignKey(nameof(PriceTypeId))]
        public PriceType? PriceType { get; set; }

        [ForeignKey(nameof(UnitId))]
        public Unit? Unit { get; set; }

        [ForeignKey(nameof(OrderCostId))]
        public OrderCost? OrderCost { get; set; }
    }
}
