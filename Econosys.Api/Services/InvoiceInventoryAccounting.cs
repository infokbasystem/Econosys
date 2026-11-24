using System.Globalization;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services;

public static class InvoiceInventoryAccounting
{
    public static async Task<List<InvoiceAccountRowMutationRequest>> BuildAsync(
        ApplicationDbContext dbContext,
        IReadOnlyCollection<InvoiceRowMutationRequest> invoiceRows,
        CancellationToken cancellationToken = default,
        bool isCredit = false)
    {
        var stockRows = invoiceRows
            .Where(row => row.PriceTypeId == 1 && row.DeliveryFromStockId > 0)
            .ToList();
        if (stockRows.Count == 0)
            return [];

        var deliveryIds = stockRows.Select(row => row.DeliveryFromStockId!.Value).Distinct().ToArray();
        var deliveries = await dbContext.DeliveryFromStocks
            .AsNoTracking()
            .Where(delivery => deliveryIds.Contains(delivery.Id))
            .Include(delivery => delivery.Inventory)
            .Include(delivery => delivery.CustomerOrder)
                .ThenInclude(order => order!.SupplierOrder)
                    .ThenInclude(order => order!.PurchaseCurrency)
            .Include(delivery => delivery.CustomerOrder)
                .ThenInclude(order => order!.SupplierOrder)
                    .ThenInclude(order => order!.Unit)
            .ToDictionaryAsync(delivery => delivery.Id, cancellationToken);

        Dictionary<int, int?>? accounts = null;
        var postings = new Dictionary<int, (string Text, decimal Sum)>();
        var counterpartAccount = 0;

        foreach (var row in stockRows)
        {
            var deliveryId = row.DeliveryFromStockId!.Value;
            if (!deliveries.TryGetValue(deliveryId, out var delivery))
                throw new InvalidOperationException($"Inventory valuation: stock delivery {deliveryId} was not found.");
            if (delivery.Inventory?.NoInventoryValue == true)
                continue;
            if (delivery.InventoryId.GetValueOrDefault() != 0 && delivery.Inventory is null)
                throw new InvalidOperationException($"Inventory valuation: inventory {delivery.InventoryId} for stock delivery {deliveryId} was not found.");

            var supplierOrder = delivery.CustomerOrder?.SupplierOrder
                ?? throw new InvalidOperationException($"Inventory valuation: stock delivery {deliveryId} has no supplier order through its customer order.");
            var currency = supplierOrder.PurchaseCurrency
                ?? throw new InvalidOperationException($"Inventory valuation: supplier order {supplierOrder.Id} has no purchase currency.");
            var unit = supplierOrder.Unit
                ?? throw new InvalidOperationException($"Inventory valuation: supplier order {supplierOrder.Id} has no unit.");
            var purchasePrice = supplierOrder.PurchasePrice
                ?? throw new InvalidOperationException($"Inventory valuation: supplier order {supplierOrder.Id} has no purchase price.");
            var stockRate = currency.RateStockValue
                ?? throw new InvalidOperationException($"Inventory valuation: purchase currency {currency.Id} has no stock valuation rate.");
            var quantity = row.NrOf
                ?? throw new InvalidOperationException($"Inventory valuation: invoice row for stock delivery {deliveryId} has no quantity.");

            if (accounts is null)
            {
                accounts = await dbContext.Accounts.AsNoTracking()
                    .Where(account => account.Id == 18 || account.Id == 19)
                    .ToDictionaryAsync(account => account.Id, account => account.AccountNr, cancellationToken);
                counterpartAccount = GetRequiredAccount(accounts, 19);
            }

            var inventoryAccount = delivery.Inventory?.AccountNr ?? 0;
            if (inventoryAccount == 0)
                inventoryAccount = GetRequiredAccount(accounts, 18);

            var multiplicator = unit.Multiplicator.GetValueOrDefault();
            var value = quantity * purchasePrice * stockRate / (multiplicator == 0 ? 1 : multiplicator) * (isCredit ? -1 : 1);
            // Round each line using the legacy double calculation; sum the rounded amounts in decimal.
            var rounded = (decimal)Math.Round(value, 2, MidpointRounding.ToEven);
            AddPosting(inventoryAccount, "Lager ut", rounded);
            AddPosting(counterpartAccount, "Lagerförändring", -rounded);
        }

        return postings.Select(entry => BuildRow(entry.Key, entry.Value.Text, entry.Value.Sum)).ToList();

        void AddPosting(int account, string text, decimal amount)
        {
            postings[account] = postings.TryGetValue(account, out var posting)
                ? (posting.Text, posting.Sum + amount)
                : (text, amount);
        }
    }

    private static int GetRequiredAccount(Dictionary<int, int?> accounts, int accountId)
    {
        var accountNumber = accounts.GetValueOrDefault(accountId).GetValueOrDefault();
        return accountNumber != 0
            ? accountNumber
            : throw new InvalidOperationException($"Inventory valuation: a nonzero account number is required for account ID {accountId}.");
    }

    private static InvoiceAccountRowMutationRequest BuildRow(int account, string text, decimal sum) => new()
    {
        AccountNr = account.ToString(CultureInfo.InvariantCulture),
        Text = text,
        Sum = (double)sum,
    };
}
