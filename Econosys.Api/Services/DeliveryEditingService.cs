using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services;

public sealed class DeliveryEditingService(ApplicationDbContext db)
{
    public async Task<DeliveryEditResponse?> LoadAsync(string type, int id, CancellationToken cancellationToken = default)
    {
        var context = await LoadContextAsync(type, id, cancellationToken);
        return context?.Response;
    }

    public async Task<(DeliveryEditResponse? Response, DeliveryEditError? Error)> PreviewAsync(
        string type, int id, UpdateDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        var context = await LoadContextAsync(type, id, cancellationToken);
        if (context is null) return (null, null);
        var error = await ValidateAsync(context, request, cancellationToken);
        if (error is not null) return (null, error);
        var factor = await CalculateFactorAsync(context, request, cancellationToken);
        var delivery = context.Response.Delivery;
        delivery.PalletCalcFactor = factor;
        var stock = await LoadStockAsync(delivery, request.InventoryId, cancellationToken);
        return (context.Response with { AvailableItems = stock?.Items, AvailablePallets = stock?.Pallets }, null);
    }

    public async Task<(DeliveryEditResponse? Response, DeliveryEditError? Error)> SaveAsync(
        string type, int id, UpdateDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var context = await LoadContextAsync(type, id, cancellationToken);
        if (context is null) return (null, null);
        if (request.Version != context.Response.Version)
            return (null, new("StaleVersion", "Leveransen har ändrats. Läs in den igen innan du sparar."));

        var error = await ValidateAsync(context, request, cancellationToken);
        if (error is not null) return (null, error);
        var stockTakingDate = context.Response.LatestStockTakingDate;
        if (stockTakingDate.HasValue && request.DeliveryDate <= stockTakingDate
            && request.ConfirmedStockTakingDate != stockTakingDate)
            return (null, new("StockTakingConfirmationRequired",
                "Det finns en inventering efter angivet leveransdatum. Vill du spara ändå? Kontrollera saldot efter leveransen.", stockTakingDate));

        var factor = await CalculateFactorAsync(context, request, cancellationToken);
        var original = context.Response.Delivery;
        Set(context.Entity, nameof(DeliveryDto.DeliveryDate), request.DeliveryDate?.Date);
        Set(context.Entity, nameof(DeliveryDto.NrOfItems), request.NrOfItems);
        Set(context.Entity, nameof(DeliveryDto.NrOfPallets), request.NrOfPallets);
        Set(context.Entity, nameof(DeliveryDto.NrOfBunt), request.NrOfBunt);
        Set(context.Entity, nameof(DeliveryDto.NrOfYtterforpackning), request.NrOfYtterforpackning);
        Set(context.Entity, nameof(DeliveryDto.CallOff), request.CallOff);
        Set(context.Entity, nameof(DeliveryDto.InventoryId), request.InventoryId);
        Set(context.Entity, nameof(DeliveryDto.PalletLength), request.PalletLength);
        Set(context.Entity, nameof(DeliveryDto.PalletWidth), request.PalletWidth);
        Set(context.Entity, nameof(DeliveryDto.PalletHeight), request.PalletHeight);
        Set(context.Entity, nameof(DeliveryDto.PalletIsStackable), request.PalletIsStackable);
        Set(context.Entity, nameof(DeliveryDto.PalletCalcFactor), factor);
        Set(context.Entity, nameof(DeliveryDto.SpcsRefNr), request.SpcsRefNr);
        Set(context.Entity, nameof(DeliveryDto.SupplierInvoiceNr), request.SupplierInvoiceNr);
        if (request.IsDelivered != (original.DeliveryStatus == 2))
            Set(context.Entity, nameof(DeliveryDto.DeliveryStatus), request.IsDelivered ? 2 : 1);

        var formatChanged = original.PalletLength != request.PalletLength || original.PalletWidth != request.PalletWidth
            || original.PalletHeight != request.PalletHeight || original.PalletIsStackable != request.PalletIsStackable;
        foreach (var related in context.Related)
        {
            if (!original.IsSlattPallet && formatChanged)
            {
                Set(related, nameof(DeliveryDto.PalletLength), request.PalletLength);
                Set(related, nameof(DeliveryDto.PalletWidth), request.PalletWidth);
                Set(related, nameof(DeliveryDto.PalletHeight), request.PalletHeight);
                Set(related, nameof(DeliveryDto.PalletIsStackable), request.PalletIsStackable);
            }
            if (original.PalletCalcFactor != factor && (context.Response.AutoFreightCalc || !original.IsSlattPallet)
                || formatChanged && !original.IsSlattPallet)
                Set(related, nameof(DeliveryDto.PalletCalcFactor), factor);
        }

        await db.SaveChangesAsync(cancellationToken);
        var response = await LoadAsync(type, id, cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return (response, null);
    }

    private sealed record EditContext(object Entity, IReadOnlyList<object> Related, DeliveryEditResponse Response);

    private async Task<EditContext?> LoadContextAsync(string type, int id, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(type, id, cancellationToken);
        if (entity is null) return null;
        await db.Entry(entity).ReloadAsync(cancellationToken);
        var delivery = Map(entity, type);
        var order = delivery.CustomerOrderId.HasValue
            ? await db.CustomerOrders.AsNoTracking().FirstOrDefaultAsync(item => item.Id == delivery.CustomerOrderId, cancellationToken)
            : null;
        delivery.SupplierOrderId ??= order?.SupplierOrderId;
        var supplier = delivery.SupplierOrderId.HasValue
            ? await db.SupplierOrders.AsNoTracking().FirstOrDefaultAsync(item => item.Id == delivery.SupplierOrderId, cancellationToken)
            : null;
        var calculationId = order?.SelectedCalculationRowId ?? supplier?.SelectedCalculationRowId;
        var calculation = calculationId.HasValue
            ? await db.CalculationRows.AsNoTracking().FirstOrDefaultAsync(item => item.Id == calculationId, cancellationToken)
            : null;
        delivery.ProductName = supplier?.Product ?? order?.Product;
        delivery.CustomerName = order?.CustomerName ?? supplier?.Customer2;
        delivery.SupplierOrderNr = supplier?.SupplierOrderNr;
        delivery.CustomersOrderNr = order?.CustomerOrderNr ?? supplier?.CustomerOrderNr;
        var groupId = delivery.ParentDeliveryId ?? delivery.Id;
        List<object> group = type switch
        {
            "DeliveryToStock" => (await db.DeliveryToStocks.Where(item => item.Id == groupId || item.ParentDeliveryId == groupId).ToListAsync(cancellationToken)).Cast<object>().ToList(),
            "DeliveryFromStock" => (await db.DeliveryFromStocks.Where(item => item.Id == groupId || item.ParentDeliveryId == groupId).ToListAsync(cancellationToken)).Cast<object>().ToList(),
            _ => (await db.DeliveryToCustomers.Where(item => item.Id == groupId || item.ParentDeliveryId == groupId).ToListAsync(cancellationToken)).Cast<object>().ToList(),
        };
        foreach (var item in group) await db.Entry(item).ReloadAsync(cancellationToken);
        var versionValues = group.OrderBy(item => db.Entry(item).Property("Id").CurrentValue)
            .Select(item => db.Entry(item).CurrentValues.Properties.OrderBy(property => property.Name)
                .ToDictionary(property => property.Name, property => db.Entry(item).Property(property.Name).CurrentValue)).ToList();
        var inventories = await db.Inventories.AsNoTracking()
            .Where(item => item.IsInventory || item.Id == delivery.InventoryId)
            .OrderBy(item => item.Name).Select(item => new DeliveryInventoryOption(item.Id, item.Name ?? string.Empty))
            .ToListAsync(cancellationToken);
        var stock = await LoadStockAsync(delivery, delivery.InventoryId, cancellationToken);
        var response = new DeliveryEditResponse
        {
            Delivery = delivery,
            Version = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { type, versionValues }))),
            OrderedQty = order?.Edition ?? supplier?.Edition,
            AutoFreightCalc = calculation?.ArchivedAutoFreightCalc ?? false,
            PackagingType = type == "DeliveryFromStock" ? supplier?.PackagingType ?? string.Empty : string.Empty,
            UnitsPerPackage = supplier?.PackagingType == "BUNT" ? calculation?.ArchivedNrOfPerBundle : calculation?.ArchivedNrOfPerOuterPackaging,
            AvailableItems = stock?.Items,
            AvailablePallets = stock?.Pallets,
            LatestStockTakingDate = stock?.SnapshotDate,
            Inventories = inventories,
            RelatedDeliveryIds = group.Where(item => item != entity).Select(item => (int)db.Entry(item).Property("Id").CurrentValue!).ToList(),
        };
        return new(entity, group.Where(item => item != entity).ToList(), response);
    }

    private async Task<DeliveryEditError?> ValidateAsync(EditContext context, UpdateDeliveryRequest request, CancellationToken cancellationToken)
    {
        var delivery = context.Response.Delivery;
        if (request.DeliveryDate is null) return new("Validation", "Måste ange datum.");
        if (request.NrOfItems is double quantity && (!double.IsFinite(quantity) || quantity < 0)
            || request.NrOfPallets < 0 || request.NrOfBunt < 0 || request.NrOfYtterforpackning < 0
            || request.PalletLength < 0 || request.PalletWidth < 0 || request.PalletHeight < 0
            || request.PalletCalcFactor < 0 || request.PalletCalcFactor > 99.999m || request.SupplierInvoiceNr < 0)
            return new("Validation", "Antal, pallformat och pallfaktor måste vara giltiga, positiva värden.");
        if (request.CallOff?.Length > 50 || request.SpcsRefNr?.Length > 50)
            return new("Validation", "Referenser får vara högst 50 tecken.");
        if (delivery.IsSlattPallet && (delivery.PalletLength != request.PalletLength
            || delivery.PalletWidth != request.PalletWidth || delivery.PalletHeight != request.PalletHeight))
            return new("Validation", "Pallformat för slattpall ändras på huvudleveransen.");
        if (delivery.Type != "DeliveryToCustomer" && request.InventoryId.HasValue
            && !await db.Inventories.AnyAsync(item => item.Id == request.InventoryId
                && (item.IsInventory || item.Id == delivery.InventoryId), cancellationToken))
            return new("Validation", "Valt lager kunde inte hittas.");
        if (delivery.Type == "DeliveryFromStock")
        {
            var quantitiesChanged = request.NrOfItems != delivery.NrOfItems || request.NrOfPallets != delivery.NrOfPallets
                || request.NrOfBunt != delivery.NrOfBunt || request.NrOfYtterforpackning != delivery.NrOfYtterforpackning;
            var units = context.Response.UnitsPerPackage;
            if (quantitiesChanged && context.Response.PackagingType is "BUNT" or "YTTERFORPACKNING" && units is > 0)
            {
                var packages = context.Response.PackagingType == "BUNT" ? request.NrOfBunt : request.NrOfYtterforpackning;
                if (request.NrOfItems.HasValue && (request.NrOfItems.Value % units.Value != 0
                    || packages * units.Value != request.NrOfItems))
                    return new("Validation", "Antalet går ej jämnt upp för vald förpackning.");
            }
            else if (quantitiesChanged && delivery.EditionPerPallet is > 0 && request.NrOfItems.HasValue
                && (request.NrOfItems.Value % delivery.EditionPerPallet.Value != 0
                    || (double?)request.NrOfPallets * delivery.EditionPerPallet != request.NrOfItems))
                return new("Validation", "Antalet går ej jämnt upp pallvis.");
            var stockAffected = request.NrOfItems > delivery.NrOfItems || request.NrOfPallets > delivery.NrOfPallets
                || request.InventoryId != delivery.InventoryId || request.DeliveryDate?.Date != delivery.DeliveryDate?.Date
                || request.IsDelivered != (delivery.DeliveryStatus == 2);
            if (stockAffected)
            {
                if (!delivery.SupplierOrderId.HasValue) return new("Validation", "Beställning saknas för lagerleveransen.");
                var stock = await LoadStockAsync(delivery, request.InventoryId, cancellationToken);
                if (request.NrOfItems > stock?.Items || request.NrOfPallets > stock?.Pallets)
                    return new("Validation", "Saldot efter denna leverans blir negativt. Kontrollera antal och pallantal.");
            }
        }
        if (delivery.Type == "DeliveryToStock" && (request.InventoryId != delivery.InventoryId
            || request.NrOfItems < delivery.NrOfItems || request.NrOfPallets < delivery.NrOfPallets))
        {
            var stock = await LoadStockAsync(delivery, delivery.InventoryId, cancellationToken);
            var removedItems = request.InventoryId == delivery.InventoryId ? (delivery.NrOfItems ?? 0) - (request.NrOfItems ?? 0) : delivery.NrOfItems ?? 0;
            var removedPallets = request.InventoryId == delivery.InventoryId ? (delivery.NrOfPallets ?? 0) - (request.NrOfPallets ?? 0) : delivery.NrOfPallets ?? 0;
            if (removedItems > 0 && removedItems > stock?.Items || removedPallets > 0 && removedPallets > stock?.Pallets)
                return new("Validation", "Ändringen ger negativt saldo i det ursprungliga lagret.");
        }
        return null;
    }

    private sealed record StockBalance(double Items, double Pallets, DateTime? SnapshotDate);
    private sealed record StockRow(int Id, int? Edition, int? Inventory, double? Items, int? Pallets, int? Status, DateTime? Date);

    private async Task<StockBalance?> LoadStockAsync(DeliveryDto delivery, int? inventoryId, CancellationToken cancellationToken)
    {
        if (!delivery.SupplierOrderId.HasValue) return null;
        var inbound = await db.DeliveryToStocks.AsNoTracking().Where(item => item.SupplierOrderId == delivery.SupplierOrderId)
            .Select(item => new StockRow(item.Id, item.EditionPerPallet, item.InventoryId, item.NrOfItems, item.NrOfPallets, item.DeliveryStatus, item.DeliveryDate)).ToListAsync(cancellationToken);
        var outbound = await db.DeliveryFromStocks.AsNoTracking()
            .Where(item => item.CustomerOrder != null && item.CustomerOrder.SupplierOrderId == delivery.SupplierOrderId
                && (delivery.Type != "DeliveryFromStock" || item.Id != delivery.Id))
            .Select(item => new StockRow(item.Id, item.EditionPerPallet, item.InventoryId, item.NrOfItems, item.NrOfPallets, item.DeliveryStatus, item.DeliveryDate)).ToListAsync(cancellationToken);
        var snapshot = await db.StockTakingItems.AsNoTracking()
            .Where(item => item.SupplierOrderId == delivery.SupplierOrderId && item.StockTaking != null && item.StockTaking.StockTakingDate.HasValue)
            .OrderByDescending(item => item.StockTaking!.StockTakingDate).ThenByDescending(item => item.Id)
            .Select(item => new { Date = item.StockTaking!.StockTakingDate, item.NrOfItems, item.NrOfPallets })
            .FirstOrDefaultAsync(cancellationToken);
        bool InGroup(StockRow row) => row.Edition == delivery.EditionPerPallet && row.Inventory == inventoryId;
        bool AfterSnapshot(StockRow row) => snapshot is null || row.Status != 2 || row.Date > snapshot.Date;
        var groupItems = inbound.Where(InGroup).Sum(row => row.Items ?? 0) - outbound.Where(InGroup).Sum(row => row.Items ?? 0);
        var groupPallets = inbound.Where(InGroup).Sum(row => row.Pallets ?? 0) - outbound.Where(InGroup).Sum(row => row.Pallets ?? 0);
        if (snapshot is null) return new(groupItems, groupPallets, null);
        var totalItems = (snapshot.NrOfItems ?? 0) + inbound.Where(AfterSnapshot).Sum(row => row.Items ?? 0) - outbound.Where(AfterSnapshot).Sum(row => row.Items ?? 0);
        var totalPallets = (snapshot.NrOfPallets ?? 0) + inbound.Where(AfterSnapshot).Sum(row => row.Pallets ?? 0) - outbound.Where(AfterSnapshot).Sum(row => row.Pallets ?? 0);
        return new(Math.Min(groupItems, totalItems), Math.Min(groupPallets, totalPallets), snapshot.Date);
    }

    private async Task<decimal?> CalculateFactorAsync(EditContext context, UpdateDeliveryRequest request, CancellationToken cancellationToken)
    {
        if (!context.Response.AutoFreightCalc) return request.PalletCalcFactor.HasValue ? decimal.Round(request.PalletCalcFactor.Value, 3) : null;
        var original = context.Response.Delivery;
        if (request.NrOfPallets == original.NrOfPallets && request.PalletLength == original.PalletLength
            && request.PalletWidth == original.PalletWidth && request.PalletIsStackable == original.PalletIsStackable)
            return original.PalletCalcFactor;
        var length = Math.Max(request.PalletLength ?? 0, request.PalletWidth ?? 0);
        var width = Math.Min(request.PalletLength ?? 0, request.PalletWidth ?? 0);
        var factors = await db.PalletFactors.AsNoTracking()
            .Where(item => item.LengthFrom < length && item.LengthTo >= length && item.WidthFrom < width && item.WidthTo >= width)
            .Select(item => item.PalletFactors).Take(2).ToListAsync(cancellationToken);
        var palletCount = (double)(request.NrOfPallets ?? 0) + context.Related.Sum(item => Map(item, context.Response.Delivery.Type).NrOfPallets ?? 0);
        var places = Math.Ceiling(palletCount / (request.PalletIsStackable ? 2 : 1));
        var values = factors.Count == 1 ? factors[0]?.Split(';', StringSplitOptions.TrimEntries) : null;
        return values is not null && places > 0 && places <= values.Length
            && decimal.TryParse(values[(int)places - 1], NumberStyles.Number, CultureInfo.InvariantCulture, out var factor)
            && factor >= 0 && factor < 100 ? decimal.Round(factor, 3) : 0;
    }

    private void Set(object entity, string name, object? value)
    {
        var entry = db.Entry(entity);
        if (entry.Metadata.FindProperty(name) is not null) entry.Property(name).CurrentValue = value;
    }

    private async Task<object?> FindAsync(string type, int id, CancellationToken cancellationToken) => type switch
    {
        "DeliveryToStock" => await db.DeliveryToStocks.FirstOrDefaultAsync(item => item.Id == id, cancellationToken),
        "DeliveryFromStock" => await db.DeliveryFromStocks.FirstOrDefaultAsync(item => item.Id == id, cancellationToken),
        "DeliveryToCustomer" => await db.DeliveryToCustomers.FirstOrDefaultAsync(item => item.Id == id, cancellationToken),
        _ => null,
    };

    private DeliveryDto Map(object entity, string type)
    {
        var entry = db.Entry(entity);
        ValueType Read<ValueType>(string name) => entry.Metadata.FindProperty(name) is null
            ? default!
            : (ValueType)entry.Property(name).CurrentValue!;

        return new DeliveryDto
        {
            Id = Read<int>(nameof(DeliveryDto.Id)),
            Type = type,
            SupplierOrderId = Read<int?>(nameof(DeliveryDto.SupplierOrderId)),
            CustomerOrderId = Read<int?>(nameof(DeliveryDto.CustomerOrderId)),
            DeliveryDate = Read<DateTime?>(nameof(DeliveryDto.DeliveryDate)),
            NrOfItems = Read<double?>(nameof(DeliveryDto.NrOfItems)),
            NrOfPallets = Read<int?>(nameof(DeliveryDto.NrOfPallets)),
            NrOfBunt = Read<int?>(nameof(DeliveryDto.NrOfBunt)),
            NrOfYtterforpackning = Read<int?>(nameof(DeliveryDto.NrOfYtterforpackning)),
            InventoryId = Read<int?>(nameof(DeliveryDto.InventoryId)),
            CallOff = Read<string?>(nameof(DeliveryDto.CallOff)),
            SpcsRefNr = Read<string?>(nameof(DeliveryDto.SpcsRefNr)),
            SupplierInvoiceNr = Read<int?>(nameof(DeliveryDto.SupplierInvoiceNr)),
            PalletLength = Read<int?>(nameof(DeliveryDto.PalletLength)),
            PalletWidth = Read<int?>(nameof(DeliveryDto.PalletWidth)),
            PalletHeight = Read<int?>(nameof(DeliveryDto.PalletHeight)),
            PalletIsStackable = Read<bool>(nameof(DeliveryDto.PalletIsStackable)),
            PalletCalcFactor = Read<decimal?>(nameof(DeliveryDto.PalletCalcFactor)),
            EditionPerPallet = Read<int?>(nameof(DeliveryDto.EditionPerPallet)),
            IsSlattPallet = Read<bool>(nameof(DeliveryDto.IsSlattPallet)),
            ParentDeliveryId = Read<int?>(nameof(DeliveryDto.ParentDeliveryId)),
            DeliveryStatus = Read<int?>(nameof(DeliveryDto.DeliveryStatus)),
        };
    }
}