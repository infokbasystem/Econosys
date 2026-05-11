using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("DocumentFileRelation")]
    public class DocumentFileRelation
    {
        [Key]
        public int Id { get; set; }

        public int DocumentFileId { get; set; }

        public int? SupplierOrderId { get; set; }

        public int? CustomerOrderId { get; set; }

        public int? DeviationId { get; set; }

        public virtual DocumentFile DocumentFile { get; set; } = null!;
        public virtual SupplierOrder? SupplierOrder { get; set; }
        public virtual CustomerOrder? CustomerOrder { get; set; }
        public virtual Deviation? Deviation { get; set; }
    }
}