using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class UnitDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int? TranslationCode { get; set; }
        public short? Multiplicator { get; set; }
        public bool IsDefault { get; set; }
        public bool Active { get; set; }
        public short? NrOfCalcDecimals { get; set; }
        public short? NrOfCalcDecimalsQty { get; set; }
        public int? OldDbId { get; set; }
    }

    public class CreateUnitRequest
    {
        [MaxLength(50)]
        public string? Name { get; set; }

        public int? TranslationCode { get; set; }
        public short? Multiplicator { get; set; }
        public bool IsDefault { get; set; }
        public bool Active { get; set; }
        public short? NrOfCalcDecimals { get; set; }
        public short? NrOfCalcDecimalsQty { get; set; }
        public int? OldDbId { get; set; }
    }

    public class UpdateUnitRequest
    {
        [MaxLength(50)]
        public string? Name { get; set; }

        public int? TranslationCode { get; set; }
        public short? Multiplicator { get; set; }
        public bool? IsDefault { get; set; }
        public bool? Active { get; set; }
        public short? NrOfCalcDecimals { get; set; }
        public short? NrOfCalcDecimalsQty { get; set; }
        public int? OldDbId { get; set; }
    }

    public class SearchUnitsRequest
    {
        public FilterRequest? Filter { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }
}