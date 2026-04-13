using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class ReportingController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReportingController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public ActionResult<object> GetAvailableReports()
        {
            return Ok(new
            {
                reports = new[] { "inventory" }
            });
        }

        [HttpPost("inventory")]
        public async Task<ActionResult<InventoryReportResponseDto>> GetInventoryReport([FromBody] InventoryReportRequestDto? request)
        {
            var calculationDate = request?.CalculationDate?.Date ?? DateTime.UtcNow.Date;
            var calcDatePlusOne = calculationDate.AddDays(1);
            var inventoryIds = request?.InventoryIds ?? new List<int>();

            try
            {
                // Query SupplierOrders with filtering logic from legacy code
                var query = (from supplierOrder in _context.SupplierOrders
                             join inventory in _context.Inventories on supplierOrder.InventoryId equals inventory.Id into inventoryJoin
                             from inventory in inventoryJoin.DefaultIfEmpty()
                             where
                             (inventoryIds.Count == 0 || supplierOrder.InventoryId == inventoryIds.First() || (supplierOrder.InventoryId.HasValue && inventoryIds.Contains(supplierOrder.InventoryId.Value))) &&
                             inventory != null && inventory.IsInventory &&
                             _context.DeliveryToStocks.Where(d => d.SupplierOrderId == supplierOrder.Id && d.DeliveryStatus == 2).Sum(d => d.NrOfItems) > 0
                             orderby supplierOrder.Id
                             select new
                             {
                                 SupplierOrder = supplierOrder,
                                 Inventory = inventory,
                                 LastStockTaking = _context.StockTakingItems
                                     .Where(st => st.SupplierOrderId == supplierOrder.Id && st.StockTaking != null && st.StockTaking.StockTakingDate < calcDatePlusOne)
                                     .OrderByDescending(st => st.StockTaking!.StockTakingDate)
                                     .FirstOrDefault(),
                                 DeliveredItemsToStock = _context.DeliveryToStocks
                                     .Where(d => d.SupplierOrderId == supplierOrder.Id && d.DeliveryStatus == 2 && d.DeliveryDate < calcDatePlusOne)
                                     .Sum(d => d.NrOfItems),
                                 DeliveredItemsFromStock = _context.DeliveryFromStocks
                                     .Where(d => d.CustomerOrderId != null &&
                                                _context.CustomerOrders.Where(co => co.Id == d.CustomerOrderId && co.SupplierOrderId == supplierOrder.Id).Any() &&
                                                d.DeliveryStatus == 2 && d.DeliveryDate < calcDatePlusOne)
                                     .Sum(d => d.NrOfItems),
                                 DeliveredPalletsToStock = _context.DeliveryToStocks
                                     .Where(d => d.SupplierOrderId == supplierOrder.Id && d.DeliveryStatus == 2 && d.DeliveryDate < calcDatePlusOne)
                                     .Sum(d => d.NrOfPallets),
                                 DeliveredPalletsFromStock = _context.DeliveryFromStocks
                                     .Where(d => d.CustomerOrderId != null &&
                                                _context.CustomerOrders.Where(co => co.Id == d.CustomerOrderId && co.SupplierOrderId == supplierOrder.Id).Any() &&
                                                d.DeliveryStatus == 2 && d.DeliveryDate < calcDatePlusOne)
                                     .Sum(d => d.NrOfPallets),
                                 CustomerOrder = _context.CustomerOrders
                                     .Where(co => co.SupplierOrderId == supplierOrder.Id)
                                     .FirstOrDefault()
                             }).AsNoTracking();

                var rows = new List<InventoryReportRowDto>();
                decimal totalValue = 0;

                foreach (var item in await query.ToListAsync())
                {
                    // Calculate current inventory levels
                    double? currentItems = (item.LastStockTaking?.NrOfItems ?? 0) + (item.DeliveredItemsToStock ?? 0) - (item.DeliveredItemsFromStock ?? 0);
                    int currentPallets = (item.LastStockTaking?.NrOfPallets ?? 0) + (item.DeliveredPalletsToStock ?? 0) - (item.DeliveredPalletsFromStock ?? 0);

                    // Skip rows with no inventory
                    if (currentItems <= 0)
                        continue;

                    // Calculate sales value
                    decimal salesValue = 0;
                    if (item.SupplierOrder.PurchasePrice.HasValue && item.SupplierOrder.PurchaseCurrencyRate.HasValue && item.CustomerOrder != null)
                    {
                        double purchasePrice = item.SupplierOrder.PurchasePrice.Value;
                        double currencyRate = item.SupplierOrder.PurchaseCurrencyRate.Value;
                        salesValue = (decimal)(currentItems.GetValueOrDefault() * purchasePrice * currencyRate);
                    }

                    var row = new InventoryReportRowDto
                    {
                        SupplierOrderId = item.SupplierOrder.Id,
                        CustomerName = item.CustomerOrder?.CustomerName ?? string.Empty,
                        ProductName = item.SupplierOrder.Product ?? string.Empty,
                        InventoryId = item.Inventory?.Id ?? 0,
                        InventoryName = item.Inventory?.Name ?? string.Empty,
                        NrOfItems = item.SupplierOrder.Edition ?? 0,
                        LastInventoryDate = item.LastStockTaking?.StockTaking?.StockTakingDate,
                        CurrentInventoryLevel = currentItems.GetValueOrDefault(),
                        CurrentNrOfPallets = currentPallets,
                        TotalSalesValue = salesValue
                    };

                    rows.Add(row);
                    totalValue += salesValue;
                }

                return Ok(new InventoryReportResponseDto
                {
                    CalculationDate = calculationDate,
                    CurrentInventoryValue = totalValue,
                    Rows = rows.OrderBy(r => r.SupplierOrderId).ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to generate inventory report", details = ex.Message });
            }
        }
    }
}