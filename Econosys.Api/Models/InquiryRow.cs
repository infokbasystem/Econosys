using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("InquiryRow")]
    public class InquiryRow
    {
        [Key]
        public int Id { get; set; }

        [Column("InquiryId")]
        public int? InquiryId { get; set; }

        [Column("CalculationRowId")]
        public int? CalculationRowId { get; set; }

        [Column("Edition")]
        public int? Edition { get; set; }

        public virtual Inquiry? Inquiry { get; set; }
        public virtual CalculationRow? CalculationRow { get; set; }
    }
}
