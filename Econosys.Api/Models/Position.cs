using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Position")]
    public class Position
    {
        [Key]
        public int Id { get; set; }

        [Column(TypeName = "varchar(100)")]
        public string? Name { get; set; }

        [Column(TypeName = "varchar(200)")]
        public string? Street { get; set; }

        [Column(TypeName = "varchar(50)")]
        public string? PostalNr { get; set; }

        [Column(TypeName = "varchar(100)")]
        public string? PostalAddress { get; set; }

        [Column(TypeName = "varchar(10)")]
        public string? CountryCode { get; set; }

        [Column(TypeName = "varchar(100)")]
        public string? Country { get; set; }

        [Column(TypeName = "decimal(12,8)")]
        public decimal? Longitude { get; set; }

        [Column(TypeName = "decimal(12,8)")]
        public decimal? Latitude { get; set; }
    }
}