using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("ProductInternalInfo")]
    public class ProductInternalInfo
    {
        [Key]
        [ForeignKey(nameof(Product))]
        public int ProductId { get; set; }

        public bool IsOffset { get; set; }
        public bool IsDigital { get; set; }
        public bool IsFlexo { get; set; }
        public DateTime? PpwrUpdatedAt { get; set; }

        public Product Product { get; set; } = null!;
    }
}
