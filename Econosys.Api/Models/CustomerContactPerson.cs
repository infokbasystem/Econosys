using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("CustomerContactPerson")]
    public class CustomerContactPerson
    {
        [Key]
        [Column("lngCustomerContactPerson_ID")]
        public int Id { get; set; }

        [Column("lngCustomer_ID")]
        public int? CustomerId { get; set; }

        [Column("strCustomerContactPerson")]
        [MaxLength(50)]
        public string? CustomerContactPersonName { get; set; }

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
        [MaxLength(255)]
        public string? Email { get; set; }

        [Column("bolMailQuotation")]
        public bool MailQuotation { get; set; }

        [Column("bolMailCustomerOrder")]
        public bool MailCustomerOrder { get; set; }

        [Column("bolMailInvoice")]
        public bool MailInvoice { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("MailTransportOrder")]
        public bool MailTransportOrder { get; set; }

        [Column("MailGeneralInfo")]
        public bool MailGeneralInfo { get; set; }

        [Column("MailCallOffConfirmation")]
        public bool MailCallOffConfirmation { get; set; }

        [Column("Title")]
        [MaxLength(100)]
        public string? Title { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }
    }
}