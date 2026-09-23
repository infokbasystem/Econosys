using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Econosys.Api.Common;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/calloff")]
    public class CallOffController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILegacyUserResolutionService _legacyUserResolution;

        public CallOffController(ApplicationDbContext dbContext, ILegacyUserResolutionService legacyUserResolution)
        {
            _dbContext = dbContext;
            _legacyUserResolution = legacyUserResolution;
        }

        [HttpGet("form-options")]
        public async Task<ActionResult<CallOffFormOptionsDto>> GetFormOptions()
        {
            var shippers = await _dbContext.Shippers
                .AsNoTracking()
                .Where(x => x.Name != null && x.Name != "")
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Id)
                .Select(x => new FilterOptionDto<int>
                {
                    Id = x.Id,
                    Name = x.Name!,
                    IsActive = true,
                })
                .ToListAsync();

            return Ok(new CallOffFormOptionsDto { Shippers = shippers });
        }

        [HttpGet("{id:int}/aggregate")]
        public async Task<ActionResult<CallOffAggregateDto>> GetAggregateById(int id)
        {
            var callOff = await _dbContext.CallOffs
                .AsNoTracking()
                .Include(x => x.CreatedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (callOff is null)
            {
                return NotFound();
            }

            return Ok(await BuildAggregateDtoAsync(callOff));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var callOff = await _dbContext.CallOffs
                .Include(x => x.CallOffDeliveries)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (callOff is null)
            {
                return NotFound();
            }

            _dbContext.CallOffDeliveries.RemoveRange(callOff.CallOffDeliveries);

            var deliveryLegs = await _dbContext.DeliveryLegs
                .Where(x => x.CallOffId == id)
                .ToListAsync();
            _dbContext.DeliveryLegs.RemoveRange(deliveryLegs);

            var documentFiles = await _dbContext.DocumentFiles
                .Where(x => x.CallOffId == id)
                .ToListAsync();
            _dbContext.DocumentFiles.RemoveRange(documentFiles);

            _dbContext.CallOffs.Remove(callOff);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost]
        public async Task<ActionResult<CallOffAggregateDto>> CreateCallOff([FromBody] SaveCallOffAggregateRequest request)
        {
            if (request is null)
            {
                return BadRequest("Ingen data att spara.");
            }

            if (request.Reference?.Length > 100)
            {
                return BadRequest("Referensen får vara högst 100 tecken.");
            }

            if (request.Note?.Length > 500)
            {
                return BadRequest("Noteringen får vara högst 500 tecken.");
            }

            if (request.DeliveryStatus is < 0 or > 2)
            {
                return BadRequest("Leveransstatus är ogiltig.");
            }

            if (request.ShipperId.HasValue && !await _dbContext.Shippers.AnyAsync(x => x.Id == request.ShipperId.Value))
            {
                return BadRequest("Den valda speditören finns inte.");
            }

            var legacyUser = await _legacyUserResolution.ResolveCurrentUserAsync(User);

            var callOff = new CallOff
            {
                ShipperId = request.ShipperId,
                Reference = request.Reference,
                Note = request.Note,
                DeliveryDate = request.DeliveryDate,
                DeliveryStatus = request.DeliveryStatus,
                IsSentToShipper = request.IsSentToShipper,
                DoDebitFreight = request.DoDebitFreight,
                FreightCostToDebit = request.FreightCostToDebit,
                CustomerDeliveryAddressId = request.CustomerDeliveryAddressId,
            };

            if (legacyUser is not null)
            {
                callOff.CreatedDateTime = SwedishTime.Now;
                callOff.CreatedByUserId = legacyUser.Id;
            }

            _dbContext.CallOffs.Add(callOff);
            await _dbContext.SaveChangesAsync();

            var createSyncError = await SyncCallOffDeliveriesAsync(callOff, request.CallOffDeliveryList ?? new List<SaveCallOffDeliveryDto>(), new List<CallOffDelivery>());
            if (createSyncError is not null)
            {
                return BadRequest(createSyncError);
            }

            await _dbContext.SaveChangesAsync();

            return Ok(await BuildAggregateDtoAsync(callOff));
        }

        [HttpPut("{id:int}/aggregate")]
        public async Task<ActionResult<CallOffAggregateDto>> SaveAggregateById(int id, [FromBody] SaveCallOffAggregateRequest request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Version))
            {
                return BadRequest("En aktuell versionsnyckel krävs för att spara avropet.");
            }

            if (request.Reference?.Length > 100)
            {
                return BadRequest("Referensen får vara högst 100 tecken.");
            }

            if (request.Note?.Length > 500)
            {
                return BadRequest("Noteringen får vara högst 500 tecken.");
            }

            if (request.DeliveryStatus is < 0 or > 2)
            {
                return BadRequest("Leveransstatus är ogiltig.");
            }

            var callOff = await _dbContext.CallOffs
                .Include(x => x.CreatedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (callOff is null)
            {
                return NotFound();
            }

            var existingDeliveryList = await _dbContext.CallOffDeliveries
                .Where(x => x.CallOffId == id)
                .OrderBy(x => x.Id)
                .ToListAsync();

            var currentVersion = CalculateVersion(callOff, existingDeliveryList);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(request.Version),
                    Encoding.UTF8.GetBytes(currentVersion)))
            {
                return Conflict(new
                {
                    code = "CALL_OFF_STALE",
                    message = "Avropet har ändrats av en annan användare. Läs in den senaste versionen innan du sparar igen.",
                });
            }

            if (request.ShipperId.HasValue && !await _dbContext.Shippers.AnyAsync(x => x.Id == request.ShipperId.Value))
            {
                return BadRequest("Den valda speditören finns inte.");
            }

            var incomingDeliveryList = request.CallOffDeliveryList ?? new List<SaveCallOffDeliveryDto>();

            var legacyUser = await _legacyUserResolution.ResolveCurrentUserAsync(User);
            if (legacyUser is not null && !callOff.CreatedByUserId.HasValue)
            {
                callOff.CreatedDateTime ??= SwedishTime.Now;
                callOff.CreatedByUserId = legacyUser.Id;
            }

            callOff.ShipperId = request.ShipperId;
            callOff.Reference = request.Reference;
            callOff.Note = request.Note;
            callOff.DeliveryDate = request.DeliveryDate;
            callOff.DeliveryStatus = request.DeliveryStatus;
            callOff.IsSentToShipper = request.IsSentToShipper;
            callOff.DoDebitFreight = request.DoDebitFreight;
            callOff.FreightCostToDebit = request.FreightCostToDebit;
            callOff.CustomerDeliveryAddressId = request.CustomerDeliveryAddressId;

            var syncError = await SyncCallOffDeliveriesAsync(callOff, incomingDeliveryList, existingDeliveryList);
            if (syncError is not null)
            {
                return BadRequest(syncError);
            }

            await _dbContext.SaveChangesAsync();

            var updatedCallOff = await _dbContext.CallOffs
                .AsNoTracking()
                .Include(x => x.CreatedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);

            return Ok(await BuildAggregateDtoAsync(updatedCallOff ?? callOff));
        }

        // Legacy parity: SaveCallOff deletes the removed CallOffDelivery rows (and their underlying stock deliveries)
        // immediately; this endpoint exposes that behaviour so removal does not have to wait for a full save.
        [HttpDelete("{id:int}/deliveries/{callOffDeliveryId:int}")]
        public async Task<IActionResult> DeleteCallOffDelivery(int id, int callOffDeliveryId)
        {
            var callOff = await _dbContext.CallOffs.FirstOrDefaultAsync(x => x.Id == id);
            if (callOff is null)
            {
                return NotFound();
            }

            var delivery = await _dbContext.CallOffDeliveries
                .FirstOrDefaultAsync(x => x.Id == callOffDeliveryId && x.CallOffId == id);
            if (delivery is null)
            {
                return NotFound();
            }

            _dbContext.CallOffDeliveries.Remove(delivery);

            // Legacy parity: SyncCallOffDeliveriesAsync deletes the underlying stock delivery for removed rows.
            if (delivery.DeliveryFromStockId.HasValue)
            {
                var deliveryFromStock = await _dbContext.DeliveryFromStocks
                    .FirstOrDefaultAsync(x => x.Id == delivery.DeliveryFromStockId.Value);
                if (deliveryFromStock is not null)
                {
                    _dbContext.DeliveryFromStocks.Remove(deliveryFromStock);
                }
            }

            await _dbContext.SaveChangesAsync();

            var remainingDeliveries = await _dbContext.CallOffDeliveries
                .Where(x => x.CallOffId == id)
                .OrderBy(x => x.Id)
                .ToListAsync();

            return Ok(new { version = CalculateVersion(callOff, remainingDeliveries) });
        }

        // Legacy parity: CreateDeliveryFromStockViewModel.LoadDeliveryList groups stock balance by EditionPerPallet.
        [HttpGet("delivery-candidates")]
        public async Task<ActionResult<CallOffDeliveryCandidateResponseDto>> GetDeliveryCandidates([FromQuery] string? customerOrderNr)
        {
            if (string.IsNullOrWhiteSpace(customerOrderNr))
            {
                return BadRequest("Ordernummer krävs.");
            }

            var customerOrder = await _dbContext.CustomerOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CustomerOrderNr == customerOrderNr);

            if (customerOrder is null)
            {
                return NotFound();
            }

            var (toStockRows, fromStockRows) = await LoadStockBalanceRowsAsync(customerOrder.Id, customerOrder.SupplierOrderId);
            var today = SwedishTime.Today;

            var editionPerPalletKeys = toStockRows.Select(x => x.EditionPerPallet)
                .Concat(fromStockRows.Select(x => x.EditionPerPallet))
                .Distinct()
                .ToList();

            // Legacy parity: AddDeliveryCommand copies pallet dimensions/format/inventory from the matching stock-in template.
            var templatesByEditionPerPallet = customerOrder.SupplierOrderId.HasValue
                ? (await _dbContext.DeliveryToStocks
                    .AsNoTracking()
                    .Where(x => x.SupplierOrderId == customerOrder.SupplierOrderId.Value && editionPerPalletKeys.Contains(x.EditionPerPallet))
                    .GroupBy(x => x.EditionPerPallet)
                    .Select(g => g.OrderByDescending(x => x.Id).First())
                    .ToListAsync())
                    .ToDictionary(x => x.EditionPerPallet ?? int.MinValue)
                : new Dictionary<int, DeliveryToStock>();

            var templateInventoryIds = templatesByEditionPerPallet.Values
                .Where(x => x.InventoryId.HasValue)
                .Select(x => x.InventoryId!.Value)
                .Distinct()
                .ToList();
            var inventoryNameById = await _dbContext.Inventories
                .AsNoTracking()
                .Where(x => templateInventoryIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name ?? string.Empty);

            var groups = editionPerPalletKeys
                .Select(editionPerPallet =>
                {
                    var toStockForGroup = toStockRows.Where(x => x.EditionPerPallet == editionPerPallet).ToList();
                    var fromStockForGroup = fromStockRows.Where(x => x.EditionPerPallet == editionPerPallet).ToList();
                    var template = templatesByEditionPerPallet.GetValueOrDefault(editionPerPallet ?? int.MinValue);

                    return new CallOffDeliveryCandidateGroupDto
                    {
                        EditionPerPallet = editionPerPallet,
                        NrOf = toStockForGroup.Sum(x => x.NrOfItems ?? 0) - fromStockForGroup.Sum(x => x.NrOfItems ?? 0),
                        NrOfPallets = toStockForGroup.Sum(x => x.NrOfPallets ?? 0) - fromStockForGroup.Sum(x => x.NrOfPallets ?? 0),
                        NrOfNow = toStockForGroup.Where(x => x.DeliveryStatus == 2 && x.DeliveryDate <= today).Sum(x => x.NrOfItems ?? 0)
                            - fromStockForGroup.Where(x => x.DeliveryStatus == 2 && x.DeliveryDate <= today).Sum(x => x.NrOfItems ?? 0),
                        NrOfPalletsNow = toStockForGroup.Where(x => x.DeliveryStatus == 2 && x.DeliveryDate <= today).Sum(x => x.NrOfPallets ?? 0)
                            - fromStockForGroup.Where(x => x.DeliveryStatus == 2 && x.DeliveryDate <= today).Sum(x => x.NrOfPallets ?? 0),
                        InventoryId = template?.InventoryId ?? (customerOrder.SupplierOrderId.HasValue ? customerOrder.SupplierOrder?.InventoryId : null),
                        InventoryName = template?.InventoryId.HasValue == true && inventoryNameById.TryGetValue(template.InventoryId.Value, out var inventoryName)
                            ? inventoryName
                            : string.Empty,
                        PalletLength = template?.PalletLength,
                        PalletWidth = template?.PalletWidth,
                        PalletHeight = template?.PalletHeight,
                        PalletIsStackable = template?.PalletIsStackable ?? false,
                        PalletCalcFactor = template?.PalletCalcFactor,
                    };
                })
                .Where(x => x.NrOfPallets > 0 || x.NrOf > 0)
                .OrderBy(x => x.EditionPerPallet)
                .ToList();

            return Ok(new CallOffDeliveryCandidateResponseDto
            {
                CustomerOrderId = customerOrder.Id,
                CustomerOrderNr = customerOrder.CustomerOrderNr ?? string.Empty,
                CustomerName = customerOrder.CustomerName ?? string.Empty,
                ProductName = customerOrder.Product ?? string.Empty,
                OrderedQty = customerOrder.Edition,
                Groups = groups,
            });
        }

        // Legacy parity: CallOffViewModel.AddDeliveryCommand creates the DeliveryFromStock immediately; the CallOffDelivery
        // link row is only persisted when the call-off itself is saved (see SyncCallOffDeliveriesAsync).
        [HttpPost("deliveries")]
        public async Task<ActionResult<CallOffDeliveryRowDto>> CreateDeliveryFromStock([FromBody] AddCallOffDeliveryFromStockRequest request)
        {
            if (request is null)
            {
                return BadRequest("Ingen data att spara.");
            }

            var customerOrder = await _dbContext.CustomerOrders
                .Include(x => x.SupplierOrder)
                .FirstOrDefaultAsync(x => x.Id == request.CustomerOrderId);

            if (customerOrder is null)
            {
                return BadRequest("Ordern kunde inte hittas.");
            }

            var (toStockRows, fromStockRows) = await LoadStockBalanceRowsAsync(customerOrder.Id, customerOrder.SupplierOrderId, request.EditionPerPallet);

            var balanceNrOfItems = toStockRows.Sum(x => x.NrOfItems ?? 0) - fromStockRows.Sum(x => x.NrOfItems ?? 0);
            var balanceNrOfPallets = toStockRows.Sum(x => x.NrOfPallets ?? 0) - fromStockRows.Sum(x => x.NrOfPallets ?? 0);

            // Legacy parity: copy pallet dimensions/format from the matching stock-in delivery template.
            var template = customerOrder.SupplierOrderId.HasValue
                ? await _dbContext.DeliveryToStocks
                    .AsNoTracking()
                    .Where(x => x.SupplierOrderId == customerOrder.SupplierOrderId.Value && x.EditionPerPallet == request.EditionPerPallet)
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefaultAsync()
                : null;

            var newDelivery = new DeliveryFromStock
            {
                CustomerOrderId = customerOrder.Id,
                DeliveryDate = request.DeliveryDate,
                NrOfItems = request.NrOfItems ?? balanceNrOfItems,
                NrOfPallets = request.NrOfPallets ?? balanceNrOfPallets,
                // Legacy parity: DeliveryViewModel.DoSave sets DeliveryStatus 2 (levererad) or 1 (planerad), never 0 for a manually created delivery.
                DeliveryStatus = request.IsDelivered ? 2 : 1,
                CallOff = request.CallOff,
                InventoryId = request.InventoryId ?? template?.InventoryId ?? customerOrder.SupplierOrder?.InventoryId,
                PalletFormatId = template?.PalletFormatId,
                PalletIsStackable = request.PalletIsStackable,
                PalletWidth = request.PalletWidth ?? template?.PalletWidth,
                PalletHeight = request.PalletHeight ?? template?.PalletHeight,
                PalletLength = request.PalletLength ?? template?.PalletLength,
                EditionPerPallet = request.EditionPerPallet,
                PalletCalcFactor = request.PalletCalcFactor ?? template?.PalletCalcFactor,
                IsSlattPallet = false,
            };

            _dbContext.DeliveryFromStocks.Add(newDelivery);
            await _dbContext.SaveChangesAsync();

            return Ok(await BuildUnlinkedDeliveryRowDtoAsync(newDelivery, customerOrder));
        }

        // Legacy parity: CallOffViewModel.OnClosedAsync deletes deliveries that were added but never saved as a CallOffDelivery.
        [HttpDelete("deliveries/{deliveryFromStockId:int}")]
        public async Task<IActionResult> DeleteUnlinkedDeliveryFromStock(int deliveryFromStockId)
        {
            var delivery = await _dbContext.DeliveryFromStocks.FirstOrDefaultAsync(x => x.Id == deliveryFromStockId);
            if (delivery is null)
            {
                return NotFound();
            }

            if (await _dbContext.CallOffDeliveries.AnyAsync(x => x.DeliveryFromStockId == deliveryFromStockId))
            {
                return BadRequest("Leveransen är kopplad till ett avrop och kan inte raderas härifrån.");
            }

            _dbContext.DeliveryFromStocks.Remove(delivery);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        // View mode: read the details of an already created DeliveryFromStock so the Calloff page can show it.
        [HttpGet("deliveries/fromstock/{deliveryFromStockId:int}")]
        public async Task<ActionResult<DeliveryFromStockDetailsDto>> GetDeliveryFromStockDetails(int deliveryFromStockId)
        {
            var delivery = await _dbContext.DeliveryFromStocks
                .AsNoTracking()
                .Include(x => x.CustomerOrder)
                .Include(x => x.Inventory)
                .FirstOrDefaultAsync(x => x.Id == deliveryFromStockId);

            if (delivery is null)
            {
                return NotFound();
            }

            return Ok(new DeliveryFromStockDetailsDto
            {
                Id = delivery.Id,
                CustomerOrderId = delivery.CustomerOrderId,
                CustomerOrderNr = delivery.CustomerOrder?.CustomerOrderNr ?? string.Empty,
                ProductName = delivery.CustomerOrder?.Product ?? string.Empty,
                OrderedQty = delivery.CustomerOrder?.Edition,
                EditionPerPallet = delivery.EditionPerPallet,
                NrOf = delivery.NrOfItems.HasValue ? (int?)Convert.ToInt32(delivery.NrOfItems.Value) : null,
                NrOfPallets = delivery.NrOfPallets,
                DeliveryDate = delivery.DeliveryDate,
                NrOfItems = delivery.NrOfItems,
                CallOff = delivery.CallOff,
                InventoryId = delivery.InventoryId,
                InventoryName = delivery.Inventory?.Name ?? string.Empty,
                PalletLength = delivery.PalletLength,
                PalletWidth = delivery.PalletWidth,
                PalletHeight = delivery.PalletHeight,
                PalletIsStackable = delivery.PalletIsStackable,
                PalletCalcFactor = delivery.PalletCalcFactor,
                // Legacy parity: DeliveryStatus 2 means the delivery has been delivered.
                IsDelivered = delivery.DeliveryStatus == 2,
            });
        }

        private async Task<CallOffDeliveryRowDto> BuildUnlinkedDeliveryRowDtoAsync(DeliveryFromStock delivery, CustomerOrder customerOrder)
        {
            var inventoryName = delivery.InventoryId.HasValue
                ? await _dbContext.Inventories.AsNoTracking().Where(x => x.Id == delivery.InventoryId.Value).Select(x => x.Name).FirstOrDefaultAsync()
                : null;
            var palletFormatName = customerOrder.PalletFormatId.HasValue
                ? await _dbContext.PalletFormats.AsNoTracking().Where(x => x.Id == customerOrder.PalletFormatId.Value).Select(x => x.Name).FirstOrDefaultAsync()
                : null;

            return new CallOffDeliveryRowDto
            {
                Id = 0,
                DeliveryFromStockId = delivery.Id,
                SortOrder = 0,
                Note = null,
                DeliveryAddressFreeText = null,
                NrOfPalletPlaces = null,
                CustomerOrderNr = customerOrder.CustomerOrderNr ?? string.Empty,
                CustomerName = customerOrder.CustomerName ?? string.Empty,
                ProductName = customerOrder.Product ?? string.Empty,
                InventoryName = inventoryName ?? string.Empty,
                NrOfPallets = delivery.NrOfPallets ?? 0,
                PalletFormatName = palletFormatName ?? string.Empty,
                KolliFormat = $"{delivery.PalletLength ?? 0} x {delivery.PalletWidth ?? 0} x {delivery.PalletHeight ?? 0}",
            };
        }

        // Legacy parity: CallOffViewModel.SaveCallOff reconciles CallOffDeliveryList against the DB, updating existing
        // rows, removing deleted ones, and inserting rows for deliveries added (but not yet linked) since the last save.
        private async Task<string?> SyncCallOffDeliveriesAsync(CallOff callOff, List<SaveCallOffDeliveryDto> incomingDeliveryList, List<CallOffDelivery> existingDeliveryList)
        {
            var existingDeliveryById = existingDeliveryList.ToDictionary(x => x.Id);
            var incomingExistingItems = incomingDeliveryList.Where(x => x.Id != 0).ToList();
            var incomingNewItems = incomingDeliveryList.Where(x => x.Id == 0).ToList();

            if (incomingExistingItems.Any(x => !existingDeliveryById.ContainsKey(x.Id)))
            {
                return "En eller flera avropsleveranser kunde inte hittas på avropet.";
            }

            if (incomingNewItems.Any(x => !x.DeliveryFromStockId.HasValue))
            {
                return "En ny avropsleverans saknar koppling till en lagerleverans.";
            }

            var newDeliveryFromStockIds = incomingNewItems.Select(x => x.DeliveryFromStockId!.Value).ToList();
            if (newDeliveryFromStockIds.Count > 0)
            {
                var alreadyLinkedIds = await _dbContext.CallOffDeliveries
                    .Where(x => x.DeliveryFromStockId.HasValue && newDeliveryFromStockIds.Contains(x.DeliveryFromStockId.Value))
                    .Select(x => x.DeliveryFromStockId!.Value)
                    .ToListAsync();
                if (alreadyLinkedIds.Count > 0)
                {
                    return "En eller flera leveranser är redan kopplade till ett avrop.";
                }

                var validDeliveryFromStockIds = await _dbContext.DeliveryFromStocks
                    .Where(x => newDeliveryFromStockIds.Contains(x.Id))
                    .Select(x => x.Id)
                    .ToListAsync();
                if (newDeliveryFromStockIds.Except(validDeliveryFromStockIds).Any())
                {
                    return "En eller flera lagerleveranser kunde inte hittas.";
                }
            }

            var incomingExistingIdSet = incomingExistingItems.Select(x => x.Id).ToHashSet();
            var deliveriesToRemove = existingDeliveryList
                .Where(x => !incomingExistingIdSet.Contains(x.Id))
                .ToList();
            _dbContext.CallOffDeliveries.RemoveRange(deliveriesToRemove);

            // Legacy parity: SaveCallOff deletes the underlying stock delivery for rows removed from CallOffDeliveryList.
            var removedDeliveryFromStockIds = deliveriesToRemove
                .Where(x => x.DeliveryFromStockId.HasValue)
                .Select(x => x.DeliveryFromStockId!.Value)
                .ToList();
            if (removedDeliveryFromStockIds.Count > 0)
            {
                var deliveriesFromStockToRemove = await _dbContext.DeliveryFromStocks
                    .Where(x => removedDeliveryFromStockIds.Contains(x.Id))
                    .ToListAsync();
                _dbContext.DeliveryFromStocks.RemoveRange(deliveriesFromStockToRemove);
            }

            foreach (var item in incomingExistingItems)
            {
                var existingDelivery = existingDeliveryById[item.Id];
                existingDelivery.Note = item.Note;
                existingDelivery.NrOfPalletPlaces = item.NrOfPalletPlaces;
            }

            var nextSortOrder = existingDeliveryList.Select(x => x.SortOrder ?? 0).DefaultIfEmpty(0).Max() + 1;
            foreach (var item in incomingNewItems)
            {
                _dbContext.CallOffDeliveries.Add(new CallOffDelivery
                {
                    CallOffId = callOff.Id,
                    DeliveryFromStockId = item.DeliveryFromStockId,
                    SortOrder = nextSortOrder++,
                    Note = item.Note,
                    NrOfPalletPlaces = item.NrOfPalletPlaces,
                });
            }

            return null;
        }

        private async Task<(List<StockBalanceRow> ToStockRows, List<StockBalanceRow> FromStockRows)> LoadStockBalanceRowsAsync(
            int customerOrderId, int? supplierOrderId, int? editionPerPalletFilter = null)
        {
            List<StockBalanceRow> toStockRows;
            if (supplierOrderId.HasValue)
            {
                var toStockQuery = _dbContext.DeliveryToStocks.AsNoTracking().Where(x => x.SupplierOrderId == supplierOrderId.Value);
                if (editionPerPalletFilter.HasValue)
                {
                    toStockQuery = toStockQuery.Where(x => x.EditionPerPallet == editionPerPalletFilter.Value);
                }

                toStockRows = await toStockQuery
                    .Select(x => new StockBalanceRow(x.EditionPerPallet, x.NrOfItems, x.NrOfPallets, x.DeliveryStatus, x.DeliveryDate))
                    .ToListAsync();
            }
            else
            {
                toStockRows = new List<StockBalanceRow>();
            }

            var fromStockQuery = _dbContext.DeliveryFromStocks.AsNoTracking().Where(x => x.CustomerOrderId == customerOrderId);
            if (editionPerPalletFilter.HasValue)
            {
                fromStockQuery = fromStockQuery.Where(x => x.EditionPerPallet == editionPerPalletFilter.Value);
            }

            var fromStockRows = await fromStockQuery
                .Select(x => new StockBalanceRow(x.EditionPerPallet, x.NrOfItems, x.NrOfPallets, x.DeliveryStatus, x.DeliveryDate))
                .ToListAsync();

            return (toStockRows, fromStockRows);
        }

        private sealed record StockBalanceRow(int? EditionPerPallet, double? NrOfItems, int? NrOfPallets, int? DeliveryStatus, DateTime? DeliveryDate);

        private async Task<CallOffAggregateDto> BuildAggregateDtoAsync(CallOff callOff)
        {
            var deliveryRows = await _dbContext.CallOffDeliveries
                .AsNoTracking()
                .Where(x => x.CallOffId == callOff.Id)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    SortOrder = x.SortOrder ?? 0,
                    x.DeliveryFromStockId,
                    x.Note,
                    x.DeliveryAddressFreeText,
                    x.NrOfPalletPlaces,
                    x.DeliveryFromStock,
                })
                .ToListAsync();

            var customerOrderIds = deliveryRows
                .Where(x => x.DeliveryFromStock != null && x.DeliveryFromStock.CustomerOrderId.HasValue)
                .Select(x => x.DeliveryFromStock!.CustomerOrderId!.Value)
                .Distinct()
                .ToList();

            var customerOrderById = await _dbContext.CustomerOrders
                .AsNoTracking()
                .Where(x => customerOrderIds.Contains(x.Id))
                .Select(x => new
                {
                    x.Id,
                    x.CustomerId,
                    x.CustomerOrderNr,
                    x.CustomerName,
                    x.Product,
                    x.PalletFormatId,
                })
                .ToDictionaryAsync(x => x.Id);

            var inventoryIds = deliveryRows
                .Where(x => x.DeliveryFromStock != null && x.DeliveryFromStock.InventoryId.HasValue)
                .Select(x => x.DeliveryFromStock!.InventoryId!.Value)
                .Distinct()
                .ToList();
            var inventoryNameById = await _dbContext.Inventories
                .AsNoTracking()
                .Where(x => inventoryIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name ?? string.Empty);

            var palletFormatIds = customerOrderById.Values
                .Where(x => x.PalletFormatId.HasValue)
                .Select(x => x.PalletFormatId!.Value)
                .Distinct()
                .ToList();
            var palletFormatNameById = await _dbContext.PalletFormats
                .AsNoTracking()
                .Where(x => palletFormatIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name ?? string.Empty);

            var deliveryList = deliveryRows
                .Select(x =>
                {
                    var customerOrder = x.DeliveryFromStock?.CustomerOrderId.HasValue == true
                        && customerOrderById.TryGetValue(x.DeliveryFromStock.CustomerOrderId.Value, out var foundCustomerOrder)
                        ? foundCustomerOrder
                        : null;

                    return new CallOffDeliveryRowDto
                    {
                        Id = x.Id,
                        DeliveryFromStockId = x.DeliveryFromStockId,
                        SortOrder = x.SortOrder,
                        Note = x.Note,
                        DeliveryAddressFreeText = x.DeliveryAddressFreeText,
                        NrOfPalletPlaces = x.NrOfPalletPlaces,
                        CustomerOrderNr = customerOrder?.CustomerOrderNr ?? string.Empty,
                        CustomerName = customerOrder?.CustomerName ?? string.Empty,
                        ProductName = customerOrder?.Product ?? string.Empty,
                        InventoryName = x.DeliveryFromStock?.InventoryId.HasValue == true
                            && inventoryNameById.TryGetValue(x.DeliveryFromStock.InventoryId.Value, out var inventoryName)
                            ? inventoryName
                            : string.Empty,
                        NrOfPallets = x.DeliveryFromStock?.NrOfPallets ?? 0,
                        PalletFormatName = customerOrder?.PalletFormatId.HasValue == true
                            && palletFormatNameById.TryGetValue(customerOrder.PalletFormatId.Value, out var palletFormatName)
                            ? palletFormatName
                            : string.Empty,
                        // Legacy parity: HelppropKolliFormat = PalletLength x PalletWidth x PalletHeight from the linked delivery.
                        KolliFormat = x.DeliveryFromStock != null
                            ? $"{x.DeliveryFromStock.PalletLength ?? 0} x {x.DeliveryFromStock.PalletWidth ?? 0} x {x.DeliveryFromStock.PalletHeight ?? 0}"
                            : string.Empty,
                    };
                })
                .ToList();

            // Legacy parity: LoadDeliveryAddressList loads delivery addresses for the customer of the linked deliveries.
            var deliveryCustomerId = customerOrderById.Values.Select(x => x.CustomerId).FirstOrDefault(x => x.HasValue);
            var deliveryAddressOptions = deliveryCustomerId.HasValue
                ? await _dbContext.CustomerDeliveryAddresses
                    .AsNoTracking()
                    .Where(x => x.CustomerId == deliveryCustomerId.Value)
                    .OrderBy(x => x.Name)
                    .Select(x => new CallOffDeliveryAddressOptionDto
                    {
                        Id = x.Id,
                        Name = x.Name ?? string.Empty,
                        Street = x.Address ?? string.Empty,
                        PostalNr = x.PostalNr ?? string.Empty,
                        City = x.PostalAddress ?? string.Empty,
                    })
                    .ToListAsync()
                : new List<CallOffDeliveryAddressOptionDto>();

            var existingDeliveryEntities = await _dbContext.CallOffDeliveries
                .AsNoTracking()
                .Where(x => x.CallOffId == callOff.Id)
                .OrderBy(x => x.Id)
                .ToListAsync();

            return new CallOffAggregateDto
            {
                CallOff = MapToDto(callOff),
                CallOffDeliveryList = deliveryList,
                DeliveryAddressOptions = deliveryAddressOptions,
                DeliveryLegs = await BuildDeliveryLegsAsync(callOff.Id),
                Version = CalculateVersion(callOff, existingDeliveryEntities),
            };
        }

        private async Task<TransportOrderDeliveryLegsResponseDto> BuildDeliveryLegsAsync(int callOffId)
        {
            var deliveryLegList = await _dbContext.DeliveryLegs
                .AsNoTracking()
                .Where(x => x.CallOffId == callOffId)
                .OrderBy(x => x.SortOrder)
                .Select(x => new TransportOrderDeliveryLegDto
                {
                    Id = x.Id,
                    CallOffId = x.CallOffId,
                    DeliveryToStockId = x.DeliveryToStockId,
                    DeliveryToCustomerId = x.DeliveryToCustomerId,
                    DeliveryFromStockId = x.DeliveryFromStockId,
                    FromPositionId = x.FromPositionId,
                    ToPositionId = x.ToPositionId,
                    LegFromPositionId = x.FromPositionIdLeg,
                    LegToPositionId = x.ToPositionIdLeg,
                    PositionDistanceId = x.PositionDistanceId,
                    DistanceKm = x.DistanceKm,
                    TypeOfTransport = x.TypeOfTransport,
                    SortOrder = x.SortOrder,
                    LatitudeStart = x.LatitudeStart,
                    LongitudeStart = x.LongitudeStart,
                    LatitudeEnd = x.LatitudeEnd,
                    LongitudeEnd = x.LongitudeEnd,
                    HelppropFromPositionName = x.FromPosition != null ? (x.FromPosition.Name ?? string.Empty) : string.Empty,
                    HelppropFromPositionPostalAddress = x.FromPosition != null ? (x.FromPosition.PostalAddress ?? string.Empty) : string.Empty,
                    HelppropToPositionName = x.ToPosition != null ? (x.ToPosition.Name ?? string.Empty) : string.Empty,
                    HelppropToPositionPostalAddress = x.ToPosition != null ? (x.ToPosition.PostalAddress ?? string.Empty) : string.Empty,
                    HelppropLegFromPositionName = x.LegFromPosition != null ? (x.LegFromPosition.Name ?? string.Empty) : string.Empty,
                    HelppropLegFromPositionPostalAddress = x.LegFromPosition != null ? (x.LegFromPosition.PostalAddress ?? string.Empty) : string.Empty,
                    HelppropLegToPositionName = x.LegToPosition != null ? (x.LegToPosition.Name ?? string.Empty) : string.Empty,
                    HelppropLegToPositionPostalAddress = x.LegToPosition != null ? (x.LegToPosition.PostalAddress ?? string.Empty) : string.Empty,
                })
                .ToListAsync();

            var deliveryLegGroupedList = deliveryLegList
                .Where(x => !IsDistributionLeg(x))
                .GroupBy(x => new { x.SortOrder, x.LatitudeStart, x.LongitudeStart })
                .OrderBy(x => x.Key.SortOrder)
                .Select(x => x.First())
                .ToList();

            var distributionDeliveryLegGroupedList = deliveryLegList
                .Where(IsDistributionLeg)
                .GroupBy(x => new { x.SortOrder, x.LatitudeStart, x.LongitudeStart })
                .OrderBy(x => x.Key.SortOrder)
                .Select(x => x.First())
                .ToList();

            var distributionLegList = deliveryLegList
                .Where(IsDistributionLeg)
                .GroupBy(x => new { x.DeliveryToCustomerId, x.DeliveryToStockId })
                .Select(x => x.OrderBy(y => y.SortOrder).ToList())
                .ToList();

            return new TransportOrderDeliveryLegsResponseDto
            {
                DeliveryLegGroupedList = deliveryLegGroupedList,
                DistributionDeliveryLegGroupedList = distributionDeliveryLegGroupedList,
                DistributionLegList = distributionLegList,
            };
        }

        private static bool IsDistributionLeg(TransportOrderDeliveryLegDto deliveryLeg)
        {
            return !string.IsNullOrWhiteSpace(deliveryLeg.TypeOfTransport)
                && deliveryLeg.TypeOfTransport.Contains("SPEDITION", StringComparison.OrdinalIgnoreCase);
        }

        private static CallOffDto MapToDto(CallOff callOff)
        {
            return new CallOffDto
            {
                Id = callOff.Id,
                ShipperId = callOff.ShipperId,
                Reference = callOff.Reference,
                Note = callOff.Note,
                CreatedDateTime = callOff.CreatedDateTime,
                DeliveryDate = callOff.DeliveryDate,
                DeliveryStatus = callOff.DeliveryStatus,
                IsSentToShipper = callOff.IsSentToShipper,
                DoDebitFreight = callOff.DoDebitFreight,
                FreightCostToDebit = callOff.FreightCostToDebit,
                CustomerDeliveryAddressId = callOff.CustomerDeliveryAddressId,
                CreatedByUserId = callOff.CreatedByUserId,
                CreatedByUserName = callOff.CreatedByUser?.Name,
            };
        }

        private static string CalculateVersion(CallOff callOff, IEnumerable<CallOffDelivery> deliveryList)
        {
            var values = new List<string>
            {
                callOff.Id.ToString(CultureInfo.InvariantCulture),
                callOff.ShipperId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                callOff.Reference ?? string.Empty,
                callOff.Note ?? string.Empty,
                callOff.CreatedDateTime?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                callOff.DeliveryDate?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                callOff.DeliveryStatus?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                callOff.IsSentToShipper.ToString(CultureInfo.InvariantCulture),
                callOff.DoDebitFreight.ToString(CultureInfo.InvariantCulture),
                callOff.FreightCostToDebit?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                callOff.CustomerDeliveryAddressId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                callOff.CreatedByUserId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            };

            values.AddRange(deliveryList
                .OrderBy(x => x.Id)
                .Select(x => string.Join("|", new[]
                {
                    x.Id.ToString(CultureInfo.InvariantCulture),
                    x.Note ?? string.Empty,
                    x.NrOfPalletPlaces?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                })));

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", values))));
        }
    }
}
