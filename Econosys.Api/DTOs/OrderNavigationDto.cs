namespace Econosys.Api.DTOs
{
    public class OrderNavigationTreeDto
    {
        public int? ProductId { get; set; }
        public string CurrentEntityType { get; set; } = string.Empty;
        public int CurrentEntityId { get; set; }
        public List<OrderNavigationCalculationDto> Calculations { get; set; } = new();
    }

    public class OrderNavigationCalculationDto
    {
        public int Id { get; set; }
        public bool IsCurrent { get; set; }
        public List<OrderNavigationInquiryDto> Inquiries { get; set; } = new();
        public List<OrderNavigationQuotationDto> Quotations { get; set; } = new();
        public List<OrderNavigationSupplierOrderDto> SupplierOrders { get; set; } = new();
    }

    public class OrderNavigationInquiryDto
    {
        public int Id { get; set; }
        public bool IsCurrent { get; set; }
        public List<OrderNavigationQuotationDto> Quotations { get; set; } = new();
        public List<OrderNavigationSupplierOrderDto> SupplierOrders { get; set; } = new();
    }

    public class OrderNavigationQuotationDto
    {
        public int Id { get; set; }
        public bool IsCurrent { get; set; }
        public List<OrderNavigationSupplierOrderDto> SupplierOrders { get; set; } = new();
        public List<OrderNavigationCustomerOrderDto> CustomerOrders { get; set; } = new();
    }

    public class OrderNavigationSupplierOrderDto
    {
        public int Id { get; set; }
        public bool IsCurrent { get; set; }
        public List<OrderNavigationCustomerOrderDto> CustomerOrders { get; set; } = new();
    }

    public class OrderNavigationCustomerOrderDto
    {
        public int Id { get; set; }
        public bool IsCurrent { get; set; }
    }
}
