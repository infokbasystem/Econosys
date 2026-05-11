using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Product")]
    public class Product
    {
        [Key]
        [Column("lngProduct_ID")]
        public int Id { get; set; }

        [Column("strProduct")]
        [MaxLength(50)]
        public string? Name { get; set; }

        [Column("strNoteInternal")]
        [MaxLength(255)]
        public string? NoteInternal { get; set; }

        [Column("strNoteExternal")]
        [MaxLength(255)]
        public string? NoteExternal { get; set; }

        [Column("strNoteExtra")]
        [MaxLength(255)]
        public string? NoteExtra { get; set; }

        [Column("lngMaterial_ID")]
        public int? MaterialId { get; set; }

        [Column("strMaterialThickness")]
        [MaxLength(50)]
        public string? MaterialThickness { get; set; }

        [Column("lngLength_mm")]
        public int? LengthMm { get; set; }

        [Column("lngWidth_mm")]
        public int? WidthMm { get; set; }

        [Column("lngHeight_mm")]
        public int? HeightMm { get; set; }

        [Column("strFormat")]
        [MaxLength(50)]
        public string? Format { get; set; }

        [Column("strNrOfColors")]
        [MaxLength(255)]
        public string? NrOfColors { get; set; }

        [Column("lngConstruction_ID")]
        public int? ConstructionId { get; set; }

        [Column("lngVarnish_ID")]
        public int? VarnishId { get; set; }

        [Column("strVarnish_Other")]
        [MaxLength(50)]
        public string? VarnishOther { get; set; }

        [Column("lngNetWeightPer1000")]
        public int? NetWeightPer1000 { get; set; }

        [Column("dteCreated")]
        public DateTime? CreatedAt { get; set; }

        [Column("dteEdited")]
        public DateTime? EditedAt { get; set; }

        [Column("lngCreatedBy")]
        public int? CreatedBy { get; set; }

        [Column("lngEditedBy")]
        public int? EditedBy { get; set; }

        [Column("strProductCode")]
        [MaxLength(50)]
        public string? ProductCode { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("MaterialGroup")]
        public int? MaterialGroup { get; set; }

        [Column("Active")]
        public bool Active { get; set; }

        [Column("IsServicePackaging")]
        public bool? IsServicePackaging { get; set; }

        [Column("LastSupplierOrderCreated")]
        public DateTime? LastSupplierOrderCreated { get; set; }

        [Column("LastCustomerOrderCreated")]
        public DateTime? LastCustomerOrderCreated { get; set; }

        public Material? Material { get; set; }
        public Construction? Construction { get; set; }
        public Varnish? Varnish { get; set; }
        public ICollection<Calculation> Calculations { get; set; } = new List<Calculation>();
        public ICollection<DocumentFile> DocumentFiles { get; set; } = new List<DocumentFile>();
    }
}
