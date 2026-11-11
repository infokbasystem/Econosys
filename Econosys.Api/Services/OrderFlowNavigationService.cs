using System.Globalization;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services;

public sealed class OrderFlowNavigationService(ApplicationDbContext db) : IOrderFlowNavigationService
{
    public Task<IReadOnlyList<OrderFlowNavigationItemDto>?> GetCalculationFlowAsync(
        int id, CancellationToken cancellationToken) => GetFlowAsync("calculation", id, cancellationToken);

    public async Task<IReadOnlyList<OrderFlowNavigationItemDto>?> GetFlowAsync(
        string entityType, int id, CancellationToken cancellationToken)
    {
        var exists = entityType switch
        {
            "product" => await db.Products.AnyAsync(x => x.Id == id, cancellationToken),
            "calculation" => await db.Calculations.AnyAsync(x => x.Id == id, cancellationToken),
            "inquiry" => await db.Inquiries.AnyAsync(x => x.Id == id, cancellationToken),
            "quotation" => await db.Quotations.AnyAsync(x => x.Id == id, cancellationToken),
            "supplierorder" => await db.SupplierOrders.AnyAsync(x => x.Id == id, cancellationToken),
            "customerorder" => await db.CustomerOrders.AnyAsync(x => x.Id == id, cancellationToken),
            _ => throw new ArgumentException("Unsupported entity type for order navigation.", nameof(entityType)),
        };
        if (!exists) return null;

        var customerParents = entityType == "customerorder" ? await db.CustomerOrders.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.CalculationId, x.SupplierOrderId, x.QuotationId })
            .ToListAsync(cancellationToken) : [];
        var parentSupplierIds = customerParents.Where(x => x.SupplierOrderId.HasValue).Select(x => x.SupplierOrderId!.Value).ToList();
        var supplierParents = entityType == "supplierorder" || parentSupplierIds.Count > 0 ? await db.SupplierOrders.AsNoTracking()
            .Where(x => (entityType == "supplierorder" && x.Id == id) || parentSupplierIds.Contains(x.Id))
            .Select(x => new { x.Id, x.CalculationId, x.InquiryId, x.QuotationId })
            .ToListAsync(cancellationToken) : [];
        var parentQuotationIds = customerParents.Select(x => x.QuotationId)
            .Concat(supplierParents.Select(x => x.QuotationId))
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var quotationParents = entityType == "quotation" || parentQuotationIds.Count > 0 ? await db.Quotations.AsNoTracking()
            .Where(x => (entityType == "quotation" && x.Id == id) || parentQuotationIds.Contains(x.Id))
            .Select(x => new { x.Id, x.CalculationId, x.InquiryId })
            .ToListAsync(cancellationToken) : [];
        var parentInquiryIds = supplierParents.Select(x => x.InquiryId)
            .Concat(quotationParents.Select(x => x.InquiryId))
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var inquiryParents = entityType == "inquiry" || parentInquiryIds.Count > 0 ? await db.Inquiries.AsNoTracking()
            .Where(x => (entityType == "inquiry" && x.Id == id) || parentInquiryIds.Contains(x.Id))
            .Select(x => new { x.Id, x.CalculationId })
            .ToListAsync(cancellationToken) : [];
        var parentCalculationIds = customerParents.Select(x => x.CalculationId)
            .Concat(supplierParents.Select(x => x.CalculationId))
            .Concat(quotationParents.Select(x => x.CalculationId))
            .Concat(inquiryParents.Select(x => x.CalculationId))
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (entityType == "calculation") parentCalculationIds.Add(id);

        var productIds = await db.Calculations.AsNoTracking()
            .Where(x => parentCalculationIds.Contains(x.Id) && x.ProductId.HasValue)
            .Select(x => x.ProductId!.Value).Distinct().ToListAsync(cancellationToken);
        if (entityType == "product") productIds.Add(id);

        var calculations = await db.Calculations.AsNoTracking()
            .Where(x => parentCalculationIds.Contains(x.Id)
                || (entityType != "calculation" && x.ProductId.HasValue && productIds.Contains(x.ProductId.Value)))
            .Select(x => new
            {
                x.Id, x.IsArchived,
                SupplierName = x.Supplier != null ? x.Supplier.Name : null,
                CustomerName = x.Customer != null ? x.Customer.Name : null,
                x.EditedAt,
            })
            .ToListAsync(cancellationToken);

        var items = new List<OrderFlowNavigationItemDto>();
        var products = await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name }).ToListAsync(cancellationToken);
        items.AddRange(products.Select(x => new OrderFlowNavigationItemDto("product", x.Id, null, x.Name, null, null, null)));
        items.AddRange(calculations.Select(x => new OrderFlowNavigationItemDto("calculation", x.Id, null, null,
            x.SupplierName, x.CustomerName, FormatDate(x.EditedAt), x.IsArchived)));

        var calculationIds = calculations.Select(x => x.Id).ToList();
        var seedInquiryIds = inquiryParents.Select(x => x.Id).ToList();
        var seedQuotationIds = quotationParents.Select(x => x.Id).ToList();
        var seedSupplierIds = supplierParents.Select(x => x.Id).ToList();
        var inquiries = db.Inquiries.AsNoTracking()
            .Where(x => seedInquiryIds.Contains(x.Id) || (x.CalculationId.HasValue && calculationIds.Contains(x.CalculationId.Value)));
        var inquiryIds = inquiries.Select(x => x.Id);
        var quotations = db.Quotations.AsNoTracking()
            .Where(x => seedQuotationIds.Contains(x.Id)
                || (x.CalculationId.HasValue && calculationIds.Contains(x.CalculationId.Value))
                || (x.InquiryId.HasValue && inquiryIds.Contains(x.InquiryId.Value)));
        var quotationIds = quotations.Select(x => x.Id);
        var supplierOrders = db.SupplierOrders.AsNoTracking()
            .Where(x => seedSupplierIds.Contains(x.Id)
                || (x.CalculationId.HasValue && calculationIds.Contains(x.CalculationId.Value))
                || (x.InquiryId.HasValue && inquiryIds.Contains(x.InquiryId.Value))
                || (x.QuotationId.HasValue && quotationIds.Contains(x.QuotationId.Value)));
        var supplierOrderIds = supplierOrders.Select(x => x.Id);
        var customerOrders = db.CustomerOrders.AsNoTracking()
            .Where(x => (entityType == "customerorder" && x.Id == id)
                || (x.CalculationId.HasValue && calculationIds.Contains(x.CalculationId.Value))
                || (x.QuotationId.HasValue && quotationIds.Contains(x.QuotationId.Value))
                || (x.SupplierOrderId.HasValue && supplierOrderIds.Contains(x.SupplierOrderId.Value)));

        var inquiryItems = await inquiries.OrderByDescending(x => x.Id)
            .Select(x => new FlowProjection
            {
                Id = x.Id, CalculationId = x.CalculationId,
                SupplierName = x.SelectedInquiryRecipient != null && x.SelectedInquiryRecipient.Supplier != null
                    ? x.SelectedInquiryRecipient.Supplier.Name : null,
                CustomerName = x.Customer != null ? x.Customer.Name : x.CustomerName,
                Date = x.CreatedAt,
            }).ToListAsync(cancellationToken);
        var quotationItems = await quotations.OrderByDescending(x => x.Id)
            .Select(x => new FlowProjection
            {
                Id = x.Id, CalculationId = x.CalculationId, InquiryId = x.InquiryId,
                CustomerName = x.Customer != null ? x.Customer.Name : x.CustomerName,
                Date = x.CreatedAt,
            }).ToListAsync(cancellationToken);
        var supplierOrderItems = await supplierOrders.OrderByDescending(x => x.Id)
            .Select(x => new FlowProjection
            {
                Id = x.Id, Number = x.SupplierOrderNr,
                CalculationId = x.CalculationId, InquiryId = x.InquiryId, QuotationId = x.QuotationId,
                SupplierName = x.Supplier != null ? x.Supplier.Name : x.SupplierName,
                CustomerName = x.Customer != null ? x.Customer.Name : x.Customer2,
                Date = x.Created,
            }).ToListAsync(cancellationToken);
        var customerOrderItems = await customerOrders.OrderByDescending(x => x.Id)
            .Select(x => new FlowProjection
            {
                Id = x.Id, Number = x.CustomerOrderNr,
                CalculationId = x.CalculationId, QuotationId = x.QuotationId, SupplierOrderId = x.SupplierOrderId,
                CustomerName = x.Customer != null ? x.Customer.Name : x.CustomerName,
                Date = x.Created,
            }).ToListAsync(cancellationToken);

        var archiveStatus = calculations.ToDictionary(x => ("calculation", x.Id), x => x.IsArchived);
        bool IsArchived(FlowProjection item)
        {
            var parents = new[]
            {
                ("calculation", item.CalculationId), ("inquiry", item.InquiryId),
                ("quotation", item.QuotationId), ("supplierOrder", item.SupplierOrderId),
            };
            var knownParents = parents.Where(x => x.Item2.HasValue && archiveStatus.ContainsKey((x.Item1, x.Item2.Value))).ToList();
            return knownParents.Count > 0 && knownParents.All(x => archiveStatus[(x.Item1, x.Item2!.Value)]);
        }
        void AddItems(string type, List<FlowProjection> projections)
        {
            foreach (var item in projections)
            {
                var isArchived = IsArchived(item);
                archiveStatus[(type, item.Id)] = isArchived;
                items.Add(Map(type, item) with { IsArchived = isArchived });
            }
        }
        AddItems("inquiry", inquiryItems);
        AddItems("quotation", quotationItems);
        AddItems("supplierOrder", supplierOrderItems);
        AddItems("customerOrder", customerOrderItems);
        return items;
    }

    private static OrderFlowNavigationItemDto Map(string type, FlowProjection item) =>
        new(type, item.Id, item.Number, null, item.SupplierName, item.CustomerName, FormatDate(item.Date));

    private static string? FormatDate(DateTime? date) => date?.ToString("yyMMdd", CultureInfo.InvariantCulture);

    private sealed class FlowProjection
    {
        public int Id { get; init; }
        public int? CalculationId { get; init; }
        public int? InquiryId { get; init; }
        public int? QuotationId { get; init; }
        public int? SupplierOrderId { get; init; }
        public string? Number { get; init; }
        public string? SupplierName { get; init; }
        public string? CustomerName { get; init; }
        public DateTime? Date { get; init; }
    }
}
