using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services;

public sealed class PartyPricesService(ApplicationDbContext db) : IPartyPricesService
{
    private Task<bool> ExistsAsync(int id, bool isSupplier, CancellationToken ct) =>
        isSupplier
            ? db.Suppliers.AnyAsync(x => x.Id == id, ct)
            : db.Customers.AnyAsync(x => x.Id == id, ct);

    private IQueryable<PalletFormatPrice> PalletPrices(int id, bool isSupplier) =>
        db.PalletFormatPrices.Where(x => isSupplier ? x.SupplierId == id : x.CustomerId == id);

    private IQueryable<CostPrice> CostPrices(int id, bool isSupplier) =>
        db.CostPrices.Where(x => isSupplier ? x.SupplierId == id : x.CustomerId == id);

    public async Task<PartyPricesResponse?> GetAsync(int id, bool isSupplier, CancellationToken cancellationToken)
    {
        if (!await ExistsAsync(id, isSupplier, cancellationToken))
            return null;

        var pallets = await PalletPrices(id, isSupplier).AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new PartyPriceDto { Id = x.Id, ItemId = x.PalletFormatId ?? 0, Price = x.PalletPrice })
            .ToListAsync(cancellationToken);
        var costs = await CostPrices(id, isSupplier).AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new PartyPriceDto
            {
                Id = x.Id,
                ItemId = x.CostId ?? 0,
                Price = isSupplier ? x.DefaultInPrice : x.DefaultOutPrice,
            }).ToListAsync(cancellationToken);
        var palletFormats = await db.PalletFormats.AsNoTracking().OrderBy(x => x.SortNr).ThenBy(x => x.Id)
            .Select(x => new FilterOptionDto<int>
            {
                Id = x.Id,
                Name = x.Name ?? "",
                IsActive = x.Active && x.PalletType != null && x.PalletType.IsActive,
            })
            .ToListAsync(cancellationToken);
        var costOptions = await db.Costs.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new FilterOptionDto<int> { Id = x.Id, Name = x.Name, IsActive = x.IsActive })
            .ToListAsync(cancellationToken);

        return new PartyPricesResponse(pallets, costs, palletFormats, costOptions);
    }

    public async Task<(bool Found, string? Error)> SaveAsync(
        int id, bool isSupplier, UpdatePartyPricesRequest request, CancellationToken cancellationToken)
    {
        if (!await ExistsAsync(id, isSupplier, cancellationToken))
            return (false, null);

        var pallets = await PalletPrices(id, isSupplier).ToListAsync(cancellationToken);
        var costs = await CostPrices(id, isSupplier).ToListAsync(cancellationToken);
        var palletIds = await db.PalletFormats.Select(x => x.Id).ToListAsync(cancellationToken);
        var costIds = await db.Costs.Select(x => x.Id).ToListAsync(cancellationToken);

        if (!HasValidRows(request.PalletPrices, pallets.Select(x => x.Id).ToHashSet(), palletIds.ToHashSet())
            || !HasValidRows(request.CostPrices, costs.Select(x => x.Id).ToHashSet(), costIds.ToHashSet()))
        {
            return (true, "Kontrollera valda pallformat, kostnader och priser (högst två decimaler).");
        }

        db.PalletFormatPrices.RemoveRange(pallets.Where(x => !request.PalletPrices.Any(row => row.Id == x.Id)));
        db.CostPrices.RemoveRange(costs.Where(x => !request.CostPrices.Any(row => row.Id == x.Id)));

        foreach (var row in request.PalletPrices)
        {
            var entity = pallets.FirstOrDefault(x => x.Id == row.Id);
            if (entity is null)
            {
                entity = new PalletFormatPrice
                {
                    CustomerId = isSupplier ? null : id,
                    SupplierId = isSupplier ? id : null,
                };
                db.PalletFormatPrices.Add(entity);
            }
            entity.PalletFormatId = row.ItemId;
            entity.PalletPrice = row.Price;
        }

        foreach (var row in request.CostPrices)
        {
            var entity = costs.FirstOrDefault(x => x.Id == row.Id);
            if (entity is null)
            {
                entity = new CostPrice
                {
                    CustomerId = isSupplier ? null : id,
                    SupplierId = isSupplier ? id : null,
                };
                db.CostPrices.Add(entity);
            }
            entity.CostId = row.ItemId;
            if (isSupplier)
                entity.DefaultInPrice = row.Price;
            else
                entity.DefaultOutPrice = row.Price;
        }

        await db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    private static bool HasValidRows(List<PartyPriceDto> rows, HashSet<int> ownedIds, HashSet<int> itemIds) =>
        rows.Where(x => x.Id != 0).Select(x => x.Id).Distinct().Count() == rows.Count(x => x.Id != 0)
        && rows.All(x => (x.Id == 0 || ownedIds.Contains(x.Id))
            && itemIds.Contains(x.ItemId)
            && (x.Price is null || (x.Price >= -99999999.99m && x.Price <= 99999999.99m
                && decimal.Round(x.Price.Value, 2) == x.Price)));
}
