using Econosys.Api.Common;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services;

public interface IOrderHistoryService
{
    Task<OrderHistoryResponse?> GetAsync(int orderId, bool customerOrder, CancellationToken cancellationToken);
}

public sealed class OrderHistoryService(ApplicationDbContext context) : IOrderHistoryService
{
    public async Task<OrderHistoryResponse?> GetAsync(
        int orderId, bool customerOrder, CancellationToken cancellationToken)
    {
        int? supplierOrderId = orderId;
        if (customerOrder)
        {
            var order = await context.CustomerOrders.AsNoTracking()
                .Where(x => x.Id == orderId)
                .Select(x => new { x.SupplierOrderId })
                .FirstOrDefaultAsync(cancellationToken);
            if (order is null) return null;
            supplierOrderId = order.SupplierOrderId;
        }

        var supplier = await context.SupplierOrders.AsNoTracking()
            .Include(x => x.Inventory)
            .FirstOrDefaultAsync(x => x.Id == supplierOrderId, cancellationToken);
        if (supplier is null && !customerOrder) return null;
        var supplierId = supplier?.Id;

        var inbound = await context.DeliveryToStocks.AsNoTracking()
            .Where(x => supplierId.HasValue && x.SupplierOrderId == supplierId)
            .Include(x => x.Inventory)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var direct = await context.DeliveryToCustomers.AsNoTracking()
            .Where(x => customerOrder
                ? x.CustomerOrderId == orderId
                : x.SupplierOrderId == supplierId)
            .Include(x => x.CustomerOrder)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var outbound = await context.DeliveryFromStocks.AsNoTracking()
            .Where(x => customerOrder
                ? x.CustomerOrderId == orderId
                : x.CustomerOrder != null && x.CustomerOrder.SupplierOrderId == supplierId)
            .Include(x => x.CustomerOrder)
            .Include(x => x.Inventory)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var stocktakes = await context.StockTakingItems.AsNoTracking()
            .Where(x => supplierId.HasValue && x.SupplierOrderId == supplierId && x.StockTaking != null)
            .Include(x => x.StockTaking)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var inboundIds = inbound.Select(x => x.Id).ToArray();
        var directIds = direct.Select(x => x.Id).ToArray();
        var outboundIds = outbound.Select(x => x.Id).ToArray();
        var transports = await context.TransportOrderDeliveries.AsNoTracking()
            .Where(x => (x.DeliveryToStockId.HasValue && inboundIds.Contains(x.DeliveryToStockId.Value))
                || (x.DeliveryToCustomerId.HasValue && directIds.Contains(x.DeliveryToCustomerId.Value))
                || (x.DeliveryFromStockId.HasValue && outboundIds.Contains(x.DeliveryFromStockId.Value)))
            .Include(x => x.TransportOrder)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var calloffs = await context.CallOffDeliveries.AsNoTracking()
            .Where(x => x.DeliveryFromStockId.HasValue && outboundIds.Contains(x.DeliveryFromStockId.Value))
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        // Resolve row-level links once instead of repeating a correlated EXISTS for every invoice.
        var invoiceRows = await context.InvoiceRows.AsNoTracking()
            .Where(row => row.InvoiceId.HasValue
                && ((row.DeliveryToCustomerId.HasValue && directIds.Contains(row.DeliveryToCustomerId.Value))
                    || (row.DeliveryFromStockId.HasValue && outboundIds.Contains(row.DeliveryFromStockId.Value))))
            .Select(row => new InvoiceRow
            {
                InvoiceId = row.InvoiceId,
                DeliveryToCustomerId = row.DeliveryToCustomerId,
                DeliveryFromStockId = row.DeliveryFromStockId,
                NrOf = row.NrOf,
            })
            .ToListAsync(cancellationToken);
        var rowInvoiceIds = invoiceRows.Select(x => x.InvoiceId!.Value).Distinct().ToArray();
        var invoices = await context.Invoices.AsNoTracking()
            .Where(x => (customerOrder
                    ? x.CustomerOrderId == orderId
                    : context.CustomerOrders.Any(order =>
                        order.Id == x.CustomerOrderId && order.SupplierOrderId == supplierId))
                || (x.DeliveryToCustomerId.HasValue && directIds.Contains(x.DeliveryToCustomerId.Value))
                || (x.DeliveryFromStockId.HasValue && outboundIds.Contains(x.DeliveryFromStockId.Value))
                || rowInvoiceIds.Contains(x.Id))
            .OrderBy(x => x.Id)
            .Select(x => new Invoice
            {
                Id = x.Id,
                InvoiceNumber = x.InvoiceNumber,
                InvoiceTypeCode = x.InvoiceTypeCode,
                InvoiceDate = x.InvoiceDate,
                DeliveryToCustomerId = x.DeliveryToCustomerId,
                DeliveryFromStockId = x.DeliveryFromStockId,
            })
            .ToListAsync(cancellationToken);
        var rowsByInvoice = invoiceRows.ToLookup(x => x.InvoiceId);
        foreach (var invoice in invoices)
            invoice.InvoiceRows = rowsByInvoice[invoice.Id].ToList();

        return OrderHistoryBuilder.Build(supplier?.ProducedEdition,
            !customerOrder || supplier?.Inventory?.IsInventory == true,
            customerOrder, inbound, direct, outbound, stocktakes, transports, calloffs, invoices);
    }
}

