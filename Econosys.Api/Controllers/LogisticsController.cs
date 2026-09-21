using System.Globalization;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/logistics/transportorderoverview")]
    public class LogisticsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public LogisticsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("/api/logistics/calloffoverview/top-table")]
        public async Task<ActionResult<List<CallOffOverviewTopTableRowDto>>> GetCallOffTopTable(
            [FromQuery] bool includePlanned = true,
            [FromQuery] bool includeNotPlanned = true)
        {
            if (!includePlanned && !includeNotPlanned)
            {
                return Ok(new List<CallOffOverviewTopTableRowDto>());
            }

            // Legacy SearchCustomerOrders(... IsCallOff: true, IsNotCompleted: true)
            // selects incomplete orders whose supplier order is tied to a real inventory.
            var customerOrders = await _dbContext.CustomerOrders
                .AsNoTracking()
                .Where(x =>
                    !x.Completed
                    && x.SupplierOrderId.HasValue
                    && x.SupplierOrder != null
                    && x.SupplierOrder.Inventory != null
                    && x.SupplierOrder.Inventory.IsInventory)
                .Select(x => new
                {
                    x.Id,
                    x.SupplierOrderId,
                    x.CustomerOrderNr,
                    x.CustomerName,
                    x.DeliveryPostalAddress,
                    x.Product,
                    x.DeliveryDate,
                    x.DeliveryDateWeekMode,
                    x.TimeOfDelivery,
                    x.QuotationId,
                    x.Edition,
                    x.LogisticsInfoInternal,
                    InventoryName = x.SupplierOrder != null && x.SupplierOrder.Inventory != null
                        ? x.SupplierOrder.Inventory.Name
                        : null,
                    EditionProduced = x.SupplierOrder != null ? x.SupplierOrder.ProducedEdition : null,
                    EditionPerPallet = x.SelectedCalculationRow != null ? x.SelectedCalculationRow.ArchivedEditionPerPallet : null,
                    NrOfStorageMonths = x.SelectedCalculationRow != null ? x.SelectedCalculationRow.NrOfStorageMonths : null,
                    PalletIsStackable = x.SelectedCalculationRow != null && x.SelectedCalculationRow.ArchivedPalletIsStackable,
                })
                .ToListAsync();

            if (customerOrders.Count == 0)
            {
                return Ok(new List<CallOffOverviewTopTableRowDto>());
            }

            var customerOrderIds = customerOrders.Select(x => x.Id).ToList();
            var supplierOrderIds = customerOrders
                .Where(x => x.SupplierOrderId.HasValue)
                .Select(x => x.SupplierOrderId!.Value)
                .Distinct()
                .ToList();
            var today = SwedishTime.Today;
            var deliveryToStockRows = await _dbContext.DeliveryToStocks
                .AsNoTracking()
                .Where(x =>
                    x.SupplierOrderId.HasValue
                    && supplierOrderIds.Contains(x.SupplierOrderId.Value))
                .Select(x => new
                {
                    SupplierOrderId = x.SupplierOrderId!.Value,
                    x.NrOfItems,
                    x.NrOfPallets,
                    x.EditionPerPallet,
                    x.DeliveryStatus,
                    x.DeliveryDate,
                })
                .ToListAsync();
            var deliveryRows = await _dbContext.DeliveryFromStocks
                .AsNoTracking()
                .Where(x =>
                    x.CustomerOrderId.HasValue
                    && customerOrderIds.Contains(x.CustomerOrderId.Value))
                .Select(x => new
                {
                    CustomerOrderId = x.CustomerOrderId!.Value,
                    x.NrOfItems,
                    x.NrOfPallets,
                    x.EditionPerPallet,
                    x.DeliveryStatus,
                    x.DeliveryDate,
                    IsPlanned = x.CallOffDeliveries.Any(callOffDelivery => callOffDelivery.CallOff != null && callOffDelivery.CallOff.DeliveryStatus != 2),
                })
                .ToListAsync();

            var activeTransportOrderLinks = await _dbContext.TransportOrderDeliveries
                .AsNoTracking()
                .Where(x => x.TransportOrder != null && x.TransportOrder.DeliveryStatus <= 1)
                .Select(x => new
                {
                    CustomerOrderFromStockId = x.DeliveryFromStock != null ? x.DeliveryFromStock.CustomerOrderId : null,
                    CustomerOrderToCustomerId = x.DeliveryToCustomer != null ? x.DeliveryToCustomer.CustomerOrderId : null,
                    SupplierOrderToStockId = x.DeliveryToStock != null ? x.DeliveryToStock.SupplierOrderId : null,
                })
                .ToListAsync();
            var activeTransportCustomerOrderIds = activeTransportOrderLinks
                .SelectMany(x => new[] { x.CustomerOrderFromStockId, x.CustomerOrderToCustomerId })
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToHashSet();
            var activeTransportSupplierOrderIds = activeTransportOrderLinks
                .Select(x => x.SupplierOrderToStockId)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToHashSet();

            var quotationIds = customerOrders
                .Where(x => x.QuotationId.HasValue)
                .Select(x => x.QuotationId!.Value)
                .Distinct()
                .ToList();
            var calculationByQuotationId = await _dbContext.Quotations
                .AsNoTracking()
                .Where(x => quotationIds.Contains(x.Id))
                .Select(x => new
                {
                    x.Id,
                    PalletLength = x.Calculation != null ? x.Calculation.PalletLength : null,
                    PalletWidth = x.Calculation != null ? x.Calculation.PalletWidth : null,
                    PalletHeight = x.Calculation != null ? x.Calculation.PalletHeight : null,
                    PalletIsStackable = x.Calculation != null && x.Calculation.PalletIsStackable,
                })
                .ToDictionaryAsync(x => x.Id);

            var result = customerOrders
                .Select(customerOrder =>
                {
                    var relatedDeliveries = deliveryRows.Where(x => x.CustomerOrderId == customerOrder.Id).ToList();
                    var relatedStockDeliveries = deliveryToStockRows
                        .Where(x => customerOrder.SupplierOrderId.HasValue && x.SupplierOrderId == customerOrder.SupplierOrderId.Value)
                        .ToList();
                    var deliveredFromStockNow = relatedDeliveries
                        .Where(x => x.DeliveryStatus == 2 && x.DeliveryDate <= today)
                        .Sum(x => x.NrOfItems ?? 0);
                    var deliveredFromStock = relatedDeliveries.Sum(x => x.NrOfItems ?? 0);
                    var deliveredToStockNow = relatedStockDeliveries
                        .Where(x => x.DeliveryStatus == 2 && x.DeliveryDate <= today)
                        .Sum(x => x.NrOfItems ?? 0);
                    var deliveredToStock = relatedStockDeliveries.Sum(x => x.NrOfItems ?? 0);
                    var stockBalanceNow = deliveredToStockNow - deliveredFromStockNow;
                    var stockBalance = deliveredToStock - deliveredFromStock;
                    var calculationDate = customerOrder.DeliveryDate.HasValue && customerOrder.NrOfStorageMonths.HasValue
                        ? customerOrder.DeliveryDate.Value.AddDays(customerOrder.NrOfStorageMonths.Value * 30)
                        : (DateTime?)null;
                    var palletsNow = BuildPalletBalanceInfo(
                        relatedStockDeliveries.Where(x => x.DeliveryStatus == 2 && x.DeliveryDate <= today).Select(x => (x.EditionPerPallet, x.NrOfPallets)),
                        relatedDeliveries.Where(x => x.DeliveryStatus == 2 && x.DeliveryDate <= today).Select(x => (x.EditionPerPallet, x.NrOfPallets)));
                    var pallets = BuildPalletBalanceInfo(
                        relatedStockDeliveries.Select(x => (x.EditionPerPallet, x.NrOfPallets)),
                        relatedDeliveries.Select(x => (x.EditionPerPallet, x.NrOfPallets)));
                    var isPlanned = relatedDeliveries.Any(x => x.IsPlanned);
                    var isOnActiveTransportOrder = activeTransportCustomerOrderIds.Contains(customerOrder.Id)
                        || (customerOrder.SupplierOrderId.HasValue && activeTransportSupplierOrderIds.Contains(customerOrder.SupplierOrderId.Value));
                    var inventoryName = customerOrder.InventoryName ?? string.Empty;
                    var calculation = customerOrder.QuotationId.HasValue
                        && calculationByQuotationId.TryGetValue(customerOrder.QuotationId.Value, out var quotationCalculation)
                        ? quotationCalculation
                        : null;
                    var palletFormat = calculation == null
                        ? string.Empty
                        : $"{calculation.PalletLength} x {calculation.PalletWidth} x {calculation.PalletHeight}";

                    if (palletFormat == " x ")
                    {
                        palletFormat = string.Empty;
                    }

                    if (calculation?.PalletIsStackable == true && !string.IsNullOrWhiteSpace(palletFormat))
                    {
                        palletFormat += "  (stp)";
                    }

                    return new CallOffOverviewTopTableRowDto
                    {
                        CustomerOrderId = customerOrder.Id,
                        CustomerOrderNr = customerOrder.CustomerOrderNr ?? string.Empty,
                        CustomerName = customerOrder.CustomerName ?? string.Empty,
                        CustomerCity = customerOrder.DeliveryPostalAddress ?? string.Empty,
                        ProductName = customerOrder.Product ?? string.Empty,
                        DeliveryDate = customerOrder.DeliveryDate,
                        DeliveryTime = FormatDateInfo(customerOrder.DeliveryDate, customerOrder.DeliveryDateWeekMode, customerOrder.TimeOfDelivery),
                        CalculationDate = calculationDate,
                        IsCalculationDateOverdue = calculationDate < SwedishTime.Now,
                        InventoryName = inventoryName,
                        EditionProduced = customerOrder.EditionProduced,
                        EditionCustomerOrder = customerOrder.Edition,
                        StockBalanceNow = stockBalanceNow,
                        StockBalance = stockBalance,
                        PalletsNow = palletsNow,
                        Pallets = pallets,
                        PalletFormat = palletFormat,
                        HasLogisticsInfo = !string.IsNullOrWhiteSpace(customerOrder.LogisticsInfoInternal),
                        LogisticsInfoInternal = customerOrder.LogisticsInfoInternal ?? string.Empty,
                        IsPlanned = isPlanned,
                        IsOnActiveTransportOrder = isOnActiveTransportOrder,
                    };
                })
                .Where(x => (x.IsPlanned && includePlanned) || (!x.IsPlanned && includeNotPlanned))
                .OrderBy(x => ParseOrderNr(x.CustomerOrderNr))
                .ThenBy(x => x.CustomerOrderNr)
                .ToList();

            return Ok(result);
        }

        private static string BuildPalletBalanceInfo(
            IEnumerable<(int? EditionPerPallet, int? NrOfPallets)> deliveredToStock,
            IEnumerable<(int? EditionPerPallet, int? NrOfPallets)> deliveredFromStock)
        {
            var balances = new Dictionary<int, int>();

            foreach (var delivery in deliveredToStock)
            {
                var editionPerPallet = delivery.EditionPerPallet ?? 0;
                balances[editionPerPallet] = balances.GetValueOrDefault(editionPerPallet) + (delivery.NrOfPallets ?? 0);
            }

            foreach (var delivery in deliveredFromStock)
            {
                var editionPerPallet = delivery.EditionPerPallet ?? 0;
                balances[editionPerPallet] = balances.GetValueOrDefault(editionPerPallet) - (delivery.NrOfPallets ?? 0);
            }

            return string.Join(" / ", balances.Values);
        }

        [HttpGet("/api/logistics/calloffoverview/active-table")]
        public async Task<ActionResult<List<CallOffOverviewActiveRowDto>>> GetActiveCallOffTable()
        {
            var callOffRows = await _dbContext.CallOffs
                .AsNoTracking()
                // Legacy SearchCallOff(... OnlyActive: true) includes all non-final call-offs.
                .Where(x => x.DeliveryStatus != 2)
                .Select(x => new
                {
                    x.Id,
                    x.IsSentToShipper,
                    ShipperName = x.Shipper != null ? x.Shipper.Name : null,
                    x.DeliveryDate,
                    x.Note,
                })
                .OrderBy(x => x.DeliveryDate == null)
                .ThenBy(x => x.DeliveryDate)
                .ThenBy(x => x.Id)
                .ToListAsync();

            if (callOffRows.Count == 0)
            {
                return Ok(new List<CallOffOverviewActiveRowDto>());
            }

            var callOffIds = callOffRows.Select(x => x.Id).ToList();
            var deliveryRows = await _dbContext.CallOffDeliveries
                .AsNoTracking()
                .Where(x => x.CallOffId.HasValue && callOffIds.Contains(x.CallOffId.Value))
                .Select(x => new
                {
                    CallOffId = x.CallOffId!.Value,
                    CustomerOrderNr = x.DeliveryFromStock != null && x.DeliveryFromStock.CustomerOrder != null
                        ? x.DeliveryFromStock.CustomerOrder.CustomerOrderNr
                        : null,
                    CustomerName = x.DeliveryFromStock != null && x.DeliveryFromStock.CustomerOrder != null
                        ? x.DeliveryFromStock.CustomerOrder.CustomerName
                        : null,
                })
                .ToListAsync();

            return Ok(callOffRows
                .Select(callOff =>
                {
                    var relatedDeliveries = deliveryRows.Where(x => x.CallOffId == callOff.Id);
                    return new CallOffOverviewActiveRowDto
                    {
                        Id = callOff.Id,
                        IsSentToShipper = callOff.IsSentToShipper,
                        ShipperName = callOff.ShipperName ?? string.Empty,
                        DeliveryDate = callOff.DeliveryDate,
                        CustomerOrderNrs = string.Join(", ", relatedDeliveries
                            .Select(x => x.CustomerOrderNr)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Select(x => x!)
                            .Distinct(StringComparer.OrdinalIgnoreCase)),
                        CustomerNames = string.Join(", ", relatedDeliveries
                            .Select(x => x.CustomerName)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Select(x => x!)
                            .Distinct(StringComparer.OrdinalIgnoreCase)),
                        Note = callOff.Note ?? string.Empty,
                    };
                })
                .ToList());
        }

        [HttpGet("planning-table")]
        public async Task<ActionResult<List<TransportOrderOverviewPlanningRowDto>>> GetPlanningTable()
        {
            // Legacy parity: SearchTransportOrder(... OnlyInPlanning: true) => DeliveryStatus == 0.
            var planningRows = await GetTransportOrdersByDeliveryStatusAsync(0);

            return Ok(planningRows
                .Select(x => new TransportOrderOverviewPlanningRowDto
                {
                    Id = x.Id,
                    TransportOrderNr = x.TransportOrderNr,
                    TransporterName = x.TransporterName,
                    LoadingCities = x.LoadingCities,
                    UnloadingCities = x.UnloadingCities,
                    FilledInfo = string.Empty,
                })
                .ToList());
        }

        [HttpGet("active-table")]
        public async Task<ActionResult<List<TransportOrderOverviewActiveRowDto>>> GetActiveTable()
        {
            // Legacy parity: SearchTransportOrder(... OnlyActive: true) => DeliveryStatus == 1.
            var activeRows = await GetTransportOrdersByDeliveryStatusAsync(1);

            return Ok(activeRows
                .Select(x => new TransportOrderOverviewActiveRowDto
                {
                    Id = x.Id,
                    TransportOrderNr = x.TransportOrderNr,
                    TransporterName = x.TransporterName,
                    LoadingCities = x.LoadingCities,
                    UnloadingCities = x.UnloadingCities,
                })
                .ToList());
        }

        [HttpGet("report-back-table")]
        public async Task<ActionResult<List<TransportOrderOverviewReportBackRowDto>>> GetReportBackTable()
        {
            // Legacy parity: SearchTransportOrder(... OnlyToReportBack: true) => DeliveryStatus == 2.
            var reportBackRows = await GetTransportOrdersByDeliveryStatusAsync(2);
            var transportOrderIds = reportBackRows.Select(x => x.Id).ToList();

            var notAttestedTransportOrderIdSet = await _dbContext.DocumentFiles
                .AsNoTracking()
                .Where(x =>
                    x.TransportOrderId.HasValue
                    && transportOrderIds.Contains(x.TransportOrderId.Value)
                    && x.DocumentTypeId == 9
                    && !x.IsAttested)
                .Select(x => x.TransportOrderId!.Value)
                .Distinct()
                .ToHashSetAsync();

            return Ok(reportBackRows
                .Select(x => new TransportOrderOverviewReportBackRowDto
                {
                    Id = x.Id,
                    TransportOrderNr = x.TransportOrderNr,
                    TransporterName = x.TransporterName,
                    HasNotAttestedTransportInvoices = notAttestedTransportOrderIdSet.Contains(x.Id),
                })
                .ToList());
        }

        [HttpGet("order-info/{customerOrderId:int}")]
        public async Task<ActionResult<TransportOrderOverviewOrderInfoDto>> GetOrderInfo(int customerOrderId)
        {
            var customerOrder = await _dbContext.CustomerOrders
                .AsNoTracking()
                .Where(x => x.Id == customerOrderId)
                .Select(x => new
                {
                    x.Id,
                    x.CustomerOrderNr,
                    x.SupplierOrderId,
                    x.Completed,
                })
                .FirstOrDefaultAsync();

            if (customerOrder == null)
            {
                return NotFound();
            }

            var supplierOrder = customerOrder.SupplierOrderId == null
                ? null
                : await _dbContext.SupplierOrders
                    .AsNoTracking()
                    .Where(x => x.Id == customerOrder.SupplierOrderId)
                    .Select(x => new { x.Id, x.Edition, x.ProducedEdition })
                    .FirstOrDefaultAsync();

            return Ok(new TransportOrderOverviewOrderInfoDto
            {
                CustomerOrderId = customerOrder.Id,
                CustomerOrderNr = customerOrder.CustomerOrderNr ?? string.Empty,
                SupplierOrderId = supplierOrder?.Id,
                OrderedEdition = supplierOrder?.Edition,
                ProducedEdition = supplierOrder?.ProducedEdition,
                IsCompleted = customerOrder.Completed,
            });
        }

        [HttpPut("order-info/{customerOrderId:int}")]
        public async Task<IActionResult> SaveOrderInfo(int customerOrderId, [FromBody] SaveTransportOrderOverviewOrderInfoDto dto)
        {
            if (dto == null)
            {
                return BadRequest();
            }

            if (dto.ProducedEdition is < 0)
            {
                return BadRequest("Producerad upplaga kan inte vara negativ.");
            }

            var customerOrder = await _dbContext.CustomerOrders
                .FirstOrDefaultAsync(x => x.Id == customerOrderId);

            if (customerOrder == null)
            {
                return NotFound();
            }

            customerOrder.Completed = dto.IsCompleted;
            customerOrder.Edited = SwedishTime.Now;

            if (customerOrder.SupplierOrderId != null)
            {
                var supplierOrder = await _dbContext.SupplierOrders
                    .FirstOrDefaultAsync(x => x.Id == customerOrder.SupplierOrderId);

                if (supplierOrder != null)
                {
                    supplierOrder.ProducedEdition = dto.ProducedEdition;
                    supplierOrder.Edited = SwedishTime.Now;
                }
            }

            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("top-table")]
        public async Task<ActionResult<List<TransportOrderOverviewTopTableRowDto>>> GetTopTable(
            [FromQuery] bool includePlanned = true,
            [FromQuery] bool includeNotPlanned = true,
            [FromQuery] string? search = null)
        {
            if (!includePlanned && !includeNotPlanned)
            {
                return Ok(new List<TransportOrderOverviewTopTableRowDto>());
            }

            var rows = await _dbContext.CustomerOrders
                .AsNoTracking()
                .Where(x =>
                    x.SupplierOrderId != null
                    && x.SupplierOrder != null
                    && x.SupplierOrder.EconopackTransportResponsible
                    && !x.Completed)
                .Select(x => new
                {
                    x.Id,
                    x.SupplierOrderId,
                    x.CustomerOrderNr,
                    x.CustomerName,
                    x.DeliveryPostalAddress,
                    x.Product,
                    x.DeliveryDate,
                    x.DeliveryDateWeekMode,
                    x.TimeOfDelivery,
                    x.Edition,
                    x.LogisticsInfoInternal,
                    x.PalletFormatId,
                    x.SelectedCalculationRow,
                    SupplierOrder = x.SupplierOrder == null
                        ? null
                        : new
                        {
                            x.SupplierOrder.Id,
                            x.SupplierOrder.SupplierName,
                            x.SupplierOrder.SupplierFactoryId,
                            x.SupplierOrder.DeliveryPostalAddress,
                            x.SupplierOrder.ConfirmedDeliveryDate,
                            x.SupplierOrder.ConfirmedDeliveryDateWeekMode,
                            x.SupplierOrder.Edition,
                            x.SupplierOrder.ProducedEdition,
                            x.SupplierOrder.PalletFormatId
                        }
                })
                .ToListAsync();

            var supplierOrderIds = rows
                .Where(x => x.SupplierOrderId.HasValue)
                .Select(x => x.SupplierOrderId!.Value)
                .Distinct()
                .ToList();

            var supplierFactoryIds = rows
                .Where(x => x.SupplierOrder?.SupplierFactoryId != null)
                .Select(x => x.SupplierOrder!.SupplierFactoryId!.Value)
                .Distinct()
                .ToList();

            var palletFormatIds = rows
                .SelectMany(x => new[]
                {
                    x.PalletFormatId,
                    x.SupplierOrder?.PalletFormatId,
                    x.SelectedCalculationRow != null ? x.SelectedCalculationRow.ArchivedPalletFormatIdCustomerOrder : null,
                    x.SelectedCalculationRow != null ? x.SelectedCalculationRow.ArchivedPalletFormatId : null
                })
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var deliveredToStockBySupplierOrderId = await _dbContext.DeliveryToStocks
                .AsNoTracking()
                .Where(x => x.SupplierOrderId.HasValue && supplierOrderIds.Contains(x.SupplierOrderId.Value))
                .GroupBy(x => x.SupplierOrderId!.Value)
                .Select(g => new { SupplierOrderId = g.Key, Delivered = g.Sum(x => x.NrOfItems ?? 0) })
                .ToDictionaryAsync(x => x.SupplierOrderId, x => x.Delivered);

            var deliveredToCustomerBySupplierOrderId = await _dbContext.DeliveryToCustomers
                .AsNoTracking()
                .Where(x => x.SupplierOrderId.HasValue && supplierOrderIds.Contains(x.SupplierOrderId.Value))
                .GroupBy(x => x.SupplierOrderId!.Value)
                .Select(g => new { SupplierOrderId = g.Key, Delivered = g.Sum(x => x.NrOfItems ?? 0) })
                .ToDictionaryAsync(x => x.SupplierOrderId, x => x.Delivered);

            var plannedSupplierOrderIds = await _dbContext.TransportOrderDeliveries
                .AsNoTracking()
                .Where(x =>
                    x.SupplierOrderId.HasValue
                    && supplierOrderIds.Contains(x.SupplierOrderId.Value)
                    && x.TransportOrder != null
                    && x.TransportOrder.DeliveryStatus <= 1)
                .Select(x => x.SupplierOrderId!.Value)
                .Distinct()
                .ToListAsync();

            var plannedSupplierOrderIdSet = plannedSupplierOrderIds.ToHashSet();

            var supplierFactoryCityById = await _dbContext.SupplierFactories
                .AsNoTracking()
                .Where(x => supplierFactoryIds.Contains(x.Id))
                .Select(x => new { x.Id, x.City })
                .ToDictionaryAsync(x => x.Id, x => x.City ?? string.Empty);

            var palletFormatById = await _dbContext.PalletFormats
                .AsNoTracking()
                .Where(x => palletFormatIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name ?? string.Empty);

            var normalizedSearch = (search ?? string.Empty).Trim();

            var result = rows
                .Select(row =>
                {
                    var supplierOrderId = row.SupplierOrderId;
                    var so = row.SupplierOrder;
                    var isPlanned = supplierOrderId.HasValue && plannedSupplierOrderIdSet.Contains(supplierOrderId.Value);

                    var deliveredToStock = supplierOrderId.HasValue && deliveredToStockBySupplierOrderId.TryGetValue(supplierOrderId.Value, out var stockDelivered)
                        ? stockDelivered
                        : 0;
                    var deliveredToCustomer = supplierOrderId.HasValue && deliveredToCustomerBySupplierOrderId.TryGetValue(supplierOrderId.Value, out var customerDelivered)
                        ? customerDelivered
                        : 0;

                    var orderedEdition = so?.Edition;
                    var producedEdition = so?.ProducedEdition;
                    var effectiveProducedEdition = producedEdition ?? orderedEdition ?? 0;
                    var rest = effectiveProducedEdition - deliveredToStock - deliveredToCustomer;

                    if (rest <= 0 && !isPlanned)
                    {
                        return null;
                    }

                    if ((isPlanned && !includePlanned) || (!isPlanned && !includeNotPlanned))
                    {
                        return null;
                    }

                    var supplierCity = string.Empty;
                    if (so?.SupplierFactoryId != null && supplierFactoryCityById.TryGetValue(so.SupplierFactoryId.Value, out var supplierFactoryCity))
                    {
                        supplierCity = supplierFactoryCity;
                    }
                    else
                    {
                        supplierCity = so?.DeliveryPostalAddress ?? string.Empty;
                    }

                    var customerPostalCity = row.DeliveryPostalAddress ?? string.Empty;
                    string customerCity;
                    if (string.IsNullOrWhiteSpace(supplierCity))
                    {
                        customerCity = customerPostalCity;
                    }
                    else if (string.Equals(supplierCity, customerPostalCity, StringComparison.OrdinalIgnoreCase))
                    {
                        customerCity = customerPostalCity;
                    }
                    else if (string.IsNullOrWhiteSpace(customerPostalCity))
                    {
                        customerCity = supplierCity;
                    }
                    else
                    {
                        customerCity = $"{supplierCity} / {customerPostalCity}";
                    }

                    var deliveryTime = FormatDateInfo(row.DeliveryDate, row.DeliveryDateWeekMode, row.TimeOfDelivery);
                    var confirmedDelivery = FormatDateInfo(so?.ConfirmedDeliveryDate, so?.ConfirmedDeliveryDateWeekMode ?? false, string.Empty);

                    var editionPerPallet = row.SelectedCalculationRow?.ArchivedEditionPerPallet;
                    var pallets = editionPerPallet.HasValue && editionPerPallet.Value > 0
                        ? rest / editionPerPallet.Value
                        : (double?)null;

                    var palletFormatId = row.PalletFormatId
                        ?? row.SelectedCalculationRow?.ArchivedPalletFormatIdCustomerOrder
                        ?? row.SelectedCalculationRow?.ArchivedPalletFormatId
                        ?? so?.PalletFormatId;

                    var palletFormat = string.Empty;
                    if (palletFormatId.HasValue && palletFormatById.TryGetValue(palletFormatId.Value, out var formatName))
                    {
                        palletFormat = formatName;
                    }

                    if (row.SelectedCalculationRow?.ArchivedPalletIsStackable == true && !string.IsNullOrWhiteSpace(palletFormat))
                    {
                        palletFormat += " (stp)";
                    }

                    var dto = new TransportOrderOverviewTopTableRowDto
                    {
                        CustomerOrderId = row.Id,
                        SupplierOrderId = supplierOrderId,
                        CustomerOrderNr = row.CustomerOrderNr ?? string.Empty,
                        SupplierName = so?.SupplierName ?? string.Empty,
                        SupplierCity = supplierCity,
                        CustomerName = row.CustomerName ?? string.Empty,
                        CustomerCity = customerCity,
                        ProductName = row.Product ?? string.Empty,
                        DeliveryTime = deliveryTime,
                        ConfirmedDeliveryTime = confirmedDelivery,
                        EditionOrdered = orderedEdition,
                        EditionProduced = producedEdition,
                        EditionCustomerOrder = row.Edition,
                        Rest = Math.Max(0, rest),
                        Pallets = pallets,
                        PalletFormat = palletFormat,
                        IsPlanned = isPlanned,
                        HasLogisticsInfo = !string.IsNullOrWhiteSpace(row.LogisticsInfoInternal),
                        LogisticsInfoInternal = row.LogisticsInfoInternal ?? string.Empty,
                    };

                    return dto;
                })
                .Where(x => x != null)
                .Cast<TransportOrderOverviewTopTableRowDto>();

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                result = result.Where(x =>
                    x.CustomerOrderNr.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || x.SupplierName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || x.SupplierCity.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || x.CustomerName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || x.CustomerCity.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || x.ProductName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
            }

            var orderedResult = result
                .OrderBy(x => ParseOrderNr(x.CustomerOrderNr))
                .ThenBy(x => x.CustomerOrderNr)
                .ToList();

            return Ok(orderedResult);
        }

        private async Task<List<TransportOrderOverviewStatusRow>> GetTransportOrdersByDeliveryStatusAsync(int deliveryStatus)
        {
            var statusRows = await _dbContext.TransportOrders
                .AsNoTracking()
                .Where(x => x.DeliveryStatus == deliveryStatus)
                .Select(x => new
                {
                    x.Id,
                    x.TransportOrderNr,
                    TransporterName = x.Shipper != null ? x.Shipper.Name : null,
                })
                .OrderBy(x => x.TransportOrderNr)
                .ToListAsync();

            if (statusRows.Count == 0)
            {
                return new List<TransportOrderOverviewStatusRow>();
            }

            var transportOrderIds = statusRows.Select(x => x.Id).ToList();

            var deliveryRows = await _dbContext.TransportOrderDeliveries
                .AsNoTracking()
                .Where(x => x.TransportOrderId.HasValue && transportOrderIds.Contains(x.TransportOrderId.Value))
                .Select(x => new
                {
                    TransportOrderId = x.TransportOrderId!.Value,
                    SupplierCity = x.SupplierOrder != null ? x.SupplierOrder.PostalAddress : null,
                    CustomerCity = x.SupplierOrder != null ? x.SupplierOrder.DeliveryPostalAddress : null,
                })
                .ToListAsync();

            return statusRows
                .Select(order =>
                {
                    var relatedRows = deliveryRows.Where(x => x.TransportOrderId == order.Id);

                    var supplierCities = relatedRows
                        .Select(x => x.SupplierCity)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase);

                    var customerCities = relatedRows
                        .Select(x => x.CustomerCity)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase);

                    return new TransportOrderOverviewStatusRow
                    {
                        Id = order.Id,
                        TransportOrderNr = order.TransportOrderNr,
                        TransporterName = order.TransporterName ?? string.Empty,
                        LoadingCities = string.Join(", ", supplierCities),
                        UnloadingCities = string.Join(", ", customerCities),
                    };
                })
                .ToList();
        }

        private sealed class TransportOrderOverviewStatusRow
        {
            public int Id { get; set; }
            public int TransportOrderNr { get; set; }
            public string TransporterName { get; set; } = string.Empty;
            public string LoadingCities { get; set; } = string.Empty;
            public string UnloadingCities { get; set; } = string.Empty;
        }

        private static string FormatDateInfo(DateTime? date, bool weekMode, string? fallback)
        {
            if (!date.HasValue)
            {
                return fallback ?? string.Empty;
            }

            if (weekMode)
            {
                return $"v. {ISOWeek.GetWeekOfYear(date.Value)}";
            }

            return date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static int ParseOrderNr(string? orderNr)
        {
            if (int.TryParse(orderNr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return int.MaxValue;
        }
    }
}
