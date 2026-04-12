using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class InventoryDto
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string? Name { get; set; }
        public int? AccountNr { get; set; }
        public int? OldDbId { get; set; }
        public bool IsInventory { get; set; }
        public string? Email { get; set; }
        public bool IsOmlast { get; set; }
        public string? Address { get; set; }
        public int? PostalNr { get; set; }
        public string? PostalAddress { get; set; }
        public string? CountryCode { get; set; }
        public string? Country { get; set; }
        public string? PostalNrText { get; set; }
        public bool NoInventoryValue { get; set; }
        public int? PositionId { get; set; }
        public string? AddressExtra { get; set; }
    }

    public class CreateInventoryRequest
    {
        [Required]
        public int CompanyId { get; set; }

        [MaxLength(100)]
        public string? Name { get; set; }

        public int? AccountNr { get; set; }
        public int? OldDbId { get; set; }
        public bool IsInventory { get; set; }

        [MaxLength(50)]
        [EmailAddress]
        public string? Email { get; set; }

        public bool IsOmlast { get; set; }

        [MaxLength(50)]
        public string? Address { get; set; }

        public int? PostalNr { get; set; }

        [MaxLength(50)]
        public string? PostalAddress { get; set; }

        [MaxLength(10)]
        public string? CountryCode { get; set; }

        [MaxLength(50)]
        public string? Country { get; set; }

        [MaxLength(50)]
        public string? PostalNrText { get; set; }

        public bool NoInventoryValue { get; set; }
        public int? PositionId { get; set; }

        [MaxLength(50)]
        public string? AddressExtra { get; set; }
    }

    public class SearchInventoriesRequest
    {
        public FilterRequest? Filter { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }
}
