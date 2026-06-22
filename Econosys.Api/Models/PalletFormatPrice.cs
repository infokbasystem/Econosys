using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("PalletFormatPrice")]
    public class PalletFormatPrice
    {
        [Key]
        public int Id { get; set; }

        public int? CustomerId { get; set; }

        public int? SupplierId { get; set; }

        public int? PalletFormatId { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal? PalletPrice { get; set; }

        public Customer? Customer { get; set; }
        public Supplier? Supplier { get; set; }
        public PalletFormat? PalletFormat { get; set; }
    }
}