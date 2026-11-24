using System.Data;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services;

public interface IInvoiceStockDeliveryAdjustmentService
{
    Task<IReadOnlyList<InvoiceStockDeliveryAdjustmentResponse>?> PreviewAsync(int invoiceId, CancellationToken cancellationToken);
    Task<bool> ApplyAsync(int invoiceId, IReadOnlyList<InvoiceStockDeliveryAdjustmentResponse> expected, CancellationToken cancellationToken);
}

public sealed class InvoiceStockDeliveryAdjustmentException(string message) : Exception(message);

public sealed class InvoiceStockDeliveryAdjustmentService(ApplicationDbContext db) : IInvoiceStockDeliveryAdjustmentService
{
    public async Task<IReadOnlyList<InvoiceStockDeliveryAdjustmentResponse>?> PreviewAsync(int invoiceId, CancellationToken cancellationToken)
    {
        var credit = await db.Invoices.AsNoTracking()
            .Include(x => x.InvoiceRows)
            .SingleOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);
        if (credit is null) return null;
        if (credit.InvoiceTypeCode != InvoiceTypeCode.Credit || credit.CreditingInvoiceNumber is null or <= 0)
            throw new InvoiceStockDeliveryAdjustmentException("Fakturan är inte en kreditfaktura med en ursprunglig faktura.");

        var originals = await db.Invoices.AsNoTracking().Include(x => x.InvoiceRows)
            .Where(x => x.Id != invoiceId && x.InvoiceNumber == credit.CreditingInvoiceNumber)
            .Take(2).ToListAsync(cancellationToken);
        if (originals.Count != 1 || originals[0].CustomerId != credit.CustomerId)
            throw new InvoiceStockDeliveryAdjustmentException("Den ursprungliga fakturan kunde inte identifieras entydigt.");

        var creditRows = credit.InvoiceRows.Where(x => x.PriceTypeId == 1 && x.DeliveryFromStockId > 0).ToList();
        if (creditRows.Count == 0)
            throw new InvoiceStockDeliveryAdjustmentException("Kreditfakturan saknar lagerleveranser att justera.");
        if (creditRows.GroupBy(x => x.DeliveryFromStockId).Any(group => group.Count() != 1))
            throw new InvoiceStockDeliveryAdjustmentException("En lagerleverans förekommer flera gånger på kreditfakturan.");

        var ids = creditRows.Select(x => x.DeliveryFromStockId!.Value).ToArray();
        var deliveries = await db.DeliveryFromStocks.AsNoTracking()
            .Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var originalRows = originals[0].InvoiceRows
            .Where(x => x.PriceTypeId == 1 && x.DeliveryFromStockId > 0)
            .GroupBy(x => x.DeliveryFromStockId!.Value).ToDictionary(x => x.Key, x => x.ToList());

        var changes = new List<InvoiceStockDeliveryAdjustmentResponse>();
        foreach (var row in creditRows.OrderBy(x => x.DeliveryFromStockId))
        {
            var id = row.DeliveryFromStockId!.Value;
            if (!deliveries.TryGetValue(id, out var delivery)
                || !originalRows.TryGetValue(id, out var matchingRows) || matchingRows.Count != 1)
                throw new InvoiceStockDeliveryAdjustmentException($"Lagerleverans {id} saknas eller kan inte matchas entydigt mot ursprungsfakturan.");

            var originalQuantity = matchingRows[0].NrOf;
            var creditQuantity = row.NrOf;
            var currentQuantity = delivery.NrOfItems;
            if (!originalQuantity.HasValue || !creditQuantity.HasValue || !currentQuantity.HasValue
                || !double.IsFinite(originalQuantity.Value) || !double.IsFinite(creditQuantity.Value)
                || !double.IsFinite(currentQuantity.Value) || creditQuantity.Value < 0
                || creditQuantity.Value > originalQuantity.Value)
                throw new InvoiceStockDeliveryAdjustmentException($"Antalet på lagerleverans {id} eller fakturaraderna är ogiltigt.");

            if (currentQuantity.Value != originalQuantity.Value)
                throw new InvoiceStockDeliveryAdjustmentException($"Lagerleverans {id} har redan ändrats eller justerats. Kontrollera leveransen manuellt.");

            changes.Add(new(id, currentQuantity.Value, creditQuantity.Value, originalQuantity.Value - creditQuantity.Value));
        }

        return changes;
    }

    public async Task<bool> ApplyAsync(int invoiceId, IReadOnlyList<InvoiceStockDeliveryAdjustmentResponse> expected, CancellationToken cancellationToken)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var changes = await PreviewAsync(invoiceId, cancellationToken);
        if (changes is null) return false;
        if (expected is null || !changes.SequenceEqual(expected))
            throw new InvoiceStockDeliveryAdjustmentException("Underlaget har ändrats. Stäng och öppna dialogen igen för att granska leveranserna.");

        var ids = changes.Select(x => x.DeliveryId).ToArray();
        var deliveries = await db.DeliveryFromStocks.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var change in changes)
            deliveries[change.DeliveryId].NrOfItems = change.NewQuantity;
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
