using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("OrderCost")]
    public class OrderCost
    {
        [Key]
        public int Id { get; set; }

        public int? QuotationId { get; set; }

        public int? QuotationRowId { get; set; }

        public int? CalculationRowId { get; set; }

        public int? CustomerOrderId { get; set; }

        public int? SupplierOrderId { get; set; }

        public int? CostId { get; set; }

        [Column(TypeName = "decimal(10, 5)")]
        public decimal? NrOf { get; set; }

        [Column(TypeName = "decimal(15, 5)")]
        public decimal? InPrice { get; set; }

        public int? InPriceCurrencyId { get; set; }

        [Column(TypeName = "decimal(15, 5)")]
        public decimal? OutPrice { get; set; }

        public bool DoDebit { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        public int? SupplierId { get; set; }

        public bool DoPrintOnCustomerOrder { get; set; }

        public bool DoPrintOnSupplierOrder { get; set; }

        public bool DoPrintOnQuotation { get; set; }

        [MaxLength(100)]
        public string? SupplierName { get; set; }

        [Column(TypeName = "decimal(15, 5)")]
        public decimal? InPriceAttested { get; set; }

        public int? CreatedById { get; set; }

        public DateTime? CreatedDateTime { get; set; }

        public int? EditedById { get; set; }

        public DateTime? EditedDateTime { get; set; }

        [Column(TypeName = "decimal(10, 5)")]
        public decimal? AttestedInPriceCurrencyRate { get; set; }

        [Column(TypeName = "decimal(10, 5)")]
        public decimal? AttestedOutPriceCurrencyRate { get; set; }

        [MaxLength(100)]
        public string? AtttestedBySignature { get; set; }

        public DateTime? AttestedDateTime { get; set; }

        public bool DoInvoiceSeparately { get; set; }

        public bool DoInvoiceSeparatelyImmediately { get; set; }

        public bool IsCostInvoicedSeparately { get; set; }

        // Navigation properties
        public SupplierOrder? SupplierOrder { get; set; }
        public CustomerOrder? CustomerOrder { get; set; }
        public Cost? Cost { get; set; }
        public Supplier? Supplier { get; set; }
        public Currency? InPriceCurrency { get; set; }
        public ICollection<InvoiceRow> InvoiceRows { get; set; } = new List<InvoiceRow>();
    }
}
