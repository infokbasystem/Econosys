using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class PalletFactorDto
    {
        public int Id { get; set; }
        public int LengthFrom { get; set; }
        public int LengthTo { get; set; }
        public int WidthFrom { get; set; }
        public int WidthTo { get; set; }
        public string? PalletFactors { get; set; }
    }

    public class SearchPalletFactorsRequest
    {
        [Required]
        [Range(0, int.MaxValue)]
        public int? LengthFrom { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int? LengthTo { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int? WidthFrom { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int? WidthTo { get; set; }

        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 100;
    }

    public class UpdatePalletFactorRequest
    {
        [MaxLength(500)]
        public string? PalletFactors { get; set; }
    }
}