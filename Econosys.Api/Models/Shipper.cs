using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Shipper")]
    public class Shipper
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(100)]
        public string? Name { get; set; }

        [Column("ContactPErson")]
        [MaxLength(100)]
        public string? ContactPerson { get; set; }

        [MaxLength(200)]
        public string? Telephone { get; set; }

        [MaxLength(200)]
        public string? Mail { get; set; }

        public virtual ICollection<CallOff> CallOffs { get; set; } = new List<CallOff>();
    }
}
