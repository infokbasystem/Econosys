using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class DeviationCostDto
    {
        public int Id { get; set; }
        public int? DeviationId { get; set; }
        public string? Text { get; set; }
        public decimal? CostSEK { get; set; }
    }

    public class DeviationDto
    {
        public int Id { get; set; }
        public int? CustomerOrderId { get; set; }
        public string? CustomerOrderNr { get; set; }
        public string? CustomerOrderCustomerName { get; set; }
        public bool IsInternal { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? DeviationNr { get; set; }
        public int? CreatedByUserId { get; set; }
        public string? CreatedByUserName { get; set; }
        public DateTime? CreatedTimeStamp { get; set; }
        public int? EditedByUserId { get; set; }
        public string? EditedByUserName { get; set; }
        public DateTime? EditedTimeStamp { get; set; }
        public int? ResponsibleUserId { get; set; }
        public string? ResponsibleUserName { get; set; }
        public DateTime? DeviationOpened { get; set; }
        public DateTime? DeviationClosed { get; set; }
        public int? OpenDays { get; set; }
        public string? DeviationProcessCode { get; set; }
        public int? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? DeviationTypeCode { get; set; }
        public string? Description { get; set; }
        public string? RootCause { get; set; }
        public string? MeasureTaken { get; set; }
        public decimal? EstimatedInternalCostSEK { get; set; }
        public decimal? EstimatedInternalCostPercentageOfOrder { get; set; }
        public string? SupplierDecisionCode { get; set; }
        public string? CompanyDecisionCode { get; set; }
        public decimal? ActualInternalCostSEK { get; set; }
        public decimal? NrOfProduced { get; set; }
        public decimal? NrOfClaimed { get; set; }
        public decimal? FreightCostPer1000 { get; set; }
        public decimal? PricePer1000 { get; set; }
        public int? FreightCostCurrencyId { get; set; }
        public string? ClaimReason { get; set; }
        public string? FollowupInfo { get; set; }
        public DateTime? DeviationClosedToSupplier { get; set; }
        public IReadOnlyList<DeviationCostDto> Costs { get; set; } = Array.Empty<DeviationCostDto>();
        public IReadOnlyList<DocumentFileDto> AttachedFiles { get; set; } = Array.Empty<DocumentFileDto>();
    }

    public class SearchDeviationsRequest
    {
        public string? SearchTerm { get; set; }
        public string? Status { get; set; }
        public bool? IsInternal { get; set; }
        public int? CustomerOrderId { get; set; }
        public int? SupplierId { get; set; }
        public int? CustomerId { get; set; }
        public int? ResponsibleUserId { get; set; }
        public DateTime? OpenedFrom { get; set; }
        public DateTime? OpenedTo { get; set; }
        public DateTime? ClosedFrom { get; set; }
        public DateTime? ClosedTo { get; set; }
        public string? DeviationProcessCode { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }

    public class DeviationOverviewDto
    {
        public int Year { get; set; }
        public IReadOnlyList<DeviationOpenItemDto> OpenItems { get; set; } = Array.Empty<DeviationOpenItemDto>();
        public IReadOnlyList<DeviationMonthlyStatsDto> MonthlyStats { get; set; } = Array.Empty<DeviationMonthlyStatsDto>();
    }

    public class DeviationOpenItemDto
    {
        public int Id { get; set; }
        public string? ResponsibleUserName { get; set; }
        public string? DeviationNr { get; set; }
        public string? CustomerOrderNr { get; set; }
        public string? CustomerName { get; set; }
        public string? SupplierName { get; set; }
        public int OpenDays { get; set; }
    }

    public class DeviationMonthlyStatsDto
    {
        public int MonthNumber { get; set; }
        public int OpenedCount { get; set; }
        public int ClosedCount { get; set; }
        public int OpenAtMonthEndCount { get; set; }
        public decimal AverageHandlingDays { get; set; }
        public decimal HandledWithin10DaysPercent { get; set; }
        public decimal ActualInternalCostSek { get; set; }
    }

    public class DeviationFilterOptionsDto
    {
        public IReadOnlyList<FilterOptionDto<string>> Statuses { get; set; } = Array.Empty<FilterOptionDto<string>>();
        public IReadOnlyList<FilterOptionDto<int>> ResponsibleUsers { get; set; } = Array.Empty<FilterOptionDto<int>>();
        public IReadOnlyList<FilterOptionDto<int>> Customers { get; set; } = Array.Empty<FilterOptionDto<int>>();
        public IReadOnlyList<FilterOptionDto<int>> Suppliers { get; set; } = Array.Empty<FilterOptionDto<int>>();
    }

    public class DeviationEnumOptionsDto
    {
        public IReadOnlyList<FilterOptionDto<string>> DeviationProcessCodes { get; set; } = Array.Empty<FilterOptionDto<string>>();
        public IReadOnlyList<FilterOptionDto<string>> DeviationTypeCodes { get; set; } = Array.Empty<FilterOptionDto<string>>();
        public IReadOnlyList<FilterOptionDto<string>> SupplierDecisionCodes { get; set; } = Array.Empty<FilterOptionDto<string>>();
        public IReadOnlyList<FilterOptionDto<string>> CompanyDecisionCodes { get; set; } = Array.Empty<FilterOptionDto<string>>();
    }

    public class DeviationFormOptionsDto : DeviationEnumOptionsDto
    {
        public IReadOnlyList<FilterOptionDto<int>> Suppliers { get; set; } = Array.Empty<FilterOptionDto<int>>();
        public IReadOnlyList<FilterOptionDto<int>> Customers { get; set; } = Array.Empty<FilterOptionDto<int>>();
        public IReadOnlyList<FilterOptionDto<int>> Users { get; set; } = Array.Empty<FilterOptionDto<int>>();
        public IReadOnlyList<FilterOptionDto<int>> Currencies { get; set; } = Array.Empty<FilterOptionDto<int>>();
    }

    public class CreateDeviationCostRequest
    {
        [MaxLength(100)]
        public string? Text { get; set; }

        public decimal? CostSEK { get; set; }
    }

    public class CreateDeviationRequest
    {
        public int? CustomerOrderId { get; set; }
        public bool IsInternal { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? DeviationNr { get; set; }

        public int? CreatedByUserId { get; set; }
        public DateTime? CreatedTimeStamp { get; set; }
        public int? EditedByUserId { get; set; }
        public DateTime? EditedTimeStamp { get; set; }
        public int? ResponsibleUserId { get; set; }
        public DateTime? DeviationOpened { get; set; }
        public DateTime? DeviationClosed { get; set; }

        [MaxLength(50)]
        public string? DeviationProcessCode { get; set; }

        public int? SupplierId { get; set; }
        public int? CustomerId { get; set; }

        [MaxLength(50)]
        public string? DeviationTypeCode { get; set; }

        [MaxLength(4000)]
        public string? Description { get; set; }

        [MaxLength(4000)]
        public string? RootCause { get; set; }

        [MaxLength(4000)]
        public string? MeasureTaken { get; set; }

        public decimal? EstimatedInternalCostSEK { get; set; }
        public decimal? EstimatedInternalCostPercentageOfOrder { get; set; }

        [MaxLength(100)]
        public string? SupplierDecisionCode { get; set; }

        [MaxLength(100)]
        public string? CompanyDecisionCode { get; set; }

        public decimal? ActualInternalCostSEK { get; set; }
        public decimal? NrOfProduced { get; set; }
        public decimal? NrOfClaimed { get; set; }
        public decimal? FreightCostPer1000 { get; set; }
        public decimal? PricePer1000 { get; set; }
        public int? FreightCostCurrencyId { get; set; }

        [MaxLength(4000)]
        public string? ClaimReason { get; set; }

        [MaxLength(4000)]
        public string? FollowupInfo { get; set; }

        public DateTime? DeviationClosedToSupplier { get; set; }
        public List<CreateDeviationCostRequest>? Costs { get; set; }
    }

    public class UpdateDeviationRequest
    {
        public int? CustomerOrderId { get; set; }
        public bool? IsInternal { get; set; }

        [MaxLength(50)]
        public string? Status { get; set; }

        [MaxLength(100)]
        public string? DeviationNr { get; set; }

        public int? CreatedByUserId { get; set; }
        public DateTime? CreatedTimeStamp { get; set; }
        public int? EditedByUserId { get; set; }
        public DateTime? EditedTimeStamp { get; set; }
        public int? ResponsibleUserId { get; set; }
        public DateTime? DeviationOpened { get; set; }
        public DateTime? DeviationClosed { get; set; }

        [MaxLength(50)]
        public string? DeviationProcessCode { get; set; }

        public int? SupplierId { get; set; }
        public int? CustomerId { get; set; }

        [MaxLength(50)]
        public string? DeviationTypeCode { get; set; }

        [MaxLength(4000)]
        public string? Description { get; set; }

        [MaxLength(4000)]
        public string? RootCause { get; set; }

        [MaxLength(4000)]
        public string? MeasureTaken { get; set; }

        public decimal? EstimatedInternalCostSEK { get; set; }
        public decimal? EstimatedInternalCostPercentageOfOrder { get; set; }

        [MaxLength(100)]
        public string? SupplierDecisionCode { get; set; }

        [MaxLength(100)]
        public string? CompanyDecisionCode { get; set; }

        public decimal? ActualInternalCostSEK { get; set; }
        public decimal? NrOfProduced { get; set; }
        public decimal? NrOfClaimed { get; set; }
        public decimal? FreightCostPer1000 { get; set; }
        public decimal? PricePer1000 { get; set; }
        public int? FreightCostCurrencyId { get; set; }

        [MaxLength(4000)]
        public string? ClaimReason { get; set; }

        [MaxLength(4000)]
        public string? FollowupInfo { get; set; }

        public DateTime? DeviationClosedToSupplier { get; set; }
        public List<CreateDeviationCostRequest>? Costs { get; set; }
    }

    public class CustomerOrderLinkedDataDto
    {
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public decimal? PricePer1000 { get; set; }
        public decimal? NrOfProduced { get; set; }
        public decimal? FreightCostPer1000 { get; set; }
    }
}
