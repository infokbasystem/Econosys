using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("SupplierContactPerson")]
    public class SupplierContactPerson
    {
        [Key]
        [Column("lngSupplierContactPerson_ID")]
        public int Id { get; set; }

        [Column("lngSupplier_ID")]
        public int? SupplierId { get; set; }

        [Column("strSupplierContactPerson")]
        [MaxLength(50)]
        public string? SupplierContactPersonName { get; set; }

        [Column("strContactPerson")]
        [MaxLength(50)]
        public string? ContactPerson { get; set; }

        [Column("strTelephone")]
        [MaxLength(50)]
        public string? Telephone { get; set; }

        [Column("strCellphone")]
        [MaxLength(50)]
        public string? Cellphone { get; set; }

        [Column("strEmail")]
        [MaxLength(50)]
        public string? Email { get; set; }

        [Column("bolMailInquiry")]
        public bool MailInquiry { get; set; }

        [Column("bolMailSupplierOrder")]
        public bool MailSupplierOrder { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("DoMailTransportOrder")]
        public bool DoMailTransportOrder { get; set; }

        [Column("Title")]
        [MaxLength(100)]
        public string? Title { get; set; }

        [ForeignKey(nameof(SupplierId))]
        public virtual Supplier? Supplier { get; set; }
    }
}