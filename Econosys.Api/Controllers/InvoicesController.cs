using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
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
    public class InvoicesController : ControllerBase
    {
        private static readonly Dictionary<string, PropertyInfo> FieldMap = BuildFieldMap();

        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<InvoicesController> _logger;

        public InvoicesController(ApplicationDbContext dbContext, ILogger<InvoicesController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<InvoiceDto>> GetById(int id)
        {
            var invoice = await _dbContext.Invoices
                .AsNoTracking()
                .Include(x => x.InvoiceRows)
                .Include(x => x.InvoiceAccountRows)
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

            var invoice = new Invoice();
            ApplyRequestToInvoice(invoice, request);

            invoice.Created ??= DateTime.UtcNow;
            invoice.Edited = request.Edited ?? invoice.Created;

            invoice.InvoiceRows = BuildInvoiceRows(request.InvoiceRows, invoice.Id);
            invoice.InvoiceAccountRows = BuildInvoiceAccountRows(request.InvoiceAccountRows, invoice.Id);

            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync();

            var created = await _dbContext.Invoices
                .AsNoTracking()
                .Include(x => x.InvoiceRows)
                .Include(x => x.InvoiceAccountRows)
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

            var invoice = await _dbContext.Invoices
                .Include(x => x.InvoiceRows)
                .Include(x => x.InvoiceAccountRows)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (invoice is null)
            {
                return NotFound();
            }

            ApplyRequestToInvoice(invoice, request);
            invoice.Edited = request.Edited ?? DateTime.UtcNow;

            _dbContext.InvoiceRows.RemoveRange(invoice.InvoiceRows);
            _dbContext.InvoiceAccountRows.RemoveRange(invoice.InvoiceAccountRows);

            invoice.InvoiceRows = BuildInvoiceRows(request.InvoiceRows, invoice.Id);
            invoice.InvoiceAccountRows = BuildInvoiceAccountRows(request.InvoiceAccountRows, invoice.Id);

            await _dbContext.SaveChangesAsync();

            var updated = await _dbContext.Invoices
                .AsNoTracking()
                .Include(x => x.InvoiceRows)
                .Include(x => x.InvoiceAccountRows)
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
        public async Task<ActionResult<PagedResultDto<InvoiceDto>>> Search([FromBody] SearchInvoicesRequest? request)
        {
            IQueryable<Invoice> query = _dbContext.Invoices.AsNoTracking();
            var pagination = request?.Pagination ?? new PaginationRequest();

            if (request?.Filter?.Conditions?.Count > 0)
            {
                foreach (var condition in request.Filter.Conditions)
                {
                    try
                    {
                        query = ApplyCondition(query, condition);
                    }
                    catch (ArgumentException ex)
                    {
                        _logger.LogWarning(ex, "Invalid invoice search condition for field {Field}", condition.Field);
                        return BadRequest(new { message = ex.Message, field = condition.Field });
                    }
                }
            }

            IOrderedQueryable<Invoice> orderedQuery;
            try
            {
                orderedQuery = ApplyOrdering(query, request?.OrderBy);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid invoice order by request");
                return BadRequest(new { message = ex.Message });
            }

            var totalCount = await query.CountAsync();
            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pagination.PageSize);

            var pagedIds = await orderedQuery
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .Select(x => x.Id)
                .ToListAsync();

            var invoices = await _dbContext.Invoices
                .AsNoTracking()
                .Where(x => pagedIds.Contains(x.Id))
                .Include(x => x.InvoiceRows)
                .Include(x => x.InvoiceAccountRows)
                .ToListAsync();

            var invoiceMap = invoices.ToDictionary(x => x.Id);
            var items = pagedIds
                .Where(invoiceMap.ContainsKey)
                .Select(id => MapToDto(invoiceMap[id]))
                .ToList();

            return Ok(new PagedResultDto<InvoiceDto>
            {
                Items = items,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        [HttpPost("search-list")]
        public async Task<ActionResult<PagedResultWithTotalsDto<InvoiceSearchRowDto, InvoiceSearchTotalsDto>>> SearchList([FromBody] SearchInvoiceListRequest? request)
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
                orderedQuery = ApplySearchListOrdering(query, request?.OrderBy);
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
                    row.VatGround
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
                    invoiceSumExVat += amount;
                    invoiceSumInclVat += row.VatGround ? amount * 1.25m : amount;
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
                    sumExVat += amount;
                    sumInclVat += row.VatGround ? amount * 1.25m : amount;

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

        private static IOrderedQueryable<Invoice> ApplySearchListOrdering(
            IQueryable<Invoice> query,
            List<SortRequest>? orderBy)
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
                    "id" => ApplySearchListSort(ordered, query, x => x.Id, isDescending),
                    "invoicenumber" => ApplySearchListSort(ordered, query, x => x.InvoiceNumber, isDescending),
                    "customername" => ApplySearchListSort(ordered, query, x => x.CustomerName, isDescending),
                    "invoicedate" => ApplySearchListSort(ordered, query, x => x.InvoiceDate, isDescending),
                    "duedate" => ApplySearchListSort(ordered, query, x => x.InvoiceDate.HasValue && x.InvoiceDays.HasValue
                        ? x.InvoiceDate.Value.AddDays(x.InvoiceDays.Value)
                        : x.InvoiceDate, isDescending),
                    "status" => ApplySearchListSort(ordered, query, x => x.InvoiceTypeCode == InvoiceTypeCode.Credit ? 1 : 0, isDescending),
                    "currencyname" => ApplySearchListSort(ordered, query, x => x.SalesCurrency != null ? x.SalesCurrency.Name : null, isDescending),
                    _ => throw new ArgumentException($"Unsupported order by field '{sort.Field}'.")
                };
            }

            return (ordered ?? query.OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.InvoiceNumber)).ThenByDescending(x => x.Id);
        }

        private static IOrderedQueryable<Invoice> ApplySearchListSort<TKey>(
            IOrderedQueryable<Invoice>? ordered,
            IQueryable<Invoice> source,
            Expression<Func<Invoice, TKey>> selector,
            bool isDescending)
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
                EditedBy = invoice.EditedBy,
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

        private static void ApplyRequestToInvoice(Invoice invoice, InvoiceMutationRequest request)
        {
            invoice.InvoiceTypeCode = request.InvoiceTypeCode;
            invoice.CustomerOrderId = request.CustomerOrderId;
            invoice.DeliveryToCustomerId = request.DeliveryToCustomerId;
            invoice.DeliveryFromStockId = request.DeliveryFromStockId;
            invoice.InvoiceNumber = request.InvoiceNumber;
            invoice.CreditingInvoiceNumber = request.CreditingInvoiceNumber;
            invoice.CustomerId = request.CustomerId;
            invoice.CustomerName = request.CustomerName;
            invoice.Address = request.Address;
            invoice.PostalNr = request.PostalNr;
            invoice.PostalAddress = request.PostalAddress;
            invoice.Country = request.Country;
            invoice.VatNr = request.VatNr;
            invoice.InvoiceDate = request.InvoiceDate;
            invoice.YourReference = request.YourReference;
            invoice.OurReference = request.OurReference;
            invoice.Product = request.Product;
            invoice.Material = request.Material;
            invoice.MaterialThickness = request.MaterialThickness;
            invoice.Format = request.Format;
            invoice.Color = request.Color;
            invoice.Construction = request.Construction;
            invoice.InvoiceDays = request.InvoiceDays;
            invoice.TermsOfPayment = request.TermsOfPayment;
            invoice.SalesCurrencyId = request.SalesCurrencyId;
            invoice.UnitId = request.UnitId;
            invoice.AccountDate = request.AccountDate;
            invoice.JournalNr = request.JournalNr;
            invoice.Account = request.Account;
            invoice.CostCenter = request.CostCenter;
            invoice.InvoiceOk = request.InvoiceOk;
            invoice.EndInvoiced = request.EndInvoiced;
            invoice.Printed = request.Printed;
            invoice.Created = request.Created;
            invoice.Edited = request.Edited;
            invoice.CreatedBy = request.CreatedBy;
            invoice.EditedBy = request.EditedBy;
            invoice.YourOrderNr = request.YourOrderNr;
            invoice.CollectInvoice = request.CollectInvoice;
            invoice.OurOrderNrFreeInvoice = request.OurOrderNrFreeInvoice;
            invoice.TimeOfDelivery = request.TimeOfDelivery;
            invoice.Address2 = request.Address2;
            invoice.IsSettled = request.IsSettled;
            invoice.OldDbId = request.OldDbId;
            invoice.NoteInternal = request.NoteInternal;
            invoice.LanguageId = request.LanguageId;
            invoice.SalesCurrencyRate = request.SalesCurrencyRate;
            invoice.InventoryCurrencyRate = request.InventoryCurrencyRate;
        }

        private static List<InvoiceRow> BuildInvoiceRows(IEnumerable<InvoiceRowMutationRequest>? rows, int invoiceId)
        {
            return (rows ?? Enumerable.Empty<InvoiceRowMutationRequest>())
                .Select(row => new InvoiceRow
                {
                    InvoiceId = invoiceId > 0 ? invoiceId : null,
                    SortOrder = row.SortOrder,
                    InvoiceRowNumber = row.InvoiceRowNumber,
                    DeliveryToCustomerId = row.DeliveryToCustomerId,
                    DeliveryFromStockId = row.DeliveryFromStockId,
                    PriceTypeId = row.PriceTypeId,
                    PriceKey = row.PriceKey,
                    Text = row.Text,
                    Text2 = row.Text2,
                    NrOf = row.NrOf,
                    UnitPrice = row.UnitPrice,
                    Sum = row.Sum,
                    VatGround = row.VatGround,
                    CompareWithOrder = row.CompareWithOrder,
                    Calculate = row.Calculate,
                    AccountNr = row.AccountNr,
                    CostCenter = row.CostCenter,
                    UnitId = row.UnitId,
                    OldDbId = row.OldDbId,
                    DoNotAggregateWithParent = row.DoNotAggregateWithParent,
                    Weight = row.Weight,
                    OrderCostId = row.OrderCostId,
                })
                .ToList();
        }

        private static List<InvoiceAccountRow> BuildInvoiceAccountRows(IEnumerable<InvoiceAccountRowMutationRequest>? rows, int invoiceId)
        {
            return (rows ?? Enumerable.Empty<InvoiceAccountRowMutationRequest>())
                .Select(row => new InvoiceAccountRow
                {
                    InvoiceId = invoiceId > 0 ? invoiceId : null,
                    Text = row.Text,
                    NrOf = row.NrOf,
                    UnitPrice = row.UnitPrice,
                    Sum = row.Sum,
                    AccountNr = row.AccountNr,
                    CostCenter = row.CostCenter,
                    OldDbId = row.OldDbId,
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

        private static PropertyInfo ResolveProperty(string field)
        {
            var key = field.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Filter field is required.");
            }

            if (!FieldMap.TryGetValue(key, out var property))
            {
                throw new ArgumentException($"Unsupported field '{field}'.");
            }

            return property;
        }

        private static IOrderedQueryable<Invoice> ApplyOrdering(
            IQueryable<Invoice> query,
            List<SortRequest>? orderBy)
        {
            if (orderBy is null || orderBy.Count == 0)
            {
                return query.OrderBy(x => x.Id);
            }

            IOrderedQueryable<Invoice>? ordered = null;

            foreach (var sort in orderBy)
            {
                var property = ResolveProperty(sort.Field);
                var isDescending = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
                ordered = ApplySort(ordered, query, property, isDescending);
            }

            return ordered ?? query.OrderBy(x => x.Id);
        }

        private static IOrderedQueryable<Invoice> ApplySort(
            IOrderedQueryable<Invoice>? ordered,
            IQueryable<Invoice> source,
            PropertyInfo property,
            bool isDescending)
        {
            var parameter = Expression.Parameter(typeof(Invoice), "x");
            var propertyExpression = Expression.Property(parameter, property);
            var lambda = Expression.Lambda(propertyExpression, parameter);

            var methodName = ordered is null
                ? (isDescending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy))
                : (isDescending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy));

            var method = typeof(Queryable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(Invoice), property.PropertyType);

            var result = method.Invoke(null, new object[] { ordered ?? source, lambda });
            return (IOrderedQueryable<Invoice>)result!;
        }

        private static IQueryable<Invoice> ApplyCondition(IQueryable<Invoice> query, FilterConditionDto condition)
        {
            var property = ResolveProperty(condition.Field);
            var parameter = Expression.Parameter(typeof(Invoice), "x");
            var member = Expression.Property(parameter, property);
            var operation = condition.Operator.Trim().ToLowerInvariant();

            Expression body = operation switch
            {
                "contains" => BuildStringCondition(member, condition, nameof(string.Contains)),
                "startswith" => BuildStringCondition(member, condition, nameof(string.StartsWith)),
                "endswith" => BuildStringCondition(member, condition, nameof(string.EndsWith)),
                "in" => BuildInCondition(member, property.PropertyType, condition),
                _ => BuildComparisonCondition(member, property.PropertyType, condition, operation)
            };

            var lambda = Expression.Lambda<Func<Invoice, bool>>(body, parameter);
            return query.Where(lambda);
        }

        private static Expression BuildStringCondition(Expression member, FilterConditionDto condition, string methodName)
        {
            var underlyingType = Nullable.GetUnderlyingType(member.Type) ?? member.Type;
            if (underlyingType != typeof(string))
            {
                throw new ArgumentException($"Operator '{condition.Operator}' requires a string field.");
            }

            var value = GetSingleValue(condition) ?? string.Empty;
            var notNull = Expression.NotEqual(member, Expression.Constant(null, member.Type));
            var method = typeof(string).GetMethod(methodName, new[] { typeof(string) })!;
            var call = Expression.Call(member, method, Expression.Constant(value));
            return Expression.AndAlso(notNull, call);
        }

        private static Expression BuildInCondition(Expression member, Type propertyType, FilterConditionDto condition)
        {
            var values = condition.Values ??
                (condition.Value.HasValue ? new List<JsonElement> { condition.Value.Value } : new List<JsonElement>());

            if (values.Count == 0)
            {
                throw new ArgumentException("Operator 'in' requires at least one value.");
            }

            var elementType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            var typedArray = Array.CreateInstance(elementType, values.Count);
            for (var index = 0; index < values.Count; index++)
            {
                typedArray.SetValue(ConvertJsonElement(values[index], elementType), index);
            }

            var arrayExpression = Expression.Constant(typedArray, typedArray.GetType());
            var containsMethod = typeof(Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
                .MakeGenericMethod(elementType);

            Expression valueExpression = member;
            if (member.Type != elementType)
            {
                var hasValue = Expression.Property(member, nameof(Nullable<int>.HasValue));
                var value = Expression.Property(member, nameof(Nullable<int>.Value));
                return Expression.AndAlso(
                    hasValue,
                    Expression.Call(null, containsMethod, arrayExpression, value));
            }

            return Expression.Call(null, containsMethod, arrayExpression, valueExpression);
        }

        private static Expression BuildComparisonCondition(Expression member, Type propertyType, FilterConditionDto condition, string operation)
        {
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            var rawValue = condition.Value ?? throw new ArgumentException($"Operator '{condition.Operator}' requires a value.");
            var typedValue = ConvertJsonElement(rawValue, underlyingType);
            var constant = Expression.Constant(typedValue, underlyingType);

            Expression left = member;
            Expression? hasValue = null;

            if (propertyType != underlyingType)
            {
                hasValue = Expression.Property(member, nameof(Nullable<int>.HasValue));
                left = Expression.Property(member, nameof(Nullable<int>.Value));
            }

            Expression comparison = operation switch
            {
                "eq" => Expression.Equal(left, constant),
                "neq" => Expression.NotEqual(left, constant),
                "gt" => Expression.GreaterThan(left, constant),
                "gte" => Expression.GreaterThanOrEqual(left, constant),
                "lt" => Expression.LessThan(left, constant),
                "lte" => Expression.LessThanOrEqual(left, constant),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}'.")
            };

            return hasValue is null ? comparison : Expression.AndAlso(hasValue, comparison);
        }

        private static object ConvertJsonElement(JsonElement element, Type targetType)
        {
            if (targetType == typeof(string))
            {
                return element.ValueKind == JsonValueKind.Null ? string.Empty : element.GetString() ?? string.Empty;
            }

            if (targetType == typeof(int))
            {
                return element.ValueKind == JsonValueKind.Number
                    ? element.GetInt32()
                    : int.Parse(GetStringValue(element), CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(short))
            {
                return element.ValueKind == JsonValueKind.Number
                    ? element.GetInt16()
                    : short.Parse(GetStringValue(element), CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(long))
            {
                return element.ValueKind == JsonValueKind.Number
                    ? element.GetInt64()
                    : long.Parse(GetStringValue(element), CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(decimal))
            {
                return element.ValueKind == JsonValueKind.Number
                    ? element.GetDecimal()
                    : decimal.Parse(GetStringValue(element), CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(double))
            {
                return element.ValueKind == JsonValueKind.Number
                    ? element.GetDouble()
                    : double.Parse(GetStringValue(element), CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(float))
            {
                return element.ValueKind == JsonValueKind.Number
                    ? element.GetSingle()
                    : float.Parse(GetStringValue(element), CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(bool))
            {
                return element.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => bool.Parse(GetStringValue(element))
                };
            }

            if (targetType == typeof(DateTime))
            {
                return element.ValueKind == JsonValueKind.String
                    ? DateTime.Parse(element.GetString() ?? string.Empty, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                    : element.GetDateTime();
            }

            if (targetType.IsEnum)
            {
                return element.ValueKind == JsonValueKind.Number
                    ? Enum.ToObject(targetType, element.GetInt32())
                    : Enum.Parse(targetType, GetStringValue(element), ignoreCase: true);
            }

            throw new ArgumentException($"Filtering is not supported for type '{targetType.Name}'.");
        }

        private static string GetSingleValue(FilterConditionDto condition)
        {
            if (!condition.Value.HasValue)
            {
                throw new ArgumentException($"Operator '{condition.Operator}' requires a value.");
            }

            return GetStringValue(condition.Value.Value);
        }

        private static string GetStringValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => element.ToString()
            };
        }
    }

    public class SearchInvoicesRequest
    {
        public FilterRequest? Filter { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
        public PaginationRequest? Pagination { get; set; }
    }

    public abstract class InvoiceMutationRequest
    {
        public InvoiceTypeCode? InvoiceTypeCode { get; set; }
        public int? CustomerOrderId { get; set; }
        public int? DeliveryToCustomerId { get; set; }
        public int? DeliveryFromStockId { get; set; }
        public int? InvoiceNumber { get; set; }
        public int? CreditingInvoiceNumber { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? Address { get; set; }
        public string? PostalNr { get; set; }
        public string? PostalAddress { get; set; }
        public string? Country { get; set; }
        public string? VatNr { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string? YourReference { get; set; }
        public string? OurReference { get; set; }
        public string? Product { get; set; }
        public string? Material { get; set; }
        public string? MaterialThickness { get; set; }
        public string? Format { get; set; }
        public string? Color { get; set; }
        public string? Construction { get; set; }
        public short? InvoiceDays { get; set; }
        public string? TermsOfPayment { get; set; }
        public int? SalesCurrencyId { get; set; }
        public int? UnitId { get; set; }
        public DateTime? AccountDate { get; set; }
        public int? JournalNr { get; set; }
        public string? Account { get; set; }
        public string? CostCenter { get; set; }
        public bool InvoiceOk { get; set; }
        public bool EndInvoiced { get; set; }
        public bool Printed { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? Edited { get; set; }
        public int? CreatedBy { get; set; }
        public int? EditedBy { get; set; }
        public string? YourOrderNr { get; set; }
        public bool CollectInvoice { get; set; }
        public string? OurOrderNrFreeInvoice { get; set; }
        public string? TimeOfDelivery { get; set; }
        public string? Address2 { get; set; }
        public bool IsSettled { get; set; }
        public int? OldDbId { get; set; }
        public string? NoteInternal { get; set; }
        public int? LanguageId { get; set; }
        public decimal? SalesCurrencyRate { get; set; }
        public decimal? InventoryCurrencyRate { get; set; }
        public List<InvoiceRowMutationRequest>? InvoiceRows { get; set; }
        public List<InvoiceAccountRowMutationRequest>? InvoiceAccountRows { get; set; }
    }

    public class CreateInvoiceRequest : InvoiceMutationRequest
    {
    }

    public class UpdateInvoiceRequest : InvoiceMutationRequest
    {
    }

    public class InvoiceRowMutationRequest
    {
        public int? SortOrder { get; set; }
        public int? InvoiceRowNumber { get; set; }
        public int? DeliveryToCustomerId { get; set; }
        public int? DeliveryFromStockId { get; set; }
        public int? PriceTypeId { get; set; }
        public string? PriceKey { get; set; }
        public string? Text { get; set; }
        public string? Text2 { get; set; }
        public double? NrOf { get; set; }
        public double? UnitPrice { get; set; }
        public double? Sum { get; set; }
        public bool VatGround { get; set; }
        public bool CompareWithOrder { get; set; }
        public bool Calculate { get; set; }
        public string? AccountNr { get; set; }
        public string? CostCenter { get; set; }
        public int? UnitId { get; set; }
        public int? OldDbId { get; set; }
        public bool DoNotAggregateWithParent { get; set; }
        public decimal? Weight { get; set; }
        public int? OrderCostId { get; set; }
    }

    public class InvoiceAccountRowMutationRequest
    {
        public string? Text { get; set; }
        public double? NrOf { get; set; }
        public double? UnitPrice { get; set; }
        public double? Sum { get; set; }
        public string? AccountNr { get; set; }
        public string? CostCenter { get; set; }
        public int? OldDbId { get; set; }
    }

    public class SearchInvoiceListRequest
    {
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public int? InvoiceNumber { get; set; }
        public string? CustomerName { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
        public PaginationRequest? Pagination { get; set; }
    }
}