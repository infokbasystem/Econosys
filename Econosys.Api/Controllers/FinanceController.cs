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
    [Route("api/[controller]")]
    public class FinanceController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILegacyUserResolutionService _legacyUserResolution;
        private readonly IJeevesAccountingService _jeevesAccountingService;

        public FinanceController(
            ApplicationDbContext dbContext,
            ILegacyUserResolutionService legacyUserResolution,
            IJeevesAccountingService jeevesAccountingService)
        {
            _dbContext = dbContext;
            _legacyUserResolution = legacyUserResolution;
            _jeevesAccountingService = jeevesAccountingService;
        }

        [HttpGet("overview")]
        public async Task<ActionResult<FinanceOverviewDto>> GetOverview()
        {
            var today = SwedishTime.Today;

            var invoices = await _dbContext.Invoices
                .AsNoTracking()
                .Where(invoice => invoice.InvoiceDate.HasValue)
                .Select(invoice => new
                {
                    InvoiceDate = invoice.InvoiceDate!.Value,
                    RowSum = invoice.InvoiceRows.Sum(row => row.Sum ?? 0),
                    invoice.SalesCurrencyRate,
                    IsCredit = invoice.InvoiceTypeCode == InvoiceTypeCode.Credit,
                    IsBooked = invoice.AccountDate.HasValue
                })
                .ToListAsync();

            var amounts = invoices
                .Select(invoice => new
                {
                    invoice.InvoiceDate,
                    invoice.IsBooked,
                    AmountSek = ToSek(invoice.RowSum, invoice.SalesCurrencyRate, invoice.IsCredit)
                })
                .ToList();

            var monthly = amounts
                .GroupBy(x => new { x.InvoiceDate.Year, x.InvoiceDate.Month })
                .Select(group => new FinanceMonthlyPointDto
                {
                    Year = group.Key.Year,
                    Month = group.Key.Month,
                    Amount = Round2(group.Sum(x => x.AmountSek))
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToList();

            // Years without any invoicing would only add noise to the chart's year toggles.
            var years = monthly
                .GroupBy(x => x.Year)
                .Where(group => group.Any(point => point.Amount != 0))
                .Select(group => group.Key)
                .OrderByDescending(x => x)
                .ToList();

            monthly = monthly
                .Where(point => years.Contains(point.Year))
                .ToList();

            // Same day-of-year window in both years so the comparison is like-for-like.
            var invoicedYtd = SumYearToDate(amounts.Select(x => (x.InvoiceDate, x.AmountSek)), today.Year, today);
            var invoicedPreviousYtd = SumYearToDate(amounts.Select(x => (x.InvoiceDate, x.AmountSek)), today.Year - 1, today.AddYears(-1));

            var notBooked = amounts.Where(x => !x.IsBooked).ToList();

            var uninvoiced = await GetUninvoicedDeliveryRows();

            return Ok(new FinanceOverviewDto
            {
                ReportDate = today,
                InvoicedYtd = Round2(invoicedYtd),
                InvoicedPreviousYtd = Round2(invoicedPreviousYtd),
                YtdChangePercent = invoicedPreviousYtd == 0
                    ? null
                    : (double)Math.Round((invoicedYtd - invoicedPreviousYtd) / Math.Abs(invoicedPreviousYtd) * 100m, 1),
                NotInvoicedTotal = Round2(uninvoiced.Sum(x => x.Value)),
                NotInvoicedCount = uninvoiced.Count,
                NotBookedTotal = Round2(notBooked.Sum(x => x.AmountSek)),
                NotBookedCount = notBooked.Count,
                Years = years,
                Monthly = monthly
            });
        }

        [HttpPost("uninvoiced-deliveries")]
        public async Task<ActionResult<PagedResultWithTotalsDto<UninvoicedDeliveryRowDto, UninvoicedDeliveryTotalsDto>>> GetUninvoicedDeliveries(
            [FromBody] PaginationRequest? pagination)
        {
            var request = NormalizePagination(pagination);

            var rows = await GetUninvoicedDeliveryRows();

            var ordered = rows
                .OrderByDescending(x => x.DeliveryDate ?? DateTime.MinValue)
                .ThenByDescending(x => x.DeliveryId)
                .ToList();

            return Ok(BuildPagedResult(
                ordered,
                request,
                new UninvoicedDeliveryTotalsDto
                {
                    TotalValue = Round2(ordered.Sum(x => x.Value)),
                    TotalDeliveries = ordered.Count
                }));
        }

        [HttpPost("unbooked-invoices")]
        public async Task<ActionResult<PagedResultWithTotalsDto<UnbookedInvoiceRowDto, UnbookedInvoiceTotalsDto>>> GetUnbookedInvoices(
            [FromBody] SearchUnbookedInvoicesRequest? request)
        {
            var pagination = NormalizePagination(request);

            var query = _dbContext.Invoices
                .AsNoTracking()
                .Where(invoice => invoice.InvoiceDate.HasValue && !invoice.AccountDate.HasValue);

            if (request?.InvoiceDateFrom is not null)
            {
                var start = request.InvoiceDateFrom.Value.ToDateTime(TimeOnly.MinValue);
                query = query.Where(invoice => invoice.InvoiceDate >= start);
            }

            if (request?.InvoiceDateTo is not null)
            {
                var endExclusive = request.InvoiceDateTo.Value.ToDateTime(TimeOnly.MinValue).AddDays(1);
                query = query.Where(invoice => invoice.InvoiceDate < endExclusive);
            }

            if (request?.InvoiceNumber is not null)
            {
                query = query.Where(invoice => invoice.InvoiceNumber == request.InvoiceNumber);
            }

            var invoices = await query
                .Select(invoice => new
                {
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.CustomerName,
                    InvoiceDate = invoice.InvoiceDate!.Value,
                    invoice.InvoiceDays,
                    RowSum = invoice.InvoiceRows.Sum(row => row.Sum ?? 0),
                    invoice.SalesCurrencyRate,
                    IsCredit = invoice.InvoiceTypeCode == InvoiceTypeCode.Credit
                })
                .ToListAsync();

            var rows = invoices
                .Select(invoice => new UnbookedInvoiceRowDto
                {
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    CustomerName = invoice.CustomerName,
                    Amount = Round2(ToSek(invoice.RowSum, invoice.SalesCurrencyRate, invoice.IsCredit)),
                    InvoiceDate = invoice.InvoiceDate,
                    DueDate = invoice.InvoiceDate.AddDays(invoice.InvoiceDays ?? 0)
                })
                .OrderBy(x => x.DueDate)
                .ThenBy(x => x.InvoiceNumber)
                .ToList();

            return Ok(BuildPagedResult(
                rows,
                pagination,
                new UnbookedInvoiceTotalsDto
                {
                    TotalAmount = Round2(rows.Sum(x => x.Amount)),
                    TotalInvoices = rows.Count
                }));
        }

        [HttpPost("book-invoices")]
        public async Task<ActionResult<BookInvoicesResponse>> BookInvoices([FromBody] BookInvoicesRequest? request)
        {
            var invoiceIds = request?.InvoiceIds
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? [];

            if (invoiceIds.Count == 0)
            {
                return BadRequest(new { message = "Select at least one invoice to book." });
            }

            var legacyUser = await _legacyUserResolution.ResolveCurrentUserAsync(User);
            if (legacyUser is null)
            {
                return Unauthorized(new { message = "Could not map authenticated user to a legacy user by email." });
            }

            var invoices = await _dbContext.Invoices
                .Where(invoice => invoiceIds.Contains(invoice.Id))
                .ToListAsync();

            var alreadyBookedInvoiceIds = invoices
                .Where(invoice => invoice.AccountDate.HasValue)
                .Select(invoice => invoice.Id)
                .ToList();
            var bookedInvoiceIds = new List<int>();
            var results = new List<BookInvoiceResultDto>();

            foreach (var invoice in invoices.Where(invoice => !invoice.AccountDate.HasValue))
            {
                var exportResult = await _jeevesAccountingService.ExportInvoiceAsync(invoice.Id, request!.UseTestApi, HttpContext.RequestAborted);
                results.Add(new BookInvoiceResultDto
                {
                    InvoiceId = invoice.Id,
                    Succeeded = exportResult.Succeeded,
                    Message = exportResult.Message
                });

                if (!exportResult.Succeeded || request.UseTestApi)
                {
                    continue;
                }

                var now = SwedishTime.Now;
                invoice.AccountDate = now.Date;
                invoice.Edited = now;
                invoice.EditedBy = legacyUser.Id;
                bookedInvoiceIds.Add(invoice.Id);
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new BookInvoicesResponse
            {
                BookedInvoiceIds = bookedInvoiceIds,
                AlreadyBookedInvoiceIds = alreadyBookedInvoiceIds,
                MissingInvoiceIds = invoiceIds.Except(invoices.Select(invoice => invoice.Id)).ToList(),
                Results = results
            });
        }

        [HttpPost("sync-customers")]
        public async Task<ActionResult<SyncCustomersResponse>> SyncCustomers([FromBody] SyncCustomersRequest? request)
        {
            var customerIds = request?.CustomerIds
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? [];

            if (customerIds.Count == 0)
            {
                return BadRequest(new { message = "Välj minst en kund att synka." });
            }

            var existingCustomerIds = await _dbContext.Customers
                .AsNoTracking()
                .Where(customer => customerIds.Contains(customer.Id))
                .Select(customer => customer.Id)
                .ToListAsync(HttpContext.RequestAborted);

            var results = new List<SyncCustomerResultDto>();
            foreach (var customerId in existingCustomerIds)
            {
                var exportResult = await _jeevesAccountingService.ExportCustomerAsync(customerId, request!.UseTestApi, HttpContext.RequestAborted);
                results.Add(new SyncCustomerResultDto
                {
                    CustomerId = customerId,
                    Succeeded = exportResult.Succeeded,
                    Message = exportResult.Message
                });
            }

            return Ok(new SyncCustomersResponse
            {
                MissingCustomerIds = customerIds.Except(existingCustomerIds).ToList(),
                Results = results
            });
        }

        private async Task<List<UninvoicedDeliveryRowDto>> GetUninvoicedDeliveryRows()
        {
            // Mirrors the "Fakturera leveranser" selection so both pages report the same set.
            var minDeliveryDate = new DateTime(2016, 1, 1);

            var deliveries = await _dbContext.DeliveryToCustomers
                .AsNoTracking()
                .Where(delivery =>
                    !delivery.DoNotInvoice &&
                    !delivery.InvoiceRows.Any() &&
                    delivery.NrOfItems > 0 &&
                    delivery.DeliveryDate.HasValue && delivery.DeliveryDate.Value >= minDeliveryDate &&
                    delivery.DeliveryStatus == (int)DeliveryStatus.Delivered &&
                    delivery.CustomerOrder != null &&
                    !delivery.CustomerOrder.Completed)
                .Select(delivery => new
                {
                    delivery.Id,
                    delivery.CustomerOrderId,
                    delivery.DeliveryDate,
                    delivery.NrOfItems,
                    CustomerOrderNr = delivery.CustomerOrder!.CustomerOrderNr,
                    CustomerName = delivery.CustomerOrder.CustomerName,
                    SalesPrice = delivery.CustomerOrder.SalesPrice,
                    SalesCurrencyRate = delivery.CustomerOrder.SalesCurrencyRate,
                    UnitMultiplicator = delivery.CustomerOrder.Unit != null ? delivery.CustomerOrder.Unit.Multiplicator : (short?)null
                })
                .ToListAsync();

            return deliveries
                .Select(delivery =>
                {
                    var divisor = delivery.UnitMultiplicator.GetValueOrDefault() == 0 ? 1m : delivery.UnitMultiplicator!.Value;
                    var value = (decimal)delivery.NrOfItems.GetValueOrDefault()
                        * (decimal)delivery.SalesPrice.GetValueOrDefault()
                        * (delivery.SalesCurrencyRate.GetValueOrDefault() == 0 ? 1m : (decimal)delivery.SalesCurrencyRate!.Value)
                        / divisor;

                    return new UninvoicedDeliveryRowDto
                    {
                        DeliveryId = delivery.Id,
                        CustomerOrderId = delivery.CustomerOrderId,
                        CustomerOrderNr = delivery.CustomerOrderNr,
                        CustomerName = delivery.CustomerName,
                        Quantity = delivery.NrOfItems,
                        Value = Round2(value),
                        DeliveryDate = delivery.DeliveryDate
                    };
                })
                .ToList();
        }

        private static decimal SumYearToDate(IEnumerable<(DateTime Date, decimal Amount)> source, int year, DateTime endInclusive)
        {
            return source
                .Where(x => x.Date.Year == year && x.Date.Date <= endInclusive.Date)
                .Sum(x => x.Amount);
        }

        private static decimal ToSek(double rowSum, decimal? salesCurrencyRate, bool isCredit)
        {
            var rate = salesCurrencyRate.GetValueOrDefault() == 0 ? 1m : salesCurrencyRate!.Value;
            var amount = (decimal)rowSum * rate;
            return isCredit ? -amount : amount;
        }

        private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private static PaginationRequest NormalizePagination(PaginationRequest? pagination)
        {
            return new PaginationRequest
            {
                PageNumber = Math.Max(1, pagination?.PageNumber ?? 1),
                PageSize = Math.Clamp(pagination?.PageSize ?? 25, 1, 1000)
            };
        }

        private static PagedResultWithTotalsDto<TItem, TTotals> BuildPagedResult<TItem, TTotals>(
            IReadOnlyList<TItem> orderedRows,
            PaginationRequest pagination,
            TTotals totals)
        {
            var totalPages = (int)Math.Ceiling(orderedRows.Count / (double)pagination.PageSize);
            var pageNumber = totalPages == 0 ? 1 : Math.Min(pagination.PageNumber, totalPages);

            return new PagedResultWithTotalsDto<TItem, TTotals>
            {
                Items = orderedRows
                    .Skip((pageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToList(),
                PageNumber = pageNumber,
                PageSize = pagination.PageSize,
                TotalCount = orderedRows.Count,
                TotalPages = totalPages,
                Totals = new TotalsDto<TTotals> { Values = totals }
            };
        }
    }
}
