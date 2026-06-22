using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("ImprovementProposition")]
    public class ImprovementProposition
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        [Column(TypeName = "varchar(200)")]
        public string CreatedByName { get; set; } = string.Empty;

        [Required]
        public DateTime CreatedTimestamp { get; set; }

        [Required]
        [MaxLength(4000)]
        [Column(TypeName = "varchar(4000)")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(4000)]
        [Column(TypeName = "varchar(4000)")]
        public string ProposedMeasure { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        [Column(TypeName = "varchar(200)")]
        public string Responsible { get; set; } = string.Empty;

        [Required]
        public ImprovementPropositionAreaCode AreaCode { get; set; } = ImprovementPropositionAreaCode.SALES;

        [Required]
        public ImprovementPropositionStatusCode StatusCode { get; set; } = ImprovementPropositionStatusCode.NEW;

        [MaxLength(4000)]
        [Column(TypeName = "varchar(4000)")]
        public string? Note { get; set; }

        [MaxLength(4000)]
        [Column(TypeName = "varchar(4000)")]
        public string? FollowUp { get; set; }
    }
}
