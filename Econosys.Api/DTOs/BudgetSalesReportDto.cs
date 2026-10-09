namespace Econosys.Api.DTOs
{
    public class BudgetSalesReportDto
    {
        public int BudgetId { get; set; }
        public string? BudgetName { get; set; }
        public int Year { get; set; }
        public List<BudgetSalesReportEmployeeDto> Employees { get; set; } = new();
        public List<BudgetSalesReportCustomerDto> Customers { get; set; } = new();
        public List<BudgetSalesReportMetricRowDto> CurrentYear { get; set; } = new();
        public List<BudgetSalesReportMetricRowDto> PreviousYear { get; set; } = new();
        public List<BudgetSalesReportBudgetRowDto> BudgetRows { get; set; } = new();
    }

    public class BudgetSalesReportEmployeeDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public class BudgetSalesReportCustomerDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public int? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
    }

    public class BudgetSalesReportMetricRowDto
    {
        public int CustomerId { get; set; }
        public decimal?[] SalesMonths { get; set; } = new decimal?[12];
        public decimal?[] TbMonths { get; set; } = new decimal?[12];
        public int[] OrderCountMonths { get; set; } = new int[12];
        public int YtdOrderCount { get; set; }
        public int[] ActiveCustomerMonths { get; set; } = new int[12];
        public int YtdActiveCustomer { get; set; }
        public decimal?[] NewSalesMonths { get; set; } = new decimal?[12];
        public decimal?[] NewTbMonths { get; set; } = new decimal?[12];
        public int[] NewCustomerCountMonths { get; set; } = new int[12];
        public int YtdNewCustomerCount { get; set; }
        public decimal?[] ExistingNewSalesMonths { get; set; } = new decimal?[12];
        public decimal?[] ExistingNewTbMonths { get; set; } = new decimal?[12];
        public int[] ExistingNewCustomerCountMonths { get; set; } = new int[12];
        public int YearExistingNewCustomerCount { get; set; }
        public int YtdExistingNewCustomerCount { get; set; }
    }

    public class BudgetSalesReportBudgetRowDto
    {
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public DateOnly? BudgetCountAsNewUntilMonth { get; set; }
        public decimal?[] SalesMonths { get; set; } = new decimal?[12];
        public decimal?[] AdditionMonths { get; set; } = new decimal?[12];
    }
}