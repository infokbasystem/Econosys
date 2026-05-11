using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Material")]
    public class Material
    {
        [Key]
        [Column("lngMaterial_ID")]
        public int Id { get; set; }

        [Column("strMaterial")]
        [MaxLength(200)]
        public string? Name { get; set; }

        [Column("lngTranslationCode")]
        public int? TranslationCode { get; set; }

        [Column("bolActive")]
        public bool Active { get; set; }

        [Column("MaterialGroup")]
        public int? MaterialGroup { get; set; }

        [Column("Color1")]
        [MaxLength(50)]
        public string? Color1 { get; set; }

        [Column("Color2")]
        [MaxLength(50)]
        public string? Color2 { get; set; }

        [Column("Layer1Material")]
        [MaxLength(50)]
        public string? Layer1Material { get; set; }

        [Column("Layer1Weight")]
        public int? Layer1Weight { get; set; }

        [Column("Layer1Thickness")]
        [MaxLength(50)]
        public string? Layer1Thickness { get; set; }

        [Column("Layer2Material")]
        [MaxLength(50)]
        public string? Layer2Material { get; set; }

        [Column("Layer2Weight")]
        public int? Layer2Weight { get; set; }

        [Column("Layer2Thickness")]
        [MaxLength(50)]
        public string? Layer2Thickness { get; set; }

        [Column("Layer3Material")]
        [MaxLength(50)]
        public string? Layer3Material { get; set; }

        [Column("Layer3Weight")]
        public int? Layer3Weight { get; set; }

        [Column("Layer3Thickness")]
        [MaxLength(50)]
        public string? Layer3Thickness { get; set; }

        [Column("Layer4Material")]
        [MaxLength(50)]
        public string? Layer4Material { get; set; }

        [Column("Layer4Weight")]
        public int? Layer4Weight { get; set; }

        [Column("Layer4Thickness")]
        [MaxLength(50)]
        public string? Layer4Thickness { get; set; }

        [Column("Layer5Material")]
        [MaxLength(50)]
        public string? Layer5Material { get; set; }

        [Column("Layer5Weight")]
        public int? Layer5Weight { get; set; }

        [Column("Layer5Thickness")]
        [MaxLength(50)]
        public string? Layer5Thickness { get; set; }

        [Column("ThicknessMm")]
        public double? ThicknessMm { get; set; }

        [Column("WeightGr")]
        public int? WeightGr { get; set; }

        [Column("Material")]
        [MaxLength(200)]
        public string? MaterialText { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("PackagingFeeCategoryId")]
        public int? PackagingFeeCategoryId { get; set; }

        public PackagingFeeCategory? PackagingFeeCategory { get; set; }
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
