using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Log")]
    public class LogEntry
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(100)]
        public string? Item { get; set; }

        public int ItemId { get; set; }

        [MaxLength(100)]
        public string? Action { get; set; }

        public DateTime DateTime { get; set; }

        public int? EmployeeId { get; set; }

        [MaxLength(1000)]
        public string? Info1 { get; set; }

        public string? Info2 { get; set; }

        [MaxLength(255)]
        public string? Source { get; set; }
    }
}
