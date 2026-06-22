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
    public class DeviationsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        private readonly ILegacyUserResolutionService _legacyUserResolution;

        public DeviationsController(ApplicationDbContext dbContext, ILegacyUserResolutionService legacyUserResolution)
        {
            _dbContext = dbContext;
            _legacyUserResolution = legacyUserResolution;
        }

        [HttpGet("form-options")]
        public async Task<ActionResult<DeviationFormOptionsDto>> GetFormOptions()
        {
            var suppliers = await _dbContext.Suppliers
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new FilterOptionDto<int> { Id = x.Id, Name = x.Name ?? string.Empty, IsActive = x.Active })
                .ToListAsync();

            var customers = await _dbContext.Customers
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new FilterOptionDto<int> { Id = x.Id, Name = x.Name ?? string.Empty, IsActive = x.Active })
                .ToListAsync();

            var users = await _dbContext.LegacyUsers
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new FilterOptionDto<int> { Id = x.Id, Name = x.Name ?? string.Empty, IsActive = true })
                .ToListAsync();

            var currencies = await _dbContext.Currencies
                .AsNoTracking()
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .Select(x => new FilterOptionDto<int> { Id = x.Id, Name = x.Name ?? string.Empty, IsActive = x.Active })
                .ToListAsync();

            return Ok(new DeviationFormOptionsDto
            {
                Suppliers = suppliers,
                Customers = customers,
                Users = users,
                Currencies = currencies,
                DeviationProcessCodes = new List<FilterOptionDto<string>>
                {
                    new() { Id = "ORDER", Name = "Beställning" },
                    new() { Id = "DELIVERY", Name = "Leverans" },
                    new() { Id = "INVOICE", Name = "Fakturering" },
                    new() { Id = "SERVICE", Name = "Tjänst" },
                    new() { Id = "OTHER", Name = "Övrigt" }
                },
                DeviationTypeCodes = new List<FilterOptionDto<string>>
                {
                    new() { Id = "PRODUCTIONERROR", Name = "Produktionsfel" },
                    new() { Id = "MATERIALERROR", Name = "Materialfel" },
                    new() { Id = "PACKAGINGERROR", Name = "Packningsfel" },
                    new() { Id = "PRICEERROR", Name = "Prisfel" },
                    new() { Id = "DELIVERYDELAY", Name = "Leveransförsening" },
                    new() { Id = "DELIVERYDAMAGE", Name = "Leveransskada" },
                    new() { Id = "TRANSPORTORLOADING", Name = "Transport/Lastning" },
                    new() { Id = "OTHER", Name = "Övrigt" }
                },
                SupplierDecisionCodes = new List<FilterOptionDto<string>>
                {
                    new() { Id = "ACCEPTED", Name = "Accepterad" },
                    new() { Id = "PARTLYACCEPTED", Name = "Delvis accepterad" },
                    new() { Id = "DENIED", Name = "Nekad" },
                    new() { Id = "NOTRECLAIMED", Name = "Ej reklamerad" }
                },
                CompanyDecisionCodes = new List<FilterOptionDto<string>>
                {
                    new() { Id = "ACCEPTED", Name = "Accepterad" },
                    new() { Id = "PARTLYACCEPTED", Name = "Delvis accepterad" },
                    new() { Id = "DENIED", Name = "Nekad" },
                    new() { Id = "NOTRECLAIMEDBYCUSTOMER", Name = "Ej reklamerad av kund" }
                }
            });
        }

        [HttpGet("filter-options")]
        public async Task<ActionResult<DeviationFilterOptionsDto>> GetFilterOptions()
        {
            var statuses = await _dbContext.Deviations
                .AsNoTracking()
                .Where(x => !string.IsNullOrWhiteSpace(x.Status))
                .Select(x => x.Status)
                .Distinct()
                .OrderBy(x => x)
                .Select(x => new FilterOptionDto<string>
                {
                    Id = x,
                    Name = x
                })
                .ToListAsync();

            var responsibleUsers = await _dbContext.LegacyUsers
                .AsNoTracking()
                .Where(x => x.ResponsibleForDeviations.Any())
                .OrderBy(x => x.Name)
                .Select(x => new FilterOptionDto<int>
                {
                    Id = x.Id,
                    Name = x.Name ?? string.Empty
                })
                .ToListAsync();

            var customers = await _dbContext.Customers
                .AsNoTracking()
                .Where(x => x.Deviations.Any())
                .OrderBy(x => x.Name)
                .Select(x => new FilterOptionDto<int>
                {
                    Id = x.Id,
                    Name = x.Name ?? string.Empty
                })
                .ToListAsync();

            var suppliers = await _dbContext.Suppliers
                .AsNoTracking()
                .Where(x => x.Deviations.Any())
                .OrderBy(x => x.Name)
                .Select(x => new FilterOptionDto<int>
                {
                    Id = x.Id,
                    Name = x.Name ?? string.Empty
                })
                .ToListAsync();

            return Ok(new DeviationFilterOptionsDto
            {
                Statuses = statuses,
                ResponsibleUsers = responsibleUsers,
                Customers = customers,
                Suppliers = suppliers
            });
        }

        [HttpGet("overview")]
        public async Task<ActionResult<DeviationOverviewDto>> GetOverview([FromQuery] int? year = null)
        {
            var selectedYear = year ?? SwedishTime.Today.Year;
            var startOfYear = new DateTime(selectedYear, 1, 1);
            var endOfYear = new DateTime(selectedYear, 12, 31, 23, 59, 59);

            var openItems = await BuildBaseQuery()
                .Where(x => x.Status.ToUpper() == "OPEN")
                .OrderByDescending(x => x.DeviationOpened)
                .Select(x => new DeviationOpenItemDto
                {
                    Id = x.Id,
                    ResponsibleUserName = x.ResponsibleUser != null ? x.ResponsibleUser.Name : null,
                    DeviationNr = x.DeviationNr,
                    CustomerOrderNr = x.CustomerOrder != null ? x.CustomerOrder.CustomerOrderNr : null,
                    CustomerName = x.Customer != null ? x.Customer.Name : null,
                    SupplierName = x.Supplier != null ? x.Supplier.Name : null,
                    OpenDays = x.DeviationOpened.HasValue
                        ? EF.Functions.DateDiffDay(x.DeviationOpened.Value, SwedishTime.Today)
                        : 0
                })
                .ToListAsync();

            var yearRows = await _dbContext.Deviations
                .AsNoTracking()
                .Where(x =>
                    (x.DeviationOpened.HasValue && x.DeviationOpened.Value >= startOfYear && x.DeviationOpened.Value <= endOfYear) ||
                    (x.DeviationClosed.HasValue && x.DeviationClosed.Value >= startOfYear && x.DeviationClosed.Value <= endOfYear) ||
                    (x.DeviationOpened.HasValue && x.DeviationOpened.Value <= endOfYear && (!x.DeviationClosed.HasValue || x.DeviationClosed.Value >= startOfYear)))
                .Select(x => new
                {
                    x.DeviationOpened,
                    x.DeviationClosed,
                    x.ActualInternalCostSEK
                })
                .ToListAsync();

            var monthlyStats = Enumerable.Range(1, 12)
                .Select(month =>
                {
                    var monthStart = new DateTime(selectedYear, month, 1);
                    var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

                    var openedCount = yearRows.Count(x => x.DeviationOpened.HasValue && x.DeviationOpened.Value >= monthStart && x.DeviationOpened.Value <= monthEnd);
                    var closedInMonth = yearRows
                        .Where(x => x.DeviationClosed.HasValue && x.DeviationClosed.Value >= monthStart && x.DeviationClosed.Value <= monthEnd)
                        .ToList();

                    var closedCount = closedInMonth.Count;
                    var openAtMonthEndCount = yearRows.Count(x =>
                        x.DeviationOpened.HasValue &&
                        x.DeviationOpened.Value <= monthEnd &&
                        (!x.DeviationClosed.HasValue || x.DeviationClosed.Value > monthEnd));

                    var handlingDays = closedInMonth
                        .Where(x => x.DeviationOpened.HasValue)
                        .Select(x => (decimal)(x.DeviationClosed!.Value.Date - x.DeviationOpened!.Value.Date).TotalDays)
                        .ToList();

                    var handledWithin10DaysPercent = handlingDays.Count == 0
                        ? 0m
                        : Math.Round(handlingDays.Count(x => x <= 10m) * 100m / handlingDays.Count, 1);

                    return new DeviationMonthlyStatsDto
                    {
                        MonthNumber = month,
                        OpenedCount = openedCount,
                        ClosedCount = closedCount,
                        OpenAtMonthEndCount = openAtMonthEndCount,
                        AverageHandlingDays = handlingDays.Count == 0 ? 0m : Math.Round(handlingDays.Average(), 1),
                        HandledWithin10DaysPercent = handledWithin10DaysPercent,
                        ActualInternalCostSek = Math.Round(closedInMonth.Sum(x => x.ActualInternalCostSEK ?? 0m), 0)
                    };
                })
                .ToList();

            return Ok(new DeviationOverviewDto
            {
                Year = selectedYear,
                OpenItems = openItems,
                MonthlyStats = monthlyStats
            });
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<DeviationDto>> GetById(int id)
        {
            var deviation = await BuildBaseQuery()
                .Include(x => x.DeviationCosts)
                .Include(x => x.DocumentFiles)
                .Include(x => x.CustomerOrder)
                    .ThenInclude(o => o!.SupplierOrder)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (deviation is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(deviation));
        }

        [HttpGet("customer-order-data/{customerOrderId:int}")]
        public async Task<ActionResult<CustomerOrderLinkedDataDto>> GetCustomerOrderData(int customerOrderId)
        {
            var customerOrder = await _dbContext.CustomerOrders
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.SupplierOrder)
                .Include(x => x.SelectedCalculationRow)
                .Include(x => x.DeliveryToCustomers)
                .FirstOrDefaultAsync(x => x.Id == customerOrderId);

            if (customerOrder is null)
            {
                return NotFound();
            }

            // Get customer info
            var customerId = customerOrder.CustomerId;
            var customerName = customerOrder.Customer?.Name ?? customerOrder.CustomerName;

            // Get supplier info from related SupplierOrder
            var supplierId = customerOrder.SupplierOrder?.SupplierId;
            var supplierName = customerOrder.SupplierOrder?.SupplierName;

            // Get price per 1000 from supplier order (PurchasePrice)
            var pricePer1000 = customerOrder.SupplierOrder?.PurchasePrice != null
                ? Convert.ToDecimal(customerOrder.SupplierOrder.PurchasePrice)
                : (decimal?)null;

            // Get NrOfProduced from delivery items
            var nrOfProduced = customerOrder.SupplierOrder?.ProducedEdition != null
                ? Convert.ToDecimal(customerOrder.SupplierOrder.ProducedEdition)
                : (decimal?)null;

            // Get FreightCostPer1000 from selected calculation row
            var freightCostPer1000 = customerOrder.SelectedCalculationRow?.Freight != null
                ? Convert.ToDecimal(customerOrder.SelectedCalculationRow.Freight)
                : (decimal?)null;

            return Ok(new CustomerOrderLinkedDataDto
            {
                CustomerId = customerId,
                CustomerName = customerName,
                SupplierId = supplierId,
                SupplierName = supplierName,
                PricePer1000 = pricePer1000,
                NrOfProduced = nrOfProduced,
                FreightCostPer1000 = freightCostPer1000
            });
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<DeviationDto>>> Search([FromBody] SearchDeviationsRequest? request)
        {
            IQueryable<Deviation> query = BuildBaseQuery()
                .Include(x => x.DeviationCosts);

            var pagination = request?.Pagination ?? new PaginationRequest();
            query = ApplyFilters(query, request);

            var orderedQuery = ApplyOrdering(query, request?.OrderBy);

            var totalCount = await query.CountAsync();
            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pagination.PageSize);

            var items = await orderedQuery
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            return Ok(new PagedResultDto<DeviationDto>
            {
                Items = items.Select(MapToDto).ToList(),
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        [HttpPost]
        public async Task<ActionResult<DeviationDto>> Create([FromBody] CreateDeviationRequest request)
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

            DateTime From = SwedishTime.Today;
            DateTime To = From.AddDays(1);
            const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXZYÅÄÖ";
            var nrOfCreatedToday = _dbContext.Deviations.Where(p => p.CreatedTimeStamp >= From && p.CreatedTimeStamp < To).Count();
            string newNr = SwedishTime.Today.Year.ToString() + SwedishTime.Today.Month.ToString() + SwedishTime.Today.ToString("dd") + "-" + Letters[nrOfCreatedToday];

            var deviation = new Deviation
            {
                CustomerOrderId = request.CustomerOrderId,
                IsInternal = request.IsInternal,
                Status = request.Status,
                DeviationNr = newNr,
                ResponsibleUserId = request.ResponsibleUserId,
                DeviationOpened = request.DeviationOpened,
                DeviationClosed = request.DeviationClosed,
                DeviationProcessCode = request.DeviationProcessCode,
                SupplierId = request.SupplierId,
                CustomerId = request.CustomerId,
                DeviationTypeCode = request.DeviationTypeCode,
                Description = request.Description,
                RootCause = request.RootCause,
                MeasureTaken = request.MeasureTaken,
                EstimatedInternalCostSEK = request.EstimatedInternalCostSEK,
                EstimatedInternalCostPercentageOfOrder = request.EstimatedInternalCostPercentageOfOrder,
                SupplierDecisionCode = request.SupplierDecisionCode,
                CompanyDecisionCode = request.CompanyDecisionCode,
                ActualInternalCostSEK = request.ActualInternalCostSEK,
                NrOfProduced = request.NrOfProduced,
                NrOfClaimed = request.NrOfClaimed,
                FreightCostPer1000 = request.FreightCostPer1000,
                FreightCostCurrencyId = request.FreightCostCurrencyId,
                ClaimReason = request.ClaimReason,
                FollowupInfo = request.FollowupInfo,
                DeviationClosedToSupplier = request.DeviationClosedToSupplier,
                DeviationCosts = (request.Costs ?? new List<CreateDeviationCostRequest>())
                    .Select(cost => new DeviationCost
                    {
                        Text = cost.Text,
                        CostSEK = cost.CostSEK
                    })
                    .ToList()
            };

            deviation.CreatedTimeStamp ??= SwedishTime.Now;
            deviation.CreatedByUserId = legacyUser.Id;
            deviation.EditedTimeStamp = SwedishTime.Now;
            deviation.EditedByUserId = legacyUser.Id;

            _dbContext.Deviations.Add(deviation);
            await _dbContext.SaveChangesAsync();

            var created = await BuildBaseQuery()
                .Include(x => x.DeviationCosts)
                .Include(x => x.DocumentFiles)
                .Include(x => x.CustomerOrder)
                    .ThenInclude(o => o!.SupplierOrder)
                .FirstAsync(x => x.Id == deviation.Id);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<DeviationDto>> Update(int id, [FromBody] UpdateDeviationRequest request)
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

            var deviation = await _dbContext.Deviations
                .Include(x => x.DeviationCosts)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (deviation is null)
            {
                return NotFound();
            }

            deviation.CustomerOrderId = request.CustomerOrderId;
            deviation.IsInternal = request.IsInternal ?? false;
            deviation.Status = request.Status ?? string.Empty;
            deviation.DeviationNr = request.DeviationNr;
            deviation.ResponsibleUserId = request.ResponsibleUserId;
            deviation.DeviationOpened = request.DeviationOpened;
            deviation.DeviationClosed = request.DeviationClosed;
            deviation.DeviationProcessCode = request.DeviationProcessCode;
            deviation.SupplierId = request.SupplierId;
            deviation.CustomerId = request.CustomerId;
            deviation.DeviationTypeCode = request.DeviationTypeCode;
            deviation.Description = request.Description;
            deviation.RootCause = request.RootCause;
            deviation.MeasureTaken = request.MeasureTaken;
            deviation.EstimatedInternalCostSEK = request.EstimatedInternalCostSEK;
            deviation.EstimatedInternalCostPercentageOfOrder = request.EstimatedInternalCostPercentageOfOrder;
            deviation.SupplierDecisionCode = request.SupplierDecisionCode;
            deviation.CompanyDecisionCode = request.CompanyDecisionCode;
            deviation.ActualInternalCostSEK = request.ActualInternalCostSEK;
            deviation.NrOfProduced = request.NrOfProduced;
            deviation.NrOfClaimed = request.NrOfClaimed;
            deviation.FreightCostPer1000 = request.FreightCostPer1000;
            deviation.FreightCostCurrencyId = request.FreightCostCurrencyId;
            deviation.ClaimReason = request.ClaimReason;
            deviation.FollowupInfo = request.FollowupInfo;
            deviation.DeviationClosedToSupplier = request.DeviationClosedToSupplier;

            deviation.CreatedTimeStamp ??= SwedishTime.Now;
            deviation.CreatedByUserId = legacyUser.Id;
            deviation.EditedTimeStamp = SwedishTime.Now;
            deviation.EditedByUserId = legacyUser.Id;

            _dbContext.DeviationCosts.RemoveRange(deviation.DeviationCosts);
            deviation.DeviationCosts = (request.Costs ?? new List<CreateDeviationCostRequest>())
                .Select(cost => new DeviationCost
                {
                    DeviationId = deviation.Id,
                    Text = cost.Text,
                    CostSEK = cost.CostSEK
                })
                .ToList();

            await _dbContext.SaveChangesAsync();

            var updated = await BuildBaseQuery()
                .Include(x => x.DeviationCosts)
                .Include(x => x.DocumentFiles)
                .Include(x => x.CustomerOrder)
                    .ThenInclude(o => o!.SupplierOrder) 
                .FirstAsync(x => x.Id == id);

            return Ok(MapToDto(updated));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deviation = await _dbContext.Deviations
                .Include(x => x.DeviationCosts)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (deviation is null)
            {
                return NotFound();
            }

            _dbContext.DeviationCosts.RemoveRange(deviation.DeviationCosts);
            _dbContext.Deviations.Remove(deviation);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        private IQueryable<Deviation> BuildBaseQuery()
        {
            return _dbContext.Deviations
                .AsNoTracking()
                .Include(x => x.CustomerOrder)
                    .ThenInclude(o => o!.Customer)
                .Include(x => x.Customer)
                .Include(x => x.Supplier)
                .Include(x => x.ResponsibleUser);
        }

        private static IOrderedQueryable<Deviation> ApplyOrdering(IQueryable<Deviation> query, List<SortRequest>? orderBy)
        {
            if (orderBy is null || orderBy.Count == 0)
            {
                return query.OrderByDescending(x => x.Id);
            }

            IOrderedQueryable<Deviation>? orderedQuery = null;

            foreach (var sort in orderBy)
            {
                var desc = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
                orderedQuery = ApplySort(orderedQuery, query, sort.Field, desc);
            }

            return orderedQuery ?? query.OrderByDescending(x => x.Id);
        }

        private static IOrderedQueryable<Deviation> ApplySort(
            IOrderedQueryable<Deviation>? orderedQuery,
            IQueryable<Deviation> source,
            string field,
            bool desc)
        {
            var key = field?.Trim().ToLowerInvariant() ?? string.Empty;

            return key switch
            {
                "id" => ApplySortCore(orderedQuery, source, x => x.Id, desc),
                "status" => ApplySortCore(orderedQuery, source, x => x.Status, desc),
                "deviationnr" => ApplySortCore(orderedQuery, source, x => x.DeviationNr, desc),
                "customerorderid" => ApplySortCore(orderedQuery, source, x => x.CustomerOrderId, desc),
                "customerordernr" => ApplySortCore(orderedQuery, source, x => x.CustomerOrder != null ? x.CustomerOrder.CustomerOrderNr : null, desc),
                "supplierid" => ApplySortCore(orderedQuery, source, x => x.SupplierId, desc),
                "suppliername" => ApplySortCore(orderedQuery, source, x => x.Supplier != null ? x.Supplier.Name : null, desc),
                "customerid" => ApplySortCore(orderedQuery, source, x => x.CustomerId, desc),
                "customername" => ApplySortCore(orderedQuery, source, x => x.Customer != null ? x.Customer.Name : null, desc),
                "responsibleuserid" => ApplySortCore(orderedQuery, source, x => x.ResponsibleUserId, desc),
                "responsibleusername" => ApplySortCore(orderedQuery, source, x => x.ResponsibleUser != null ? x.ResponsibleUser.Name : null, desc),
                "deviationopened" => ApplySortCore(orderedQuery, source, x => x.DeviationOpened, desc),
                "deviationclosed" => ApplySortCore(orderedQuery, source, x => x.DeviationClosed, desc),
                "deviationprocesscode" => ApplySortCore(orderedQuery, source, x => x.DeviationProcessCode, desc),
                "description" => ApplySortCore(orderedQuery, source, x => x.Description, desc),
                "createdtimestamp" => ApplySortCore(orderedQuery, source, x => x.CreatedTimeStamp, desc),
                "estimatedinternalcostsek" => ApplySortCore(orderedQuery, source, x => x.EstimatedInternalCostSEK, desc),
                _ => orderedQuery ?? source.OrderByDescending(x => x.Id)
            };
        }

        private static IQueryable<Deviation> ApplyFilters(IQueryable<Deviation> query, SearchDeviationsRequest? request)
        {
            if (!string.IsNullOrWhiteSpace(request?.SearchTerm))
            {
                var searchTerm = request.SearchTerm.Trim().ToLower();
                query = query.Where(x =>
                    (x.DeviationNr != null && x.DeviationNr.ToLower().Contains(searchTerm)) ||
                    (x.Status != null && x.Status.ToLower().Contains(searchTerm)) ||
                    (x.Description != null && x.Description.ToLower().Contains(searchTerm)) ||
                    (x.RootCause != null && x.RootCause.ToLower().Contains(searchTerm)) ||
                    (x.MeasureTaken != null && x.MeasureTaken.ToLower().Contains(searchTerm)) ||
                    (x.ClaimReason != null && x.ClaimReason.ToLower().Contains(searchTerm)) ||
                    (x.FollowupInfo != null && x.FollowupInfo.ToLower().Contains(searchTerm)) ||
                    (x.CustomerOrder != null && x.CustomerOrder.CustomerOrderNr != null && x.CustomerOrder.CustomerOrderNr.ToLower().Contains(searchTerm)) ||
                    (x.Customer != null && x.Customer.Name != null && x.Customer.Name.ToLower().Contains(searchTerm)) ||
                    (x.Supplier != null && x.Supplier.Name != null && x.Supplier.Name.ToLower().Contains(searchTerm)) ||
                    (x.ResponsibleUser != null && x.ResponsibleUser.Name != null && x.ResponsibleUser.Name.ToLower().Contains(searchTerm)));
            }

            if (!string.IsNullOrWhiteSpace(request?.Status))
            {
                query = query.Where(x => x.Status == request.Status);
            }

            if (!string.IsNullOrWhiteSpace(request?.DeviationProcessCode))
            {
                query = query.Where(x => x.DeviationProcessCode == request.DeviationProcessCode);
            }

            if (request?.IsInternal.HasValue == true)
            {
                query = query.Where(x => x.IsInternal == request.IsInternal.Value);
            }

            if (request?.CustomerOrderId.HasValue == true)
            {
                query = query.Where(x => x.CustomerOrderId == request.CustomerOrderId.Value);
            }

            if (request?.SupplierId.HasValue == true)
            {
                query = query.Where(x => x.SupplierId == request.SupplierId.Value);
            }

            if (request?.CustomerId.HasValue == true)
            {
                query = query.Where(x => x.CustomerId == request.CustomerId.Value);
            }

            if (request?.ResponsibleUserId.HasValue == true)
            {
                query = query.Where(x => x.ResponsibleUserId == request.ResponsibleUserId.Value);
            }

            if (request?.OpenedFrom.HasValue == true)
            {
                query = query.Where(x => x.DeviationOpened >= request.OpenedFrom.Value);
            }

            if (request?.OpenedTo.HasValue == true)
            {
                query = query.Where(x => x.DeviationOpened <= request.OpenedTo.Value);
            }

            if (request?.ClosedFrom.HasValue == true)
            {
                query = query.Where(x => x.DeviationClosed >= request.ClosedFrom.Value);
            }

            if (request?.ClosedTo.HasValue == true)
            {
                query = query.Where(x => x.DeviationClosed <= request.ClosedTo.Value);
            }

            return query;
        }

        private static IOrderedQueryable<Deviation> ApplySortCore<TKey>(
            IOrderedQueryable<Deviation>? orderedQuery,
            IQueryable<Deviation> source,
            System.Linq.Expressions.Expression<Func<Deviation, TKey>> keySelector,
            bool desc)
        {
            if (orderedQuery is null)
            {
                return desc ? source.OrderByDescending(keySelector) : source.OrderBy(keySelector);
            }

            return desc ? orderedQuery.ThenByDescending(keySelector) : orderedQuery.ThenBy(keySelector);
        }

        private static DeviationDto MapToDto(Deviation entity)
        {
            return new DeviationDto
            {
                Id = entity.Id,
                CustomerOrderId = entity.CustomerOrderId,
                CustomerOrderNr = entity.CustomerOrder?.CustomerOrderNr,
                CustomerOrderCustomerName = entity.CustomerOrder?.Customer?.Name,
                IsInternal = entity.IsInternal,
                Status = entity.Status,
                DeviationNr = entity.DeviationNr,
                CreatedByUserId = entity.CreatedByUserId,
                CreatedByUserName = "Alve Don",
                CreatedTimeStamp = entity.CreatedTimeStamp,
                EditedByUserId = entity.EditedByUserId,
                EditedByUserName = "Alve Don",
                EditedTimeStamp = entity.EditedTimeStamp,
                ResponsibleUserId = entity.ResponsibleUserId,
                ResponsibleUserName = entity.ResponsibleUser?.Name,
                DeviationOpened = entity.DeviationOpened,
                DeviationClosed = entity.DeviationClosed,
                OpenDays = GetOpenDays(entity),
                DeviationProcessCode = entity.DeviationProcessCode,
                SupplierId = entity.SupplierId,
                SupplierName = entity.Supplier?.Name,
                CustomerId = entity.CustomerId,
                CustomerName = entity.Customer?.Name,
                DeviationTypeCode = entity.DeviationTypeCode,
                Description = entity.Description,
                RootCause = entity.RootCause,
                MeasureTaken = entity.MeasureTaken,
                EstimatedInternalCostSEK = entity.EstimatedInternalCostSEK,
                EstimatedInternalCostPercentageOfOrder = entity.EstimatedInternalCostPercentageOfOrder,
                SupplierDecisionCode = entity.SupplierDecisionCode,
                CompanyDecisionCode = entity.CompanyDecisionCode,
                ActualInternalCostSEK = entity.ActualInternalCostSEK,
                NrOfProduced = entity.NrOfProduced,
                NrOfClaimed = entity.NrOfClaimed,
                FreightCostPer1000 = entity.FreightCostPer1000,
                FreightCostCurrencyId = entity.FreightCostCurrencyId,
                ClaimReason = entity.ClaimReason,
                FollowupInfo = entity.FollowupInfo,
                DeviationClosedToSupplier = entity.DeviationClosedToSupplier,
                PricePer1000 = entity.CustomerOrder != null && entity.CustomerOrder.SupplierOrder != null ? (decimal?)entity.CustomerOrder.SupplierOrder.PurchasePrice : (decimal?)null,
                Costs = entity.DeviationCosts
                    .OrderBy(x => x.Id)
                    .Select(x => new DeviationCostDto
                    {
                        Id = x.Id,
                        DeviationId = x.DeviationId,
                        Text = x.Text,
                        CostSEK = x.CostSEK
                    })
                    .ToList(),
                AttachedFiles = entity.DocumentFiles
                    .OrderBy(x => x.Id)
                    .Select(x => new DocumentFileDto
                    {
                        Id = x.Id,
                        FileNamePath = x.FileNamePath,
                        FileType = x.FileType,
                        CreatedByUserId = x.CreatedByUserId,
                        CreatedDateTime = x.CreatedDateTime,
                        AttestedByInitials = x.AttestedByInitials,
                        AttestedDateTime = x.AttestedDateTime,
                        ProductId = x.ProductId,
                        CallOffId = x.CallOffId,
                        CreatedByUserName = x.CreatedByUser != null ? x.CreatedByUser.Name : null,
                        Description = x.Description,
                        DeviationId = x.DeviationId,
                        DocumentTypeId = x.DocumentTypeId,
                        IsAttested = x.IsAttested,
                        IsPaid = x.IsPaid,
                        Name = x.Name,
                        PaidByInitials = x.PaidByInitials,
                        PaidDateTime = x.PaidDateTime,
                        SupplierOrderId = x.SupplierOrderId,
                        TransportOrderId = x.TransportOrderId,
                    })
                    .ToList()
            };
        }

        private static int? GetOpenDays(Deviation entity)
        {
            if (!entity.DeviationOpened.HasValue)
            {
                return null;
            }

            var endDate = entity.DeviationClosed?.Date ?? SwedishTime.Today;
            return Math.Max(0, (endDate - entity.DeviationOpened.Value.Date).Days);
        }
    }
}
