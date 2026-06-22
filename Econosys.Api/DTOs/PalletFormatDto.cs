using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class PalletFormatDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public double? M2 { get; set; }
        public decimal? SupplierPrice { get; set; }
        public decimal? CustomerPrice { get; set; }
        public bool Active { get; set; }
        public int? OldDbId { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int? PalletTypeId { get; set; }
        public decimal? DebitFactor { get; set; }
        public int? TranslationCode { get; set; }
        public List<EntityTranslationDto> Translations { get; set; } = new();
        public int? SortNr { get; set; }
        public bool CopyTo { get; set; }
    }

    public class CreatePalletFormatRequest
    {
        [MaxLength(50)]
        public string? Name { get; set; }

        public double? M2 { get; set; }
        public bool Active { get; set; }
        public int? OldDbId { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }

        [Required]
        public int? PalletTypeId { get; set; }
        public decimal? DebitFactor { get; set; }
        public int? TranslationCode { get; set; }
        public List<EntityTranslationRequest>? Translations { get; set; }
        public int? SortNr { get; set; }
        public bool CopyTo { get; set; }
    }

    public class UpdatePalletFormatRequest
    {
        [MaxLength(50)]
        public string? Name { get; set; }

        public double? M2 { get; set; }
        public bool? Active { get; set; }
        public int? OldDbId { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }

        [Required]
        public int? PalletTypeId { get; set; }
        public decimal? DebitFactor { get; set; }
        public int? TranslationCode { get; set; }
        public List<EntityTranslationRequest>? Translations { get; set; }
        public int? SortNr { get; set; }
        public bool? CopyTo { get; set; }
    }
}
