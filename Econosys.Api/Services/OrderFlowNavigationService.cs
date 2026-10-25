using System.Globalization;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services;

public sealed class OrderFlowNavigationService(ApplicationDbContext db) : IOrderFlowNavigationService
{
    public async Task<IReadOnlyList<OrderFlowNavigationItemDto>?> GetCalculationFlowAsync(
        int id, CancellationToken cancellationToken)
    {
        var calculation = await db.Calculations.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, x.ProductId,
                ProductName = x.Product != null ? x.Product.Name : null,
                SupplierName = x.Supplier != null ? x.Supplier.Name : null,
                CustomerName = x.Customer != null ? x.Customer.Name : null,
                x.EditedAt,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (calculation is null) return null;

        var items = new List<OrderFlowNavigationItemDto>();
        if (calculation.ProductId.HasValue)
            items.Add(new("product", calculation.ProductId.Value, null, calculation.ProductName, null, null, null));
        items.Add(new("calculation", calculation.Id, null, null,
            calculation.SupplierName, calculation.CustomerName, FormatDate(calculation.EditedAt)));

        var inquiries = db.Inquiries.AsNoTracking().Where(x => x.CalculationId == id);
        var inquiryIds = inquiries.Select(x => x.Id);
        var quotations = db.Quotations.AsNoTracking()
            .Where(x => x.CalculationId == id || (x.InquiryId.HasValue && inquiryIds.Contains(x.InquiryId.Value)));
        var quotationIds = quotations.Select(x => x.Id);
        var supplierOrders = db.SupplierOrders.AsNoTracking()
            .Where(x => x.CalculationId == id
                || (x.InquiryId.HasValue && inquiryIds.Contains(x.InquiryId.Value))
                || (x.QuotationId.HasValue && quotationIds.Contains(x.QuotationId.Value)));
        var supplierOrderIds = supplierOrders.Select(x => x.Id);
        var customerOrders = db.CustomerOrders.AsNoTracking()
            .Where(x => x.CalculationId == id
                || (x.QuotationId.HasValue && quotationIds.Contains(x.QuotationId.Value))
                || (x.SupplierOrderId.HasValue && supplierOrderIds.Contains(x.SupplierOrderId.Value)));

        var inquiryItems = await inquiries.OrderByDescending(x => x.Id)
            .Select(x => new FlowProjection
            {
                Id = x.Id,
                SupplierName = x.SelectedInquiryRecipient != null && x.SelectedInquiryRecipient.Supplier != null
                    ? x.SelectedInquiryRecipient.Supplier.Name : null,
                CustomerName = x.Customer != null ? x.Customer.Name : x.CustomerName,
                Date = x.CreatedAt,
            }).ToListAsync(cancellationToken);
        var quotationItems = await quotations.OrderByDescending(x => x.Id)
            .Select(x => new FlowProjection
            {
                Id = x.Id,
                CustomerName = x.Customer != null ? x.Customer.Name : x.CustomerName,
                Date = x.CreatedAt,
            }).ToListAsync(cancellationToken);
        var supplierOrderItems = await supplierOrders.OrderByDescending(x => x.Id)
            .Select(x => new FlowProjection
            {
                Id = x.Id, Number = x.SupplierOrderNr,
                SupplierName = x.Supplier != null ? x.Supplier.Name : x.SupplierName,
                CustomerName = x.Customer != null ? x.Customer.Name : x.Customer2,
                Date = x.Created,
            }).ToListAsync(cancellationToken);
        var customerOrderItems = await customerOrders.OrderByDescending(x => x.Id)
            .Select(x => new FlowProjection
            {
                Id = x.Id, Number = x.CustomerOrderNr,
                CustomerName = x.Customer != null ? x.Customer.Name : x.CustomerName,
                Date = x.Created,
            }).ToListAsync(cancellationToken);

        items.AddRange(inquiryItems.Select(x => Map("inquiry", x)));
        items.AddRange(quotationItems.Select(x => Map("quotation", x)));
        items.AddRange(supplierOrderItems.Select(x => Map("supplierOrder", x)));
        items.AddRange(customerOrderItems.Select(x => Map("customerOrder", x)));
        return items;
    }

    private static OrderFlowNavigationItemDto Map(string type, FlowProjection item) =>
        new(type, item.Id, item.Number, null, item.SupplierName, item.CustomerName, FormatDate(item.Date));

    private static string? FormatDate(DateTime? date) => date?.ToString("yyMMdd", CultureInfo.InvariantCulture);

    private sealed class FlowProjection
    {
        public int Id { get; init; }
        public string? Number { get; init; }
        public string? SupplierName { get; init; }
        public string? CustomerName { get; init; }
        public DateTime? Date { get; init; }
    }
}
