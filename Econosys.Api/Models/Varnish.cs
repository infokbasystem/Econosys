using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Varnish")]
    public class Varnish
    {
        [Key]
        [Column("lngVarnish_ID")]
        public int Id { get; set; }

        [Column("strVarnish")]
        [MaxLength(50)]
        public string? Name { get; set; }

        [Column("lngTranslationCode")]
        public int? TranslationCode { get; set; }

        [Column("bolActive")]
        public bool Active { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
