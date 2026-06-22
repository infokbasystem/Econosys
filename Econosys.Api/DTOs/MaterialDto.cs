using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class MaterialDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int? TranslationCode { get; set; }
        public List<EntityTranslationDto> Translations { get; set; } = new();
        public bool Active { get; set; }
        public int? MaterialGroup { get; set; }
        public string? Color1 { get; set; }
        public string? Color2 { get; set; }
        public string? Layer1Material { get; set; }
        public int? Layer1Weight { get; set; }
        public string? Layer1Thickness { get; set; }
        public string? Layer2Material { get; set; }
        public int? Layer2Weight { get; set; }
        public string? Layer2Thickness { get; set; }
        public string? Layer3Material { get; set; }
        public int? Layer3Weight { get; set; }
        public string? Layer3Thickness { get; set; }
        public string? Layer4Material { get; set; }
        public int? Layer4Weight { get; set; }
        public string? Layer4Thickness { get; set; }
        public string? Layer5Material { get; set; }
        public int? Layer5Weight { get; set; }
        public string? Layer5Thickness { get; set; }
        public double? ThicknessMm { get; set; }
        public int? WeightGr { get; set; }
        public string? MaterialText { get; set; }
        public int? OldDbId { get; set; }
        public int? PackagingFeeCategoryId { get; set; }
    }

    public class CreateMaterialRequest
    {
        [MaxLength(200)]
        public string? Name { get; set; }

        public int? TranslationCode { get; set; }
        public List<EntityTranslationRequest>? Translations { get; set; }
        public bool Active { get; set; }
        public int? MaterialGroup { get; set; }

        [MaxLength(50)]
        public string? Color1 { get; set; }

        [MaxLength(50)]
        public string? Color2 { get; set; }

        [MaxLength(50)]
        public string? Layer1Material { get; set; }

        public int? Layer1Weight { get; set; }

        [MaxLength(50)]
        public string? Layer1Thickness { get; set; }

        [MaxLength(50)]
        public string? Layer2Material { get; set; }

        public int? Layer2Weight { get; set; }

        [MaxLength(50)]
        public string? Layer2Thickness { get; set; }

        [MaxLength(50)]
        public string? Layer3Material { get; set; }

        public int? Layer3Weight { get; set; }

        [MaxLength(50)]
        public string? Layer3Thickness { get; set; }

        [MaxLength(50)]
        public string? Layer4Material { get; set; }

        public int? Layer4Weight { get; set; }

        [MaxLength(50)]
        public string? Layer4Thickness { get; set; }

        [MaxLength(50)]
        public string? Layer5Material { get; set; }

        public int? Layer5Weight { get; set; }

        [MaxLength(50)]
        public string? Layer5Thickness { get; set; }

        public double? ThicknessMm { get; set; }
        public int? WeightGr { get; set; }

        [MaxLength(200)]
        public string? MaterialText { get; set; }

        public int? OldDbId { get; set; }
        public int? PackagingFeeCategoryId { get; set; }
    }

    public class UpdateMaterialRequest
    {
        [MaxLength(200)]
        public string? Name { get; set; }

        public int? TranslationCode { get; set; }
        public List<EntityTranslationRequest>? Translations { get; set; }
        public bool? Active { get; set; }
        public int? MaterialGroup { get; set; }

        [MaxLength(50)]
        public string? Color1 { get; set; }

        [MaxLength(50)]
        public string? Color2 { get; set; }

        [MaxLength(50)]
        public string? Layer1Material { get; set; }

        public int? Layer1Weight { get; set; }

        [MaxLength(50)]
        public string? Layer1Thickness { get; set; }

        [MaxLength(50)]
        public string? Layer2Material { get; set; }

        public int? Layer2Weight { get; set; }

        [MaxLength(50)]
        public string? Layer2Thickness { get; set; }

        [MaxLength(50)]
        public string? Layer3Material { get; set; }

        public int? Layer3Weight { get; set; }

        [MaxLength(50)]
        public string? Layer3Thickness { get; set; }

        [MaxLength(50)]
        public string? Layer4Material { get; set; }

        public int? Layer4Weight { get; set; }

        [MaxLength(50)]
        public string? Layer4Thickness { get; set; }

        [MaxLength(50)]
        public string? Layer5Material { get; set; }

        public int? Layer5Weight { get; set; }

        [MaxLength(50)]
        public string? Layer5Thickness { get; set; }

        public double? ThicknessMm { get; set; }
        public int? WeightGr { get; set; }

        [MaxLength(200)]
        public string? MaterialText { get; set; }

        public int? OldDbId { get; set; }
        public int? PackagingFeeCategoryId { get; set; }
    }

    public class SearchMaterialsRequest
    {
        public FilterRequest? Filter { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }
}
