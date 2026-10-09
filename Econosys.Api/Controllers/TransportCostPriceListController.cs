using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class TransportCostPriceListController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<TransportCostPriceListController> _logger;

        public TransportCostPriceListController(ApplicationDbContext dbContext, ILogger<TransportCostPriceListController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<TransportCostPriceListOptionDto>>> GetOptions()
        {
            var priceLists = await _dbContext.TransportCostPriceLists
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new TransportCostPriceListOptionDto
                {
                    Id = x.Id,
                    Name = x.Name ?? string.Empty,
                })
                .ToListAsync();

            return Ok(priceLists);
        }

        [HttpGet("form-options")]
        public async Task<ActionResult<TransportCostPriceListFormOptionsDto>> GetFormOptions()
        {
            var currencies = await _dbContext.Currencies
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new TransportCostPriceListCurrencyOptionDto
                {
                    Id = x.Id,
                    Name = x.Name ?? string.Empty,
                })
                .ToListAsync();

            var supplierFactories = await _dbContext.SupplierFactories
                .AsNoTracking()
                .Select(x => new TransportCostPriceListSupplierFactoryOptionDto
                {
                    Id = x.Id,
                    SupplierName = x.Supplier != null ? x.Supplier.Name : null,
                    FactoryName = x.Name,
                })
                .OrderBy(x => x.SupplierName)
                .ThenBy(x => x.FactoryName)
                .ToListAsync();

            var inventories = await _dbContext.Inventories
                .AsNoTracking()
                .Where(x => x.IsOmlast)
                .OrderBy(x => x.Name)
                .Select(x => new TransportCostPriceListInventoryOptionDto
                {
                    Id = x.Id,
                    Name = x.Name,
                })
                .ToListAsync();

            return Ok(new TransportCostPriceListFormOptionsDto
            {
                Currencies = currencies,
                SupplierFactories = supplierFactories,
                Inventories = inventories,
            });
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<TransportCostPriceListDto>> GetById(int id)
        {
            var dto = await LoadDtoAsync(id);
            if (dto is null)
            {
                return NotFound();
            }

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<TransportCostPriceListDto>> Create([FromBody] SaveTransportCostPriceListRequest request)
        {
            var validationError = await ValidateRequestAsync(request, null);
            if (validationError is not null)
            {
                return BadRequest(new { message = validationError });
            }

            var priceList = new TransportCostPriceList();
            ApplyHeader(request, priceList);

            foreach (var row in request.Rows)
            {
                var entity = new TransportCostPriceListData();
                CopyRow(row, entity);
                priceList.TransportCostPriceListData.Add(entity);
            }

            foreach (var supplierFactoryId in request.SupplierFactoryIds.Distinct())
            {
                priceList.SupplierFactoryTransportCostPriceLists.Add(new SupplierFactoryTransportCostPriceList { SupplierFactoryId = supplierFactoryId });
            }

            foreach (var inventoryId in request.InventoryIds.Distinct())
            {
                priceList.InventoryTransportCostPriceLists.Add(new InventoryTransportCostPriceList { InventoryId = inventoryId });
            }

            _dbContext.TransportCostPriceLists.Add(priceList);
            await _dbContext.SaveChangesAsync();

            var dto = await LoadDtoAsync(priceList.Id);
            return CreatedAtAction(nameof(GetById), new { id = priceList.Id }, dto);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<TransportCostPriceListDto>> Update(int id, [FromBody] SaveTransportCostPriceListRequest request)
        {
            var priceList = await _dbContext.TransportCostPriceLists
                .Include(x => x.TransportCostPriceListData)
                .Include(x => x.SupplierFactoryTransportCostPriceLists)
                .Include(x => x.InventoryTransportCostPriceLists)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (priceList is null)
            {
                return NotFound();
            }

            var validationError = await ValidateRequestAsync(request, priceList);
            if (validationError is not null)
            {
                return BadRequest(new { message = validationError });
            }

            ApplyHeader(request, priceList);
            SyncRows(priceList, request.Rows);
            SyncSupplierFactories(priceList, request.SupplierFactoryIds);
            SyncInventories(priceList, request.InventoryIds);

            await _dbContext.SaveChangesAsync();

            return Ok(await LoadDtoAsync(id));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var priceList = await _dbContext.TransportCostPriceLists
                .Include(x => x.TransportCostPriceListData)
                .Include(x => x.SupplierFactoryTransportCostPriceLists)
                .Include(x => x.InventoryTransportCostPriceLists)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (priceList is null)
            {
                return NotFound();
            }

            var forcedCostCalcs = await _dbContext.TransportOrderCostCalcs
                .Where(x => x.CostCalcForcePriceListId == id)
                .ToListAsync();

            foreach (var costCalc in forcedCostCalcs)
            {
                costCalc.CostCalcForcePriceListId = null;
            }

            _dbContext.TransportCostPriceListDatas.RemoveRange(priceList.TransportCostPriceListData);
            _dbContext.SupplierFactoryTransportCostPriceLists.RemoveRange(priceList.SupplierFactoryTransportCostPriceLists);
            _dbContext.InventoryTransportCostPriceLists.RemoveRange(priceList.InventoryTransportCostPriceLists);
            _dbContext.TransportCostPriceLists.Remove(priceList);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Failed to delete transport cost price list {TransportCostPriceListId}", id);
                return Conflict(new { message = "Kunde inte radera prislistan eftersom den används." });
            }

            return NoContent();
        }

        private async Task<string?> ValidateRequestAsync(SaveTransportCostPriceListRequest request, TransportCostPriceList? existing)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return "Namn måste anges.";
            }

            if (request.CurrencyId.HasValue
                && !await _dbContext.Currencies.AnyAsync(x => x.Id == request.CurrencyId.Value))
            {
                return "Vald valuta finns inte.";
            }

            for (var index = 0; index < request.Rows.Count; index++)
            {
                var row = request.Rows[index];
                if (row.PostalNrFrom.HasValue && row.PostalNrTo.HasValue && row.PostalNrFrom > row.PostalNrTo)
                {
                    return $"Rad {index + 1}: Postnr från får inte vara större än postnr till.";
                }

                if (row.Id is > 0 && (existing is null || existing.TransportCostPriceListData.All(x => x.Id != row.Id)))
                {
                    return $"Rad {index + 1} tillhör inte prislistan.";
                }
            }

            var supplierFactoryIds = request.SupplierFactoryIds.Distinct().ToList();
            if (supplierFactoryIds.Count > 0)
            {
                var existingCount = await _dbContext.SupplierFactories.CountAsync(x => supplierFactoryIds.Contains(x.Id));
                if (existingCount != supplierFactoryIds.Count)
                {
                    return "En eller flera valda fabriker finns inte.";
                }
            }

            var inventoryIds = request.InventoryIds.Distinct().ToList();
            if (inventoryIds.Count > 0)
            {
                var existingCount = await _dbContext.Inventories.CountAsync(x => inventoryIds.Contains(x.Id));
                if (existingCount != inventoryIds.Count)
                {
                    return "En eller flera valda omlastningsplatser finns inte.";
                }
            }

            return null;
        }

        private void SyncRows(TransportCostPriceList priceList, List<SaveTransportCostPriceListDataRow> rows)
        {
            var keptIds = rows
                .Where(x => x.Id is > 0)
                .Select(x => x.Id!.Value)
                .ToHashSet();

            var removed = priceList.TransportCostPriceListData
                .Where(x => !keptIds.Contains(x.Id))
                .ToList();

            _dbContext.TransportCostPriceListDatas.RemoveRange(removed);

            foreach (var row in rows)
            {
                TransportCostPriceListData entity;
                if (row.Id is > 0)
                {
                    entity = priceList.TransportCostPriceListData.First(x => x.Id == row.Id);
                }
                else
                {
                    entity = new TransportCostPriceListData();
                    priceList.TransportCostPriceListData.Add(entity);
                }

                CopyRow(row, entity);
            }
        }

        private void SyncSupplierFactories(TransportCostPriceList priceList, List<int> supplierFactoryIds)
        {
            var wanted = supplierFactoryIds.ToHashSet();

            var removed = priceList.SupplierFactoryTransportCostPriceLists
                .Where(x => !x.SupplierFactoryId.HasValue || !wanted.Contains(x.SupplierFactoryId.Value))
                .ToList();

            _dbContext.SupplierFactoryTransportCostPriceLists.RemoveRange(removed);

            var existingIds = priceList.SupplierFactoryTransportCostPriceLists
                .Except(removed)
                .Select(x => x.SupplierFactoryId!.Value)
                .ToHashSet();

            foreach (var supplierFactoryId in wanted.Where(x => !existingIds.Contains(x)))
            {
                priceList.SupplierFactoryTransportCostPriceLists.Add(new SupplierFactoryTransportCostPriceList { SupplierFactoryId = supplierFactoryId });
            }
        }

        private void SyncInventories(TransportCostPriceList priceList, List<int> inventoryIds)
        {
            var wanted = inventoryIds.ToHashSet();

            var removed = priceList.InventoryTransportCostPriceLists
                .Where(x => !x.InventoryId.HasValue || !wanted.Contains(x.InventoryId.Value))
                .ToList();

            _dbContext.InventoryTransportCostPriceLists.RemoveRange(removed);

            var existingIds = priceList.InventoryTransportCostPriceLists
                .Except(removed)
                .Select(x => x.InventoryId!.Value)
                .ToHashSet();

            foreach (var inventoryId in wanted.Where(x => !existingIds.Contains(x)))
            {
                priceList.InventoryTransportCostPriceLists.Add(new InventoryTransportCostPriceList { InventoryId = inventoryId });
            }
        }

        private async Task<TransportCostPriceListDto?> LoadDtoAsync(int id)
        {
            var priceList = await _dbContext.TransportCostPriceLists
                .AsNoTracking()
                .Include(x => x.Currency)
                .Include(x => x.TransportCostPriceListData)
                .Include(x => x.SupplierFactoryTransportCostPriceLists)
                    .ThenInclude(x => x.SupplierFactory)
                        .ThenInclude(x => x!.Supplier)
                .Include(x => x.InventoryTransportCostPriceLists)
                    .ThenInclude(x => x.Inventory)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == id);

            return priceList is null ? null : MapToDto(priceList);
        }

        private static void ApplyHeader(TransportCostPriceListHeaderBase source, TransportCostPriceList target)
        {
            target.Name = source.Name?.Trim();
            target.CurrencyId = source.CurrencyId;
            target.SecaMarpolPallet = source.SecaMarpolPallet;
            target.DmtPercentPallet = source.DmtPercentPallet;
            target.AdditionCurrencyPercentPallet = source.AdditionCurrencyPercentPallet;
            target.OtherPallet = source.OtherPallet;
            target.OtherPercentPallet = source.OtherPercentPallet;
            target.AdditionTotalPercentPallet = source.AdditionTotalPercentPallet;
            target.SecaMarpolFtl = source.SecaMarpolFtl;
            target.DmtPercentFtl = source.DmtPercentFtl;
            target.AdditionCurrencyPercentFtl = source.AdditionCurrencyPercentFtl;
            target.OtherFtl = source.OtherFtl;
            target.OtherPercentFtl = source.OtherPercentFtl;
            target.AdditionTotalPercentFtl = source.AdditionTotalPercentFtl;
            target.IsStafflad = source.IsStafflad;
            target.LoadingCost = source.LoadingCost;
            target.UnloadingCost = source.UnloadingCost;
            target.PreCalcAdditionPercent = source.PreCalcAdditionPercent;
        }

        private static TransportCostPriceListDto MapToDto(TransportCostPriceList priceList)
        {
            return new TransportCostPriceListDto
            {
                Id = priceList.Id,
                Name = priceList.Name,
                ImportName = priceList.ImportName,
                SecaMarpolPallet = priceList.SecaMarpolPallet,
                DmtPercentPallet = priceList.DmtPercentPallet,
                AdditionCurrencyPercentPallet = priceList.AdditionCurrencyPercentPallet,
                OtherPallet = priceList.OtherPallet,
                OtherPercentPallet = priceList.OtherPercentPallet,
                AdditionTotalPercentPallet = priceList.AdditionTotalPercentPallet,
                CurrencyId = priceList.CurrencyId,
                CurrencyName = priceList.Currency?.Name,
                SecaMarpolFtl = priceList.SecaMarpolFtl,
                DmtPercentFtl = priceList.DmtPercentFtl,
                AdditionCurrencyPercentFtl = priceList.AdditionCurrencyPercentFtl,
                OtherFtl = priceList.OtherFtl,
                OtherPercentFtl = priceList.OtherPercentFtl,
                AdditionTotalPercentFtl = priceList.AdditionTotalPercentFtl,
                IsStafflad = priceList.IsStafflad,
                LastImportDateTime = priceList.LastImportDateTime,
                LoadingCost = priceList.LoadingCost,
                UnloadingCost = priceList.UnloadingCost,
                PreCalcAdditionPercent = priceList.PreCalcAdditionPercent,
                PriceListData = priceList.TransportCostPriceListData
                    .OrderBy(x => x.CountryCode)
                    .ThenBy(x => x.PostalNrFrom)
                    .ThenBy(x => x.Id)
                    .Select(MapToDto)
                    .ToList(),
                SupplierFactories = priceList.SupplierFactoryTransportCostPriceLists
                    .Where(x => x.SupplierFactoryId.HasValue)
                    .Select(x => new TransportCostPriceListSupplierFactoryLinkDto
                    {
                        Id = x.Id,
                        SupplierFactoryId = x.SupplierFactoryId!.Value,
                        SupplierName = x.SupplierFactory?.Supplier?.Name,
                        FactoryName = x.SupplierFactory?.Name,
                    })
                    .OrderBy(x => x.SupplierName)
                    .ThenBy(x => x.FactoryName)
                    .ToList(),
                Inventories = priceList.InventoryTransportCostPriceLists
                    .Where(x => x.InventoryId.HasValue)
                    .Select(x => new TransportCostPriceListInventoryLinkDto
                    {
                        Id = x.Id,
                        InventoryId = x.InventoryId!.Value,
                        InventoryName = x.Inventory?.Name,
                    })
                    .OrderBy(x => x.InventoryName)
                    .ToList(),
            };
        }

        private static TransportCostPriceListDataDto MapToDto(TransportCostPriceListData data)
        {
            var dto = new TransportCostPriceListDataDto
            {
                Id = data.Id,
                TransportCostPriceListId = data.TransportCostPriceListId,
                CountryCode = data.CountryCode,
                PostalNrFrom = data.PostalNrFrom,
                PostalNrTo = data.PostalNrTo,
                Transhipment = data.Transhipment,
                AdditionSekPerPallet = data.AdditionSekPerPallet,
            };

            dto.P1 = data.P1;
            dto.P2 = data.P2;
            dto.P3 = data.P3;
            dto.P4 = data.P4;
            dto.P5 = data.P5;
            dto.P6 = data.P6;
            dto.P7 = data.P7;
            dto.P8 = data.P8;
            dto.P9 = data.P9;
            dto.P10 = data.P10;
            dto.P11 = data.P11;
            dto.P12 = data.P12;
            dto.P13 = data.P13;
            dto.P14 = data.P14;
            dto.P15 = data.P15;
            dto.P16 = data.P16;
            dto.P17 = data.P17;
            dto.P18 = data.P18;
            dto.P19 = data.P19;
            dto.P20 = data.P20;
            dto.P21 = data.P21;
            dto.P22 = data.P22;
            dto.P23 = data.P23;
            dto.P24 = data.P24;
            dto.P25 = data.P25;
            dto.P26 = data.P26;
            dto.P27 = data.P27;
            dto.P28 = data.P28;
            dto.P29 = data.P29;
            dto.P30 = data.P30;
            dto.P31 = data.P31;
            dto.P32 = data.P32;
            dto.PFTL = data.PFTL;

            return dto;
        }

        private static void CopyRow(TransportCostPriceListDataRowBase source, TransportCostPriceListData target)
        {
            target.CountryCode = string.IsNullOrWhiteSpace(source.CountryCode) ? null : source.CountryCode.Trim().ToUpperInvariant();
            target.PostalNrFrom = source.PostalNrFrom;
            target.PostalNrTo = source.PostalNrTo;
            target.Transhipment = string.IsNullOrWhiteSpace(source.Transhipment) ? null : source.Transhipment.Trim();
            target.AdditionSekPerPallet = source.AdditionSekPerPallet;
            target.P1 = source.P1;
            target.P2 = source.P2;
            target.P3 = source.P3;
            target.P4 = source.P4;
            target.P5 = source.P5;
            target.P6 = source.P6;
            target.P7 = source.P7;
            target.P8 = source.P8;
            target.P9 = source.P9;
            target.P10 = source.P10;
            target.P11 = source.P11;
            target.P12 = source.P12;
            target.P13 = source.P13;
            target.P14 = source.P14;
            target.P15 = source.P15;
            target.P16 = source.P16;
            target.P17 = source.P17;
            target.P18 = source.P18;
            target.P19 = source.P19;
            target.P20 = source.P20;
            target.P21 = source.P21;
            target.P22 = source.P22;
            target.P23 = source.P23;
            target.P24 = source.P24;
            target.P25 = source.P25;
            target.P26 = source.P26;
            target.P27 = source.P27;
            target.P28 = source.P28;
            target.P29 = source.P29;
            target.P30 = source.P30;
            target.P31 = source.P31;
            target.P32 = source.P32;
            target.PFTL = source.PFTL;
        }
    }
}
