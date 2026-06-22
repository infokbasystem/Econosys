using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

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
                reports = new[] { "inventory", "slowmovers", "order-costs-monthly", "selectable-years", "revenue-per-order", "non-delivered-warehouse-orders", "packaging-report" }
            });
        }

        [HttpPost("packaging-report")]
        public async Task<ActionResult<PackagingReportResponseDto>> GetPackagingReport([FromBody] PackagingReportRequestDto? request)
        {
            if (request?.StartDate == null || request?.EndDate == null)
            {
                return BadRequest(new
                {
                    error = "StartDate and EndDate are required."
                });
            }

            var startDate = request.StartDate.Value.Date;
            var endDate = request.EndDate.Value.Date;

            if (endDate < startDate)
            {
                endDate = startDate;
            }

            var endDateExclusive = endDate.AddDays(1);
            var customerId = request.CustomerId;

            var filteredInvoiceRows = _context.InvoiceRows
                .AsNoTracking()
                .Where(invoiceRow =>
                    invoiceRow.Invoice != null
                    && invoiceRow.Invoice.InvoiceDate.HasValue
                    && invoiceRow.Invoice.InvoiceDate.Value >= startDate
                    && invoiceRow.Invoice.InvoiceDate.Value < endDateExclusive
                    && (!customerId.HasValue
                        || (invoiceRow.Invoice.CustomerId.HasValue && invoiceRow.Invoice.CustomerId.Value == customerId.Value)));

            var deliveryToCustomerBaseRows = await _context.DeliveryToCustomers
                .AsNoTracking()
                .Where(delivery =>
                    delivery.CustomerOrderId.HasValue
                    && filteredInvoiceRows.Any(invoiceRow => invoiceRow.DeliveryToCustomerId == delivery.Id))
                .Join(
                    _context.CustomerOrders.AsNoTracking(),
                    delivery => delivery.CustomerOrderId!.Value,
                    customerOrder => customerOrder.Id,
                    (delivery, customerOrder) => new
                    {
                        CustomerName = customerOrder.CustomerName,
                        FallbackProductName = customerOrder.Product,
                        customerOrder.CalculationId,
                        DeliveredCount = delivery.NrOfItems ?? 0,
                    })
                .ToListAsync();

            var deliveryFromStockBaseRows = await _context.DeliveryFromStocks
                .AsNoTracking()
                .Where(delivery =>
                    delivery.CustomerOrderId.HasValue
                    && filteredInvoiceRows.Any(invoiceRow => invoiceRow.DeliveryFromStockId == delivery.Id))
                .Join(
                    _context.CustomerOrders.AsNoTracking(),
                    delivery => delivery.CustomerOrderId!.Value,
                    customerOrder => customerOrder.Id,
                    (delivery, customerOrder) => new
                    {
                        CustomerName = customerOrder.CustomerName,
                        FallbackProductName = customerOrder.Product,
                        customerOrder.CalculationId,
                        DeliveredCount = delivery.NrOfItems ?? 0,
                    })
                .ToListAsync();

            var baseRows = deliveryToCustomerBaseRows
                .Concat(deliveryFromStockBaseRows)
                .ToList();

            var calculationIds = baseRows
                .Where(x => x.CalculationId.HasValue)
                .Select(x => x.CalculationId!.Value)
                .Distinct()
                .ToList();

            Dictionary<int, (string? ProductName, int? MaterialGroupCode, string? PackagingFeeCategory, int? NetWeightPer1000)> productAndMaterialByCalculationId = calculationIds.Count == 0
                ? new Dictionary<int, (string? ProductName, int? MaterialGroupCode, string? PackagingFeeCategory, int? NetWeightPer1000)>()
                : await (
                    from calculation in _context.Calculations.AsNoTracking()
                    where calculationIds.Contains(calculation.Id)
                    join product in _context.Products.AsNoTracking()
                        on calculation.ProductId equals (int?)product.Id into products
                    from product in products.DefaultIfEmpty()
                    join material in _context.Materials.AsNoTracking()
                        on product.MaterialId equals (int?)material.Id into materials
                    from material in materials.DefaultIfEmpty()
                    join packagingFeeCategory in _context.PackagingFeeCategories.AsNoTracking()
                        on material.PackagingFeeCategoryId equals (int?)packagingFeeCategory.Id into packagingFeeCategories
                    from packagingFeeCategory in packagingFeeCategories.DefaultIfEmpty()
                    select new
                    {
                        calculation.Id,
                        ProductName = product != null ? product.Name : null,
                        MaterialGroupCode = material != null ? material.MaterialGroup : null,
                        PackagingFeeCategory = packagingFeeCategory != null
                            ? (packagingFeeCategory.CategoryCode + " - " + packagingFeeCategory.Description)
                            : null,
                        NetWeightPer1000 = product != null ? product.NetWeightPer1000 : null,
                    })
                    .ToDictionaryAsync(
                        x => x.Id,
                        x => (
                            ProductName: (string?)x.ProductName,
                            MaterialGroupCode: x.MaterialGroupCode,
                            PackagingFeeCategory: (string?)x.PackagingFeeCategory,
                            NetWeightPer1000: x.NetWeightPer1000));

            var rows = baseRows
                .Select(x =>
                {
                    var productAndMaterial = (ProductName: (string?)null, MaterialGroupCode: (int?)null, PackagingFeeCategory: (string?)null, NetWeightPer1000: (int?)null);

                    if (x.CalculationId.HasValue)
                    {
                        productAndMaterialByCalculationId.TryGetValue(x.CalculationId.Value, out productAndMaterial);
                    }

                    var productName = !string.IsNullOrWhiteSpace(productAndMaterial.ProductName)
                        ? productAndMaterial.ProductName!
                        : (x.FallbackProductName ?? "Okänd produkt");

                    var materialGroup = MapPackagingMaterialGroup(productAndMaterial.MaterialGroupCode);
                    var packagingFeeCategory = productAndMaterial.PackagingFeeCategory ?? string.Empty;
                    var totalWeight = x.DeliveredCount * (productAndMaterial.NetWeightPer1000.GetValueOrDefault() / 1000d);

                    return new
                    {
                        CustomerName = x.CustomerName ?? "Okänd kund",
                        ProductName = productName,
                        MaterialGroup = materialGroup,
                        PackagingFeeCategory = packagingFeeCategory,
                        DeliveredCount = x.DeliveredCount,
                        TotalWeight = totalWeight,
                    };
                })
                .GroupBy(row => new
                {
                    row.CustomerName,
                    row.ProductName,
                    row.MaterialGroup,
                    row.PackagingFeeCategory,
                })
                .Select(group => new PackagingReportRowDto
                {
                    CustomerName = group.Key.CustomerName,
                    ProductName = group.Key.ProductName,
                    MaterialGroup = group.Key.MaterialGroup,
                    PackagingFeeCategory = group.Key.PackagingFeeCategory,
                    TotalDeliveredCount = group.Sum(x => x.DeliveredCount),
                    TotalWeight = group.Sum(x => x.TotalWeight),
                })
                .OrderBy(row => row.CustomerName)
                .ThenBy(row => row.ProductName)
                .ThenBy(row => row.MaterialGroup)
                .ToList();

            return Ok(new PackagingReportResponseDto
            {
                StartDate = startDate,
                EndDate = endDate,
                Rows = rows,
            });
        }

        private static PackagingMaterialGroupDto? MapPackagingMaterialGroup(int? materialGroupCode)
        {
            return materialGroupCode switch
            {
                1 => PackagingMaterialGroupDto.Well,
                2 => PackagingMaterialGroupDto.PapperKartong,
                3 => PackagingMaterialGroupDto.Plast,
                4 => PackagingMaterialGroupDto.Ovrigt,
                _ => null,
            };
        }

        [HttpGet("revenue-per-order/filter-options")]
        public async Task<ActionResult<RevenuePerOrderFilterOptionsDto>> GetRevenuePerOrderFilterOptions()
        {
            var sellers = await (
                from customerOrder in _context.CustomerOrders.AsNoTracking()
                let sellerId = customerOrder.ResponsibleUserId
                    ?? (customerOrder.Customer != null ? customerOrder.Customer.ResponsibleUserId : null)
                where sellerId.HasValue
                join user in _context.LegacyUsers.AsNoTracking() on sellerId.Value equals user.Id
                select new
                {
                    user.Id,
                    Name = user.Name ?? string.Empty
                })
                .Distinct()
                .OrderBy(x => x.Name)
                .ToListAsync();

            return Ok(new RevenuePerOrderFilterOptionsDto
            {
                Sellers = sellers
                    .Select(x => new FilterOptionDto<int>
                    {
                        Id = x.Id,
                        Name = x.Name,
                    })
                    .ToList()
            });
        }

        [HttpGet("non-invoiced-orders/filter-options")]
        public async Task<ActionResult<NonInvoicedOrderFilterOptionsDto>> GetNonInvoicedOrderFilterOptions()
        {
            var baseQuery = _context.CustomerOrders
                .AsNoTracking()
                .Where(customerOrder =>
                    customerOrder.Created.HasValue && customerOrder.Created.Value.Date >= new DateTime(2017, 1, 1) &&
                    customerOrder.Completed == false &&
                     customerOrder.Edition >
                        customerOrder.DeliveryFromStocks.SelectMany(p => p.InvoiceRows).Sum(p => p.NrOf ?? 0)
                        +
                        customerOrder.DeliveryToCustomers.SelectMany(p => p.InvoiceRows).Sum(p => p.NrOf ?? 0)
                );

            var optionRows = await baseQuery
                .Select(customerOrder => new
                {
                    SellerId = customerOrder.ResponsibleUserId
                        ?? (customerOrder.Customer != null ? customerOrder.Customer.ResponsibleUserId : null),
                    Seller = customerOrder.ResponsibleUser != null
                        ? customerOrder.ResponsibleUser.Name ?? string.Empty
                        : (customerOrder.Customer != null && customerOrder.Customer.ResponsibleUser != null
                            ? customerOrder.Customer.ResponsibleUser.Name ?? string.Empty
                            : string.Empty),
                    OurReferenceId = customerOrder.OurReference ?? string.Empty,
                    OurReferenceName = customerOrder.OurReference ?? string.Empty,
                    CustomerId = customerOrder.CustomerId,
                    CustomerName = customerOrder.CustomerName ?? string.Empty,
                    SupplierId = customerOrder.SupplierOrder != null
                        ? customerOrder.SupplierOrder.SupplierId
                        : null,
                    SupplierName = customerOrder.SupplierOrder != null
                        ? customerOrder.SupplierOrder.SupplierName ?? string.Empty
                        : string.Empty,
                })
                .ToListAsync();

            var sellers = optionRows
                .Where(x => x.SellerId.HasValue && !string.IsNullOrWhiteSpace(x.Seller))
                .GroupBy(x => x.SellerId!.Value)
                .Select(g => new FilterOptionDto<int>
                {
                    Id = g.Key,
                    Name = g.Select(x => x.Seller).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty,
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .OrderBy(x => x.Name)
                .ToList();

            var ourReferences = optionRows
                .Where(x => !string.IsNullOrWhiteSpace(x.OurReferenceId))
                .GroupBy(x => x.OurReferenceId)
                .Select(g => new FilterOptionDto<string>
                {
                    Id = g.Key,
                    Name = g.Select(x => x.OurReferenceName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? g.Key,
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .OrderBy(x => x.Name)
                .ToList();

            var customers = optionRows
                .Where(x => x.CustomerId.HasValue && !string.IsNullOrWhiteSpace(x.CustomerName))
                .GroupBy(x => x.CustomerId!.Value)
                .Select(g => new FilterOptionDto<int>
                {
                    Id = g.Key,
                    Name = g.Select(x => x.CustomerName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty,
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .OrderBy(x => x.Name)
                .ToList();

            var suppliers = optionRows
                .Where(x => x.SupplierId.HasValue && !string.IsNullOrWhiteSpace(x.SupplierName))
                .GroupBy(x => x.SupplierId!.Value)
                .Select(g => new FilterOptionDto<int>
                {
                    Id = g.Key,
                    Name = g.Select(x => x.SupplierName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty,
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .OrderBy(x => x.Name)
                .ToList();

            return Ok(new NonInvoicedOrderFilterOptionsDto
            {
                Sellers = sellers,
                OurReferences = ourReferences,
                Customers = customers,
                Suppliers = suppliers,
            });
        }

        [HttpPost("revenue-per-order")]
        public async Task<ActionResult<PagedResultDto<RevenuePerOrderReportRowDto>>> GetRevenuePerOrderReport([FromBody] RevenuePerOrderReportRequestDto? request)
        {
            var pagination = request?.Pagination ?? new PaginationRequest();
            var startDate = request?.StartDate?.Date;
            var endDateExclusive = request?.EndDate?.Date.AddDays(1);
            var sellerId = request?.SellerId;
            var orderNumber = request?.OrderNumber?.Trim();
            var productName = request?.ProductName?.Trim();
            var supplierName = request?.SupplierName?.Trim();
            var customerName = request?.CustomerName?.Trim();

            var baseQuery = _context.CustomerOrders
                .AsNoTracking()
                .Where(x => x.SupplierOrderId.HasValue);

            var rowsQuery = baseQuery
                .Select(customerOrder => new
                {
                    customerOrder,
                    supplierOrder = customerOrder.SupplierOrder,
                    SellerId = customerOrder.ResponsibleUserId
                        ?? (customerOrder.Customer != null ? customerOrder.Customer.ResponsibleUserId : null),
                    SellerDisplay = customerOrder.ResponsibleUser != null
                        ? customerOrder.ResponsibleUser.Name ?? string.Empty
                        : (customerOrder.Customer != null && customerOrder.Customer.ResponsibleUser != null
                            ? customerOrder.Customer.ResponsibleUser.Name ?? string.Empty
                            : string.Empty),
                    CreatedAt = customerOrder.Created,
                    ProductDisplay = customerOrder.Product
                        ?? string.Empty,
                    ConstructionDisplay = customerOrder.Construction
                        ?? string.Empty,
                    MaterialDisplay = customerOrder.Material
                        ?? string.Empty,
                    FormatDisplay = customerOrder.Format
                        ?? string.Empty,
                    SupplierDisplay = customerOrder.SupplierOrder != null
                        ? customerOrder.SupplierOrder.SupplierName ?? string.Empty
                        : string.Empty,
                    Multiplicator = customerOrder.Unit != null
                        && customerOrder.Unit.Multiplicator.HasValue
                        && customerOrder.Unit.Multiplicator.Value > 0
                            ? customerOrder.Unit.Multiplicator.Value
                            : 1.0,
                    CalculationRowFreight = customerOrder.SelectedCalculationRow != null && customerOrder.SelectedCalculationRow.Freight.HasValue
                        ? (decimal)customerOrder.SelectedCalculationRow.Freight.Value
                        : 0,
                    CalculationRowPackaging = customerOrder.SelectedCalculationRow != null && customerOrder.SelectedCalculationRow.PackagingCost.HasValue
                        ? customerOrder.SelectedCalculationRow.PackagingCost.Value
                        : 0,
                    CalculationRowStorage = customerOrder.SelectedCalculationRow != null && customerOrder.SelectedCalculationRow.Storage.HasValue
                        ? (decimal)customerOrder.SelectedCalculationRow.Storage.Value
                        : 0,
                    CalculationRowOtherCosts = customerOrder.SelectedCalculationRow != null && customerOrder.SelectedCalculationRow.Other.HasValue
                        ? (decimal)customerOrder.SelectedCalculationRow.Other.Value
                        : 0,
                    MarkupBaseCost = (decimal)(customerOrder.SupplierOrder != null ? customerOrder.SupplierOrder.PurchasePrice ?? 0 : 0)
                        + (customerOrder.SelectedCalculationRow != null && customerOrder.SelectedCalculationRow.Freight.HasValue
                            ? (decimal)customerOrder.SelectedCalculationRow.Freight.Value
                            : 0)
                        + (customerOrder.SelectedCalculationRow != null && customerOrder.SelectedCalculationRow.PackagingCost.HasValue
                            ? customerOrder.SelectedCalculationRow.PackagingCost.Value
                            : 0)
                        + (customerOrder.SelectedCalculationRow != null && customerOrder.SelectedCalculationRow.Storage.HasValue
                            ? (decimal)customerOrder.SelectedCalculationRow.Storage.Value
                            : 0)
                        + (customerOrder.SelectedCalculationRow != null && customerOrder.SelectedCalculationRow.Other.HasValue
                            ? (decimal)customerOrder.SelectedCalculationRow.Other.Value
                            : 0),
                })
                .Select(x => new RevenuePerOrderReportRowDto
                {

                    OrderId = x.customerOrder.Id,
                    OrderNumber = x.customerOrder.CustomerOrderNr ?? x.customerOrder.Id.ToString(),
                    CreatedAt = x.CreatedAt,
                    SellerId = x.SellerId,
                    CustomerName = x.customerOrder.CustomerName
                        ?? string.Empty,
                    ProductName = x.ProductDisplay,
                    Construction = x.ConstructionDisplay,
                    MaterialType = x.MaterialDisplay,
                    Seller = x.SellerDisplay,
                    Format = x.FormatDisplay,
                    SupplierName = x.SupplierDisplay,
                    RevenueSek = (decimal)(x.customerOrder.SalesPrice ?? 0)
                        * (decimal)(x.customerOrder.SalesCurrencyRate ?? 1.0)
                        * (x.customerOrder.Edition ?? 0)
                        / (decimal)x.Multiplicator,
                    TotalTbSek = ((decimal)(x.customerOrder.SalesPrice ?? 0)
                        - (decimal)(x.supplierOrder!.PurchasePrice ?? 0)
                        - x.CalculationRowFreight
                        - x.CalculationRowPackaging
                        - x.CalculationRowStorage
                        - x.CalculationRowOtherCosts)
                        * (decimal)(x.customerOrder.SalesCurrencyRate ?? 1.0)
                        * (x.customerOrder.Edition ?? 0)
                        / (decimal)x.Multiplicator,
                    MarkupPercent =
                        x.customerOrder.SalesPrice.HasValue && x.MarkupBaseCost > 0
                        ? (((decimal)(x.customerOrder.SalesPrice ?? 0) - x.MarkupBaseCost) / x.MarkupBaseCost) * 100
                        : null
                        ,
                    IsActive = !x.customerOrder.Completed,
                });

            if (startDate.HasValue)
            {
                rowsQuery = rowsQuery.Where(x => x.CreatedAt.HasValue && x.CreatedAt.Value >= startDate.Value);
            }

            if (endDateExclusive.HasValue)
            {
                rowsQuery = rowsQuery.Where(x => x.CreatedAt.HasValue && x.CreatedAt.Value < endDateExclusive.Value);
            }

            if (sellerId.HasValue)
            {
                rowsQuery = rowsQuery.Where(x => x.SellerId == sellerId.Value);
            }

            if (!string.IsNullOrWhiteSpace(orderNumber))
            {
                rowsQuery = rowsQuery.Where(x => EF.Functions.Like(x.OrderNumber, $"%{orderNumber}%"));
            }

            if (!string.IsNullOrWhiteSpace(productName))
            {
                rowsQuery = rowsQuery.Where(x => EF.Functions.Like(x.ProductName, $"%{productName}%"));
            }

            if (!string.IsNullOrWhiteSpace(supplierName))
            {
                rowsQuery = rowsQuery.Where(x => EF.Functions.Like(x.SupplierName, $"%{supplierName}%"));
            }

            if (!string.IsNullOrWhiteSpace(customerName))
            {
                rowsQuery = rowsQuery.Where(x => EF.Functions.Like(x.CustomerName, $"%{customerName}%"));
            }

            var orderedQuery = ApplyRevenuePerOrderOrdering(rowsQuery, request?.OrderBy);
            var totalCount = await rowsQuery.CountAsync();
            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pagination.PageSize);

            var items = await orderedQuery
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            return Ok(new PagedResultDto<RevenuePerOrderReportRowDto>
            {
                Items = items,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
            });
        }

        [HttpPost("non-invoiced-orders")]
        public async Task<ActionResult<PagedResultWithTotalsDto<NonInvoicedOrderReportRowDto, NonInvoicedOrderReportTotalsDto>>> GetNonInvoicedOrdersReport([FromBody] NonInvoicedOrderReportRequestDto? request)
        {
            var pagination = request?.Pagination ?? new PaginationRequest { PageSize = 100 };
            var pageNumber = pagination.PageNumber < 1 ? 1 : pagination.PageNumber;
            var pageSize = pagination.PageSize < 1 ? 100 : Math.Min(pagination.PageSize, 200);

            var sellerId = request?.SellerId;
            var ourReferenceId = request?.OurReferenceId?.Trim();
            var customerId = request?.CustomerId;
            var supplierId = request?.SupplierId;
            var orderBy = request?.OrderBy?.FirstOrDefault();
            var sortField = orderBy?.Field?.Trim().ToLowerInvariant();
            var sortDirection = string.Equals(orderBy?.Direction, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";

            var rowsQuery = _context.CustomerOrders
                .AsNoTracking()
                .Where(customerOrder =>
                    customerOrder.Created.HasValue && customerOrder.Created.Value.Date >= new DateTime(2017, 1, 1) &&
                    customerOrder.Completed == false &&
                     customerOrder.Edition >
                        customerOrder.DeliveryFromStocks.SelectMany(p => p.InvoiceRows).Sum(p => p.NrOf ?? 0)
                        +
                        customerOrder.DeliveryToCustomers.SelectMany(p => p.InvoiceRows).Sum(p => p.NrOf ?? 0)
                )
                .Select(customerOrder => new
                {
                    customerOrder.Id,
                    customerOrder.CustomerOrderNr,
                    SellerId = customerOrder.ResponsibleUserId
                        ?? (customerOrder.Customer != null ? customerOrder.Customer.ResponsibleUserId : null),
                    Seller = customerOrder.ResponsibleUser != null
                        ? customerOrder.ResponsibleUser.Name ?? string.Empty
                        : (customerOrder.Customer != null && customerOrder.Customer.ResponsibleUser != null
                            ? customerOrder.Customer.ResponsibleUser.Name ?? string.Empty
                            : string.Empty),
                    OurReferenceId = customerOrder.OurReference ?? string.Empty,
                    OurReference = customerOrder.OurReference ?? string.Empty,
                    CustomerId = customerOrder.CustomerId,
                    CustomerName = customerOrder.CustomerName ?? string.Empty,
                    SupplierId = customerOrder.SupplierOrder != null
                        ? customerOrder.SupplierOrder.SupplierId
                        : null,
                    SupplierName = customerOrder.SupplierOrder != null
                        ? customerOrder.SupplierOrder.SupplierName ?? string.Empty
                        : string.Empty,
                    ProductName = customerOrder.Product ?? string.Empty,
                    CreatedAt = customerOrder.Created,
                    DeliveryDate = customerOrder.DeliveryDate,
                    DeliveryDateWeekMode = customerOrder.DeliveryDateWeekMode,
                    Edition = customerOrder.Edition ?? 0,
                    NrOfInvoiced = customerOrder.DeliveryFromStocks.SelectMany(p => p.InvoiceRows).Sum(p => p.NrOf ?? 0) + customerOrder.DeliveryToCustomers.SelectMany(p => p.InvoiceRows).Sum(p => p.NrOf ?? 0),
                    IsInventory = customerOrder.SupplierOrder != null && customerOrder.SupplierOrder.InventoryId != null,
                    SalesPrice = customerOrder.SalesPrice,
                    SalesCurrencyRate = customerOrder.SalesCurrencyRate,
                    SupplierPurchasePrice = customerOrder.SupplierOrder != null
                        ? customerOrder.SupplierOrder.PurchasePrice
                        : null,
                    SupplierPurchaseCurrencyRate = customerOrder.SupplierOrder != null
                        ? customerOrder.SupplierOrder.PurchaseCurrencyRate
                        : null,
                    Freight = customerOrder.SelectedCalculationRow != null && customerOrder.SelectedCalculationRow.Freight.HasValue
                        ? (decimal)customerOrder.SelectedCalculationRow.Freight.Value
                        : 0,
                    Packaging = customerOrder.SelectedCalculationRow != null && customerOrder.SelectedCalculationRow.PackagingCost.HasValue
                        ? customerOrder.SelectedCalculationRow.PackagingCost.Value
                        : 0,
                    Storage = customerOrder.SelectedCalculationRow != null && customerOrder.SelectedCalculationRow.Storage.HasValue
                        ? (decimal)customerOrder.SelectedCalculationRow.Storage.Value
                        : 0,
                    Other = customerOrder.SelectedCalculationRow != null && customerOrder.SelectedCalculationRow.Other.HasValue
                        ? (decimal)customerOrder.SelectedCalculationRow.Other.Value
                        : 0,
                    Multiplicator = customerOrder.Unit != null
                        && customerOrder.Unit.Multiplicator.HasValue
                        && customerOrder.Unit.Multiplicator.Value > 0
                            ? customerOrder.Unit.Multiplicator.Value
                            : 1.0,
                });

            var unfilteredRowsQuery = rowsQuery;

            var totals = await unfilteredRowsQuery
                .Select(row => new
                {
                    SalesCurrencyRate = (decimal)(row.SalesCurrencyRate ?? 1.0),
                    PurchaseCurrencyRate = (decimal)(row.SupplierPurchaseCurrencyRate ?? 1.0),
                    row.Edition,
                    row.NrOfInvoiced,
                    UnitDivisor = (decimal)row.Multiplicator,
                    row.SalesPrice,
                    row.SupplierPurchasePrice,
                    row.Freight,
                    row.Storage,
                    row.Other,
                    row.Packaging,
                })
                .GroupBy(_ => 1)
                .Select(group => new NonInvoicedOrderReportTotalsDto
                {
                    OrderValueSek = group.Sum(row => row.SalesPrice.HasValue
                        ? (decimal)row.SalesPrice.Value * row.SalesCurrencyRate * row.Edition / row.UnitDivisor
                        : 0),
                    OrderValueLeftSek = group.Sum(row => row.SalesPrice.HasValue
                        ? (decimal)row.SalesPrice.Value * row.SalesCurrencyRate * (decimal)(row.Edition - row.NrOfInvoiced) / row.UnitDivisor
                        : 0),
                    TotalTbSek = group.Sum(row => row.SalesPrice.HasValue
                        ? (
                            ((decimal)row.SalesPrice.Value * row.SalesCurrencyRate)
                            - ((decimal)(row.SupplierPurchasePrice ?? 0) * row.PurchaseCurrencyRate)
                            - (row.Freight * row.SalesCurrencyRate)
                            - (row.Storage * row.SalesCurrencyRate)
                            - (row.Other * row.SalesCurrencyRate)
                            - (row.Packaging * row.SalesCurrencyRate)
                          ) * row.Edition / row.UnitDivisor
                        : 0),
                    TotalTbLeftSek = group.Sum(row => row.SalesPrice.HasValue
                        ? (
                            ((decimal)row.SalesPrice.Value * row.SalesCurrencyRate)
                            - ((decimal)(row.SupplierPurchasePrice ?? 0) * row.PurchaseCurrencyRate)
                            - (row.Freight * row.SalesCurrencyRate)
                            - (row.Storage * row.SalesCurrencyRate)
                            - (row.Other * row.SalesCurrencyRate)
                            - (row.Packaging * row.SalesCurrencyRate)
                          ) * (decimal)(row.Edition - row.NrOfInvoiced) / row.UnitDivisor
                        : 0),
                })
                .FirstOrDefaultAsync()
                ?? new NonInvoicedOrderReportTotalsDto();

            if (sellerId.HasValue)
            {
                rowsQuery = rowsQuery.Where(row => row.SellerId == sellerId.Value);
            }

            if (!string.IsNullOrWhiteSpace(ourReferenceId))
            {
                rowsQuery = rowsQuery.Where(row => row.OurReferenceId == ourReferenceId);
            }

            if (customerId.HasValue)
            {
                rowsQuery = rowsQuery.Where(row => row.CustomerId == customerId.Value);
            }

            if (supplierId.HasValue)
            {
                rowsQuery = rowsQuery.Where(row => row.SupplierId == supplierId.Value);
            }

            rowsQuery = (sortField, sortDirection) switch
            {
                ("id", "desc") => rowsQuery.OrderByDescending(row => row.Id),
                ("id", _) => rowsQuery.OrderBy(row => row.Id),
                ("seller", "desc") => rowsQuery.OrderByDescending(row => row.Seller).ThenByDescending(row => row.Id),
                ("seller", _) => rowsQuery.OrderBy(row => row.Seller).ThenBy(row => row.Id),
                ("ourreference", "desc") => rowsQuery.OrderByDescending(row => row.OurReference).ThenByDescending(row => row.Id),
                ("ourreference", _) => rowsQuery.OrderBy(row => row.OurReference).ThenBy(row => row.Id),
                ("customername", "desc") => rowsQuery.OrderByDescending(row => row.CustomerName).ThenByDescending(row => row.Id),
                ("customername", _) => rowsQuery.OrderBy(row => row.CustomerName).ThenBy(row => row.Id),
                ("suppliername", "desc") => rowsQuery.OrderByDescending(row => row.SupplierName).ThenByDescending(row => row.Id),
                ("suppliername", _) => rowsQuery.OrderBy(row => row.SupplierName).ThenBy(row => row.Id),
                ("productname", "desc") => rowsQuery.OrderByDescending(row => row.ProductName).ThenByDescending(row => row.Id),
                ("productname", _) => rowsQuery.OrderBy(row => row.ProductName).ThenBy(row => row.Id),
                ("deliverydate", "desc") => rowsQuery.OrderByDescending(row => row.DeliveryDate).ThenByDescending(row => row.Id),
                ("deliverydate", _) => rowsQuery.OrderBy(row => row.DeliveryDate).ThenBy(row => row.Id),
                ("edition", "desc") => rowsQuery.OrderByDescending(row => row.Edition).ThenByDescending(row => row.Id),
                ("edition", _) => rowsQuery.OrderBy(row => row.Edition).ThenBy(row => row.Id),
                _ when sortDirection == "asc" => rowsQuery.OrderBy(row => row.CreatedAt).ThenBy(row => row.Id),
                _ => rowsQuery.OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id),
            };

            var totalCount = await rowsQuery.CountAsync();
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

            var pageRows = await rowsQuery
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = pageRows.Select(row =>
            {
                var salesCurrencyRate = (decimal)(row.SalesCurrencyRate ?? 1.0);
                var purchaseCurrencyRate = (decimal)(row.SupplierPurchaseCurrencyRate ?? 1.0);
                var baseCost = (decimal)(row.SupplierPurchasePrice ?? 0)
                    + row.Freight
                    + row.Packaging
                    + row.Storage
                    + row.Other;
                decimal? markupPercent = row.SalesPrice.HasValue && baseCost > 0
                    ? (((decimal)row.SalesPrice.Value - baseCost) / baseCost) * 100
                    : (decimal?)null;
                var unitDivisor = (decimal)row.Multiplicator;
                var orderValueSek = row.SalesPrice.HasValue
                    ? (decimal)row.SalesPrice.Value * salesCurrencyRate * row.Edition / unitDivisor
                    : 0;
                var orderValueLeftSek = row.SalesPrice.HasValue
                    ? (decimal)row.SalesPrice.Value * salesCurrencyRate * (decimal)(row.Edition - row.NrOfInvoiced) / unitDivisor
                    : 0;
                var tbPerUnit = row.SalesPrice.HasValue
                    ? ((decimal)row.SalesPrice.Value * salesCurrencyRate)
                        - ((decimal)(row.SupplierPurchasePrice ?? 0) * purchaseCurrencyRate)
                        - (row.Freight * salesCurrencyRate)
                        - (row.Storage * salesCurrencyRate)
                        - (row.Other * salesCurrencyRate)
                        - (row.Packaging * salesCurrencyRate)
                    : 0;

                return new NonInvoicedOrderReportRowDto
                {
                    Id = row.Id,
                    CustomerOrderNr = row.CustomerOrderNr ?? string.Empty,
                    Seller = row.Seller,
                    OurReference = row.OurReference,
                    CustomerName = row.CustomerName,
                    SupplierName = row.SupplierName,
                    ProductName = row.ProductName,
                    CreatedAt = row.CreatedAt,
                    DeliveryDate = row.DeliveryDate,
                    DeliveryDateWeekMode = row.DeliveryDateWeekMode,
                    IsInventory = row.IsInventory,
                    Edition = row.Edition,
                    EditionLeft = (int)row.Edition - (int)row.NrOfInvoiced,
                    OrderValueSek = orderValueSek,
                    OrderValueLeftSek = orderValueLeftSek,
                    MarkupPercent = markupPercent,
                    TotalTbSek = tbPerUnit * row.Edition / unitDivisor,
                    TotalTbLeftSek = tbPerUnit * (decimal)(row.Edition - row.NrOfInvoiced) / unitDivisor,
                };
            }).ToList();

            return Ok(new PagedResultWithTotalsDto<NonInvoicedOrderReportRowDto, NonInvoicedOrderReportTotalsDto>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                Totals = new TotalsDto<NonInvoicedOrderReportTotalsDto>
                {
                    Values = totals,
                },
            });
        }

        [HttpPost("non-delivered-warehouse-orders")]
        public async Task<ActionResult<PagedResultDto<NonDeliveredWarehouseOrdersReportRowDto>>> GetNonDeliveredWarehouseOrdersReport([FromBody] NonDeliveredWarehouseOrdersReportRequestDto? request)
        {
            var deliveredStatus = (int)DeliveryStatus.Delivered;
            var pagination = request?.Pagination ?? new PaginationRequest { PageSize = 100 };
            var pageNumber = pagination.PageNumber < 1 ? 1 : pagination.PageNumber;
            var pageSize = pagination.PageSize < 1 ? 100 : Math.Min(pagination.PageSize, 200);
            var startDate = request?.StartDate?.Date;
            var endDateExclusive = request?.EndDate?.Date.AddDays(1);
            var orderBy = request?.OrderBy?.FirstOrDefault();
            var sortField = orderBy?.Field?.Trim().ToLowerInvariant();
            var sortDirection = string.Equals(orderBy?.Direction, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";

            var rowsQuery = _context.CustomerOrders
                .AsNoTracking()
                .Where(customerOrder =>
                    customerOrder.SupplierOrderId.HasValue
                    && customerOrder.SupplierOrder != null
                    && customerOrder.SupplierOrder.Inventory != null
                    && customerOrder.SupplierOrder.Inventory.IsInventory)
                .Select(customerOrder => new
                {
                    customerOrder,
                    customerOrder.Id,
                    customerOrder.CustomerOrderNr,
                    ProductName = customerOrder.Product ?? string.Empty,
                    CustomerName = customerOrder.CustomerName ?? string.Empty,
                    SupplierName = customerOrder.SupplierOrder != null
                        ? customerOrder.SupplierOrder.SupplierName ?? string.Empty
                        : string.Empty,
                    SupplierOrderCreatedAt = customerOrder.SupplierOrder != null
                        ? customerOrder.SupplierOrder.Created
                        : null,
                    SupplierOrderDeliveryDate = customerOrder.SupplierOrder != null
                        ? customerOrder.SupplierOrder.DeliveryDate
                        : null,
                    SupplierOrderDeliveryDateWeekMode = customerOrder.SupplierOrder != null
                        && customerOrder.SupplierOrder.DeliveryDateWeekMode,
                    Edition = customerOrder.Edition ?? 0,
                    ExpectedDeliveryQuantity = customerOrder.SupplierOrder != null
                        ? customerOrder.SupplierOrder.ProducedEdition
                            ?? customerOrder.SupplierOrder.Edition
                            ?? customerOrder.Edition
                            ?? 0
                        : 0,
                    DeliveredToStockQuantity = customerOrder.SupplierOrder != null
                        ? customerOrder.SupplierOrder.DeliveryToStocks
                            .Where(delivery => delivery.DeliveryStatus == deliveredStatus)
                            .Sum(delivery => delivery.NrOfItems ?? 0)
                        : 0,
                })
                .Where(row => row.DeliveredToStockQuantity < row.ExpectedDeliveryQuantity || (row.ExpectedDeliveryQuantity <= 0 && row.DeliveredToStockQuantity <= 0));

            if (startDate.HasValue)
            {
                rowsQuery = rowsQuery.Where(row => row.customerOrder.DeliveryDate.HasValue && row.customerOrder.DeliveryDate.Value >= startDate.Value);
            }

            if (endDateExclusive.HasValue)
            {
                rowsQuery = rowsQuery.Where(row => row.customerOrder.DeliveryDate.HasValue && row.customerOrder.DeliveryDate.Value < endDateExclusive.Value);
            }

            rowsQuery = (sortField, sortDirection) switch
            {
                ("ordernumber", "desc") => rowsQuery.OrderByDescending(row => row.CustomerOrderNr).ThenByDescending(row => row.Id),
                ("ordernumber", _) => rowsQuery.OrderBy(row => row.CustomerOrderNr).ThenBy(row => row.Id),
                ("productname", "desc") => rowsQuery.OrderByDescending(row => row.ProductName).ThenByDescending(row => row.Id),
                ("productname", _) => rowsQuery.OrderBy(row => row.ProductName).ThenBy(row => row.Id),
                ("customername", "desc") => rowsQuery.OrderByDescending(row => row.CustomerName).ThenByDescending(row => row.Id),
                ("customername", _) => rowsQuery.OrderBy(row => row.CustomerName).ThenBy(row => row.Id),
                ("suppliername", "desc") => rowsQuery.OrderByDescending(row => row.SupplierName).ThenByDescending(row => row.Id),
                ("suppliername", _) => rowsQuery.OrderBy(row => row.SupplierName).ThenBy(row => row.Id),
                ("supplierordercreatedat", "desc") or ("created", "desc") => rowsQuery.OrderByDescending(row => row.SupplierOrderCreatedAt).ThenByDescending(row => row.Id),
                ("supplierordercreatedat", _) or ("created", _) => rowsQuery.OrderBy(row => row.SupplierOrderCreatedAt).ThenBy(row => row.Id),
                ("supplierorderdeliverydate", "desc") or ("deliverydate", "desc") => rowsQuery.OrderByDescending(row => row.SupplierOrderDeliveryDate).ThenByDescending(row => row.Id),
                ("supplierorderdeliverydate", _) or ("deliverydate", _) => rowsQuery.OrderBy(row => row.SupplierOrderDeliveryDate).ThenBy(row => row.Id),
                ("edition", "desc") => rowsQuery.OrderByDescending(row => row.Edition).ThenByDescending(row => row.Id),
                ("edition", _) => rowsQuery.OrderBy(row => row.Edition).ThenBy(row => row.Id),
                _ when sortDirection == "asc" => rowsQuery.OrderBy(row => row.SupplierOrderCreatedAt).ThenBy(row => row.Id),
                _ => rowsQuery.OrderByDescending(row => row.SupplierOrderCreatedAt).ThenByDescending(row => row.Id),
            };

            var totalCount = await rowsQuery.CountAsync();
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await rowsQuery
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(row => new NonDeliveredWarehouseOrdersReportRowDto
                {
                    Id = row.Id,
                    OrderNumber = row.CustomerOrderNr ?? row.Id.ToString(),
                    ProductName = row.ProductName,
                    CustomerName = row.CustomerName,
                    SupplierName = row.SupplierName,
                    SupplierOrderCreatedAt = row.SupplierOrderCreatedAt,
                    SupplierOrderDeliveryDate = row.SupplierOrderDeliveryDate,
                    SupplierOrderDeliveryDateWeekMode = row.SupplierOrderDeliveryDateWeekMode,
                    Edition = row.Edition,
                })
                .ToListAsync();

            return Ok(new PagedResultDto<NonDeliveredWarehouseOrdersReportRowDto>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
            });
        }

        [HttpGet("slowmovers")]
        public async Task<ActionResult<SlowMoversReportResponseDto>> GetSlowMoversReport([FromQuery] int? inventoryId)
        {
            var today = SwedishTime.Now.Date;
            var deliveredStatus = (int)DeliveryStatus.Delivered;

            var baseRows = await _context.CustomerOrders
                .AsNoTracking()
                .Include(co => co.SelectedCalculationRow)
                .Where(customerOrder =>
                    customerOrder.SupplierOrderId.HasValue
                    && customerOrder.SupplierOrder != null
                    && customerOrder.SupplierOrder.Inventory != null
                    && customerOrder.SupplierOrder.Inventory.IsInventory
                    && !customerOrder.Completed
                    && customerOrder.DeliveryDate.HasValue
                    && (!inventoryId.HasValue
                        || customerOrder.SupplierOrder.Inventory.Id == inventoryId.Value))
                .Select(customerOrder => new
                {
                    CustomerOrderId = customerOrder.Id,
                    CustomerOrderNr = customerOrder.CustomerOrderNr,
                    Customer = customerOrder.CustomerName,
                    CustomerProduct = customerOrder.Product,
                    CustomerOrderDate = customerOrder.Date,
                    CustomerDeliveryDate = customerOrder.DeliveryDate,
                    NrOfStorageMonths = customerOrder.SelectedCalculationRow != null
                        ? customerOrder.SelectedCalculationRow.NrOfStorageMonths
                        : null,
                    SupplierOrderId = customerOrder.SupplierOrder!.Id,
                    SupplierProduct = customerOrder.SupplierOrder.Product,
                    SupplierDeliveryDate = customerOrder.SupplierOrder.DeliveryDate,
                    SupplierConfirmedDeliveryDate = customerOrder.SupplierOrder.ConfirmedDeliveryDate,
                    InventoryId = customerOrder.SupplierOrder.Inventory!.Id,
                    InventoryName = customerOrder.SupplierOrder.Inventory.Name,
                })
                .ToListAsync();

            if (baseRows.Count == 0)
            {
                return Ok(new SlowMoversReportResponseDto
                {
                    Rows = Array.Empty<SlowMoversReportRowDto>()
                });
            }

            var supplierOrderIds = baseRows.Select(x => x.SupplierOrderId).Distinct().ToList();
            var customerOrderIds = baseRows.Select(x => x.CustomerOrderId).Distinct().ToList();

            var deliveriesToStock = await _context.DeliveryToStocks
                .AsNoTracking()
                .Where(delivery =>
                    delivery.SupplierOrderId.HasValue
                    && supplierOrderIds.Contains(delivery.SupplierOrderId.Value)
                    && delivery.DeliveryStatus == deliveredStatus)
                .GroupBy(delivery => delivery.SupplierOrderId!.Value)
                .Select(group => new
                {
                    SupplierOrderId = group.Key,
                    DeliveredToStock = group.Sum(x => x.NrOfItems ?? 0),
                    LatestDeliveryNr = group
                        .OrderByDescending(x => x.DeliveryDate)
                        .ThenByDescending(x => x.Id)
                        .Select(x => x.DeliveryNr)
                        .FirstOrDefault(),
                })
                .ToDictionaryAsync(x => x.SupplierOrderId);

            var deliveriesFromStock = await _context.DeliveryFromStocks
                .AsNoTracking()
                .Where(delivery =>
                    delivery.CustomerOrderId.HasValue
                    && customerOrderIds.Contains(delivery.CustomerOrderId.Value)
                    && delivery.DeliveryStatus == deliveredStatus)
                .GroupBy(delivery => delivery.CustomerOrderId!.Value)
                .Select(group => new
                {
                    CustomerOrderId = group.Key,
                    DeliveredFromStock = group.Sum(x => x.NrOfItems ?? 0),
                })
                .ToDictionaryAsync(x => x.CustomerOrderId);

            var rows = baseRows
                .Where(row =>
                {
                    if (!row.NrOfStorageMonths.HasValue) return false;

                    var storageDeadline = row.CustomerDeliveryDate!.Value.Date
                        .AddMonths((int)row.NrOfStorageMonths.Value);
                    if (today <= storageDeadline) return false;

                    var deliveredToStock = deliveriesToStock.TryGetValue(row.SupplierOrderId, out var toStock)
                        ? toStock.DeliveredToStock
                        : 0;
                    var deliveredFromStock = deliveriesFromStock.TryGetValue(row.CustomerOrderId, out var fromStock)
                        ? fromStock.DeliveredFromStock
                        : 0;

                    return deliveredToStock > deliveredFromStock;
                })
                .Select(row =>
                {
                    var storageDeadline = row.CustomerDeliveryDate!.Value.Date
                        .AddMonths((int)row.NrOfStorageMonths!.Value);
                    var daysOver = (today - storageDeadline).Days;

                    var latestDeliveryNr = deliveriesToStock.TryGetValue(row.SupplierOrderId, out var toStock)
                        ? toStock.LatestDeliveryNr
                        : null;

                    var orderedDeliveryTime =
                        row.SupplierConfirmedDeliveryDate
                        ?? row.SupplierDeliveryDate
                        ?? row.CustomerOrderDate
                        ?? row.CustomerDeliveryDate!.Value;

                    return new SlowMoversReportRowDto
                    {
                        InventoryId = row.InventoryId,
                        DeliveryNr = latestDeliveryNr ?? string.Empty,
                        CustomerOrderNr = row.CustomerOrderNr ?? string.Empty,
                        Customer = row.Customer ?? string.Empty,
                        Product = row.CustomerProduct ?? row.SupplierProduct ?? string.Empty,
                        InventoryName = row.InventoryName ?? string.Empty,
                        OrderedDeliveryTime = orderedDeliveryTime,
                        StorageDeadline = storageDeadline,
                        DaysOverStorageDeadline = daysOver,
                    };
                })
                .OrderByDescending(x => x.DaysOverStorageDeadline)
                .ThenBy(x => x.DeliveryNr)
                .ToList();

            return Ok(new SlowMoversReportResponseDto
            {
                Rows = rows
            });
        }

        [HttpGet("selectable-years")]
        public async Task<ActionResult<ReportingSelectableYearsResponseDto>> GetSelectableYears([FromQuery] string? itemType, [FromQuery] string? dateField)
        {
            var normalizedItemType = NormalizeItemType(itemType);
            var normalizedDateField = NormalizeDateField(dateField);

            if (string.IsNullOrWhiteSpace(normalizedItemType))
            {
                return BadRequest(new
                {
                    error = "Invalid itemType. Supported values: supplier-order, customer-order, invoice."
                });
            }

            if (string.IsNullOrWhiteSpace(normalizedDateField))
            {
                return BadRequest(new
                {
                    error = "Invalid dateField. Supported values: created, edited."
                });
            }

            if (normalizedItemType == "invoice")
            {
                return BadRequest(new
                {
                    error = "itemType 'invoice' is not supported yet because Invoice does not expose created/edited date fields in the current model."
                });
            }

            IQueryable<DateTime?> datesQuery = normalizedItemType switch
            {
                "supplier-order" when normalizedDateField == "created" => _context.SupplierOrders
                    .AsNoTracking()
                    .Select(x => x.Created),
                "supplier-order" when normalizedDateField == "edited" => _context.SupplierOrders
                    .AsNoTracking()
                    .Select(x => x.Edited),
                "customer-order" when normalizedDateField == "created" => _context.CustomerOrders
                    .AsNoTracking()
                    .Select(x => x.Created),
                "customer-order" when normalizedDateField == "edited" => _context.CustomerOrders
                    .AsNoTracking()
                    .Select(x => x.Edited),
                _ => throw new InvalidOperationException("Unsupported itemType/dateField combination")
            };

            var years = await datesQuery
                .Where(x => x.HasValue)
                .Select(x => x!.Value.Year)
                .Distinct()
                .OrderByDescending(x => x)
                .ToListAsync();

            return Ok(new ReportingSelectableYearsResponseDto
            {
                ItemType = normalizedItemType,
                DateField = normalizedDateField,
                Years = years,
            });
        }

        [HttpPost("order-costs/monthly")]
        public async Task<ActionResult<MonthlyOrderCostsReportResponseDto>> GetMonthlyOrderCostsReport([FromBody] MonthlyOrderCostsReportRequestDto? request)
        {
            var now = SwedishTime.Now;
            var year = request?.Year is >= 2000 and <= 2100 ? request.Year : now.Year;
            var month = request?.Month is >= 1 and <= 12 ? request.Month : now.Month;
            var selectedCostIds = (request?.CostIds ?? new List<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1);

            var rows = await _context.OrderCosts
                .AsNoTracking()
                .Where(orderCost =>
                    orderCost.SupplierOrder != null
                    && orderCost.SupplierOrder.Created >= startDate
                    && orderCost.SupplierOrder.Created < endDate
                    && (selectedCostIds.Count == 0
                        || (orderCost.CostId.HasValue && selectedCostIds.Contains(orderCost.CostId.Value))))
                .OrderByDescending(orderCost => orderCost.SupplierOrder!.Created)
                .ThenBy(orderCost => orderCost.SupplierOrder!.SupplierOrderNr)
                .Select(orderCost => new MonthlyOrderCostsReportRowDto
                {
                    Cost = orderCost.Cost != null ? orderCost.Cost.Name : string.Empty,
                    Currency = orderCost.InPriceCurrency != null ? orderCost.InPriceCurrency.Name ?? string.Empty : string.Empty,
                    PurchasePrice = orderCost.InPrice ?? 0,
                    PurchasePriceAttested = orderCost.InPriceAttested,
                    SalesPrice = orderCost.OutPrice,
                    ShouldInvoice = orderCost.DoDebit,
                    InvoiceNumber = (
                        from invoiceRow in _context.InvoiceRows
                        join invoice in _context.Invoices on invoiceRow.InvoiceId equals invoice.Id
                        where invoiceRow.OrderCostId == orderCost.Id
                        select invoice.InvoiceNumber
                    ).FirstOrDefault(),
                    Supplier = orderCost.SupplierOrder != null ? orderCost.SupplierOrder.SupplierName ?? string.Empty : string.Empty,
                    OrderCreatedAt = orderCost.SupplierOrder != null ? orderCost.SupplierOrder.Created ?? startDate : startDate,
                    Customer = orderCost.SupplierOrder != null && orderCost.SupplierOrder.Customer != null
                        ? orderCost.SupplierOrder.Customer.Name ?? string.Empty
                        : string.Empty,
                    Responsible = (
                        from responsibleUser in _context.LegacyUsers
                        where orderCost.SupplierOrder != null
                            && orderCost.SupplierOrder.Customer != null
                            && orderCost.SupplierOrder.Customer.ResponsibleUserId.HasValue
                            && responsibleUser.Id == orderCost.SupplierOrder.Customer.ResponsibleUserId.Value
                        select responsibleUser.Name
                    ).FirstOrDefault() ?? string.Empty,
                    OrderNumber = orderCost.SupplierOrder != null ? orderCost.SupplierOrder.Id : 0,
                    Product = orderCost.SupplierOrder != null ? orderCost.SupplierOrder.Product ?? string.Empty : string.Empty,
                    PurchasePriceSEK = orderCost.InPrice.HasValue && orderCost.SupplierOrder != null && orderCost.SupplierOrder.PurchaseCurrencyRate > 0
                        ? orderCost.InPrice * (decimal)orderCost.SupplierOrder.PurchaseCurrencyRate.Value
                        : orderCost.InPrice,
                    PurchasePriceAttestedSEK = orderCost.InPriceAttested.HasValue && orderCost.SupplierOrder != null && orderCost.SupplierOrder.PurchaseCurrencyRate > 0
                        ? orderCost.InPriceAttested * (decimal)orderCost.SupplierOrder.PurchaseCurrencyRate.Value
                        : orderCost.InPriceAttested,
                    SalesPriceSEK = orderCost.OutPrice.HasValue && orderCost.CustomerOrder != null && orderCost.CustomerOrder.SalesCurrencyRate > 0
                        ? orderCost.OutPrice * (decimal)orderCost.CustomerOrder.SalesCurrencyRate.Value
                        : null,
                    Markup = orderCost.InPrice.HasValue && orderCost.InPrice != 0
                            && orderCost.OutPrice.HasValue
                            && orderCost.CustomerOrder != null && orderCost.CustomerOrder.SalesCurrencyRate > 0
                            && orderCost.SupplierOrder != null && orderCost.SupplierOrder.PurchaseCurrencyRate > 0
                        ? (orderCost.OutPrice * (decimal)orderCost.CustomerOrder.SalesCurrencyRate.Value
                           - orderCost.InPrice * (decimal)orderCost.SupplierOrder.PurchaseCurrencyRate.Value)
                          / (orderCost.InPrice * (decimal)orderCost.SupplierOrder.PurchaseCurrencyRate.Value)
                        : null,
                })
                .ToListAsync();

            return Ok(new MonthlyOrderCostsReportResponseDto
            {
                Year = year,
                Month = month,
                Rows = rows,
            });
        }

        [HttpPost("inventory")]
        public async Task<ActionResult<InventoryReportResponseDto>> GetInventoryReport([FromBody] InventoryReportRequestDto? request)
        {
            var calculationDate = request?.CalculationDate?.Date ?? SwedishTime.Now.Date;
            var calcDatePlusOne = calculationDate.AddDays(1);
            var inventoryIds = request?.InventoryIds ?? new List<int>();
            var deliveredStatus = (int)DeliveryStatus.Delivered;

            try
            {
                var baseOrders = await (
                    from supplierOrder in _context.SupplierOrders.AsNoTracking()
                    join inventory in _context.Inventories.AsNoTracking() on supplierOrder.InventoryId equals inventory.Id
                    where inventory.IsInventory
                        && (inventoryIds.Count == 0 || (supplierOrder.InventoryId.HasValue && inventoryIds.Contains(supplierOrder.InventoryId.Value)))
                    select new
                    {
                        SupplierOrderId = supplierOrder.Id,
                        SupplierOrderNr = supplierOrder.SupplierOrderNr,
                        CustomerName = supplierOrder.Customer != null ? supplierOrder.Customer.Name : null,
                        ProductName = supplierOrder.Product,
                        InventoryId = inventory.Id,
                        InventoryName = inventory.Name,
                        ProducedNrOfItems = supplierOrder.ProducedEdition,
                        PurchasePrice = supplierOrder.PurchasePrice,
                        PurchaseCurrencyRate = supplierOrder.PurchaseCurrencyRate,
                        UnitMultiplicator = supplierOrder.Unit != null ? supplierOrder.Unit.Multiplicator : null,
                    }
                ).ToListAsync();

                if (baseOrders.Count == 0)
                {
                    return Ok(new InventoryReportResponseDto
                    {
                        CalculationDate = calculationDate,
                        CurrentInventoryValue = 0,
                        Rows = Array.Empty<InventoryReportRowDto>()
                    });
                }

                var supplierOrderIds = baseOrders.Select(x => x.SupplierOrderId).ToHashSet();

                var stockTakingRows = await (
                    from stockTakingItem in _context.StockTakingItems.AsNoTracking()
                    join stockTaking in _context.StockTakings.AsNoTracking() on stockTakingItem.StockTakingId equals stockTaking.Id
                    where stockTakingItem.SupplierOrderId.HasValue
                        && supplierOrderIds.Contains(stockTakingItem.SupplierOrderId.Value)
                        && stockTaking.StockTakingDate < calcDatePlusOne
                    select new
                    {
                        SupplierOrderId = stockTakingItem.SupplierOrderId ?? 0,
                        StockTakingItemId = stockTakingItem.Id,
                        StockTakingDate = stockTaking.StockTakingDate,
                        NrOfItems = stockTakingItem.NrOfItems,
                        NrOfPallets = stockTakingItem.NrOfPallets,
                    }
                ).ToListAsync();

                var latestStockTakingByOrder = stockTakingRows
                    .GroupBy(x => x.SupplierOrderId)
                    .Select(group => group
                        .OrderByDescending(x => x.StockTakingDate)
                        .ThenByDescending(x => x.StockTakingItemId)
                        .First())
                    .ToDictionary(
                        x => x.SupplierOrderId,
                        x => new
                        {
                            x.StockTakingDate,
                            LastNrOfItems = (double)(x.NrOfItems ?? 0),
                            LastNrOfPallets = x.NrOfPallets ?? 0,
                        });

                var customerOrderRows = await _context.CustomerOrders
                    .AsNoTracking()
                    .Where(customerOrder => customerOrder.SupplierOrderId.HasValue && supplierOrderIds.Contains(customerOrder.SupplierOrderId.Value))
                    .Select(customerOrder => new
                    {
                        customerOrder.Id,
                        SupplierOrderId = customerOrder.SupplierOrderId!.Value,
                        customerOrder.SalesPrice,
                        customerOrder.SalesCurrencyRate,
                    })
                    .ToListAsync();

                var customerOrderPricingByOrder = customerOrderRows
                    .GroupBy(x => x.SupplierOrderId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.OrderBy(x => x.Id).First());

                var deliveredToStockRows = await _context.DeliveryToStocks
                    .AsNoTracking()
                    .Where(delivery =>
                        delivery.SupplierOrderId.HasValue
                        && supplierOrderIds.Contains(delivery.SupplierOrderId.Value)
                        && delivery.DeliveryStatus == deliveredStatus
                        && delivery.DeliveryDate < calcDatePlusOne)
                    .Select(delivery => new
                    {
                        SupplierOrderId = delivery.SupplierOrderId!.Value,
                        delivery.DeliveryDate,
                        delivery.NrOfItems,
                        delivery.NrOfPallets,
                    })
                    .ToListAsync();

                var deliveredToStockByOrder = deliveredToStockRows
                    .GroupBy(x => x.SupplierOrderId)
                    .ToDictionary(
                        group => group.Key,
                        group =>
                        {
                            latestStockTakingByOrder.TryGetValue(group.Key, out var latestStockTaking);
                            var cutoffDate = latestStockTaking?.StockTakingDate;
                            var filtered = cutoffDate.HasValue
                                ? group.Where(x => x.DeliveryDate > cutoffDate)
                                : group;

                            return new
                            {
                                DeliveredItemsToStock = filtered.Sum(x => x.NrOfItems ?? 0),
                                DeliveredPalletsToStock = filtered.Sum(x => x.NrOfPallets ?? 0),
                            };
                        });

                var deliveredFromStockRows = await (
                    from delivery in _context.DeliveryFromStocks.AsNoTracking()
                    join customerOrder in _context.CustomerOrders.AsNoTracking() on delivery.CustomerOrderId equals customerOrder.Id
                    where customerOrder.SupplierOrderId.HasValue
                        && supplierOrderIds.Contains(customerOrder.SupplierOrderId.Value)
                        && delivery.DeliveryStatus == deliveredStatus
                        && delivery.DeliveryDate < calcDatePlusOne
                    select new
                    {
                        SupplierOrderId = customerOrder.SupplierOrderId ?? 0,
                        delivery.DeliveryDate,
                        delivery.NrOfItems,
                        delivery.NrOfPallets,
                    }
                ).ToListAsync();

                var deliveredFromStockByOrder = deliveredFromStockRows
                    .GroupBy(x => x.SupplierOrderId)
                    .ToDictionary(
                        group => group.Key,
                        group =>
                        {
                            latestStockTakingByOrder.TryGetValue(group.Key, out var latestStockTaking);
                            var cutoffDate = latestStockTaking?.StockTakingDate;
                            var filtered = cutoffDate.HasValue
                                ? group.Where(x => x.DeliveryDate > cutoffDate)
                                : group;

                            return new
                            {
                                DeliveredItemsFromStock = filtered.Sum(x => x.NrOfItems ?? 0),
                                DeliveredPalletsFromStock = filtered.Sum(x => x.NrOfPallets ?? 0),
                            };
                        });

                var rows = new List<InventoryReportRowDto>(baseOrders.Count);

                foreach (var order in baseOrders)
                {
                    latestStockTakingByOrder.TryGetValue(order.SupplierOrderId, out var latestStockTaking);
                    deliveredToStockByOrder.TryGetValue(order.SupplierOrderId, out var deliveredToStock);
                    deliveredFromStockByOrder.TryGetValue(order.SupplierOrderId, out var deliveredFromStock);
                    customerOrderPricingByOrder.TryGetValue(order.SupplierOrderId, out var customerOrderPricing);

                    var lastItems = latestStockTaking?.LastNrOfItems ?? 0;
                    var lastPallets = latestStockTaking?.LastNrOfPallets ?? 0;
                    var deliveredItemsToStock = deliveredToStock?.DeliveredItemsToStock ?? 0;
                    var deliveredItemsFromStock = deliveredFromStock?.DeliveredItemsFromStock ?? 0;
                    var deliveredPalletsToStock = deliveredToStock?.DeliveredPalletsToStock ?? 0;
                    var deliveredPalletsFromStock = deliveredFromStock?.DeliveredPalletsFromStock ?? 0;

                    var currentItems = lastItems + deliveredItemsToStock - deliveredItemsFromStock;
                    if (currentItems <= 0)
                    {
                        continue;
                    }

                    var currentPallets = lastPallets + deliveredPalletsToStock - deliveredPalletsFromStock;
                    var unitMultiplier = order.UnitMultiplicator.HasValue && order.UnitMultiplicator.Value > 0
                        ? (double)order.UnitMultiplicator.Value
                        : 1;

                    var stockValue = (decimal)(
                        currentItems
                        * order.PurchasePrice.GetValueOrDefault()
                        * order.PurchaseCurrencyRate.GetValueOrDefault()
                        / unitMultiplier);

                    var salesValue = (decimal)(
                        currentItems
                        * (customerOrderPricing?.SalesPrice ?? 0)
                        * (customerOrderPricing?.SalesCurrencyRate ?? 0)
                        / unitMultiplier);

                    rows.Add(new InventoryReportRowDto
                    {
                        SupplierOrderId = order.SupplierOrderId,
                        SupplierOrderNr = order.SupplierOrderNr ?? string.Empty,
                        CustomerName = order.CustomerName ?? string.Empty,
                        ProductName = order.ProductName ?? string.Empty,
                        InventoryId = order.InventoryId,
                        InventoryName = order.InventoryName ?? string.Empty,
                        ProducedNrOfItems = order.ProducedNrOfItems ?? 0,
                        LastInventoryDate = latestStockTaking?.StockTakingDate,
                        CurrentInventoryNrOfItems = currentItems,
                        CurrentInventoryNrOfPallets = currentPallets,
                        TotalStockValue = stockValue,
                        TotalSalesValue = salesValue,
                    });
                }

                var totalStockValue = rows.Sum(r => r.TotalStockValue);

                return Ok(new InventoryReportResponseDto
                {
                    CalculationDate = calculationDate,
                    CurrentInventoryValue = totalStockValue,
                    Rows = rows.OrderBy(r => r.SupplierOrderId).ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to generate inventory report", details = ex.Message });
            }
        }

        [HttpPost("palletfollowup")]
        public async Task<ActionResult<PalletFollowupReportResponseDto>> GetPalletFollowupReport([FromBody] PalletFollowupReportRequestDto? request)
        {
            if (request?.StartDate == null || request?.EndDate == null)
            {
                return BadRequest(new
                {
                    error = "StartDate and EndDate are required."
                });
            }

            var customerId = request?.CustomerId;

            var startDate = request?.StartDate?.Date ?? SwedishTime.Now.Date.AddMonths(-1);
            var endDate = request?.EndDate?.Date ?? SwedishTime.Now.Date;

            if (endDate < startDate)
            {
                endDate = startDate;
            }
            endDate = endDate.AddDays(1);

            var deliveryQuery = await _context.InvoiceRows
                .AsNoTracking()
                .Where(ir =>
                    ir.Invoice != null
                    && ir.Invoice.InvoiceDate >= startDate
                    && ir.Invoice.InvoiceDate < endDate
                    && ir.PriceTypeId == 107 // Pallet follow-up
                    && (ir.DeliveryToCustomerId.HasValue || ir.DeliveryFromStockId.HasValue)
                    && (!customerId.HasValue
                        || (ir.DeliveryToCustomer != null
                            && ir.DeliveryToCustomer.CustomerOrder != null
                            && ir.DeliveryToCustomer.CustomerOrder.CustomerId == customerId.Value)
                        || (ir.DeliveryFromStock != null
                            && ir.DeliveryFromStock.CustomerOrder != null
                            && ir.DeliveryFromStock.CustomerOrder.CustomerId == customerId.Value)))
                .Select(ir => new
                {
                    Invoice = ir.Invoice,
                    InvoiceRow = ir,
                    DeliveryToCustomer = ir.DeliveryToCustomer,
                    DeliveryFromStock = ir.DeliveryFromStock,
                    CustomerOrderFromDeliveryToCustomer = ir.DeliveryToCustomer != null
                        ? ir.DeliveryToCustomer.CustomerOrder
                        : null,
                    CustomerOrderFromDeliveryFromStock = ir.DeliveryFromStock != null
                        ? ir.DeliveryFromStock.CustomerOrder
                        : null,
                    SupplierFromDeliveryToCustomer = ir.DeliveryToCustomer != null
                        && ir.DeliveryToCustomer.CustomerOrder != null
                        && ir.DeliveryToCustomer.CustomerOrder.SupplierOrder != null
                        && ir.DeliveryToCustomer.CustomerOrder.SupplierOrder.Supplier != null
                        ? ir.DeliveryToCustomer.CustomerOrder.SupplierOrder.Supplier
                        : null,
                    SupplierFromDeliveryFromStock = ir.DeliveryFromStock != null
                        && ir.DeliveryFromStock.CustomerOrder != null
                        && ir.DeliveryFromStock.CustomerOrder.SupplierOrder != null
                        && ir.DeliveryFromStock.CustomerOrder.SupplierOrder.Supplier != null
                        ? ir.DeliveryFromStock.CustomerOrder.SupplierOrder.Supplier
                        : null,
                }).ToListAsync();

            var palletFormatIds = deliveryQuery
                .SelectMany(r => new int?[]
                {
                    r.DeliveryToCustomer?.PalletFormatId,
                    r.DeliveryFromStock?.PalletFormatId,
                })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var palletFormats = palletFormatIds.Count > 0
                ? await _context.PalletFormats
                    .AsNoTracking()
                    .Where(pf => palletFormatIds.Contains(pf.Id))
                    .ToDictionaryAsync(pf => pf.Id, pf => pf.Name ?? string.Empty)
                : new Dictionary<int, string>();

            var rows = deliveryQuery.Select(r =>
            {
                string SupplierName = string.Empty;
                int nrOfPallets = 0;
                string palletFormatName = string.Empty;
                decimal sekPerPalletIn = 0;
                decimal sekPerPalletOut = 0;
                if (r.DeliveryFromStock != null && r.CustomerOrderFromDeliveryFromStock != null)
                {
                    SupplierName = r.SupplierFromDeliveryFromStock?.Name ?? string.Empty;
                    nrOfPallets = r.DeliveryFromStock.NrOfPallets ?? 0;
                    palletFormatName = r.DeliveryFromStock.PalletFormatId.HasValue && palletFormats.TryGetValue(r.DeliveryFromStock.PalletFormatId.Value, out var pfName)
                        ? pfName
                        : string.Empty;
                    sekPerPalletIn = r.CustomerOrderFromDeliveryFromStock.SupplierPricePerEurPallet.HasValue && r.CustomerOrderFromDeliveryFromStock.SupplierPricePerEurPalletCurrencyRate.HasValue
                        ? r.CustomerOrderFromDeliveryFromStock.SupplierPricePerEurPallet.Value * r.CustomerOrderFromDeliveryFromStock.SupplierPricePerEurPalletCurrencyRate.Value
                        : 0;
                    sekPerPalletOut = r.CustomerOrderFromDeliveryFromStock.EurPalletValue.HasValue && r.CustomerOrderFromDeliveryFromStock.SalesCurrencyRate.HasValue
                        ? (decimal)r.CustomerOrderFromDeliveryFromStock.EurPalletValue.Value * (decimal)r.CustomerOrderFromDeliveryFromStock.SalesCurrencyRate.Value
                        : 0;
                }
                else if (r.DeliveryToCustomer != null && r.CustomerOrderFromDeliveryToCustomer != null)
                {
                    SupplierName = r.SupplierFromDeliveryToCustomer?.Name ?? string.Empty;
                    nrOfPallets = r.DeliveryToCustomer.NrOfPallets ?? 0;
                    palletFormatName = r.DeliveryToCustomer.PalletFormatId.HasValue && palletFormats.TryGetValue(r.DeliveryToCustomer.PalletFormatId.Value, out var pfName)
                        ? pfName
                        : string.Empty;
                    sekPerPalletIn = r.CustomerOrderFromDeliveryToCustomer.SupplierPricePerEurPallet.HasValue && r.CustomerOrderFromDeliveryToCustomer.SupplierPricePerEurPalletCurrencyRate.HasValue
                        ? r.CustomerOrderFromDeliveryToCustomer.SupplierPricePerEurPallet.Value * r.CustomerOrderFromDeliveryToCustomer.SupplierPricePerEurPalletCurrencyRate.Value
                        : 0;
                    sekPerPalletOut = r.CustomerOrderFromDeliveryToCustomer.EurPalletValue.HasValue && r.CustomerOrderFromDeliveryToCustomer.SalesCurrencyRate.HasValue
                        ? (decimal)r.CustomerOrderFromDeliveryToCustomer.EurPalletValue.Value * (decimal)r.CustomerOrderFromDeliveryToCustomer.SalesCurrencyRate.Value
                        : 0;
                }
                else
                {
                    return null;
                }

                return new PalletFollowupReportRowDto
                {
                    PalletType = palletFormatName,
                    NrOfPallets = nrOfPallets,
                    SekPerPalletIn = sekPerPalletIn,
                    SekPerPalletOut = sekPerPalletOut,
                    SekPerPalletDiff = sekPerPalletOut - sekPerPalletIn,
                    SumDiff = (sekPerPalletOut - sekPerPalletIn) * nrOfPallets,
                    Supplier = SupplierName,
                };

            }).ToList();

            var groupedRows = rows
                .Where(r => r != null)
                .Select(r => r!)
                .GroupBy(r => new
                {
                    Supplier = r.Supplier ?? string.Empty,
                    PalletType = r.PalletType ?? string.Empty,
                })
                .Select(group =>
                {
                    var totalPallets = group.Sum(x => x.NrOfPallets);

                    decimal weightedInTotal = 0;
                    decimal weightedOutTotal = 0;
                    decimal weightedDiffTotal = 0;
                    var hasIn = false;
                    var hasOut = false;
                    var hasDiff = false;

                    foreach (var row in group)
                    {
                        if (row.SekPerPalletIn.HasValue)
                        {
                            weightedInTotal += row.SekPerPalletIn.Value * row.NrOfPallets;
                            hasIn = true;
                        }

                        if (row.SekPerPalletOut.HasValue)
                        {
                            weightedOutTotal += row.SekPerPalletOut.Value * row.NrOfPallets;
                            hasOut = true;
                        }

                        if (row.SekPerPalletDiff.HasValue)
                        {
                            weightedDiffTotal += row.SekPerPalletDiff.Value * row.NrOfPallets;
                            hasDiff = true;
                        }
                    }

                    return new PalletFollowupReportRowDto
                    {
                        Supplier = group.Key.Supplier,
                        PalletType = group.Key.PalletType,
                        NrOfPallets = totalPallets,
                        SekPerPalletIn = hasIn && totalPallets > 0 ? weightedInTotal / totalPallets : null,
                        SekPerPalletOut = hasOut && totalPallets > 0 ? weightedOutTotal / totalPallets : null,
                        SekPerPalletDiff = hasDiff && totalPallets > 0 ? weightedDiffTotal / totalPallets : null,
                        SumDiff = group.Sum(x => x.SumDiff ?? 0),
                    };
                })
                .ToList();


            // var endDateExclusive = endDate.AddDays(1);

            // var deliveryRows = await _context.DeliveryToCustomers
            //     .AsNoTracking()
            //     .Where(d =>
            //         d.NrOfPallets.HasValue && d.NrOfPallets > 0
            //         && d.CustomerOrderId.HasValue
            //         && d.DeliveryDate >= startDate && d.DeliveryDate < endDateExclusive
            //         && (!customerId.HasValue || (d.CustomerOrder != null && d.CustomerOrder.CustomerId == customerId.Value)))
            //     .Select(d => new
            //     {
            //         DeliveryId = d.Id,
            //         NrOfPallets = d.NrOfPallets ?? 0,
            //         PalletFormatId = d.PalletFormatId,
            //         SupplierName = d.SupplierOrder != null ? d.SupplierOrder.SupplierName : null,
            //         CustomerOrderNr = d.CustomerOrder != null ? d.CustomerOrder.CustomerOrderNr : null,
            //         CustomerName = d.CustomerOrder != null ? d.CustomerOrder.CustomerName : null,
            //         SupplierPricePerEurPallet = d.CustomerOrder != null ? d.CustomerOrder.SupplierPricePerEurPallet : null,
            //         SupplierPricePerEurPalletCurrencyRate = d.CustomerOrder != null ? d.CustomerOrder.SupplierPricePerEurPalletCurrencyRate : null,
            //         EurPalletValue = d.CustomerOrder != null ? d.CustomerOrder.EurPalletValue : null,
            //         NrOfEurPallet = d.CustomerOrder != null ? (int?)d.CustomerOrder.NrOfEurPallet : null,
            //         SalesCurrencyRate = d.CustomerOrder != null ? d.CustomerOrder.SalesCurrencyRate : null,
            //     })
            //     .ToListAsync();



            // var rows2 = deliveryRows.Select(r =>
            // {
            //     var sekPerPalletIn = r.SupplierPricePerEurPallet.HasValue && r.SupplierPricePerEurPalletCurrencyRate.HasValue
            //         ? r.SupplierPricePerEurPallet * r.SupplierPricePerEurPalletCurrencyRate
            //         : r.SupplierPricePerEurPallet;

            //     var sekPerPalletOut = r.EurPalletValue.HasValue && r.NrOfEurPallet.HasValue && r.NrOfEurPallet > 0
            //         ? (decimal?)((decimal)(r.EurPalletValue.Value / r.NrOfEurPallet.Value) * (decimal)(r.SalesCurrencyRate ?? 1.0))
            //         : null;

            //     var diff = sekPerPalletIn.HasValue && sekPerPalletOut.HasValue
            //         ? sekPerPalletOut - sekPerPalletIn
            //         : null;

            //     var sumDiff = diff.HasValue ? diff * r.NrOfPallets : null;

            //     var palletType = r.PalletFormatId.HasValue && palletFormats.TryGetValue(r.PalletFormatId.Value, out var ppfName)
            //         ? ppfName
            //         : string.Empty;

            //     return new PalletFollowupReportRowDto
            //     {
            //         DeliveryId = r.DeliveryId,
            //         Supplier = r.SupplierName ?? string.Empty,
            //         PalletType = palletType,
            //         NrOfPallets = r.NrOfPallets,
            //         SekPerPalletIn = sekPerPalletIn,
            //         SekPerPalletOut = sekPerPalletOut,
            //         SekPerPalletDiff = diff,
            //         SumDiff = sumDiff,
            //         Customer = r.CustomerName ?? string.Empty,
            //         CustomerOrderNr = r.CustomerOrderNr ?? string.Empty,
            //     };
            // }).ToList();

            return Ok(new PalletFollowupReportResponseDto
            {
                StartDate = startDate,
                EndDate = endDate,
                Rows = groupedRows.Where(r => r != null).Select(r => r!).ToList(),
            });
        }

        [HttpGet("non-confirmed-supplier-orders")]
        public async Task<ActionResult<IReadOnlyList<NonConfirmedSupplierOrderRowDto>>> GetNonConfirmedSupplierOrders()
        {
            var rawRows = await _context.SupplierOrders
                .AsNoTracking()
                .Where(
                    // so => so.Edition > (so.CustomerOrders.Sum(p => (p.Edition ?? 0) - p.CustomerOrderEditionAdjustments.Sum(p => p.NrOfItems ?? 0)))
                    so => so.CustomerOrders.Count == 0
                    )

                .Select(so => new
                {
                    so.Id,
                    so.EmailSentDateTime,
                    OurReference = so.OurReference ?? string.Empty,
                    SupplierName = so.SupplierName ?? string.Empty,
                    ProductName = so.Product ?? string.Empty,
                    so.Created,
                    so.DeliveryDate,
                    Edition = so.Edition ?? 0,
                    so.PurchasePrice,
                    so.PurchaseCurrencyRate,
                    CustomerName = so.Customer != null ? so.Customer.Name ?? string.Empty : string.Empty,
                    Seller = so.Customer != null && so.Customer.ResponsibleUser != null
                        ? so.Customer.ResponsibleUser.Name ?? string.Empty
                        : string.Empty,
                    Unit = so.Unit,
                    CalculationRow = so.SelectedCalculationRow,
                    CustomerOrderedEdition = so.CustomerOrders.Sum(co => co.Edition ?? 0),
                })
                .OrderBy(x => x.Created)
                .ToListAsync();

            var rows = rawRows.Select(r =>
            {
                decimal? salesCurrencyRate = 1;
                decimal? salesPrice = null;
                decimal? baseCost = null;
                decimal? tb = null;
                decimal? totalTb = null;
                decimal? totalTbLeft = null;
                decimal? orderValue = null;
                decimal? orderValueLeft = null;

                if (r.CalculationRow != null)
                {
                    salesCurrencyRate = r.CalculationRow.ArchivedSalesCurrencyRate ?? 1.0m;
                    salesPrice = (decimal?)r.CalculationRow.SalesPrice;
                    baseCost = (decimal)(r.PurchasePrice ?? 0)
                        + (decimal)(r.CalculationRow?.Freight ?? 0)
                        + (r.CalculationRow?.PackagingCost ?? 0)
                        + (decimal)(r.CalculationRow?.Storage ?? 0)
                        + (decimal)(r.CalculationRow?.Other ?? 0);
                    tb =
                        (decimal)(r.CalculationRow?.SalesPrice ?? 0)
                        * salesCurrencyRate
                        -
                        (
                            (decimal)r.PurchasePrice.GetValueOrDefault() * (decimal)r.PurchaseCurrencyRate.GetValueOrDefault(1)
                            +
                            (decimal)(r.CalculationRow?.Freight ?? 0) * salesCurrencyRate
                            +
                            (decimal)(r.CalculationRow?.Storage ?? 0) * salesCurrencyRate
                            +
                            (decimal)(r.CalculationRow?.Other ?? 0) * salesCurrencyRate
                            +
                            (decimal)(r.CalculationRow?.PackagingCost ?? 0) * salesCurrencyRate
                        );
                    if (r.Unit != null && r.Unit.Multiplicator > 0 && r.CalculationRow != null)
                    {
                        orderValue = (decimal)r.CalculationRow.SalesPrice.GetValueOrDefault()
                            * salesCurrencyRate
                            * r.Edition
                            / (decimal)r.Unit.Multiplicator.GetValueOrDefault(1);
                        orderValueLeft =
                            (decimal)r.CalculationRow.SalesPrice.GetValueOrDefault() * salesCurrencyRate
                            *
                            (
                                r.Edition
                                -
                                r.CustomerOrderedEdition
                            )
                            /
                            (decimal)r.Unit.Multiplicator.GetValueOrDefault(1);
                        totalTb =
                            tb
                            * r.Edition
                            / (decimal)r.Unit.Multiplicator.GetValueOrDefault(1);
                        totalTbLeft =
                            tb
                            * (r.Edition - r.CustomerOrderedEdition)
                            / (decimal)r.Unit.Multiplicator.GetValueOrDefault(1);
                    }
                }


                decimal? markup = baseCost > 0 && salesPrice > 0
                    ? ((salesPrice - baseCost) / baseCost) * 100
                    : null;

                return new NonConfirmedSupplierOrderRowDto
                {
                    Id = r.Id,
                    SentAt = r.EmailSentDateTime,
                    FileSent = r.EmailSentDateTime.HasValue ? "Fil" : null,
                    Seller = r.Seller,
                    OurReference = r.OurReference,
                    CustomerName = r.CustomerName,
                    SupplierName = r.SupplierName,
                    ProductName = r.ProductName,
                    CreatedAt = r.Created,
                    DeliveryDate = r.DeliveryDate,
                    Edition = r.Edition,
                    CustomerOrderedEdition = r.CustomerOrderedEdition,
                    EditiopnLeft = r.Edition - r.CustomerOrderedEdition,
                    OrderValueSek = orderValue.GetValueOrDefault(),
                    OrderValueLeftSek = orderValueLeft.GetValueOrDefault(),
                    MarkupPercent = markup,
                    TotalTbSek = totalTb.GetValueOrDefault(),
                    TotalTbLeftSek = totalTbLeft.GetValueOrDefault(),
                };
            }).ToList();

            return Ok(rows);
        }

        private static IOrderedQueryable<RevenuePerOrderReportRowDto> ApplyRevenuePerOrderOrdering(IQueryable<RevenuePerOrderReportRowDto> query, List<SortRequest>? orderBy)
        {
            if (orderBy is null || orderBy.Count == 0)
            {
                return query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.OrderId);
            }

            IOrderedQueryable<RevenuePerOrderReportRowDto>? ordered = null;

            foreach (var sort in orderBy)
            {
                var field = sort.Field?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(field))
                {
                    throw new ArgumentException("OrderBy field is required.");
                }

                var isDescending = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);

                ordered = field switch
                {
                    "orderid" => ApplyRevenueOrder(ordered, query, x => x.OrderId, isDescending),
                    "ordernumber" => ApplyRevenueOrder(ordered, query, x => x.OrderNumber, isDescending),
                    "createdat" or "created" => ApplyRevenueOrder(ordered, query, x => x.CreatedAt, isDescending),
                    "customername" or "customer" => ApplyRevenueOrder(ordered, query, x => x.CustomerName, isDescending),
                    "productname" or "product" => ApplyRevenueOrder(ordered, query, x => x.ProductName, isDescending),
                    "construction" => ApplyRevenueOrder(ordered, query, x => x.Construction, isDescending),
                    "materialtype" or "material" => ApplyRevenueOrder(ordered, query, x => x.MaterialType, isDescending),
                    "seller" => ApplyRevenueOrder(ordered, query, x => x.Seller, isDescending),
                    "format" => ApplyRevenueOrder(ordered, query, x => x.Format, isDescending),
                    "suppliername" or "supplier" => ApplyRevenueOrder(ordered, query, x => x.SupplierName, isDescending),
                    "revenuesek" or "revenue" => ApplyRevenueOrder(ordered, query, x => x.RevenueSek, isDescending),
                    "totaltbsek" or "tb" => ApplyRevenueOrder(ordered, query, x => x.TotalTbSek, isDescending),
                    "markuppercent" or "markup" => ApplyRevenueOrder(ordered, query, x => x.MarkupPercent, isDescending),
                    "isactive" => ApplyRevenueOrder(ordered, query, x => x.IsActive, isDescending),
                    _ => throw new ArgumentException($"Unsupported order by field '{sort.Field}'.")
                };
            }

            return ordered ?? query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.OrderId);
        }

        private static IOrderedQueryable<RevenuePerOrderReportRowDto> ApplyRevenueOrder<TKey>(IOrderedQueryable<RevenuePerOrderReportRowDto>? ordered,IQueryable<RevenuePerOrderReportRowDto> source,Expression<Func<RevenuePerOrderReportRowDto, TKey>> keySelector, bool isDescending)
        {
            if (ordered is null)
            {
                return isDescending ? source.OrderByDescending(keySelector) : source.OrderBy(keySelector);
            }

            return isDescending ? ordered.ThenByDescending(keySelector) : ordered.ThenBy(keySelector);
        }

        private static string? NormalizeItemType(string? itemType)
        {
            var value = itemType?.Trim().ToLowerInvariant();

            return value switch
            {
                "supplier-order" or "supplierorder" or "supplier_order" or "supplier order" => "supplier-order",
                "customer-order" or "customerorder" or "customer_order" or "customer order" => "customer-order",
                "invoice" => "invoice",
                _ => null,
            };
        }

        private static string? NormalizeDateField(string? dateField)
        {
            var value = dateField?.Trim().ToLowerInvariant();

            return value switch
            {
                "created" or "create" => "created",
                "edited" or "last-edited" or "lastedited" or "last edited" => "edited",
                _ => null,
            };
        }


    }
}