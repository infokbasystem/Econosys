using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class PalletTypeDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public bool IsEur { get; set; }
        public bool IsPallet { get; set; }
        public bool IsActive { get; set; }
        public int? SortNr { get; set; }
    }

    public class CreatePalletTypeRequest
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        public bool IsEur { get; set; }
        public bool IsPallet { get; set; }
        public bool IsActive { get; set; }
        public int? SortNr { get; set; }
    }

    public class UpdatePalletTypeRequest
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        public bool? IsEur { get; set; }
        public bool? IsPallet { get; set; }
        public bool? IsActive { get; set; }
        public int? SortNr { get; set; }
    }
}
