using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Unit")]
    public class Unit
    {
        [Key]
        [Column("lngUnit_ID")]
        public int Id { get; set; }

        [Column("strUnit")]
        [MaxLength(50)]
        public string? Name { get; set; }

        [Column("lngTranslationCode")]
        public int? TranslationCode { get; set; }

        [Column("intMultiplicator")]
        public short? Multiplicator { get; set; }

        [Column("bolDefault")]
        public bool IsDefault { get; set; }

        [Column("bolActive")]
        public bool Active { get; set; }

        [Column("intNrOfCalcDecimals")]
        public short? NrOfCalcDecimals { get; set; }

        [Column("intNrOfCalcDecimalsQty")]
        public short? NrOfCalcDecimalsQty { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        public virtual ICollection<CustomerOrder> CustomerOrders { get; set; } = new List<CustomerOrder>();
        public virtual ICollection<SupplierOrder> SupplierOrders { get; set; } = new List<SupplierOrder>();
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
        public virtual ICollection<InvoiceRow> InvoiceRows { get; set; } = new List<InvoiceRow>();
        public virtual ICollection<Calculation> Calculations { get; set; } = new List<Calculation>();
    }
}