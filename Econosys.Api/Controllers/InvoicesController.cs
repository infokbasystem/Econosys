using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;

namespace Econosys.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class InvoicesController : ControllerBase
    {
        private static readonly Dictionary<string, PropertyInfo> FieldMap = BuildFieldMap();

        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<InvoicesController> _logger;

        private readonly ILegacyUserResolutionService _legacyUserResolution;

        public InvoicesController(ApplicationDbContext dbContext, ILogger<InvoicesController> logger, ILegacyUserResolutionService legacyUserResolution)
        {
            _dbContext = dbContext;
            _logger = logger;
            _legacyUserResolution = legacyUserResolution;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<InvoiceDto>> GetById(int id)
        {
            var invoice = await _dbContext.Invoices
                .AsNoTracking()
                .Include(x => x.InvoiceRows)
                .Include(x => x.InvoiceAccountRows)
                .Include(x => x.CreatedByUser)
                .Include(x => x.EditedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (invoice is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(invoice));
        }

        [HttpPost]
        public async Task<ActionResult<InvoiceDto>> Create([FromBody] CreateInvoiceRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var legacyUser = await _legacyUserResolution.ResolveCurrentUserAsync(User);
            if (legacyUser is null)
            {
                return Unauthorized(new { message = "Could not map authenticated user to a legacy user by email." });
            }

            var invoice = new Invoice();
            ApplyRequestToInvoice(invoice, request);
            

            invoice.Created ??= SwedishTime.Now;
            invoice.CreatedBy = legacyUser.Id;
            invoice.Edited = SwedishTime.Now;
            invoice.EditedBy = legacyUser.Id;
            invoice.InvoiceNumber = await GetNextInvoiceNumberAsync();

            invoice.InvoiceRows = BuildInvoiceRows(request.InvoiceRows, invoice.Id);
            invoice.InvoiceAccountRows = BuildInvoiceAccountRows(request.InvoiceAccountRows, invoice.Id);

            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync();

            var created = await _dbContext.Invoices
                .AsNoTracking()
                .Include(x => x.InvoiceRows)
                .Include(x => x.InvoiceAccountRows)
                .Include(x => x.CreatedByUser)
                .Include(x => x.EditedByUser)
                .FirstAsync(x => x.Id == invoice.Id);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<InvoiceDto>> Update(int id, [FromBody] UpdateInvoiceRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var legacyUser = await _legacyUserResolution.ResolveCurrentUserAsync(User);
            if (legacyUser is null)
            {
                return Unauthorized(new { message = "Could not map authenticated user to a legacy user by email." });
            }

            var invoice = await _dbContext.Invoices
                .Include(x => x.InvoiceRows)
                .Include(x => x.InvoiceAccountRows)
                .Include(x => x.CreatedByUser)
                .Include(x => x.EditedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (invoice is null)
            {
                return NotFound();
            }

            ApplyRequestToInvoice(invoice, request);
            invoice.Edited = SwedishTime.Now;
            invoice.EditedBy = legacyUser.Id;

            _dbContext.InvoiceRows.RemoveRange(invoice.InvoiceRows);
            _dbContext.InvoiceAccountRows.RemoveRange(invoice.InvoiceAccountRows);

            invoice.InvoiceRows = BuildInvoiceRows(request.InvoiceRows, invoice.Id);
            invoice.InvoiceAccountRows = BuildInvoiceAccountRows(request.InvoiceAccountRows, invoice.Id);

            await _dbContext.SaveChangesAsync();

            var updated = await _dbContext.Invoices
                .AsNoTracking()
                .Include(x => x.InvoiceRows)
                .Include(x => x.InvoiceAccountRows)
                .Include(x => x.CreatedByUser)
                .Include(x => x.EditedByUser)
                .FirstAsync(x => x.Id == id);

            return Ok(MapToDto(updated));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var invoice = await _dbContext.Invoices
                .Include(x => x.InvoiceRows)
                .Include(x => x.InvoiceAccountRows)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (invoice is null)
            {
                return NotFound();
            }

            _dbContext.InvoiceRows.RemoveRange(invoice.InvoiceRows);
            _dbContext.InvoiceAccountRows.RemoveRange(invoice.InvoiceAccountRows);
            _dbContext.Invoices.Remove(invoice);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultWithTotalsDto<InvoiceSearchRowDto, InvoiceSearchTotalsDto>>> SearchList([FromBody] SearchInvoiceRequest? request)
        {
            var pagination = request?.Pagination ?? new PaginationRequest();

            IQueryable<Invoice> query = _dbContext.Invoices
            .AsNoTracking();

            if (request?.StartDate is not null)
            {
                var start = request.StartDate.Value.ToDateTime(TimeOnly.MinValue);
                query = query.Where(x => x.InvoiceDate.HasValue && x.InvoiceDate.Value >= start);
            }

            if (request?.EndDate is not null)
            {
                var endExclusive = request.EndDate.Value.ToDateTime(TimeOnly.MinValue).AddDays(1);
                query = query.Where(x => x.InvoiceDate.HasValue && x.InvoiceDate.Value < endExclusive);
            }

            if (request?.InvoiceNumber is not null)
            {
                query = query.Where(x => x.InvoiceNumber == request.InvoiceNumber);
            }

            if (!string.IsNullOrWhiteSpace(request?.CustomerName))
            {
                var customerName = request.CustomerName.Trim();
                query = query.Where(x => x.CustomerName != null && x.CustomerName.Contains(customerName));
            }

            IOrderedQueryable<Invoice> orderedQuery;
            try
            {
                orderedQuery = ApplySearchOrdering(query, request?.OrderBy);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid invoice search-list order by request");
                return BadRequest(new { message = ex.Message });
            }

            var totalCount = await query.CountAsync();
            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pagination.PageSize);

            var totalsRows = await (
                from invoice in query.Select(x => new
                {
                    x.Id,
                    x.IsSettled,
                    x.SalesCurrencyRate,
                    CurrencyRateToSek = x.SalesCurrency != null ? x.SalesCurrency.RateToSek : null
                })
                join row in _dbContext.InvoiceRows.AsNoTracking() on invoice.Id equals row.InvoiceId
                select new
                {
                    invoice.Id,
                    invoice.IsSettled,
                    invoice.SalesCurrencyRate,
                    invoice.CurrencyRateToSek,
                    Amount = row.Sum ?? ((row.NrOf ?? 0d) * (row.UnitPrice ?? 0d)),
                    row.VatGround,
                    row.PriceTypeId
                })
                .ToListAsync();

            decimal totalSumExVat = 0m;
            decimal totalSumInclVat = 0m;
            decimal totalSumInclVatSek = 0m;
            var paidInvoiceIds = new HashSet<int>();

            foreach (var invoiceGroup in totalsRows.GroupBy(x => x.Id))
            {
                decimal invoiceSumExVat = 0m;
                decimal invoiceSumInclVat = 0m;

                foreach (var row in invoiceGroup)
                {
                    var amount = Convert.ToDecimal(row.Amount, CultureInfo.InvariantCulture);
                    if (row.PriceTypeId != 12 && row.PriceTypeId != 13)
                    {
                        invoiceSumExVat += amount;
                    }
                    invoiceSumInclVat += amount;
                }

                var first = invoiceGroup.First();
                var rateToSek = first.SalesCurrencyRate
                    ?? (first.CurrencyRateToSek.HasValue
                        ? Convert.ToDecimal(first.CurrencyRateToSek.Value, CultureInfo.InvariantCulture)
                        : 1m);

                totalSumExVat += invoiceSumExVat;
                totalSumInclVat += invoiceSumInclVat;
                totalSumInclVatSek += invoiceSumInclVat * rateToSek;

                if (first.IsSettled)
                {
                    paidInvoiceIds.Add(first.Id);
                }
            }

            var totals = new InvoiceSearchTotalsDto
            {
                TotalInvoices = totalCount,
                PaidInvoices = paidInvoiceIds.Count,
                UnpaidInvoices = Math.Max(0, totalCount - paidInvoiceIds.Count),
                SumExVat = Math.Round(totalSumExVat, 2),
                SumInclVat = Math.Round(totalSumInclVat, 2),
                SumInclVatSek = Math.Round(totalSumInclVatSek, 2)
            };

            var pagedInvoices = await orderedQuery
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .Select(x => new
                {
                    x.Id,
                    x.InvoiceNumber,
                    x.CustomerName,
                    x.InvoiceDate,
                    x.InvoiceDays,
                    x.InvoiceTypeCode,
                    x.InvoiceOk,
                    x.Printed,
                    x.EndInvoiced,
                    x.IsSettled,
                    CurrencyName = x.SalesCurrency != null ? x.SalesCurrency.Name : null,
                    CurrencyRateToSek = x.SalesCurrency != null ? x.SalesCurrency.RateToSek : null,
                    x.SalesCurrencyRate
                })
                .ToListAsync();

            var invoiceIds = pagedInvoices.Select(x => x.Id).ToList();

            var rowAmounts = await _dbContext.InvoiceRows
                .AsNoTracking()
                .Where(x => x.InvoiceId.HasValue && invoiceIds.Contains(x.InvoiceId.Value))
                .Select(x => new
                {
                    InvoiceId = x.InvoiceId!.Value,
                    Amount = x.Sum ?? ((x.NrOf ?? 0d) * (x.UnitPrice ?? 0d)),
                    x.VatGround,
                    x.PriceTypeId,
                    x.DeliveryFromStockId,
                    x.DeliveryToCustomerId
                })
                .ToListAsync();

            var deliveryFromStockIds = rowAmounts
                .Where(x => x.DeliveryFromStockId.HasValue)
                .Select(x => x.DeliveryFromStockId!.Value)
                .Distinct()
                .ToList();

            var deliveryToCustomerIds = rowAmounts
                .Where(x => x.DeliveryToCustomerId.HasValue)
                .Select(x => x.DeliveryToCustomerId!.Value)
                .Distinct()
                .ToList();

            var deliveriesFromStock = await _dbContext.DeliveryFromStocks
                .AsNoTracking()
                .Where(x => deliveryFromStockIds.Contains(x.Id))
                .Select(x => new
                {
                    x.Id,
                    x.DeliveryDate,
                    OrderNumber = x.CustomerOrder != null ? x.CustomerOrder.CustomerOrderNr : null
                })
                .ToListAsync();

            var deliveriesToCustomer = await _dbContext.DeliveryToCustomers
                .AsNoTracking()
                .Where(x => deliveryToCustomerIds.Contains(x.Id))
                .Select(x => new
                {
                    x.Id,
                    x.DeliveryDate,
                    OrderNumber = x.CustomerOrder != null ? x.CustomerOrder.CustomerOrderNr : null
                })
                .ToListAsync();

            var fromStockMap = deliveriesFromStock.ToDictionary(x => x.Id);
            var toCustomerMap = deliveriesToCustomer.ToDictionary(x => x.Id);

            var items = new List<InvoiceSearchRowDto>(pagedInvoices.Count);
            foreach (var invoice in pagedInvoices)
            {
                var relatedRows = rowAmounts.Where(x => x.InvoiceId == invoice.Id);

                decimal sumExVat = 0m;
                decimal sumInclVat = 0m;
                var orderNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var deliveryDates = new HashSet<DateOnly>();

                foreach (var row in relatedRows)
                {
                    var amount = Convert.ToDecimal(row.Amount, CultureInfo.InvariantCulture);
                    if (row.PriceTypeId != 12 && row.PriceTypeId != 13)
                    {
                        sumExVat += amount;
                    }
                    sumInclVat += amount;

                    if (row.DeliveryFromStockId is int fromId && fromStockMap.TryGetValue(fromId, out var fromDelivery))
                    {
                        if (!string.IsNullOrWhiteSpace(fromDelivery.OrderNumber))
                        {
                            orderNumbers.Add(fromDelivery.OrderNumber);
                        }

                        if (fromDelivery.DeliveryDate.HasValue)
                        {
                            deliveryDates.Add(DateOnly.FromDateTime(fromDelivery.DeliveryDate.Value));
                        }
                    }

                    if (row.DeliveryToCustomerId is int toId && toCustomerMap.TryGetValue(toId, out var toDelivery))
                    {
                        if (!string.IsNullOrWhiteSpace(toDelivery.OrderNumber))
                        {
                            orderNumbers.Add(toDelivery.OrderNumber);
                        }

                        if (toDelivery.DeliveryDate.HasValue)
                        {
                            deliveryDates.Add(DateOnly.FromDateTime(toDelivery.DeliveryDate.Value));
                        }
                    }
                }

                var rateToSek = invoice.SalesCurrencyRate
                    ?? (invoice.CurrencyRateToSek.HasValue
                        ? Convert.ToDecimal(invoice.CurrencyRateToSek.Value, CultureInfo.InvariantCulture)
                        : 1m);

                var dueDate = invoice.InvoiceDate.HasValue && invoice.InvoiceDays.HasValue
                    ? invoice.InvoiceDate.Value.AddDays(invoice.InvoiceDays.Value)
                    : invoice.InvoiceDate;

                var status = invoice.InvoiceTypeCode == InvoiceTypeCode.Credit
                    ? "Credit"
                    : string.Empty;

                items.Add(new InvoiceSearchRowDto
                {
                    Id = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    CustomerName = invoice.CustomerName ?? string.Empty,
                    InvoiceDate = invoice.InvoiceDate,
                    DueDate = dueDate,
                    Status = status,
                    OrderNumbers = string.Join(", ", orderNumbers.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)),
                    DeliveryDates = string.Join(", ", deliveryDates.OrderBy(x => x).Select(x => x.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))),
                    SumExVat = Math.Round(sumExVat, 2),
                    SumInclVat = Math.Round(sumInclVat, 2),
                    CurrencyName = invoice.CurrencyName ?? string.Empty,
                    SumInclVatSek = Math.Round(sumInclVat * rateToSek, 2),
                    IsPaid = invoice.IsSettled
                });
            }

            return Ok(new PagedResultWithTotalsDto<InvoiceSearchRowDto, InvoiceSearchTotalsDto>
            {
                Items = items,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                Totals = new TotalsDto<InvoiceSearchTotalsDto>
                {
                    Values = totals
                }
            });
        }

        [HttpGet("invoice-source/grouped")]
        public async Task<ActionResult<IReadOnlyList<InvoiceDeliveriesAndOrderCostsGroupedCustomerDto>>> GetInvoiceSourceGrouped([FromQuery] bool onlyDelivered = true)
        {
            var rows = await BuildInvoiceSourceRows(onlyDelivered);

            var grouped = rows
                .GroupBy(x => new { x.CustomerId, x.CustomerName, x.CustomerInvoicingInfo })
                .OrderBy(x => x.Key.CustomerName)
                .Select(group => new InvoiceDeliveriesAndOrderCostsGroupedCustomerDto
                {
                    CustomerId = group.Key.CustomerId,
                    CustomerName = group.Key.CustomerName,
                    CustomerInvoicingInfo = group.Key.CustomerInvoicingInfo,
                    Deliveries = group
                        .Where(x => x.Type == "DeliveryToCustomer" || x.Type == "DeliveryFromStock")
                        .OrderBy(x => x.DeliveryDate)
                        .ThenBy(x => x.DeliveryId)
                        .Select(x => new InvoiceDeliveriesAndOrderCostsDeliveryDto
                        {
                            Type = x.Type,
                            DeliveryId = x.DeliveryId ?? 0,
                            ParentDeliveryId = x.ParentDeliveryId ?? 0,
                            SupplierName = x.SupplierName,
                            DeliveryDate = x.DeliveryDate,
                            CallOffId = x.CallOffId,
                            CallOffNr = x.CallOffNr,
                            CustomerOrderId = x.CustomerOrderId,
                            CustomerOrderNr = x.CustomerOrderNr,
                            CustomersOwnOrderNr = x.CustomersOwnOrderNr,
                            Quantity = x.Quantity,
                            PurchasePrice = x.PurchasePrice,
                            PurchasePriceCurrencyName = x.PurchasePriceCurrencyName,
                            UnitMultiplicator = x.UnitMultiplicator,
                            WeightPer1000 = x.WeightPer1000,
                            NrOfNotInvoicedPallets = x.NrOfNotInvoicedPallets ?? 0,
                            NrOfNotInvoicedOrderCosts = x.NrOfNotInvoicedOrderCosts ?? 0,
                            HasMissingWeight = x.HasMissingWeight,
                            IsWeightMissingEmailSent = x.IsWeightMissingEmailSent,
                            HasMissingCostSalesPrice = x.HasMissingCostSalesPrice,
                            IsCostMissingEmailSent = x.IsCostMissingEmailSent
                        })
                        .ToList(),
                    OrderCosts = group
                        .Where(x =>
                                (x.Type == "OrderCost"
                                || x.Type == "OrderCostSeparately"
                                || x.Type == "OrderCostImmediateSeparately")
                                && x.OrderCostId.HasValue)
                        .OrderBy(x => x.OrderCostId)
                        .Select(x => new InvoiceDeliveriesAndOrderCostsOrderCostDto
                        {
                            Type = x.Type,
                            Id = x.OrderCostId ?? 0,
                            CostId = x.CostId,
                            CostName = NormalizeOrderCostName(x.CostName) ?? string.Empty,
                            CustomerOrderId = x.CustomerOrderId,
                            CustomerOrderNr = x.CustomerOrderNr,
                            Quantity = x.Quantity,
                            PurchasePrice = x.PurchasePrice,
                            PurchasePriceCurrencyName = x.PurchasePriceCurrencyName,
                            HasMissingCostSalesPrice = x.HasMissingCostSalesPrice
                        })
                        .ToList(),
                    // SupplierOrders = group
                    //     .Where(x => x.Type == "SupplierOrder")
                    //     .OrderBy(x => x.SupplierOrderId)
                    //     .Select(x => new InvoiceDeliveriesAndOrderCostsSupplierOrderDto
                    //     {
                    //         SupplierOrderId = x.SupplierOrderId ?? 0,
                    //         SupplierName = x.SupplierName,
                    //         SupplierOrderNr = x.SupplierOrderNr,
                    //         Quantity = x.Quantity,
                    //         PurchasePrice = x.PurchasePrice,
                    //         PurchasePriceCurrencyName = x.PurchasePriceCurrencyName,
                    //         UnitMultiplicator = x.UnitMultiplicator,
                    //         WeightPer1000 = x.WeightPer1000,
                    //         HasMissingCostSalesPrice = x.HasMissingCostSalesPrice
                    //     })
                    //     .ToList(),
                })
                .ToList();

            return Ok(grouped);
        }

        [HttpGet("invoice-source/list")]
        public async Task<ActionResult<IReadOnlyList<InvoiceDeliveriesAndOrderCostsListRowDto>>> GetInvoiceSourceList([FromQuery] bool onlyDelivered = true)
        {
            var rows = await BuildInvoiceSourceRows(onlyDelivered);
            var list = rows
                .OrderBy(x => x.CustomerName)
                .ThenBy(x => x.Type)
                .ThenBy(x => x.DeliveryDate)
                .ThenBy(x => x.DeliveryId)
                .ThenBy(x => x.OrderCostId)
                .ThenBy(x => x.SupplierOrderId)
                .Select(x => new InvoiceDeliveriesAndOrderCostsListRowDto
                {
                    Type = x.Type,
                    CustomerId = x.CustomerId,
                    CustomerName = x.CustomerName,
                    CustomerInvoicingInfo = x.CustomerInvoicingInfo,
                    DeliveryId = x.DeliveryId,
                    ParentDeliveryId = x.ParentDeliveryId,
                    SupplierName = x.SupplierName,
                    DeliveryDate = x.DeliveryDate,
                    CallOffId = x.CallOffId,
                    CallOffNr = x.CallOffNr,
                    CustomerOrderId = x.CustomerOrderId,
                    CustomerOrderNr = x.CustomerOrderNr,
                    CustomersOwnOrderNr = x.CustomersOwnOrderNr,
                    Quantity = x.Quantity,
                    PurchasePrice = x.PurchasePrice,
                    PurchasePriceCurrencyName = x.PurchasePriceCurrencyName,
                    UnitMultiplicator = x.UnitMultiplicator,
                    WeightPer1000 = x.WeightPer1000,
                    NrOfNotInvoicedPallets = x.NrOfNotInvoicedPallets,
                    NrOfNotInvoicedOrderCosts = x.NrOfNotInvoicedOrderCosts,
                    HasMissingWeight = x.HasMissingWeight,
                    IsWeightMissingEmailSent = x.IsWeightMissingEmailSent,
                    HasMissingCostSalesPrice = x.HasMissingCostSalesPrice,
                    IsCostMissingEmailSent = x.IsCostMissingEmailSent,
                    OrderCostId = x.OrderCostId,
                    CostId = x.CostId,
                    CostName = NormalizeOrderCostName(x.CostName),
                    SupplierOrderId = x.SupplierOrderId,
                    SupplierOrderNr = x.SupplierOrderNr,
                })
                .ToList();

            return Ok(list);
        }

        [HttpPost("draft/from-selection")]
        public async Task<ActionResult<InvoiceDto>> CreateDraftFromSelection([FromBody] CreateInvoiceDraftFromSelectionRequest request)
        {
            if (request.CustomerId <= 0)
            {
                return BadRequest(new { message = "CustomerId must be a positive integer." });
            }

            var customer = await _dbContext.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.CustomerId);

            if (customer is null)
            {
                return NotFound(new { message = $"Customer {request.CustomerId} was not found." });
            }

            var deliverySelections = (request.Deliveries ?? new List<InvoiceDraftDeliverySelectionRequest>())
                .Where(x => x is not null && x.Id > 0 && !string.IsNullOrWhiteSpace(x.Type))
                .Select(x => new InvoiceDraftDeliverySelectionRequest
                {
                    Id = x.Id,
                    Type = x.Type.Trim().ToLowerInvariant()
                })
                .ToDictionary(x => $"{x.Type}|{x.Id}", x => x)
                .Values
                .ToList();

            var unsupportedTypes = deliverySelections
                .Select(x => x.Type)
                .Where(x => !IsSupportedDraftDeliveryType(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (unsupportedTypes.Count > 0)
            {
                return BadRequest(new { message = $"Unsupported delivery type(s): {string.Join(", ", unsupportedTypes)}." });
            }

            var deliveryToCustomerIds = deliverySelections
                .Where(x => IsDeliveryToCustomerType(x.Type))
                .Select(x => x.Id)
                .Distinct()
                .ToList();

            var deliveryFromStockIds = deliverySelections
                .Where(x => IsDeliveryFromStockType(x.Type))
                .Select(x => x.Id)
                .Distinct()
                .ToList();

            var orderCostIds = (request.OrderCostIds ?? new List<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            var selectedDeliveryRows = new List<DraftDeliveryRowSource>();

            if (deliveryToCustomerIds.Count > 0)
            {
                var rows = await _dbContext.DeliveryToCustomers
                    .AsNoTracking()
                    .Where(x => deliveryToCustomerIds.Contains(x.Id))
                    .Where(x => (x.CustomerOrder != null ? x.CustomerOrder.CustomerId : (x.SupplierOrder != null ? x.SupplierOrder.CustomerId : null)) == customer.Id)
                    .Select(x => new DraftDeliveryRowSource
                    {
                        Type = "DeliveryToCustomer",
                        DeliveryId = x.Id,
                        DeliveryDate = x.DeliveryDate,
                        CallOffNr = x.CallOff,
                        CustomerOrderId = x.CustomerOrderId,
                        CustomerOrderNr = x.CustomerOrder != null ? x.CustomerOrder.CustomerOrderNr : null,
                        Quantity = x.NrOfItems,
                        UnitPrice = x.CustomerOrder != null ? x.CustomerOrder.SalesPrice : null,
                        UnitId = x.CustomerOrder != null ? x.CustomerOrder.UnitId : null,
                        UnitMultiplicator = x.CustomerOrder != null && x.CustomerOrder.Unit != null
                            ? x.CustomerOrder.Unit.Multiplicator
                            : null,
                    })
                    .ToListAsync();

                selectedDeliveryRows.AddRange(rows);
            }

            if (deliveryFromStockIds.Count > 0)
            {
                var rows = await _dbContext.DeliveryFromStocks
                    .AsNoTracking()
                    .Where(x => deliveryFromStockIds.Contains(x.Id))
                    .Where(x => x.CustomerOrder != null && x.CustomerOrder.CustomerId == customer.Id)
                    .Select(x => new DraftDeliveryRowSource
                    {
                        Type = "DeliveryFromStock",
                        DeliveryId = x.Id,
                        DeliveryDate = x.DeliveryDate,
                        CallOffNr = x.CallOffDeliveries
                            .OrderBy(cd => cd.Id)
                            .Select(cd => cd.CallOffId.HasValue ? cd.CallOffId.Value.ToString() : null)
                            .FirstOrDefault(),
                        CustomerOrderId = x.CustomerOrderId,
                        CustomerOrderNr = x.CustomerOrder != null ? x.CustomerOrder.CustomerOrderNr : null,
                        Quantity = x.NrOfItems,
                        UnitPrice = x.CustomerOrder != null ? x.CustomerOrder.SalesPrice : null,
                        UnitId = x.CustomerOrder != null ? x.CustomerOrder.UnitId : null,
                        UnitMultiplicator = x.CustomerOrder != null && x.CustomerOrder.Unit != null
                            ? x.CustomerOrder.Unit.Multiplicator
                            : null,
                    })
                    .ToListAsync();

                selectedDeliveryRows.AddRange(rows);
            }

            if (selectedDeliveryRows.Count != deliverySelections.Count)
            {
                return BadRequest(new { message = "One or more selected deliveries were not found or do not belong to the specified customer." });
            }

            var selectedOrderCostRows = new List<DraftOrderCostRowSource>();
            if (orderCostIds.Count > 0)
            {
                selectedOrderCostRows = await _dbContext.OrderCosts
                    .AsNoTracking()
                    .Where(x => orderCostIds.Contains(x.Id))
                    .Where(x => x.CustomerOrder != null && x.CustomerOrder.CustomerId == customer.Id)
                    .Select(x => new DraftOrderCostRowSource
                    {
                        OrderCostId = x.Id,
                        CustomerOrderId = x.CustomerOrderId,
                        CustomerOrderNr = x.CustomerOrder != null ? x.CustomerOrder.CustomerOrderNr : null,
                        CostName = x.Cost != null ? x.Cost.Name : null,
                        Quantity = x.NrOf,
                        UnitPrice = x.OutPrice ?? x.InPrice,
                        UnitId = x.CustomerOrder != null ? x.CustomerOrder.UnitId : null,
                        UnitMultiplicator = x.CustomerOrder != null && x.CustomerOrder.Unit != null
                            ? x.CustomerOrder.Unit.Multiplicator
                            : null,
                    })
                    .ToListAsync();

                if (selectedOrderCostRows.Count != orderCostIds.Count)
                {
                    return BadRequest(new { message = "One or more selected order costs were not found or do not belong to the specified customer." });
                }
            }

            var accountNrReceivable = customer.AccountNrAccountsReceivable.HasValue
                ? customer.AccountNrAccountsReceivable.Value.ToString(CultureInfo.InvariantCulture)
                : null;
            var accountNrEarnings = customer.AccountNrEarnings.HasValue
                ? customer.AccountNrEarnings.Value.ToString(CultureInfo.InvariantCulture)
                : null;

            var invoiceRowRequests = new List<InvoiceRowMutationRequest>();
            var sortOrder = 1;

            foreach (var delivery in selectedDeliveryRows.OrderBy(x => x.DeliveryDate).ThenBy(x => x.DeliveryId))
            {
                var nrOf = delivery.Quantity;
                var unitPrice = delivery.UnitPrice;

                invoiceRowRequests.Add(new InvoiceRowMutationRequest
                {
                    SortOrder = sortOrder,
                    DeliveryToCustomerId = delivery.Type == "DeliveryToCustomer" ? delivery.DeliveryId : null,
                    DeliveryFromStockId = delivery.Type == "DeliveryFromStock" ? delivery.DeliveryId : null,
                    Text = BuildDraftDeliveryText(delivery),
                    NrOf = nrOf,
                    UnitPrice = unitPrice,
                    Sum = ComputeDraftRowSum(nrOf, unitPrice, delivery.UnitMultiplicator),
                    VatGround = true,
                    Calculate = true,
                    AccountNr = accountNrEarnings,
                    UnitId = delivery.UnitId,
                });

                sortOrder++;
            }

            foreach (var orderCost in selectedOrderCostRows.OrderBy(x => x.OrderCostId))
            {
                var nrOf = orderCost.Quantity.HasValue ? (double?)orderCost.Quantity.Value : null;
                var unitPrice = orderCost.UnitPrice.HasValue ? (double?)orderCost.UnitPrice.Value : null;

                invoiceRowRequests.Add(new InvoiceRowMutationRequest
                {
                    SortOrder = sortOrder,
                    OrderCostId = orderCost.OrderCostId,
                    Text = BuildDraftOrderCostText(orderCost),
                    NrOf = nrOf,
                    UnitPrice = unitPrice,
                    Sum = ComputeDraftRowSum(nrOf, unitPrice, orderCost.UnitMultiplicator),
                    VatGround = true,
                    Calculate = true,
                    AccountNr = accountNrEarnings,
                    UnitId = orderCost.UnitId,
                });

                sortOrder++;
            }

            var vatBase = invoiceRowRequests
                .Where(x => x.VatGround)
                .Sum(x => x.Sum ?? 0d);
            var vatValue = Math.Round(vatBase * 0.25d, 2, MidpointRounding.AwayFromZero);

            var totalBeforeRounding = invoiceRowRequests.Sum(x => x.Sum ?? 0d) + vatValue;
            var roundedTotal = Math.Round(totalBeforeRounding, 0, MidpointRounding.AwayFromZero);
            var roundingValue = Math.Round(roundedTotal - totalBeforeRounding, 2, MidpointRounding.AwayFromZero);

            invoiceRowRequests.Add(new InvoiceRowMutationRequest
            {
                SortOrder = sortOrder++,
                PriceTypeId = 13,
                Text = "Moms",
                Sum = vatValue,
                VatGround = false,
                Calculate = false,
            });

            invoiceRowRequests.Add(new InvoiceRowMutationRequest
            {
                SortOrder = sortOrder,
                PriceTypeId = 12,
                Text = "Oresavrundning",
                Sum = roundingValue,
                VatGround = false,
                Calculate = false,
            });

            var invoiceRows = BuildInvoiceRows(invoiceRowRequests, 0);

            var accountRowRequests = invoiceRows
                .Where(x => (x.Sum ?? 0d) != 0d)
                .Select(x => new InvoiceAccountRowMutationRequest
                {
                    Text = x.Text,
                    NrOf = x.NrOf,
                    UnitPrice = x.UnitPrice,
                    Sum = x.Sum,
                    AccountNr = x.AccountNr,
                    CostCenter = x.CostCenter,
                })
                .ToList();

            var invoice = new Invoice
            {
                Id = 0,
                InvoiceTypeCode = InvoiceTypeCode.Normal,
                InvoiceNumber = await GetNextInvoiceNumberAsync(),
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                Address = customer.Address,
                Address2 = customer.Address2,
                PostalNr = customer.PostalNr,
                PostalAddress = customer.PostalAddress,
                Country = customer.Country,
                VatNr = customer.VATNr,
                InvoiceDate = SwedishTime.Now.Date,
                InvoiceDays = customer.PaymentDays ?? 30,
                TermsOfPayment = customer.TermsOfPayment,
                SalesCurrencyId = customer.CurrencyId,
                Account = accountNrReceivable,
                YourReference = customer.Reference,
                LanguageId = customer.LanguageId,
                Created = SwedishTime.Now,
                Edited = SwedishTime.Now,
                InvoiceRows = invoiceRows,
                InvoiceAccountRows = BuildInvoiceAccountRows(accountRowRequests, 0),
            };

            return Ok(MapToDto(invoice));
        }

        private static string? NormalizeOrderCostName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            var span = name.AsSpan();
            var index = 0;

            while (index < span.Length && char.IsWhiteSpace(span[index]))
            {
                index++;
            }

            var digitsStart = index;
            while (index < span.Length && char.IsDigit(span[index]))
            {
                index++;
            }

            if (index == digitsStart)
            {
                return name;
            }

            var whitespaceStart = index;
            while (index < span.Length && char.IsWhiteSpace(span[index]))
            {
                index++;
            }

            if (index == whitespaceStart || index >= span.Length)
            {
                return name;
            }

            return name[index..].Trim();
        }

        private static bool IsSupportedDraftDeliveryType(string type)
            => IsDeliveryToCustomerType(type) || IsDeliveryFromStockType(type);

        private static bool IsDeliveryToCustomerType(string type)
            => string.Equals(type, "DeliveryToCustomer", StringComparison.OrdinalIgnoreCase);

        private static bool IsDeliveryFromStockType(string type)
            => string.Equals(type, "DeliveryFromStock", StringComparison.OrdinalIgnoreCase);

        private static string BuildDraftDeliveryText(DraftDeliveryRowSource source)
        {
            var label = source.Type == "DeliveryToCustomer" ? "Direktleverans" : "Lagerleverans";
            var parts = new List<string> { label };

            if (source.DeliveryDate.HasValue)
            {
                parts.Add(source.DeliveryDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrWhiteSpace(source.CustomerOrderNr))
            {
                parts.Add($"Order {source.CustomerOrderNr}");
            }

            if (!string.IsNullOrWhiteSpace(source.CallOffNr))
            {
                parts.Add($"Avrop {source.CallOffNr}");
            }

            return string.Join(" - ", parts);
        }

        private static string BuildDraftOrderCostText(DraftOrderCostRowSource source)
        {
            if (!string.IsNullOrWhiteSpace(source.CostName) && !string.IsNullOrWhiteSpace(source.CustomerOrderNr))
            {
                return $"{NormalizeOrderCostName(source.CostName)} - Order {source.CustomerOrderNr}";
            }

            if (!string.IsNullOrWhiteSpace(source.CostName))
            {
                return NormalizeOrderCostName(source.CostName) ?? source.CostName;
            }

            if (!string.IsNullOrWhiteSpace(source.CustomerOrderNr))
            {
                return $"Orderkostnad - Order {source.CustomerOrderNr}";
            }

            return "Orderkostnad";
        }

        private static double? ComputeDraftRowSum(double? nrOf, double? unitPrice, short? unitMultiplicator)
        {
            if (!nrOf.HasValue || !unitPrice.HasValue)
            {
                return null;
            }

            var multiplicator = unitMultiplicator.HasValue && unitMultiplicator.Value > 0
                ? unitMultiplicator.Value
                : (short)1;

            return Math.Round((nrOf.Value * unitPrice.Value) / multiplicator, 2, MidpointRounding.AwayFromZero);
        }

        private async Task<List<InvoiceSourceRow>> BuildInvoiceSourceRows(bool onlyDelivered)
        {
            DateTime MinFromDate = new DateTime(2016, 1, 1, 0, 0, 0);
            var deliveryToCustomerRows = await _dbContext.DeliveryToCustomers
                .AsNoTracking()
                .Where(x =>
                    !x.DoNotInvoice &&
                    !x.InvoiceRows.Any() &&
                    x.NrOfItems > 0 &&
                    x.DeliveryDate.HasValue && x.DeliveryDate.Value >= MinFromDate &&
                    x.CustomerOrder != null && !x.CustomerOrder.Completed &&
                    (!onlyDelivered || x.DeliveryStatus == (int)DeliveryStatus.Delivered)
                )
                .Select(x => new InvoiceSourceRow
                {
                    Type = "DeliveryToCustomer",
                    CustomerId = x.CustomerOrder != null
                        ? x.CustomerOrder.CustomerId
                        : (x.SupplierOrder != null ? x.SupplierOrder.CustomerId : null),
                    CustomerName = x.CustomerOrder != null
                        ? (x.CustomerOrder.CustomerName ?? string.Empty)
                        : (x.SupplierOrder != null
                            ? (x.SupplierOrder.Customer != null ? (x.SupplierOrder.Customer.Name ?? string.Empty) : string.Empty)
                            : string.Empty),
                    CustomerInvoicingInfo = x.CustomerOrder != null ? x.CustomerOrder.InvoicingInfo : null,
                    DeliveryId = x.Id,
                    ParentDeliveryId = x.ParentDeliveryId,
                    SupplierName = x.SupplierOrder != null
                        ? (x.SupplierOrder.SupplierName ?? string.Empty)
                        : (x.CustomerOrder != null && x.CustomerOrder.SupplierOrder != null
                            ? (x.CustomerOrder.SupplierOrder.SupplierName ?? string.Empty)
                            : string.Empty),
                    DeliveryDate = x.DeliveryDate,
                    CustomerOrderId = x.CustomerOrderId,
                    CustomerOrderNr = x.CustomerOrder != null ? x.CustomerOrder.CustomerOrderNr : null,
                    CustomersOwnOrderNr = x.CustomerOrder != null ? x.CustomerOrder.YourOrderNr : null,
                    Quantity = x.NrOfItems.HasValue ? (decimal?)x.NrOfItems.Value : null,
                    PurchasePrice = x.SupplierOrder != null
                        ? (x.SupplierOrder.PurchasePrice.HasValue ? (decimal?)x.SupplierOrder.PurchasePrice.Value : null)
                        : (x.CustomerOrder != null && x.CustomerOrder.SupplierOrder != null && x.CustomerOrder.SupplierOrder.PurchasePrice.HasValue
                            ? (decimal?)x.CustomerOrder.SupplierOrder.PurchasePrice.Value
                            : null),
                    PurchasePriceCurrencyName = x.SupplierOrder != null
                        ? (x.SupplierOrder.PurchaseCurrency != null ? x.SupplierOrder.PurchaseCurrency.Name : null)
                        : (x.CustomerOrder != null && x.CustomerOrder.SupplierOrder != null && x.CustomerOrder.SupplierOrder.PurchaseCurrency != null
                            ? x.CustomerOrder.SupplierOrder.PurchaseCurrency.Name
                            : null),
                    UnitMultiplicator = x.SupplierOrder != null
                        ? (x.SupplierOrder.Unit != null ? x.SupplierOrder.Unit.Multiplicator : null)
                        : (x.CustomerOrder != null
                            ? (x.CustomerOrder.Unit != null ? x.CustomerOrder.Unit.Multiplicator : null)
                            : null),
                    WeightPer1000 = x.CustomerOrder != null
                        && x.CustomerOrder.SelectedCalculationRow != null
                        && x.CustomerOrder.SelectedCalculationRow.Calculation != null
                        && x.CustomerOrder.SelectedCalculationRow.Calculation.Product != null
                            ? x.CustomerOrder.SelectedCalculationRow.Calculation.Product.NetWeightPer1000
                            : null,
                    NrOfNotInvoicedPallets = !x.IsPalletInvoicedSeparately ? (x.NrOfPallets ?? 0) : 0,
                    NrOfNotInvoicedOrderCosts = x.CustomerOrder != null
                        ? x.CustomerOrder.OrderCosts.Count(oc => !oc.IsCostInvoicedSeparately)
                        : (x.SupplierOrder != null ? x.SupplierOrder.OrderCosts.Count(oc => !oc.IsCostInvoicedSeparately) : 0),
                    HasMissingWeight = !(x.CustomerOrder != null
                        && x.CustomerOrder.SelectedCalculationRow != null
                        && x.CustomerOrder.SelectedCalculationRow.Calculation != null
                        && x.CustomerOrder.SelectedCalculationRow.Calculation.Product != null
                        && (x.CustomerOrder.SelectedCalculationRow.Calculation.Product.NetWeightPer1000 ?? 0) != 0),
                    IsWeightMissingEmailSent = x.WeightMissingEmailSentDateTime.HasValue,
                    HasMissingCostSalesPrice = x.CustomerOrder != null
                        && x.CustomerOrder.OrderCosts.Any(oc => !oc.OutPrice.HasValue && !oc.DoDebit),
                    IsCostMissingEmailSent = x.CostMissingEmailSentDateTime.HasValue,
                    SupplierOrderId = x.SupplierOrderId,
                    SupplierOrderNr = x.SupplierOrder != null ? x.SupplierOrder.SupplierOrderNr : null,
                })
                .ToListAsync();

            var deliveryFromStockRows = await _dbContext.DeliveryFromStocks
                .AsNoTracking()
                .Where(x =>
                    !x.DoNotInvoice &&
                    !x.InvoiceRows.Any() &&
                    x.NrOfItems > 0 &&
                    x.DeliveryDate.HasValue && x.DeliveryDate.Value >= MinFromDate &&
                    x.CustomerOrder != null && !x.CustomerOrder.Completed &&
                    (!onlyDelivered || x.DeliveryStatus == (int)DeliveryStatus.Delivered)
                )
                .Select(x => new InvoiceSourceRow
                {
                    Type = "DeliveryFromStock",
                    CustomerId = x.CustomerOrder != null ? x.CustomerOrder.CustomerId : null,
                    CustomerName = x.CustomerOrder != null
                        ? (EF.Functions.Collate(x.CustomerOrder.CustomerName, "Finnish_Swedish_CI_AS") ?? string.Empty)
                        : string.Empty,
                    CustomerInvoicingInfo = x.CustomerOrder != null ? x.CustomerOrder.InvoicingInfo : null,
                    DeliveryId = x.Id,
                    ParentDeliveryId = x.ParentDeliveryId,
                    SupplierName = x.CustomerOrder != null && x.CustomerOrder.SupplierOrder != null
                        ? (x.CustomerOrder.SupplierOrder.SupplierName ?? string.Empty)
                        : string.Empty,
                    DeliveryDate = x.DeliveryDate,
                    CallOffId = x.CallOffDeliveries.FirstOrDefault() != null ? x.CallOffDeliveries.FirstOrDefault()!.CallOffId : null,
                    CallOffNr = x.CallOffDeliveries.FirstOrDefault() != null ? x.CallOffDeliveries.FirstOrDefault()!.CallOffId.ToString() : null,
                    CustomerOrderId = x.CustomerOrderId,
                    CustomerOrderNr = x.CustomerOrder != null ? x.CustomerOrder.CustomerOrderNr : null,
                    CustomersOwnOrderNr = x.CustomerOrder != null ? x.CustomerOrder.YourOrderNr : null,
                    Quantity = x.NrOfItems.HasValue ? (decimal?)x.NrOfItems.Value : null,
                    PurchasePrice = x.CustomerOrder != null && x.CustomerOrder.SupplierOrder != null && x.CustomerOrder.SupplierOrder.PurchasePrice.HasValue
                        ? (decimal?)x.CustomerOrder.SupplierOrder.PurchasePrice.Value
                        : null,
                    PurchasePriceCurrencyName = x.CustomerOrder != null
                        && x.CustomerOrder.SupplierOrder != null
                        && x.CustomerOrder.SupplierOrder.PurchaseCurrency != null
                            ? x.CustomerOrder.SupplierOrder.PurchaseCurrency.Name
                            : null,
                    UnitMultiplicator = x.CustomerOrder != null
                        ? (x.CustomerOrder.Unit != null ? x.CustomerOrder.Unit.Multiplicator : null)
                        : null,
                    WeightPer1000 = x.CustomerOrder != null
                        && x.CustomerOrder.SelectedCalculationRow != null
                        && x.CustomerOrder.SelectedCalculationRow.Calculation != null
                        && x.CustomerOrder.SelectedCalculationRow.Calculation.Product != null
                            ? x.CustomerOrder.SelectedCalculationRow.Calculation.Product.NetWeightPer1000
                            : null,
                    NrOfNotInvoicedPallets = !x.IsPalletInvoicedSeparately ? (x.NrOfPallets ?? 0) : 0,
                    NrOfNotInvoicedOrderCosts = x.CustomerOrder != null
                        ? x.CustomerOrder.OrderCosts.Count(oc => !oc.IsCostInvoicedSeparately)
                        : 0,
                    HasMissingWeight = !(x.CustomerOrder != null
                        && x.CustomerOrder.SelectedCalculationRow != null
                        && x.CustomerOrder.SelectedCalculationRow.Calculation != null
                        && x.CustomerOrder.SelectedCalculationRow.Calculation.Product != null
                        && (x.CustomerOrder.SelectedCalculationRow.Calculation.Product.NetWeightPer1000 ?? 0) != 0),
                    IsWeightMissingEmailSent = x.WeightMissingEmailSentDateTime.HasValue,
                    HasMissingCostSalesPrice = x.CustomerOrder != null
                        && x.CustomerOrder.OrderCosts.Any(oc => !oc.OutPrice.HasValue && !oc.DoDebit),
                    IsCostMissingEmailSent = x.CostMissingEmailSentDateTime.HasValue,
                    SupplierOrderId = x.CustomerOrder != null ? x.CustomerOrder.SupplierOrderId : null,
                    SupplierOrderNr = x.CustomerOrder != null && x.CustomerOrder.SupplierOrder != null
                        ? x.CustomerOrder.SupplierOrder.SupplierOrderNr
                        : null,
                })
                .ToListAsync();

            var orderCostRowsFromCustomerOrders = await _dbContext.OrderCosts
                .AsNoTracking()
                .Where(x =>
                    !x.InvoiceRows.Any()
                    && x.DoDebit
                    && x.CreatedDateTime != null && x.CreatedDateTime.Value >= new DateTime(2026, 1, 1, 0, 0, 0)
                    && x.CustomerOrder != null
                    && (
                        !onlyDelivered
                        || (
                            x.CustomerOrder.DeliveryFromStocks.Any(d => d.DeliveryStatus == (int)DeliveryStatus.Delivered)
                            || x.CustomerOrder.DeliveryToCustomers.Any(d => d.DeliveryStatus == (int)DeliveryStatus.Delivered)
                            )
                        )
                    )
                .Select(x => new InvoiceSourceRow
                {
                    Type = x.DoInvoiceSeparatelyImmediately
                        ? "OrderCostImmediateSeparately"
                        : x.DoInvoiceSeparately
                            ? "OrderCostSeparately"
                            : "OrderCost",
                    CustomerId = x.CustomerOrder != null ? x.CustomerOrder.CustomerId : null,
                    CustomerName = x.CustomerOrder != null ? (x.CustomerOrder.CustomerName ?? string.Empty) : string.Empty,
                    CustomerInvoicingInfo = x.CustomerOrder != null ? x.CustomerOrder.InvoicingInfo : null,
                    SupplierName = x.SupplierOrder != null
                        ? (EF.Functions.Collate(x.SupplierOrder.SupplierName, "Finnish_Swedish_CI_AS") ?? string.Empty)
                        : (EF.Functions.Collate(x.SupplierName, "Finnish_Swedish_CI_AS") ?? string.Empty),
                    DeliveryDate = null,
                    CustomerOrderId = x.CustomerOrderId,
                    CustomerOrderNr = x.CustomerOrder != null ? x.CustomerOrder.CustomerOrderNr : null,
                    CustomersOwnOrderNr = x.CustomerOrder != null ? x.CustomerOrder.YourOrderNr : null,
                    Quantity = x.NrOf,
                    PurchasePrice = x.InPrice,
                    PurchasePriceCurrencyName = x.InPriceCurrency != null
                        ? EF.Functions.Collate(x.InPriceCurrency.Name, "Finnish_Swedish_CI_AS")
                        : (x.SupplierOrder != null && x.SupplierOrder.PurchaseCurrency != null
                            ? EF.Functions.Collate(x.SupplierOrder.PurchaseCurrency.Name, "Finnish_Swedish_CI_AS")
                            : null),
                    UnitMultiplicator = x.CustomerOrder != null && x.CustomerOrder.Unit != null
                        ? x.CustomerOrder.Unit.Multiplicator
                        : (x.SupplierOrder != null && x.SupplierOrder.Unit != null
                            ? x.SupplierOrder.Unit.Multiplicator
                            : null),
                    WeightPer1000 = x.CustomerOrder != null
                        && x.CustomerOrder.SelectedCalculationRow != null
                        && x.CustomerOrder.SelectedCalculationRow.Calculation != null
                        && x.CustomerOrder.SelectedCalculationRow.Calculation.Product != null
                            ? x.CustomerOrder.SelectedCalculationRow.Calculation.Product.NetWeightPer1000
                            : (x.SupplierOrder != null
                                && x.SupplierOrder.SelectedCalculationRow != null
                                && x.SupplierOrder.SelectedCalculationRow.Calculation != null
                                && x.SupplierOrder.SelectedCalculationRow.Calculation.Product != null
                                    ? x.SupplierOrder.SelectedCalculationRow.Calculation.Product.NetWeightPer1000
                                    : null),
                    HasMissingCostSalesPrice = !x.OutPrice.HasValue,
                    OrderCostId = x.Id,
                    CostId = x.CostId,
                    CostName = x.Cost != null ? x.Cost.Name : null,
                    SupplierOrderId = x.SupplierOrderId,
                    SupplierOrderNr = x.SupplierOrder != null ? x.SupplierOrder.SupplierOrderNr : null,
                })
                .ToListAsync();

            // var orderCostRowsFromSupplierOrders = await _dbContext.OrderCosts
            //     .AsNoTracking()
            //     .Where(x => !x.IsCostInvoicedSeparately && !x.InvoiceRows.Any() && x.CustomerOrderId == null && x.SupplierOrderId != null)
            //     .Select(x => new InvoiceSourceRow
            //     {
            //         Type = "OrderCost",
            //         CustomerId = x.SupplierOrder != null ? x.SupplierOrder.CustomerId : null,
            //         CustomerName = x.SupplierOrder != null && x.SupplierOrder.Customer != null
            //             ? (EF.Functions.Collate(x.SupplierOrder.Customer.Name, "Finnish_Swedish_CI_AS") ?? string.Empty)
            //             : string.Empty,
            //         CustomerInvoicingInfo = null,
            //         SupplierName = x.SupplierOrder != null
            //             ? (EF.Functions.Collate(x.SupplierOrder.SupplierName, "Finnish_Swedish_CI_AS") ?? string.Empty)
            //             : (EF.Functions.Collate(x.SupplierName, "Finnish_Swedish_CI_AS") ?? string.Empty),
            //         DeliveryDate = null,
            //         CustomerOrderId = null,
            //         CustomerOrderNr = null,
            //         CustomersOwnOrderNr = null,
            //         Quantity = x.NrOf,
            //         PurchasePrice = x.InPrice,
            //         PurchasePriceCurrencyName = x.InPriceCurrency != null
            //             ? EF.Functions.Collate(x.InPriceCurrency.Name, "Finnish_Swedish_CI_AS")
            //             : (x.SupplierOrder != null && x.SupplierOrder.PurchaseCurrency != null
            //                 ? EF.Functions.Collate(x.SupplierOrder.PurchaseCurrency.Name, "Finnish_Swedish_CI_AS")
            //                 : null),
            //         UnitMultiplicator = x.SupplierOrder != null && x.SupplierOrder.Unit != null ? x.SupplierOrder.Unit.Multiplicator : null,
            //         WeightPer1000 = x.SupplierOrder != null
            //             && x.SupplierOrder.SelectedCalculationRow != null
            //             && x.SupplierOrder.SelectedCalculationRow.Calculation != null
            //             && x.SupplierOrder.SelectedCalculationRow.Calculation.Product != null
            //                 ? x.SupplierOrder.SelectedCalculationRow.Calculation.Product.NetWeightPer1000
            //                 : null,
            //         HasMissingCostSalesPrice = !x.OutPrice.HasValue,
            //         OrderCostId = x.Id,
            //         CostId = x.CostId,
            //         CostName = x.Cost != null ? x.Cost.Name : null,
            //         SupplierOrderId = x.SupplierOrderId,
            //         SupplierOrderNr = x.SupplierOrder != null ? x.SupplierOrder.SupplierOrderNr : null,
            //     })
            //     .ToListAsync();

            // var orderCostRowsWithoutOrders = await _dbContext.OrderCosts
            //     .AsNoTracking()
            //     .Where(x => !x.IsCostInvoicedSeparately && !x.InvoiceRows.Any() && x.CustomerOrderId == null && x.SupplierOrderId == null)
            //     .Select(x => new InvoiceSourceRow
            //     {
            //         Type = "OrderCost",
            //         CustomerId = null,
            //         CustomerName = string.Empty,
            //         CustomerInvoicingInfo = null,
            //         SupplierName = EF.Functions.Collate(x.SupplierName, "Finnish_Swedish_CI_AS") ?? string.Empty,
            //         DeliveryDate = null,
            //         CustomerOrderId = null,
            //         CustomerOrderNr = null,
            //         CustomersOwnOrderNr = null,
            //         Quantity = x.NrOf,
            //         PurchasePrice = x.InPrice,
            //         PurchasePriceCurrencyName = x.InPriceCurrency != null
            //             ? EF.Functions.Collate(x.InPriceCurrency.Name, "Finnish_Swedish_CI_AS")
            //             : null,
            //         UnitMultiplicator = null,
            //         WeightPer1000 = null,
            //         HasMissingCostSalesPrice = !x.OutPrice.HasValue,
            //         OrderCostId = x.Id,
            //         CostId = x.CostId,
            //         CostName = x.Cost != null ? x.Cost.Name : null,
            //         SupplierOrderId = null,
            //         SupplierOrderNr = null,
            //     })
            //     .ToListAsync();

            // var orderCostRows = orderCostRowsFromCustomerOrders
            //     .Concat(orderCostRowsFromSupplierOrders)
            //     .Concat(orderCostRowsWithoutOrders)
            //     .ToList();

            // var supplierOrderRows = await _dbContext.SupplierOrders
            //     .AsNoTracking()
            //     .Where(x => x.CustomerOrders.Any() && !x.DeliveryToCustomers.Any() && !x.CustomerOrders.SelectMany(co => co.DeliveryFromStocks).Any())
            //     .Select(x => new InvoiceSourceRow
            //     {
            //         Type = "SupplierOrder",
            //         CustomerId = x.CustomerId,
            //         CustomerName = x.Customer != null ? (x.Customer.Name ?? string.Empty) : string.Empty,
            //         CustomerInvoicingInfo = x.CustomerOrders
            //             .Where(co => co.InvoicingInfo != null && co.InvoicingInfo != string.Empty)
            //             .Select(co => co.InvoicingInfo)
            //             .FirstOrDefault(),
            //         SupplierName = x.SupplierName ?? string.Empty,
            //         Quantity = x.Edition.HasValue ? (decimal?)x.Edition.Value : null,
            //         PurchasePrice = x.PurchasePrice.HasValue ? (decimal?)x.PurchasePrice.Value : null,
            //         PurchasePriceCurrencyName = x.PurchaseCurrency != null ? x.PurchaseCurrency.Name : null,
            //         UnitMultiplicator = x.Unit != null ? x.Unit.Multiplicator : null,
            //         WeightPer1000 = x.SelectedCalculationRow != null
            //             && x.SelectedCalculationRow.Calculation != null
            //             && x.SelectedCalculationRow.Calculation.Product != null
            //                 ? x.SelectedCalculationRow.Calculation.Product.NetWeightPer1000
            //                 : null,
            //         HasMissingCostSalesPrice = x.OrderCosts.Any(oc => !oc.IsCostInvoicedSeparately && !oc.OutPrice.HasValue),
            //         SupplierOrderId = x.Id,
            //         SupplierOrderNr = x.SupplierOrderNr,
            //     })
            //     .ToListAsync();

            return deliveryToCustomerRows
                .Concat(deliveryFromStockRows)
                .Concat(orderCostRowsFromCustomerOrders)
                // .Concat(supplierOrderRows)
                .Where(x => x.CustomerId.HasValue || !string.IsNullOrWhiteSpace(x.CustomerName))
                .ToList();
        }

        private sealed class InvoiceSourceRow
        {
            public string Type { get; set; } = string.Empty;
            public int? CustomerId { get; set; }
            public string CustomerName { get; set; } = string.Empty;
            public string? CustomerInvoicingInfo { get; set; }
            public int? DeliveryId { get; set; }
            public int? ParentDeliveryId { get; set; }
            public string SupplierName { get; set; } = string.Empty;
            public DateTime? DeliveryDate { get; set; }
            public int? CallOffId { get; set; }
            public string? CallOffNr { get; set; }
            public int? CustomerOrderId { get; set; }
            public string? CustomerOrderNr { get; set; }
            public string? CustomersOwnOrderNr { get; set; }
            public decimal? Quantity { get; set; }
            public decimal? PurchasePrice { get; set; }
            public string? PurchasePriceCurrencyName { get; set; }
            public short? UnitMultiplicator { get; set; }
            public int? WeightPer1000 { get; set; }
            public int? NrOfNotInvoicedPallets { get; set; }
            public int? NrOfNotInvoicedOrderCosts { get; set; }
            public bool HasMissingWeight { get; set; }
            public bool IsWeightMissingEmailSent { get; set; }
            public bool HasMissingCostSalesPrice { get; set; }
            public bool IsCostMissingEmailSent { get; set; }
            public int? OrderCostId { get; set; }
            public int? CostId { get; set; }
            public string? CostName { get; set; }
            public int? SupplierOrderId { get; set; }
            public string? SupplierOrderNr { get; set; }
        }

        private sealed class DraftDeliveryRowSource
        {
            public string Type { get; set; } = string.Empty;
            public int DeliveryId { get; set; }
            public DateTime? DeliveryDate { get; set; }
            public string? CallOffNr { get; set; }
            public int? CustomerOrderId { get; set; }
            public string? CustomerOrderNr { get; set; }
            public double? Quantity { get; set; }
            public double? UnitPrice { get; set; }
            public int? UnitId { get; set; }
            public short? UnitMultiplicator { get; set; }
        }

        private sealed class DraftOrderCostRowSource
        {
            public int OrderCostId { get; set; }
            public int? CustomerOrderId { get; set; }
            public string? CustomerOrderNr { get; set; }
            public string? CostName { get; set; }
            public decimal? Quantity { get; set; }
            public decimal? UnitPrice { get; set; }
            public int? UnitId { get; set; }
            public short? UnitMultiplicator { get; set; }
        }

        private static IOrderedQueryable<Invoice> ApplySearchOrdering(IQueryable<Invoice> query, List<SortRequest>? orderBy)
        {
            if (orderBy is null || orderBy.Count == 0)
            {
                return query
                    .OrderByDescending(x => x.InvoiceDate)
                    .ThenByDescending(x => x.InvoiceNumber)
                    .ThenByDescending(x => x.Id);
            }

            IOrderedQueryable<Invoice>? ordered = null;

            foreach (var sort in orderBy)
            {
                var field = sort.Field?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(field))
                {
                    throw new ArgumentException("Sort field is required.");
                }

                var isDescending = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
                ordered = field switch
                {
                    "id" => ApplySearchSort(ordered, query, x => x.Id, isDescending),
                    "invoicenumber" => ApplySearchSort(ordered, query, x => x.InvoiceNumber, isDescending),
                    "customername" => ApplySearchSort(ordered, query, x => x.CustomerName, isDescending),
                    "invoicedate" => ApplySearchSort(ordered, query, x => x.InvoiceDate, isDescending),
                    "duedate" => ApplySearchSort(ordered, query, x => x.InvoiceDate.HasValue && x.InvoiceDays.HasValue
                        ? x.InvoiceDate.Value.AddDays(x.InvoiceDays.Value)
                        : x.InvoiceDate, isDescending),
                    "status" => ApplySearchSort(ordered, query, x => x.InvoiceTypeCode == InvoiceTypeCode.Credit ? 1 : 0, isDescending),
                    "currencyname" => ApplySearchSort(ordered, query, x => x.SalesCurrency != null ? x.SalesCurrency.Name : null, isDescending),
                    _ => throw new ArgumentException($"Unsupported order by field '{sort.Field}'.")
                };
            }

            return (ordered ?? query.OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.InvoiceNumber)).ThenByDescending(x => x.Id);
        }
        private static IOrderedQueryable<Invoice> ApplySearchSort<TKey>(IOrderedQueryable<Invoice>? ordered, IQueryable<Invoice> source, Expression<Func<Invoice, TKey>> selector, bool isDescending)
        {
            if (ordered is null)
            {
                return isDescending ? source.OrderByDescending(selector) : source.OrderBy(selector);
            }

            return isDescending ? ordered.ThenByDescending(selector) : ordered.ThenBy(selector);
        }


        private static InvoiceDto MapToDto(Invoice invoice)
        {
            return new InvoiceDto
            {
                Id = invoice.Id,
                InvoiceTypeCode = invoice.InvoiceTypeCode,
                CustomerOrderId = invoice.CustomerOrderId,
                DeliveryToCustomerId = invoice.DeliveryToCustomerId,
                DeliveryFromStockId = invoice.DeliveryFromStockId,
                InvoiceNumber = invoice.InvoiceNumber,
                CreditingInvoiceNumber = invoice.CreditingInvoiceNumber,
                CustomerId = invoice.CustomerId,
                CustomerName = invoice.CustomerName,
                Address = invoice.Address,
                PostalNr = invoice.PostalNr,
                PostalAddress = invoice.PostalAddress,
                Country = invoice.Country,
                VatNr = invoice.VatNr,
                InvoiceDate = invoice.InvoiceDate,
                YourReference = invoice.YourReference,
                OurReference = invoice.OurReference,
                Product = invoice.Product,
                Material = invoice.Material,
                MaterialThickness = invoice.MaterialThickness,
                Format = invoice.Format,
                Color = invoice.Color,
                Construction = invoice.Construction,
                InvoiceDays = invoice.InvoiceDays,
                TermsOfPayment = invoice.TermsOfPayment,
                SalesCurrencyId = invoice.SalesCurrencyId,
                UnitId = invoice.UnitId,
                AccountDate = invoice.AccountDate,
                JournalNr = invoice.JournalNr,
                Account = invoice.Account,
                CostCenter = invoice.CostCenter,
                InvoiceOk = invoice.InvoiceOk,
                EndInvoiced = invoice.EndInvoiced,
                Printed = invoice.Printed,
                Created = invoice.Created,
                Edited = invoice.Edited,
                CreatedBy = invoice.CreatedBy,
                CreatedByUserName = invoice.CreatedByUser?.Name,
                EditedBy = invoice.EditedBy,
                EditedByUserName = invoice.EditedByUser?.Name,
                YourOrderNr = invoice.YourOrderNr,
                CollectInvoice = invoice.CollectInvoice,
                OurOrderNrFreeInvoice = invoice.OurOrderNrFreeInvoice,
                TimeOfDelivery = invoice.TimeOfDelivery,
                Address2 = invoice.Address2,
                IsSettled = invoice.IsSettled,
                OldDbId = invoice.OldDbId,
                NoteInternal = invoice.NoteInternal,
                LanguageId = invoice.LanguageId,
                SalesCurrencyRate = invoice.SalesCurrencyRate,
                InventoryCurrencyRate = invoice.InventoryCurrencyRate,
                InvoiceRows = invoice.InvoiceRows
                    .OrderBy(x => x.SortOrder ?? int.MaxValue)
                    .ThenBy(x => x.InvoiceRowNumber ?? int.MaxValue)
                    .ThenBy(x => x.Id)
                    .Select(x => new InvoiceRowDto
                    {
                        Id = x.Id,
                        InvoiceId = x.InvoiceId,
                        SortOrder = x.SortOrder,
                        InvoiceRowNumber = x.InvoiceRowNumber,
                        DeliveryToCustomerId = x.DeliveryToCustomerId,
                        DeliveryFromStockId = x.DeliveryFromStockId,
                        PriceTypeId = x.PriceTypeId,
                        PriceKey = x.PriceKey,
                        Text = x.Text,
                        Text2 = x.Text2,
                        NrOf = x.NrOf,
                        UnitPrice = x.UnitPrice,
                        Sum = x.Sum,
                        VatGround = x.VatGround,
                        CompareWithOrder = x.CompareWithOrder,
                        Calculate = x.Calculate,
                        AccountNr = x.AccountNr,
                        CostCenter = x.CostCenter,
                        UnitId = x.UnitId,
                        OldDbId = x.OldDbId,
                        DoNotAggregateWithParent = x.DoNotAggregateWithParent,
                        Weight = x.Weight,
                        OrderCostId = x.OrderCostId
                    })
                    .ToList(),
                InvoiceAccountRows = invoice.InvoiceAccountRows
                    .OrderBy(x => x.Id)
                    .Select(x => new InvoiceAccountRowDto
                    {
                        Id = x.Id,
                        InvoiceId = x.InvoiceId,
                        Text = x.Text,
                        NrOf = x.NrOf,
                        UnitPrice = x.UnitPrice,
                        Sum = x.Sum,
                        AccountNr = x.AccountNr,
                        CostCenter = x.CostCenter,
                        OldDbId = x.OldDbId
                    })
                    .ToList()
            };
        }

        private async Task<int> GetNextInvoiceNumberAsync()
        {
            var highestInvoiceNumber = await _dbContext.Invoices
                .AsNoTracking()
                .MaxAsync(x => (int?)x.InvoiceNumber) ?? 0;

            return highestInvoiceNumber + 1;
        }


        private static void ApplyRequestToInvoice(Invoice invoice, InvoiceMutationRequest request)
        {
            invoice.Account = request.Account;
            invoice.AccountDate = request.AccountDate;
            invoice.Address = request.Address;
            invoice.Address2 = request.Address2;
            invoice.CollectInvoice = request.CollectInvoice;
            invoice.Country = request.Country;
            invoice.Created = request.Created;
            invoice.CreatedBy = request.CreatedBy;
            invoice.CustomerId = request.CustomerId;
            invoice.CustomerName = request.CustomerName;
            invoice.CustomerOrderId = request.CustomerOrderId;
            invoice.CostCenter = request.CostCenter;
            invoice.DeliveryFromStockId = request.DeliveryFromStockId;
            invoice.DeliveryToCustomerId = request.DeliveryToCustomerId;
            invoice.EditedBy = request.EditedBy;
            invoice.EndInvoiced = request.EndInvoiced;
            invoice.InventoryCurrencyRate = request.InventoryCurrencyRate;
            invoice.InvoiceDate = request.InvoiceDate;
            invoice.InvoiceDays = request.InvoiceDays;
            invoice.InvoiceNumber = request.InvoiceNumber;
            invoice.InvoiceOk = request.InvoiceOk;
            invoice.InvoiceTypeCode = request.InvoiceTypeCode;
            invoice.IsSettled = request.IsSettled;
            invoice.JournalNr = request.JournalNr;
            invoice.LanguageId = request.LanguageId;
            invoice.NoteInternal = request.NoteInternal;
            invoice.OurOrderNrFreeInvoice = request.OurOrderNrFreeInvoice;
            invoice.OurReference = request.OurReference;
            invoice.PostalAddress = request.PostalAddress;
            invoice.PostalNr = request.PostalNr;
            invoice.Printed = request.Printed;
            invoice.SalesCurrencyRate = request.SalesCurrencyRate;
            invoice.SalesCurrencyId = request.SalesCurrencyId;
            invoice.TermsOfPayment = request.TermsOfPayment;
            invoice.TimeOfDelivery = request.TimeOfDelivery;
            invoice.UnitId = request.UnitId;
            invoice.VatNr = request.VatNr;
            invoice.CreditingInvoiceNumber = request.CreditingInvoiceNumber;
            invoice.YourOrderNr = request.YourOrderNr;
            invoice.YourReference = request.YourReference;
        }

        private static List<InvoiceRow> BuildInvoiceRows(IEnumerable<InvoiceRowMutationRequest>? rows, int invoiceId)
        {
            return (rows ?? Enumerable.Empty<InvoiceRowMutationRequest>())
                .Select(row => new InvoiceRow
                {
                    AccountNr = row.AccountNr,
                    Calculate = row.Calculate,
                    CompareWithOrder = row.CompareWithOrder,
                    CostCenter = row.CostCenter,
                    DeliveryFromStockId = row.DeliveryFromStockId,
                    DeliveryToCustomerId = row.DeliveryToCustomerId,
                    DoNotAggregateWithParent = row.DoNotAggregateWithParent,
                    InvoiceId = invoiceId > 0 ? invoiceId : null,
                    InvoiceRowNumber = row.InvoiceRowNumber ?? row.SortOrder,
                    NrOf = row.NrOf,
                    OldDbId = row.OldDbId,
                    OrderCostId = row.OrderCostId,
                    PriceKey = row.PriceKey,
                    PriceTypeId = row.PriceTypeId,
                    SortOrder = row.SortOrder,
                    Sum = row.Sum,
                    Text = row.Text,
                    Text2 = row.Text2,
                    UnitId = row.UnitId,
                    UnitPrice = row.UnitPrice,
                    VatGround = row.VatGround,
                    Weight = row.Weight,
                })
                .ToList();
        }

        private static List<InvoiceAccountRow> BuildInvoiceAccountRows(IEnumerable<InvoiceAccountRowMutationRequest>? rows, int invoiceId)
        {
            return (rows ?? Enumerable.Empty<InvoiceAccountRowMutationRequest>())
                .Select(row => new InvoiceAccountRow
                {
                    AccountNr = row.AccountNr,
                    CostCenter = row.CostCenter,
                    InvoiceId = invoiceId > 0 ? invoiceId : null,
                    NrOf = row.NrOf,
                    OldDbId = row.OldDbId,
                    Sum = row.Sum,
                    Text = row.Text,
                    UnitPrice = row.UnitPrice,
                })
                .ToList();
        }


        private static Dictionary<string, PropertyInfo> BuildFieldMap()
        {
            var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in typeof(Invoice).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                map[property.Name.ToLowerInvariant()] = property;

                var columnAttribute = property.GetCustomAttribute<ColumnAttribute>();
                if (!string.IsNullOrWhiteSpace(columnAttribute?.Name))
                {
                    map[columnAttribute.Name.ToLowerInvariant()] = property;
                }
            }

            return map;
        }


    }

}