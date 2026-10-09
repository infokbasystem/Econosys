using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("PalletFactor")]
    public class PalletFactor
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int LengthFrom { get; set; }

        public int LengthTo { get; set; }

        public int WidthFrom { get; set; }

        public int WidthTo { get; set; }

        [MaxLength(500)]
        [Column(TypeName = "varchar(500)")]
        public string? PalletFactors { get; set; }
    }
}