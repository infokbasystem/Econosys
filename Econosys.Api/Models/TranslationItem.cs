using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("TranslationItem")]
    public class TranslationItem
    {
        [Key]
        [Column("lngTranslationItem_ID")]
        public int Id { get; set; }

        [Column("lngTranslationCode")]
        public int? TranslationCode { get; set; }

        [Column("strLangCode")]
        [MaxLength(50)]
        public string? LangCode { get; set; }

        [Column("strTranslation")]
        [MaxLength(255)]
        public string? Translation { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("TranslationHtml", TypeName = "varchar(8000)")]
        [MaxLength(8000)]
        public string? TranslationHtml { get; set; }
    }
}