using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("PalletType")]
    public class PalletType
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("PalletType")]
        [MaxLength(100)]
        public string? Name { get; set; }

        [Column("IsEur")]
        public bool IsEur { get; set; }

        [Column("IsPallet")]
        public bool IsPallet { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }

        [Column("SortNr")]
        public int? SortNr { get; set; }

        public ICollection<PalletFormat> PalletFormats { get; set; } = new List<PalletFormat>();
    }
}
