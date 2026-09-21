using Econosys.Api.Common;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class TransportOrderController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILegacyUserResolutionService _legacyUserResolution;
        private readonly ITransportOrderCostCalculationService _costCalculationService;

        public TransportOrderController(ApplicationDbContext dbContext, ILegacyUserResolutionService legacyUserResolution, ITransportOrderCostCalculationService costCalculationService)
        {
            _dbContext = dbContext;
            _legacyUserResolution = legacyUserResolution;
            _costCalculationService = costCalculationService;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<TransportOrderDto>> GetById(int id)
        {
            var transportOrder = await _dbContext.TransportOrders
                .AsNoTracking()
                .Include(x => x.CreatedByUser)
                .Include(x => x.EditedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (transportOrder is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(transportOrder));
        }

        [HttpGet("{id:int}/aggregate")]
        public async Task<ActionResult<TransportOrderAggregateDto>> GetAggregateById(int id)
        {
            var transportOrder = await _dbContext.TransportOrders
                .AsNoTracking()
                .Include(x => x.CreatedByUser)
                .Include(x => x.EditedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (transportOrder is null)
            {
                return NotFound();
            }

            return Ok(await BuildAggregateDtoAsync(transportOrder));
        }

        [HttpGet("{id:int}/delivery-legs")]
        public async Task<ActionResult<TransportOrderDeliveryLegsResponseDto>> GetDeliveryLegsByTransportOrderId(int id)
        {
            var transportOrderExists = await _dbContext.TransportOrders
                .AsNoTracking()
                .AnyAsync(x => x.Id == id);

            if (!transportOrderExists)
            {
                return NotFound();
            }

            var deliveryLegList = await _dbContext.DeliveryLegs
                .AsNoTracking()
                .Where(x => x.TransportOrderId == id)
                .OrderBy(x => x.SortOrder)
                .Select(x => new TransportOrderDeliveryLegDto
                {
                    Id = x.Id,
                    TransportOrderId = x.TransportOrderId,
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

            return Ok(new TransportOrderDeliveryLegsResponseDto
            {
                DeliveryLegGroupedList = deliveryLegGroupedList,
                DistributionDeliveryLegGroupedList = distributionDeliveryLegGroupedList,
                DistributionLegList = distributionLegList,
            });
        }

        [HttpGet("{id:int}/delivery-overview")]
        public async Task<ActionResult<TransportOrderDeliveryOverviewDto>> GetDeliveryOverviewByTransportOrderId(int id)
        {
            var transportOrderExists = await _dbContext.TransportOrders
                .AsNoTracking()
                .AnyAsync(x => x.Id == id);

            if (!transportOrderExists)
            {
                return NotFound();
            }

            // Legacy WPF colors are ARGB (#AARRGGBB); CSS 8-digit hex is RGBA (#RRGGBBAA), so the alpha
            // channel is reordered here to keep the same visible colors as the WPF client.
            var colorArray = new[]
            {
                ("#2F4E7388", "#2F4E73"),
                ("#BAABCF88", "#BAABCF"),
                ("#F8B50088", "#F8B500"),
                ("#FC3C3C88", "#FC3C3C"),
                ("#00ADB588", "#00ADB5"),
                ("#00B25188", "#00B251"),
                ("#8C5B3F88", "#8C5B3F"),
                ("#DA941E88", "#DA941E"),
                ("#1DA1BF99", "#136C80"),
                ("#BFB1AA88", "#908580"),
                ("#D982FF", "#A764C4"),
                ("#82B2FF", "#5B7DB3"),
                ("#5BB3A6", "#418077"),
                ("#E88376", "#B5675C"),
                ("#807741", "#59532E"),
                ("#48D9AB", "#38AB87"),
                ("#C2E89E", "#91AD76"),
                ("#F8DED6", "#CCB6AF"),
                ("#D2ECF9", "#BCD4E0"),
                ("#B0D9B4", "#A9D1AE"),
            };

            var deliveryRows = await _dbContext.TransportOrderDeliveries
                .AsNoTracking()
                .Where(x => x.TransportOrderId == id)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    SortOrder = x.SortOrder ?? 0,
                    ParentDeliveryId = x.DeliveryToCustomer != null ? x.DeliveryToCustomer.ParentDeliveryId : null,
                    x.SupplierOrderId,
                    x.DeliveryAddressFreeText,
                    x.Note,
                    x.CustomerYourOrderNr,
                    x.ForcedOmlastStatus,
                    x.OmlastInventoryId,
                    x.OmlastInventory,
                    TransportOrderDelivery = x,
                    SupplierOrder = x.SupplierOrder,
                    DeliveryToStock = x.DeliveryToStock,
                    DeliveryToCustomer = x.DeliveryToCustomer,
                    DeliveryFromStock = x.DeliveryFromStock,
                    SupplierFactoryId = x.SupplierOrder != null ? x.SupplierOrder.SupplierFactoryId : null,
                })
                .ToListAsync();

            var supplierFactoryIds = deliveryRows
                .Where(x => x.SupplierFactoryId.HasValue)
                .Select(x => x.SupplierFactoryId!.Value)
                .Distinct()
                .ToList();

            var supplierFactoryNameById = await _dbContext.SupplierFactories
                .AsNoTracking()
                .Where(x => supplierFactoryIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name ?? string.Empty);

            var supplierFactoryList = deliveryRows
                .Select(x => new TransportOrderSupplierFactoryRowDto
                {
                    SupplierName = x.SupplierOrder?.SupplierName ?? string.Empty,
                    FactoryName = x.SupplierFactoryId.HasValue && supplierFactoryNameById.TryGetValue(x.SupplierFactoryId.Value, out var factoryName)
                        ? factoryName
                        : string.Empty,
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.SupplierName) || !string.IsNullOrWhiteSpace(x.FactoryName))
                .DistinctBy(x => $"{x.SupplierName}|{x.FactoryName}")
                .ToList();

            var cardList = new List<TransportOrderDeliveryCardDto>();
            var colorIndex = 0;

            foreach (var row in deliveryRows)
            {
                var forcedStatus = row.ForcedOmlastStatus ?? 0;
                var isSlattPallet = row.DeliveryToStock?.IsSlattPallet == true
                    || row.DeliveryToCustomer?.IsSlattPallet == true
                    || row.DeliveryFromStock?.IsSlattPallet == true;

                var numberOfPallets = row.DeliveryToStock?.NrOfPallets
                    ?? row.DeliveryToCustomer?.NrOfPallets
                    ?? row.DeliveryFromStock?.NrOfPallets
                    ?? 0;

                var palletIsStackable = row.DeliveryToStock?.PalletIsStackable == true
                    || row.DeliveryToCustomer?.PalletIsStackable == true
                    || row.DeliveryFromStock?.PalletIsStackable == true;

                var confirmedDeliveryDate = row.SupplierOrder?.ConfirmedDeliveryDate.HasValue == true
                    ? row.SupplierOrder!.ConfirmedDeliveryDate!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : string.Empty;

                var customerName = row.DeliveryFromStock?.CustomerOrder?.CustomerName
                    ?? row.DeliveryToCustomer?.CustomerOrder?.CustomerName
                    ?? row.SupplierOrder?.Customer?.Name
                    ?? string.Empty;

                var addressName = row.SupplierOrder?.DeliveryAddressName ?? string.Empty;
                var addressStreet = row.SupplierOrder?.DeliveryAddress ?? string.Empty;
                var addressPostalNr = row.SupplierOrder?.DeliveryPostalNr ?? string.Empty;
                var addressPostalAddress = row.SupplierOrder?.DeliveryPostalAddress ?? string.Empty;

                if (row.DeliveryToStock?.Inventory != null)
                {
                    addressName = row.DeliveryToStock.Inventory.Name ?? addressName;
                    addressStreet = row.DeliveryToStock.Inventory.Address ?? addressStreet;
                    addressPostalNr = row.DeliveryToStock.Inventory.PostalNrText ?? row.DeliveryToStock.Inventory.PostalNr?.ToString() ?? addressPostalNr;
                    addressPostalAddress = row.DeliveryToStock.Inventory.PostalAddress ?? addressPostalAddress;
                }
                else if (row.DeliveryFromStock?.CustomerOrder != null)
                {
                    addressName = row.DeliveryFromStock.CustomerOrder.DeliveryAddressName ?? addressName;
                    addressStreet = row.DeliveryFromStock.CustomerOrder.DeliveryAddress ?? addressStreet;
                    addressPostalNr = row.DeliveryFromStock.CustomerOrder.DeliveryPostalNr ?? addressPostalNr;
                    addressPostalAddress = row.DeliveryFromStock.CustomerOrder.DeliveryPostalAddress ?? addressPostalAddress;
                }

                if (forcedStatus == 1 && row.OmlastInventory != null)
                {
                    addressName = row.OmlastInventory.Name ?? addressName;
                    addressStreet = row.OmlastInventory.Address ?? addressStreet;
                    addressPostalNr = row.OmlastInventory.PostalNrText ?? row.OmlastInventory.PostalNr?.ToString() ?? addressPostalNr;
                    addressPostalAddress = row.OmlastInventory.PostalAddress ?? addressPostalAddress;
                }

                var customerNameAddress = string.Join(Environment.NewLine, new[]
                {
                    customerName,
                    addressName,
                    addressStreet,
                    string.Join(" ", new[] { addressPostalNr, addressPostalAddress }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim(),
                }.Where(x => !string.IsNullOrWhiteSpace(x)));

                var paletteColors = colorArray[colorIndex % colorArray.Length];
                if (!isSlattPallet)
                {
                    colorIndex += 1;
                }

                cardList.Add(new TransportOrderDeliveryCardDto
                {
                    TransportOrderDelivery = MapToSaveDto(row.TransportOrderDelivery),
                    Id = row.Id,
                    ParentDeliveryId = row.ParentDeliveryId,
                    SupplierOrderId = row.SupplierOrderId ?? 0,
                    SupplierOrderNr = row.SupplierOrder?.SupplierOrderNr ?? string.Empty,
                    SortOrder = row.SortOrder,
                    CustomerNameAddress = customerNameAddress,
                    DeliveryAddressFreeText = row.DeliveryAddressFreeText,
                    Note = row.Note,
                    CustomerYourOrderNr = row.CustomerYourOrderNr,
                    SupplierOrderConfirmedDeliveryDate = confirmedDeliveryDate,
                    ForcedOmlastCaption = "Omlast / direkt",
                    ForcedOmlastStatus = forcedStatus,
                    IsSlattPallet = isSlattPallet,
                    NrOfPallets = numberOfPallets,
                    PalletIsStackable = palletIsStackable,
                    PalletInfo = palletIsStackable
                        ? $"{numberOfPallets}/{Math.Round(numberOfPallets / 2d, 0, MidpointRounding.AwayFromZero)}"
                        : numberOfPallets.ToString(CultureInfo.InvariantCulture),
                    PalletsLeftToPlace = "0 kvar",
                    Background = isSlattPallet ? "#000000" : paletteColors.Item1,
                    BorderBrush = isSlattPallet ? "#000000" : paletteColors.Item2,
                    InfoPopupVisible = !string.IsNullOrWhiteSpace(row.DeliveryFromStock?.CustomerOrder?.LogisticsInfoInternal),
                    LogisticsInfoInternal = row.DeliveryFromStock?.CustomerOrder?.LogisticsInfoInternal ?? string.Empty,
                });
            }

            return Ok(new TransportOrderDeliveryOverviewDto
            {
                SupplierFactoryList = supplierFactoryList,
                TransportOrderDeliveryList = cardList,
            });
        }

        [HttpGet("{id:int}/truck-plan")]
        public async Task<ActionResult<TransportOrderTruckPlanResponseDto>> GetTruckPlanByTransportOrderId(int id)
        {
            var transportOrderExists = await _dbContext.TransportOrders
                .AsNoTracking()
                .AnyAsync(x => x.Id == id);

            if (!transportOrderExists)
            {
                return NotFound();
            }

            var deliveryIds = await _dbContext.TransportOrderDeliveries
                .AsNoTracking()
                .Where(x => x.TransportOrderId == id)
                .Select(x => x.Id)
                .ToListAsync();

            if (deliveryIds.Count == 0)
            {
                return Ok(new TransportOrderTruckPlanResponseDto());
            }

            var palletList = await _dbContext.TransportOrderDeliveryPallets
                .AsNoTracking()
                .Where(x => x.TransportOrderDeliveryId.HasValue && deliveryIds.Contains(x.TransportOrderDeliveryId.Value))
                .OrderBy(x => x.Id)
                .Select(x => new TransportOrderTruckPlanPalletDto
                {
                    Id = x.Id,
                    TransportOrderDeliveryId = x.TransportOrderDeliveryId ?? 0,
                    PosCmX = x.PosCmX,
                    PosCmY = x.PosCmY,
                    Rotation = x.Rotation,
                    Width = x.Width,
                    Height = x.Height,
                })
                .ToListAsync();

            return Ok(new TransportOrderTruckPlanResponseDto
            {
                PalletList = palletList,
            });
        }

        [HttpPut("{id:int}/truck-plan")]
        public async Task<ActionResult<TransportOrderTruckPlanResponseDto>> SaveTruckPlanByTransportOrderId(int id, [FromBody] TransportOrderTruckPlanSaveRequestDto request)
        {
            var transportOrderExists = await _dbContext.TransportOrders
                .AsNoTracking()
                .AnyAsync(x => x.Id == id);

            if (!transportOrderExists)
            {
                return NotFound();
            }

            var deliveryIds = await _dbContext.TransportOrderDeliveries
                .AsNoTracking()
                .Where(x => x.TransportOrderId == id)
                .Select(x => x.Id)
                .ToHashSetAsync();

            var incomingPalletList = request?.PalletList ?? new List<TransportOrderTruckPlanPalletDto>();
            var hasForeignDelivery = incomingPalletList.Any(x => !deliveryIds.Contains(x.TransportOrderDeliveryId));
            if (hasForeignDelivery)
            {
                return BadRequest("En eller flera pallar är kopplade till en leverans som inte tillhör transportordern.");
            }

            var existingPalletList = await _dbContext.TransportOrderDeliveryPallets
                .Where(x => x.TransportOrderDeliveryId.HasValue && deliveryIds.Contains(x.TransportOrderDeliveryId.Value))
                .ToListAsync();

            var existingById = existingPalletList.ToDictionary(x => x.Id);
            var incomingIds = incomingPalletList
                .Where(x => x.Id > 0)
                .Select(x => x.Id)
                .ToHashSet();

            var palletsToDelete = existingPalletList
                .Where(x => !incomingIds.Contains(x.Id))
                .ToList();

            if (palletsToDelete.Count > 0)
            {
                _dbContext.TransportOrderDeliveryPallets.RemoveRange(palletsToDelete);
            }

            foreach (var item in incomingPalletList)
            {
                if (item.Id > 0)
                {
                    if (!existingById.TryGetValue(item.Id, out var existingPallet))
                    {
                        return BadRequest($"Pall med id {item.Id} kunde inte hittas på transportordern.");
                    }

                    existingPallet.TransportOrderDeliveryId = item.TransportOrderDeliveryId;
                    existingPallet.PosCmX = item.PosCmX;
                    existingPallet.PosCmY = item.PosCmY;
                    existingPallet.Rotation = item.Rotation;
                    existingPallet.Width = item.Width;
                    existingPallet.Height = item.Height;
                    continue;
                }

                var newPallet = new TransportOrderDeliveryPallet
                {
                    TransportOrderDeliveryId = item.TransportOrderDeliveryId,
                    PosCmX = item.PosCmX,
                    PosCmY = item.PosCmY,
                    Rotation = item.Rotation,
                    Width = item.Width,
                    Height = item.Height,
                };

                _dbContext.TransportOrderDeliveryPallets.Add(newPallet);
            }

            await _dbContext.SaveChangesAsync();

            var savedPalletList = await _dbContext.TransportOrderDeliveryPallets
                .AsNoTracking()
                .Where(x => x.TransportOrderDeliveryId.HasValue && deliveryIds.Contains(x.TransportOrderDeliveryId.Value))
                .OrderBy(x => x.Id)
                .Select(x => new TransportOrderTruckPlanPalletDto
                {
                    Id = x.Id,
                    TransportOrderDeliveryId = x.TransportOrderDeliveryId ?? 0,
                    PosCmX = x.PosCmX,
                    PosCmY = x.PosCmY,
                    Rotation = x.Rotation,
                    Width = x.Width,
                    Height = x.Height,
                })
                .ToListAsync();

            return Ok(new TransportOrderTruckPlanResponseDto
            {
                PalletList = savedPalletList,
            });
        }

        [HttpPut("{id:int}/aggregate")]
        public async Task<ActionResult<TransportOrderAggregateDto>> SaveAggregateById(int id, [FromBody] SaveTransportOrderAggregateRequest request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Version))
            {
                return BadRequest("En aktuell versionsnyckel krävs för att spara transportordern.");
            }

            if (request.SenderReference?.Length > 255)
            {
                return BadRequest("Referensen får vara högst 255 tecken.");
            }

            if (request.Note?.Length > 1000)
            {
                return BadRequest("Noteringen får vara högst 1000 tecken.");
            }

            if (request.DeliveryStatus is < 0 or > 3)
            {
                return BadRequest("Leveransstatus är ogiltig.");
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var transportOrder = await _dbContext.TransportOrders
                .Include(x => x.CreatedByUser)
                .Include(x => x.EditedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (transportOrder is null)
            {
                return NotFound();
            }

            var deliveryIds = await _dbContext.TransportOrderDeliveries
                .Where(x => x.TransportOrderId == id)
                .Select(x => x.Id)
                .ToHashSetAsync();

            var existingPalletList = await _dbContext.TransportOrderDeliveryPallets
                .Where(x => x.TransportOrderDeliveryId.HasValue && deliveryIds.Contains(x.TransportOrderDeliveryId.Value))
                .OrderBy(x => x.Id)
                .ToListAsync();

            var existingDeliveryList = await _dbContext.TransportOrderDeliveries
                .Where(x => x.TransportOrderId == id)
                .OrderBy(x => x.Id)
                .ToListAsync();

            var currentVersion = CalculateVersion(transportOrder, existingDeliveryList, existingPalletList);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(request.Version),
                    Encoding.UTF8.GetBytes(currentVersion)))
            {
                return Conflict(new
                {
                    code = "TRANSPORT_ORDER_STALE",
                    message = "Transportordern har ändrats av en annan användare. Läs in den senaste versionen innan du sparar igen.",
                });
            }

            if (request.ShipperId.HasValue && !await _dbContext.Shippers.AnyAsync(x => x.Id == request.ShipperId.Value))
            {
                return BadRequest("Den valda transportören finns inte.");
            }

            var incomingDeliveryList = request.TransportOrderDeliveryList ?? new List<TransportOrderDeliverySaveDto>();
            var incomingDeliveryIds = incomingDeliveryList
                .Where(x => x.Id > 0)
                .Select(x => x.Id)
                .ToList();
            if (incomingDeliveryIds.Count != incomingDeliveryIds.Distinct().Count())
            {
                return BadRequest("Samma transportorderleverans kan inte skickas flera gånger.");
            }

            var existingDeliveryById = existingDeliveryList.ToDictionary(x => x.Id);
            if (incomingDeliveryIds.Any(x => !existingDeliveryById.ContainsKey(x)))
            {
                return BadRequest("En eller flera transportorderleveranser kunde inte hittas på transportordern.");
            }

            if (!await HasValidDeliveryReferencesAsync(incomingDeliveryList))
            {
                return BadRequest("En eller flera leveransreferenser är ogiltiga.");
            }

            var incomingPalletList = request.PalletList ?? new List<TransportOrderTruckPlanPalletDto>();
            var activeDeliveryIdSet = incomingDeliveryIds.ToHashSet();
            if (incomingPalletList.Any(x => x.TransportOrderDeliveryId <= 0 || !activeDeliveryIdSet.Contains(x.TransportOrderDeliveryId)))
            {
                return BadRequest("En eller flera pallar är kopplade till en leverans som inte tillhör transportordern.");
            }

            var incomingExistingIds = incomingPalletList
                .Where(x => x.Id > 0)
                .Select(x => x.Id)
                .ToList();
            if (incomingExistingIds.Count != incomingExistingIds.Distinct().Count())
            {
                return BadRequest("Samma pall kan inte skickas flera gånger.");
            }

            var activePalletList = existingPalletList
                .Where(x => x.TransportOrderDeliveryId.HasValue && activeDeliveryIdSet.Contains(x.TransportOrderDeliveryId.Value))
                .ToList();
            var existingById = activePalletList.ToDictionary(x => x.Id);
            if (incomingExistingIds.Any(x => !existingById.ContainsKey(x)))
            {
                return BadRequest("En eller flera pallar kunde inte hittas på transportordern.");
            }

            var legacyUser = await _legacyUserResolution.ResolveCurrentUserAsync(User);
            if (legacyUser is not null)
            {
                if (!transportOrder.CreatedTimeStamp.HasValue || !transportOrder.CreatedByUserId.HasValue)
                {
                    transportOrder.CreatedTimeStamp ??= SwedishTime.Now;
                    transportOrder.CreatedByUserId ??= legacyUser.Id;
                }

                transportOrder.EditedTimeStamp = SwedishTime.Now;
                transportOrder.EditedByUserId = legacyUser.Id;
            }

            var prevDeliveryStatus = transportOrder.DeliveryStatus ?? 0;
            var deliveryStatusError = await ValidateDeliveryStatusTransitionAsync(id, prevDeliveryStatus, request.DeliveryStatus ?? 0);
            if (deliveryStatusError is not null)
            {
                return BadRequest(deliveryStatusError);
            }

            transportOrder.SenderReference = request.SenderReference;
            transportOrder.Note = request.Note;
            transportOrder.DateCreated = request.DateCreated;
            transportOrder.DateLoading = request.DateLoading;
            transportOrder.DateDelivery = request.DateDelivery;
            transportOrder.DeliveryStatus = request.DeliveryStatus;
            transportOrder.ShipperId = request.ShipperId;

            var incomingDeliveryIdSet = incomingDeliveryIds.ToHashSet();
            var deliveriesToRemove = existingDeliveryList
                .Where(x => !incomingDeliveryIdSet.Contains(x.Id))
                .ToList();
            var palletsToRemoveWithDeliveries = existingPalletList
                .Where(x => x.TransportOrderDeliveryId.HasValue && !incomingDeliveryIdSet.Contains(x.TransportOrderDeliveryId.Value))
                .ToList();
            _dbContext.TransportOrderDeliveryPallets.RemoveRange(palletsToRemoveWithDeliveries);
            _dbContext.TransportOrderDeliveries.RemoveRange(deliveriesToRemove);

            var mappedDeliveryList = new List<TransportOrderDelivery>();
            foreach (var item in incomingDeliveryList)
            {
                if (item.Id > 0)
                {
                    var existingDelivery = existingDeliveryById[item.Id];
                    MapToEntity(item, existingDelivery);
                    mappedDeliveryList.Add(existingDelivery);
                    continue;
                }

                var newDelivery = new TransportOrderDelivery { TransportOrderId = id };
                MapToEntity(item, newDelivery);
                _dbContext.TransportOrderDeliveries.Add(newDelivery);
                mappedDeliveryList.Add(newDelivery);
            }

            var deliveryToCustomerParentById = await _dbContext.DeliveryToCustomers
                .Where(x => x.ParentDeliveryId.HasValue)
                .Select(x => new { x.Id, x.ParentDeliveryId })
                .ToDictionaryAsync(x => x.Id, x => x.ParentDeliveryId!.Value);

            var transportDeliveryByCustomerId = mappedDeliveryList
                .Where(x => x.DeliveryToCustomerId.HasValue)
                .GroupBy(x => x.DeliveryToCustomerId!.Value)
                .ToDictionary(x => x.Key, x => x.First());

            foreach (var childDelivery in mappedDeliveryList)
            {
                if (!childDelivery.DeliveryToCustomerId.HasValue
                    || !deliveryToCustomerParentById.TryGetValue(childDelivery.DeliveryToCustomerId.Value, out var parentDeliveryId)
                    || !transportDeliveryByCustomerId.TryGetValue(parentDeliveryId, out var parentTransportDelivery))
                {
                    continue;
                }

                childDelivery.SortOrder = parentTransportDelivery.SortOrder;
                childDelivery.Note = parentTransportDelivery.Note;
                childDelivery.CustomerYourOrderNr = parentTransportDelivery.CustomerYourOrderNr;
            }

            var incomingIdSet = incomingExistingIds.ToHashSet();
            _dbContext.TransportOrderDeliveryPallets.RemoveRange(activePalletList.Where(x => !incomingIdSet.Contains(x.Id)));

            foreach (var item in incomingPalletList)
            {
                if (item.Id > 0)
                {
                    var existingPallet = existingById[item.Id];
                    existingPallet.TransportOrderDeliveryId = item.TransportOrderDeliveryId;
                    existingPallet.PosCmX = item.PosCmX;
                    existingPallet.PosCmY = item.PosCmY;
                    existingPallet.Rotation = item.Rotation;
                    existingPallet.Width = item.Width;
                    existingPallet.Height = item.Height;
                    continue;
                }

                _dbContext.TransportOrderDeliveryPallets.Add(new TransportOrderDeliveryPallet
                {
                    TransportOrderDeliveryId = item.TransportOrderDeliveryId,
                    PosCmX = item.PosCmX,
                    PosCmY = item.PosCmY,
                    Rotation = item.Rotation,
                    Width = item.Width,
                    Height = item.Height,
                });
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            if (prevDeliveryStatus != request.DeliveryStatus)
            {
                // Legacy parity: CreateFromData recalculates/creates a cost-calc version on every status change.
                await _costCalculationService.RecalculateAsync(id, request.DeliveryStatus ?? prevDeliveryStatus);
            }

            var updatedTransportOrder = await _dbContext.TransportOrders
                .AsNoTracking()
                .Include(x => x.CreatedByUser)
                .Include(x => x.EditedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);

            return Ok(await BuildAggregateDtoAsync(updatedTransportOrder ?? transportOrder));
        }

        [HttpGet("form-options")]
        public async Task<ActionResult<TransportOrderFormOptionsDto>> GetFormOptions()
        {
            var transporters = await _dbContext.Shippers
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

            return Ok(new TransportOrderFormOptionsDto
            {
                Transporters = transporters,
            });
        }

        private static TransportOrderDto MapToDto(TransportOrder transportOrder)
        {
            return new TransportOrderDto
            {
                Id = transportOrder.Id,
                TransportOrderNr = transportOrder.TransportOrderNr,
                SenderReference = transportOrder.SenderReference,
                Note = transportOrder.Note,
                DateCreated = transportOrder.DateCreated,
                DateLoading = transportOrder.DateLoading,
                DateDelivery = transportOrder.DateDelivery,
                DeliveryStatus = transportOrder.DeliveryStatus,
                ShipperId = transportOrder.ShipperId,
                IsSentToShipper = transportOrder.IsSentToShipper,
                IsReportedBack = transportOrder.IsReportedBack,
                CreatedByUserId = transportOrder.CreatedByUserId,
                CreatedByUserName = transportOrder.CreatedByUser?.Name,
                CreatedTimeStamp = transportOrder.CreatedTimeStamp,
                EditedByUserId = transportOrder.EditedByUserId,
                EditedByUserName = transportOrder.EditedByUser?.Name,
                EditedTimeStamp = transportOrder.EditedTimeStamp,
            };
        }

        private async Task<TransportOrderAggregateDto> BuildAggregateDtoAsync(TransportOrder transportOrder)
        {
            var deliveryLegsResult = await GetDeliveryLegsByTransportOrderId(transportOrder.Id);
            var deliveryOverviewResult = await GetDeliveryOverviewByTransportOrderId(transportOrder.Id);
            var truckPlanResult = await GetTruckPlanByTransportOrderId(transportOrder.Id);

            var deliveryLegs = GetOkValue(deliveryLegsResult);
            var deliveryOverview = GetOkValue(deliveryOverviewResult);
            var truckPlan = GetOkValue(truckPlanResult);
            var deliveryList = await _dbContext.TransportOrderDeliveries
                .AsNoTracking()
                .Where(x => x.TransportOrderId == transportOrder.Id)
                .OrderBy(x => x.Id)
                .ToListAsync();

            return new TransportOrderAggregateDto
            {
                TransportOrder = MapToDto(transportOrder),
                DeliveryLegs = deliveryLegs,
                DeliveryOverview = deliveryOverview,
                TruckPlan = truckPlan,
                Version = CalculateVersion(transportOrder, deliveryList, truckPlan.PalletList),
            };
        }

        private static T GetOkValue<T>(ActionResult<T> result)
        {
            if (result.Value is not null)
            {
                return result.Value;
            }

            if (result.Result is OkObjectResult { Value: T value })
            {
                return value;
            }

            throw new InvalidOperationException("Det gick inte att bygga transportorderns samlade svar.");
        }

        private static TransportOrderDeliverySaveDto MapToSaveDto(TransportOrderDelivery entity)
        {
            return new TransportOrderDeliverySaveDto
            {
                Id = entity.Id,
                SortOrder = entity.SortOrder ?? 0,
                DeliveryFromStockId = entity.DeliveryFromStockId,
                DeliveryToCustomerId = entity.DeliveryToCustomerId,
                DeliveryToStockId = entity.DeliveryToStockId,
                SupplierOrderId = entity.SupplierOrderId,
                DeliveryAddressFreeText = entity.DeliveryAddressFreeText,
                Note = entity.Note,
                CostCalcFreightInternational = entity.CostCalcFreightInternational,
                CostCalcFreightDomestic = entity.CostCalcFreightDomestic,
                CostCalcFreightUnloading = entity.CostCalcFreightUnloading,
                CostCalcFreightLoading = entity.CostCalcFreightLoading,
                CostCalcFreightOther = entity.CostCalcFreightOther,
                CostCalcFreightInternationalCost = entity.CostCalcFreightInternationalCost,
                CostCalcFreightDomesticCost = entity.CostCalcFreightDomesticCost,
                CostCalcFreightUnloadingCost = entity.CostCalcFreightUnloadingCost,
                CostCalcFreightFtlCost = entity.CostCalcFreightFtlCost,
                ForcedOmlastStatus = entity.ForcedOmlastStatus,
                OmlastInventoryId = entity.OmlastInventoryId,
                OmlastInDate = entity.OmlastInDate,
                OmlastDeliveryDate = entity.OmlastDeliveryDate,
                OmlastNote = entity.OmlastNote,
                OmlastPalletPlaces = entity.OmlastPalletPlaces,
                TotalWeight = entity.TotalWeight,
                CustomerYourOrderNr = entity.CustomerYourOrderNr,
            };
        }

        private static void MapToEntity(TransportOrderDeliverySaveDto source, TransportOrderDelivery target)
        {
            target.SortOrder = source.SortOrder;
            target.DeliveryFromStockId = source.DeliveryFromStockId;
            target.DeliveryToCustomerId = source.DeliveryToCustomerId;
            target.DeliveryToStockId = source.DeliveryToStockId;
            target.SupplierOrderId = source.SupplierOrderId;
            target.DeliveryAddressFreeText = source.DeliveryAddressFreeText;
            target.Note = source.Note;
            target.CostCalcFreightInternational = source.CostCalcFreightInternational;
            target.CostCalcFreightDomestic = source.CostCalcFreightDomestic;
            target.CostCalcFreightUnloading = source.CostCalcFreightUnloading;
            target.CostCalcFreightLoading = source.CostCalcFreightLoading;
            target.CostCalcFreightOther = source.CostCalcFreightOther;
            target.CostCalcFreightInternationalCost = source.CostCalcFreightInternationalCost;
            target.CostCalcFreightDomesticCost = source.CostCalcFreightDomesticCost;
            target.CostCalcFreightUnloadingCost = source.CostCalcFreightUnloadingCost;
            target.CostCalcFreightFtlCost = source.CostCalcFreightFtlCost;
            target.ForcedOmlastStatus = source.ForcedOmlastStatus;
            target.OmlastInventoryId = source.OmlastInventoryId;
            target.OmlastInDate = source.OmlastInDate;
            target.OmlastDeliveryDate = source.OmlastDeliveryDate;
            target.OmlastNote = source.OmlastNote;
            target.OmlastPalletPlaces = source.OmlastPalletPlaces;
            target.TotalWeight = source.TotalWeight;
            target.CustomerYourOrderNr = source.CustomerYourOrderNr;
        }

        // Legacy parity: TransportOrderViewModel.TransportOrderNotificationWrapper_PropertyChanged "DeliveryStatus" branch.
        private async Task<string?> ValidateDeliveryStatusTransitionAsync(int transportOrderId, int prevStatus, int newStatus)
        {
            if (newStatus - prevStatus > 1)
            {
                return "Leveransstatus kan inte hoppa över flera steg i taget.";
            }

            if (newStatus != 3 || prevStatus == 3)
            {
                return null;
            }

            var hasNotAttestedTransportInvoices = await _dbContext.DocumentFiles
                .AsNoTracking()
                .AnyAsync(x => x.TransportOrderId == transportOrderId && x.DocumentTypeId == 9 && !x.IsAttested);

            if (hasNotAttestedTransportInvoices)
            {
                return "Det finns ej attesterade transportfakturor, åtgärda detta först.";
            }

            var costCalcStatusCounts = await _dbContext.TransportOrderCostCalcs
                .AsNoTracking()
                .Where(x => x.TransportOrderId == transportOrderId)
                .GroupBy(x => x.CreatedAtStatus)
                .Select(x => new { CreatedAtStatus = x.Key, Count = x.Count() })
                .ToListAsync();

            int CountAtStatus(int status) => costCalcStatusCounts.FirstOrDefault(x => x.CreatedAtStatus == status)?.Count ?? 0;

            var hasValidCostCalcVersions = CountAtStatus(0) == 1
                && CountAtStatus(1) == 0
                && CountAtStatus(2) == 1
                && CountAtStatus(3) == 0;

            if (!hasValidCostCalcVersions)
            {
                return "På efterkalkylen ska det finnas en version av status 'Planerad' och en version av status 'Återrapporterad'. Vv åtgärda och försök igen.";
            }

            return null;
        }

        private async Task<bool> HasValidDeliveryReferencesAsync(IEnumerable<TransportOrderDeliverySaveDto> deliveryList)
        {
            var deliveries = deliveryList.ToList();

            return await HasExistingIdsAsync(_dbContext.DeliveryFromStocks, deliveries.Select(x => x.DeliveryFromStockId))
                && await HasExistingIdsAsync(_dbContext.DeliveryToCustomers, deliveries.Select(x => x.DeliveryToCustomerId))
                && await HasExistingIdsAsync(_dbContext.DeliveryToStocks, deliveries.Select(x => x.DeliveryToStockId))
                && await HasExistingIdsAsync(_dbContext.SupplierOrders, deliveries.Select(x => x.SupplierOrderId))
                && await HasExistingIdsAsync(_dbContext.Inventories, deliveries.Select(x => x.OmlastInventoryId));
        }

        private static async Task<bool> HasExistingIdsAsync<TEntity>(IQueryable<TEntity> query, IEnumerable<int?> ids)
            where TEntity : class
        {
            var requestedIds = ids
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            if (requestedIds.Count == 0)
            {
                return true;
            }

            var existingIds = await query
                .Where(x => requestedIds.Contains(EF.Property<int>(x, "Id")))
                .Select(x => EF.Property<int>(x, "Id"))
                .ToListAsync();

            return existingIds.Count == requestedIds.Count;
        }

        private static string CalculateVersion(
            TransportOrder transportOrder,
            IEnumerable<TransportOrderDelivery> deliveryList,
            IEnumerable<TransportOrderDeliveryPallet> palletList)
        {
            var palletDtos = palletList.Select(x => new TransportOrderTruckPlanPalletDto
            {
                Id = x.Id,
                TransportOrderDeliveryId = x.TransportOrderDeliveryId ?? 0,
                PosCmX = x.PosCmX,
                PosCmY = x.PosCmY,
                Rotation = x.Rotation,
                Width = x.Width,
                Height = x.Height,
            });

            return CalculateVersion(transportOrder, deliveryList.Select(MapToSaveDto), palletDtos);
        }

        private static string CalculateVersion(
            TransportOrder transportOrder,
            IEnumerable<TransportOrderDelivery> deliveryList,
            IEnumerable<TransportOrderTruckPlanPalletDto> palletList)
        {
            return CalculateVersion(transportOrder, deliveryList.Select(MapToSaveDto), palletList);
        }

        private static string CalculateVersion(
            TransportOrder transportOrder,
            IEnumerable<TransportOrderDeliverySaveDto> deliveryList,
            IEnumerable<TransportOrderTruckPlanPalletDto> palletList)
        {
            var values = new List<string>
            {
                transportOrder.Id.ToString(CultureInfo.InvariantCulture),
                transportOrder.TransportOrderNr.ToString(CultureInfo.InvariantCulture),
                transportOrder.SenderReference ?? string.Empty,
                transportOrder.Note ?? string.Empty,
                transportOrder.DateCreated?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                transportOrder.DateLoading?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                transportOrder.DateDelivery?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                transportOrder.DeliveryStatus?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                transportOrder.ShipperId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                transportOrder.IsSentToShipper.ToString(CultureInfo.InvariantCulture),
                transportOrder.IsReportedBack.ToString(CultureInfo.InvariantCulture),
                transportOrder.CreatedByUserId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                transportOrder.CreatedTimeStamp?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                transportOrder.EditedByUserId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                transportOrder.EditedTimeStamp?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            };

            values.AddRange(palletList
                .OrderBy(x => x.Id)
                .Select(x => string.Join("|", new[]
                {
                    x.Id.ToString(CultureInfo.InvariantCulture),
                    x.TransportOrderDeliveryId.ToString(CultureInfo.InvariantCulture),
                    x.PosCmX.ToString(CultureInfo.InvariantCulture),
                    x.PosCmY.ToString(CultureInfo.InvariantCulture),
                    x.Rotation.ToString(CultureInfo.InvariantCulture),
                    x.Width.ToString(CultureInfo.InvariantCulture),
                    x.Height.ToString(CultureInfo.InvariantCulture),
                })));

            values.AddRange(deliveryList
                .OrderBy(x => x.Id)
                .Select(x => string.Join("|", new[]
                {
                    x.Id.ToString(CultureInfo.InvariantCulture),
                    x.SortOrder.ToString(CultureInfo.InvariantCulture),
                    x.DeliveryFromStockId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    x.DeliveryToCustomerId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    x.DeliveryToStockId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    x.SupplierOrderId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    x.DeliveryAddressFreeText ?? string.Empty,
                    x.Note ?? string.Empty,
                    x.CostCalcFreightInternational?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    x.CostCalcFreightDomestic?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    x.CostCalcFreightUnloading?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    x.CostCalcFreightLoading?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    x.CostCalcFreightOther?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    x.CostCalcFreightInternationalCost?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    x.CostCalcFreightDomesticCost?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    x.CostCalcFreightUnloadingCost?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    x.CostCalcFreightFtlCost?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    x.ForcedOmlastStatus?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    x.OmlastInventoryId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    x.OmlastInDate?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                    x.OmlastDeliveryDate?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                    x.OmlastNote ?? string.Empty,
                    x.OmlastPalletPlaces?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    x.TotalWeight?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    x.CustomerYourOrderNr ?? string.Empty,
                })));

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", values))));
        }

        private static bool IsDistributionLeg(TransportOrderDeliveryLegDto deliveryLeg)
        {
            return !string.IsNullOrWhiteSpace(deliveryLeg.TypeOfTransport)
                && deliveryLeg.TypeOfTransport.Contains("SPEDITION", StringComparison.OrdinalIgnoreCase);
        }
    }
}