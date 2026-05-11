using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("PackagingFeeCagetory")]
    public class PackagingFeeCategory
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("CategoryCode")]
        [MaxLength(20)]
        public string? CategoryCode { get; set; }

        [Column("Description")]
        [MaxLength(80)]
        public string? Description { get; set; }

        public ICollection<Material> Materials { get; set; } = new List<Material>();
    }
}
