using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("AccountType")]
    public class AccountType
    {
        [Key]
        [Column("lngAccountType_ID")]
        public int Id { get; set; }

        [Column("strAccountType")]
        [MaxLength(50)]
        public string? Name { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        public ICollection<Account> Accounts { get; set; } = new List<Account>();
    }
}