public static class OrderHistoryBuilder
{
    public static OrderHistoryResponse Build(
        int? producedEdition,
        bool showStockBalance,
        bool customerOrder,
        IReadOnlyList<DeliveryToStock> inbound,
        IReadOnlyList<DeliveryToCustomer> direct,
        IReadOnlyList<DeliveryFromStock> outbound,
        IReadOnlyList<StockTakingItem> stocktakes,
        IReadOnlyList<TransportOrderDelivery> transports,
        IReadOnlyList<CallOffDelivery> calloffs,
        IReadOnlyList<Invoice> invoices)
    {
        var rows = new List<OrderHistoryRowResponse>();
        if (producedEdition.HasValue)
        {
            rows.Add(new OrderHistoryRowResponse
            {
                Type = "Produced", TypeName = "Producerat", Quantity = producedEdition.Value,
            });
        }

        rows.AddRange(outbound.Select(x => Delivery(
            x.Id, "DeliveryFromStock", x.Inventory?.NoInventoryValue == true ? "Fabrikslev." : "Från lager",
            x.DeliveryDate, x.NrOfItems, x.DeliveryStatus, x.CustomerOrder?.CustomerName ?? "")));
        rows.AddRange(direct.Select(x => Delivery(
            x.Id, "DeliveryToCustomer", "Direktleverans",
            x.DeliveryDate, x.NrOfItems, x.DeliveryStatus, x.CustomerOrder?.CustomerName ?? "")));
        rows.AddRange(inbound.Select(x => Delivery(
            x.Id, "DeliveryToStock", x.IsAdjustment ? $"Justering {(x.NrOfItems < 0 ? "-" : "+")}" : "Till lager",
            x.DeliveryDate, x.NrOfItems, x.DeliveryStatus,
            string.IsNullOrWhiteSpace(x.Inventory?.Name) ? "Till lager" : $"{x.Inventory.Name} (lager)")));
        rows.AddRange(stocktakes.Select(x => new OrderHistoryRowResponse
        {
            Id = x.Id, Type = "StockTaking", TypeName = "Inventering",
            Date = ToDate(x.StockTaking?.StockTakingDate),
            Quantity = x.NrOfItems ?? 0, StockTakingId = x.StockTakingId,
        }));

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var transport = transports.FirstOrDefault(x => x.TransportOrderId.HasValue && Matches(
                row, x.DeliveryToStockId, x.DeliveryToCustomerId, x.DeliveryFromStockId));
            var calloff = row.Type == "DeliveryFromStock"
                ? calloffs.FirstOrDefault(x => x.CallOffId.HasValue && x.DeliveryFromStockId == row.Id)
                : null;
            var linkedInvoices = invoices
                .Where(x => x.InvoiceTypeCode != InvoiceTypeCode.Credit
                    && (Matches(row, null, x.DeliveryToCustomerId, x.DeliveryFromStockId)
                        || x.InvoiceRows.Any(item => Matches(
                            row, null, item.DeliveryToCustomerId, item.DeliveryFromStockId))))
                .Select(x => new OrderHistoryInvoiceResponse(x.Id, x.InvoiceNumber))
                .ToArray();
            rows[index] = row with
            {
                TransportOrderId = transport?.TransportOrderId,
                TransportOrderNr = transport?.TransportOrder?.TransportOrderNr,
                CallOffId = calloff?.CallOffId,
                Invoices = linkedInvoices,
            };
        }

        if (customerOrder)
        {
            foreach (var invoice in invoices.Where(x => x.InvoiceTypeCode == InvoiceTypeCode.Credit))
            {
                rows.Add(new OrderHistoryRowResponse
                {
                    Id = invoice.Id, Type = "Invoice", TypeName = "Kreditfaktura",
                    Date = ToDate(invoice.InvoiceDate),
                    Quantity = invoice.InvoiceRows
                        .Where(item => rows.Any(row => Matches(
                            row, null, item.DeliveryToCustomerId, item.DeliveryFromStockId)))
                        .Sum(item => item.NrOf ?? 0),
                    Invoices = [new(invoice.Id, invoice.InvoiceNumber)],
                });
            }
        }

        double stockBalance = 0;
        double productionBalance = producedEdition ?? 0;
        // Legacy balances include planned deliveries; a stocktake replaces, rather than adds to, stock.
        var ordered = rows.OrderBy(x => x.Type == "Produced" ? 0 : 1).ThenBy(x => x.Date).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            var row = ordered[index];
            stockBalance = row.Type switch
            {
                "StockTaking" => row.Quantity,
                "DeliveryToStock" => stockBalance + row.Quantity,
                "DeliveryFromStock" => stockBalance - row.Quantity,
                _ => stockBalance,
            };
            if (row.Type is "DeliveryToStock" or "DeliveryToCustomer")
                productionBalance -= row.Quantity;
            ordered[index] = row with { StockBalance = stockBalance, ProductionBalance = productionBalance };
        }
        return new OrderHistoryResponse(showStockBalance, ordered);

        OrderHistoryRowResponse Delivery(int id, string type, string name, DateTime? date,
            double? quantity, int? status, string to) => new()
        {
            Id = id, Type = type, TypeName = name, Date = ToDate(date),
            Quantity = quantity ?? 0, To = to,
            Status = status == 2 ? "Levererad" : status == 1 ? customerOrder ? "Ej levererad" : "Plan." : "-",
        };
    }

    private static bool Matches(OrderHistoryRowResponse row, int? inboundId, int? directId, int? outboundId) =>
        row.Type switch
        {
            "DeliveryToStock" => row.Id == inboundId,
            "DeliveryToCustomer" => row.Id == directId,
            "DeliveryFromStock" => row.Id == outboundId,
            _ => false,
        };

    private static DateTimeOffset? ToDate(DateTime? date) =>
        date.HasValue ? SwedishTime.ToDateTimeOffset(date.Value) : null;
}
