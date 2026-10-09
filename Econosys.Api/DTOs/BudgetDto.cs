using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class BudgetListItemDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int? Year { get; set; }
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
    }

    public class BudgetDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int? Year { get; set; }
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public decimal?[] DistributionMonths { get; set; } = new decimal?[12];
        public int? CompareBudgetId { get; set; }
        public int? ComparePrevYear { get; set; }
        public int? ComparePrevPrevYear { get; set; }
        public bool IsOwned { get; set; }
        public DateTime? OwnedDateTime { get; set; }
        public string? OwnedByUserName { get; set; }
        public int? OwnedByUserId { get; set; }
        public List<BudgetCustomerDto> Customers { get; set; } = new();
    }

    public class BudgetCustomerDto
    {
        public int Id { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public bool CustomerActive { get; set; } = true;
        public DateOnly? BudgetCountAsNewUntilMonth { get; set; }
        public int? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public decimal? TotalSalesBudget { get; set; }
        public decimal? AdditionBudget { get; set; }
        public bool IsRemovedFromBudget { get; set; }
        public decimal?[] SalesMonths { get; set; } = new decimal?[12];
        public decimal?[] AdditionMonths { get; set; } = new decimal?[12];
    }

    public class SaveBudgetRequest
    {
        [MaxLength(50)]
        public string? Name { get; set; }

        public int? Year { get; set; }
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public decimal?[]? DistributionMonths { get; set; }
        public int? CompareBudgetId { get; set; }
        public int? ComparePrevYear { get; set; }
        public int? ComparePrevPrevYear { get; set; }
        public List<SaveBudgetCustomerRequest> Customers { get; set; } = new();
    }

    public class SaveBudgetCustomerRequest
    {
        public int Id { get; set; }
        public int? CustomerId { get; set; }
        public int? EmployeeId { get; set; }
        public decimal? TotalSalesBudget { get; set; }
        public decimal? AdditionBudget { get; set; }
        public bool IsRemovedFromBudget { get; set; }
        public decimal?[]? SalesMonths { get; set; }
        public decimal?[]? AdditionMonths { get; set; }
    }

    public class BudgetEmployeeOptionDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Initials { get; set; }
    }

    public class BudgetFormOptionsDto
    {
        public List<BudgetEmployeeOptionDto> Employees { get; set; } = new();
        public List<BudgetListItemDto> Budgets { get; set; } = new();
        public List<int> Years { get; set; } = new();
    }

    public class BudgetSalesStatRequest
    {
        [Range(1900, 2200)]
        public int Year { get; set; }

        public bool Ytd { get; set; }
        public List<int> EmployeeIds { get; set; } = new();
        public List<int> ExcludeCustomerIds { get; set; } = new();
    }

    public class BudgetSalesStatMonthDto
    {
        public int Month { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalTb { get; set; }
        public decimal AverageAddition { get; set; }
    }

    public class BudgetSalesStatCustomerDto
    {
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalTb { get; set; }
        public decimal AverageAddition { get; set; }
    }

    public class BudgetOwnershipDto
    {
        public bool IsOwned { get; set; }
        public int? OwnedByUserId { get; set; }
        public string? OwnedByUserName { get; set; }
        public DateTime? OwnedDateTime { get; set; }
        public bool IsOwnedByCurrentUser { get; set; }
    }

    public class ClaimBudgetOwnershipRequest
    {
        public bool Force { get; set; }
    }

    public class BudgetAvailableCustomerDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int? ResponsibleUserId { get; set; }
        public string? ResponsibleUserName { get; set; }
        public DateOnly? BudgetCountAsNewUntilMonth { get; set; }
    }

    public class SetBudgetCustomerActiveRequest
    {
        public bool Active { get; set; }
    }

    public class AllocateBudgetCustomerRequest
    {
        [Required]
        public int? EmployeeId { get; set; }

        [Required]
        public DateTime? AllocateFromDate { get; set; }
    }
}
