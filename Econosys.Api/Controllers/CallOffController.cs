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
            var existingDeliveryById = existingDeliveryList.ToDictionary(x => x.Id);
            if (incomingDeliveryList.Any(x => !existingDeliveryById.ContainsKey(x.Id)))
            {
                return BadRequest("En eller flera avropsleveranser kunde inte hittas på avropet.");
            }

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

            var incomingDeliveryIdSet = incomingDeliveryList.Select(x => x.Id).ToHashSet();
            var deliveriesToRemove = existingDeliveryList
                .Where(x => !incomingDeliveryIdSet.Contains(x.Id))
                .ToList();
            _dbContext.CallOffDeliveries.RemoveRange(deliveriesToRemove);

            foreach (var item in incomingDeliveryList)
            {
                var existingDelivery = existingDeliveryById[item.Id];
                existingDelivery.Note = item.Note;
                existingDelivery.NrOfPalletPlaces = item.NrOfPalletPlaces;
            }

            await _dbContext.SaveChangesAsync();

            var updatedCallOff = await _dbContext.CallOffs
                .AsNoTracking()
                .Include(x => x.CreatedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);

            return Ok(await BuildAggregateDtoAsync(updatedCallOff ?? callOff));
        }

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
