using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Construction")]
    public class Construction
    {
        [Key]
        [Column("lngConstruction_ID")]
        public int Id { get; set; }

        [Column("strConstruction")]
        [MaxLength(50)]
        public string? Name { get; set; }

        [Column("lngTranslationCode")]
        public int? TranslationCode { get; set; }

        [Column("bolActive")]
        public bool Active { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("IsPackaging")]
        public bool? IsPackaging { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
