namespace Econosys.Api.DTOs
{
    public class FinanceOverviewDto
    {
        public DateTime ReportDate { get; set; }
        public decimal InvoicedYtd { get; set; }
        public decimal InvoicedPreviousYtd { get; set; }
        public double? YtdChangePercent { get; set; }
        public decimal NotInvoicedTotal { get; set; }
        public int NotInvoicedCount { get; set; }
        public decimal NotBookedTotal { get; set; }
        public int NotBookedCount { get; set; }
        public IReadOnlyList<int> Years { get; set; } = Array.Empty<int>();
        public IReadOnlyList<FinanceMonthlyPointDto> Monthly { get; set; } = Array.Empty<FinanceMonthlyPointDto>();
    }

    public class FinanceMonthlyPointDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Amount { get; set; }
    }

    public class UninvoicedDeliveryRowDto
    {
        public int DeliveryId { get; set; }
        public int? CustomerOrderId { get; set; }
        public string? CustomerOrderNr { get; set; }
        public string? CustomerName { get; set; }
        public double? Quantity { get; set; }
        public decimal Value { get; set; }
        public DateTime? DeliveryDate { get; set; }
    }

    public class UninvoicedDeliveryTotalsDto
    {
        public decimal TotalValue { get; set; }
        public int TotalDeliveries { get; set; }
    }

    public class UnbookedInvoiceRowDto
    {
        public int InvoiceId { get; set; }
        public int? InvoiceNumber { get; set; }
        public string? CustomerName { get; set; }
        public decimal Amount { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public class UnbookedInvoiceTotalsDto
    {
        public decimal TotalAmount { get; set; }
        public int TotalInvoices { get; set; }
    }

    public class SearchUnbookedInvoicesRequest : PaginationRequest
    {
        public DateOnly? InvoiceDateFrom { get; set; }
        public DateOnly? InvoiceDateTo { get; set; }
        public int? InvoiceNumber { get; set; }
    }

    public class BookInvoicesRequest
    {
        public List<int> InvoiceIds { get; set; } = [];
        public bool UseTestApi { get; set; }
    }

    public class BookInvoicesResponse
    {
        public List<int> BookedInvoiceIds { get; set; } = [];
        public List<int> AlreadyBookedInvoiceIds { get; set; } = [];
        public List<int> MissingInvoiceIds { get; set; } = [];
        public List<BookInvoiceResultDto> Results { get; set; } = [];
    }

    public class BookInvoiceResultDto
    {
        public int InvoiceId { get; set; }
        public bool Succeeded { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class SyncCustomersRequest
    {
        public List<int> CustomerIds { get; set; } = [];
        public bool UseTestApi { get; set; }
    }

    public class SyncCustomersResponse
    {
        public List<int> MissingCustomerIds { get; set; } = [];
        public List<SyncCustomerResultDto> Results { get; set; } = [];
    }

    public class SyncCustomerResultDto
    {
        public int CustomerId { get; set; }
        public bool Succeeded { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
