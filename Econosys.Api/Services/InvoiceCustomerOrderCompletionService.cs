using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services;

public interface IInvoiceCustomerOrderCompletionService
{
    Task<IReadOnlyList<InvoiceCustomerOrderCompletionResponse>?> GetAsync(int invoiceId, CancellationToken cancellationToken);
    Task<bool?> UpdateAsync(int invoiceId, IReadOnlyList<InvoiceCustomerOrderCompletionChange> changes, CancellationToken cancellationToken);
}

public sealed class InvoiceCustomerOrderCompletionService(ApplicationDbContext db) : IInvoiceCustomerOrderCompletionService
{
    private IQueryable<CustomerOrder> LinkedOrders(int invoiceId, int? headerOrderId) =>
        db.CustomerOrders.Where(order =>
            order.Id == headerOrderId
            || order.DeliveryFromStocks.Any(delivery => delivery.InvoiceRows.Any(row => row.InvoiceId == invoiceId))
            || order.DeliveryToCustomers.Any(delivery => delivery.InvoiceRows.Any(row => row.InvoiceId == invoiceId)));

    public async Task<IReadOnlyList<InvoiceCustomerOrderCompletionResponse>?> GetAsync(int invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.AsNoTracking().Where(x => x.Id == invoiceId)
            .Select(x => new { x.CustomerOrderId }).SingleOrDefaultAsync(cancellationToken);
        if (invoice is null) return null;

        var rows = await LinkedOrders(invoiceId, invoice.CustomerOrderId).AsNoTracking()
            .OrderBy(x => x.CustomerOrderNr).ThenBy(x => x.Id)
            .Select(order => new
            {
                order.Id,
                order.CustomerOrderNr,
                order.InvoicingInfo,
                order.Completed,
                order.CompletedDate,
                InventoryName = order.SupplierOrder != null && order.SupplierOrder.Inventory != null ? order.SupplierOrder.Inventory.Name : null,
                IsInventory = order.SupplierOrder != null && order.SupplierOrder.Inventory != null && order.SupplierOrder.Inventory.IsInventory,
                ProducedEdition = order.SupplierOrder != null ? order.SupplierOrder.ProducedEdition : null,
                order.Edition,
                DeliveredToStock = order.SupplierOrder == null ? 0 : order.SupplierOrder.DeliveryToStocks
                    .Where(x => x.DeliveryStatus == 2).Sum(x => x.NrOfItems ?? 0),
                DeliveredFromStock = order.DeliveryFromStocks.Where(x => x.DeliveryStatus == 2).Sum(x => x.NrOfItems ?? 0),
                DeliveredToCustomer = order.DeliveryToCustomers.Where(x => x.DeliveryStatus == 2).Sum(x => x.NrOfItems ?? 0),
                InvoicedFromStock = order.DeliveryFromStocks.Where(x => x.InvoiceRows.Any()).Sum(x => x.NrOfItems ?? 0),
                InvoicedToCustomer = order.DeliveryToCustomers.Where(x => x.InvoiceRows.Any()).Sum(x => x.NrOfItems ?? 0),
            }).ToListAsync(cancellationToken);

        return rows.Select(row =>
        {
            double edition = row.IsInventory ? (int)row.DeliveredToStock : row.ProducedEdition ?? row.Edition ?? 0;
            var delivered = row.DeliveredFromStock + row.DeliveredToCustomer;
            return new InvoiceCustomerOrderCompletionResponse(row.Id, row.CustomerOrderNr, row.InvoicingInfo, row.InventoryName,
                edition, delivered, row.InvoicedFromStock + row.InvoicedToCustomer, edition - delivered,
                row.Completed, row.CompletedDate.HasValue ? DateOnly.FromDateTime(row.CompletedDate.Value) : null);
        }).ToList();
    }

    public async Task<bool?> UpdateAsync(int invoiceId, IReadOnlyList<InvoiceCustomerOrderCompletionChange> changes, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.AsNoTracking().Where(x => x.Id == invoiceId)
            .Select(x => new { x.CustomerOrderId }).SingleOrDefaultAsync(cancellationToken);
        if (invoice is null) return null;
        if (changes.Any(x => x is null || x.Id <= 0) || changes.Select(x => x.Id).Distinct().Count() != changes.Count)
            return false;

        var ids = changes.Select(x => x.Id).ToArray();
        var orders = await LinkedOrders(invoiceId, invoice.CustomerOrderId)
            .Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        if (orders.Count != ids.Length) return false;

        var changesById = changes.ToDictionary(x => x.Id);
        foreach (var order in orders)
        {
            var change = changesById[order.Id];
            order.Completed = change.Completed;
            order.CompletedDate = change.CompletedDate?.ToDateTime(TimeOnly.MinValue);
        }
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
