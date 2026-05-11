using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("PalletFormat")]
    public class PalletFormat
    {
        [Key]
        [Column("lngPalletFormat_ID")]
        public int Id { get; set; }

        [Column("strPalletFormat")]
        [MaxLength(50)]
        public string? Name { get; set; }

        [Column("dblM2")]
        public double? M2 { get; set; }

        [Column("bolActive")]
        public bool Active { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("Width")]
        public int? Width { get; set; }

        [Column("Height")]
        public int? Height { get; set; }

        [Column("PalletTypeId")]
        public int? PalletTypeId { get; set; }

        [Column("DebitFactor", TypeName = "decimal(10,4)")]
        public decimal? DebitFactor { get; set; }

        [Column("TranslationCode")]
        public int? TranslationCode { get; set; }

        [Column("SortNr")]
        public int? SortNr { get; set; }

        [Column("CopyTo")]
        public bool CopyTo { get; set; }

        public ICollection<Calculation> Calculations { get; set; } = new List<Calculation>();
    }
}
