using System.ComponentModel.DataAnnotations;
using Econosys.Api.Models;

namespace Econosys.Api.DTOs
{
    public class ImprovementPropositionDto
    {
        public int Id { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedTimestamp { get; set; }
        public string Description { get; set; } = string.Empty;
        public string ProposedMeasure { get; set; } = string.Empty;
        public string Responsible { get; set; } = string.Empty;
        public ImprovementPropositionAreaCode AreaCode { get; set; }
        public ImprovementPropositionStatusCode StatusCode { get; set; }
        public string? Note { get; set; }
        public string? FollowUp { get; set; }
    }

    public class CreateImprovementPropositionRequest
    {
        [Required]
        [MaxLength(200)]
        public string CreatedByName { get; set; } = string.Empty;

        public DateTime? CreatedTimestamp { get; set; }

        [Required]
        [MaxLength(4000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(4000)]
        public string ProposedMeasure { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Responsible { get; set; } = string.Empty;

        [Required]
        public ImprovementPropositionAreaCode AreaCode { get; set; } = ImprovementPropositionAreaCode.SALES;

        [Required]
        public ImprovementPropositionStatusCode StatusCode { get; set; } = ImprovementPropositionStatusCode.NEW;

        [MaxLength(4000)]
        public string? Note { get; set; }

        [MaxLength(4000)]
        public string? FollowUp { get; set; }
    }

    public class UpdateImprovementPropositionRequest
    {
        [MaxLength(200)]
        public string? CreatedByName { get; set; }

        public DateTime? CreatedTimestamp { get; set; }

        [MaxLength(4000)]
        public string? Description { get; set; }

        [MaxLength(4000)]
        public string? ProposedMeasure { get; set; }

        [MaxLength(200)]
        public string? Responsible { get; set; }

        public ImprovementPropositionAreaCode? AreaCode { get; set; }

        public ImprovementPropositionStatusCode? StatusCode { get; set; }

        [MaxLength(4000)]
        public string? Note { get; set; }

        [MaxLength(4000)]
        public string? FollowUp { get; set; }
    }

    public class SearchImprovementPropositionsRequest
    {
        public string? SearchTerm { get; set; }
        public ImprovementPropositionStatusCode? StatusCode { get; set; }
        public ImprovementPropositionAreaCode? AreaCode { get; set; }
        public string? Responsible { get; set; }
        public DateTime? CreatedFrom { get; set; }
        public DateTime? CreatedTo { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
    }
}
