using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Currency")]
    public class Currency
    {
        [Key]
        [Column("lngCurrency_ID")]
        public int Id { get; set; }

        [Column("strCurrency")]
        [MaxLength(50)]
        public string? Name { get; set; }

        [Column("lngTranslationCode")]
        public int? TranslationCode { get; set; }

        [Column("bolDefault")]
        public bool IsDefault { get; set; }

        [Column("dblRateToSEK")]
        public double? RateToSek { get; set; }

        [Column("bolActive")]
        public bool Active { get; set; }

        [Column("dblRateStockValue")]
        public double? RateStockValue { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("SpcsKey")]
        [MaxLength(4)]
        public string? SpcsKey { get; set; }

        [Column("WarningTolerancePercent")]
        public int? WarningTolerancePercent { get; set; }

        public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();
        public virtual ICollection<SupplierOrder> PurchaseSupplierOrders { get; set; } = new List<SupplierOrder>();
        public virtual ICollection<CustomerOrder> SalesCustomerOrders { get; set; } = new List<CustomerOrder>();
        public virtual ICollection<CustomerOrder> SupplierPricePerEurPalletCustomerOrders { get; set; } = new List<CustomerOrder>();
    }
}