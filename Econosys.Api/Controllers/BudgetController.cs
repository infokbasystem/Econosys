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
    public class BudgetController : ControllerBase
    {
        private const int MonthCount = 12;
        private const int AvailableCustomersTake = 50;
        private const string BudgetOwnedByOtherMessage = "Budgeten är öppnad av en annan användare.";

        private readonly ApplicationDbContext _dbContext;
        private readonly ILegacyUserResolutionService _legacyUserResolutionService;

        public BudgetController(ApplicationDbContext dbContext, ILegacyUserResolutionService legacyUserResolutionService)
        {
            _dbContext = dbContext;
            _legacyUserResolutionService = legacyUserResolutionService;
        }

        [HttpGet]
        public async Task<ActionResult<List<BudgetListItemDto>>> GetAll()
        {
            return Ok(await LoadBudgetListAsync());
        }

        [HttpGet("form-options")]
        public async Task<ActionResult<BudgetFormOptionsDto>> GetFormOptions()
        {
            var employees = await _dbContext.LegacyUsers
                .AsNoTracking()
                .Where(x => x.Active && x.IncludeInBudget)
                .OrderBy(x => x.Name)
                .Select(x => new BudgetEmployeeOptionDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Initials = x.Initials,
                })
                .ToListAsync();

            var currentYear = SwedishTime.Now.Year;

            return Ok(new BudgetFormOptionsDto
            {
                Employees = employees,
                Budgets = await LoadBudgetListAsync(),
                Years = Enumerable.Range(currentYear - 5, 6).ToList(),
            });
        }

        [HttpGet("active")]
        public async Task<ActionResult<BudgetDto>> GetActive()
        {
            var activeId = await _dbContext.Budgets
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.Year)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync();

            if (activeId is null)
            {
                return NoContent();
            }

            var dto = await LoadBudgetDtoAsync(activeId.Value);
            return dto is null ? NoContent() : Ok(dto);
        }

        [HttpGet("new-template")]
        public async Task<ActionResult<BudgetDto>> GetNewTemplate()
        {
            var distribution = await _dbContext.BudgetMonthDistributions
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync();

            var defaultShare = 100m / MonthCount;
            var distributionMonths = distribution is null
                ? Enumerable.Repeat<decimal?>(defaultShare, MonthCount).ToArray()
                : new[]
                {
                    distribution.Month1, distribution.Month2, distribution.Month3, distribution.Month4,
                    distribution.Month5, distribution.Month6, distribution.Month7, distribution.Month8,
                    distribution.Month9, distribution.Month10, distribution.Month11, distribution.Month12,
                };

            var customers = await _dbContext.Customers
                .AsNoTracking()
                .Where(x => x.Active && x.ResponsibleUserId != null)
                .OrderBy(x => x.Name)
                .Select(x => new BudgetCustomerDto
                {
                    CustomerId = x.Id,
                    CustomerName = x.Name,
                    CustomerActive = x.Active,
                    BudgetCountAsNewUntilMonth = x.BudgetCountAsNewUntilMonth,
                    EmployeeId = x.ResponsibleUserId,
                    EmployeeName = x.ResponsibleUser != null ? x.ResponsibleUser.Initials : null,
                    TotalSalesBudget = 0,
                    AdditionBudget = 0,
                })
                .ToListAsync();

            var employees = await _dbContext.LegacyUsers
                .AsNoTracking()
                .Where(x => x.Active && x.IncludeInBudget)
                .OrderBy(x => x.Name)
                .Select(x => new { x.Id, x.Initials })
                .ToListAsync();

            customers.AddRange(employees.Select(x => new BudgetCustomerDto
            {
                CustomerName = $"Nysälj {x.Initials}",
                EmployeeId = x.Id,
                EmployeeName = x.Initials,
                TotalSalesBudget = 0,
                AdditionBudget = 0,
            }));

            var currentYear = SwedishTime.Now.Year;

            return Ok(new BudgetDto
            {
                Name = "Ny budget",
                Year = currentYear + 1,
                DistributionMonths = distributionMonths,
                ComparePrevYear = currentYear,
                ComparePrevPrevYear = currentYear - 1,
                Customers = customers,
            });
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<BudgetDto>> GetById(int id)
        {
            var dto = await LoadBudgetDtoAsync(id);
            return dto is null ? NotFound() : Ok(dto);
        }

        [HttpPost("aggregate")]
        public async Task<ActionResult<BudgetDto>> Create([FromBody] SaveBudgetRequest request)
        {
            var currentUser = await _legacyUserResolutionService.ResolveCurrentUserAsync(User);
            if (currentUser is null)
            {
                return CurrentUserMissing();
            }

            var validationError = await ValidateAsync(request);
            if (validationError is not null)
            {
                return validationError;
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            var entity = new Budget
            {
                IsOwned = true,
                OwnedByUserId = currentUser.Id,
                OwnedByUserName = currentUser.Initials,
                OwnedDateTime = SwedishTime.Now,
            };
            MapHeader(request, entity);

            if (entity.IsActive)
            {
                await DeactivateOtherBudgetsAsync(null);
            }

            _dbContext.Budgets.Add(entity);
            await _dbContext.SaveChangesAsync();

            SyncCustomers(entity, new List<BudgetCustomer>(), request.Customers);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            var dto = await LoadBudgetDtoAsync(entity.Id);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, dto);
        }

        [HttpPut("{id:int}/aggregate")]
        public async Task<ActionResult<BudgetDto>> Update(int id, [FromBody] SaveBudgetRequest request)
        {
            var currentUser = await _legacyUserResolutionService.ResolveCurrentUserAsync(User);
            if (currentUser is null)
            {
                return CurrentUserMissing();
            }

            var validationError = await ValidateAsync(request);
            if (validationError is not null)
            {
                return validationError;
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            var entity = await _dbContext.Budgets.FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null)
            {
                return NotFound();
            }

            if (IsOwnedByOther(entity, currentUser.Id))
            {
                return Conflict(new { message = BudgetOwnedByOtherMessage });
            }

            MapHeader(request, entity);

            if (entity.IsActive)
            {
                await DeactivateOtherBudgetsAsync(entity.Id);
            }

            var existingCustomers = await _dbContext.BudgetCustomers
                .Include(x => x.BudgetCustomerMonths)
                .Where(x => x.BudgetId == id)
                .ToListAsync();

            SyncCustomers(entity, existingCustomers, request.Customers);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(await LoadBudgetDtoAsync(id));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var currentUser = await _legacyUserResolutionService.ResolveCurrentUserAsync(User);
            if (currentUser is null)
            {
                return CurrentUserMissing();
            }

            var entity = await _dbContext.Budgets
                .AsSplitQuery()
                .Include(x => x.BudgetCustomers)
                    .ThenInclude(x => x.BudgetCustomerMonths)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            if (IsOwnedByOther(entity, currentUser.Id))
            {
                return Conflict(new { message = BudgetOwnedByOtherMessage });
            }

            if (entity.IsLocked)
            {
                return BadRequest(new { message = "En låst budget kan inte raderas." });
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            _dbContext.BudgetCustomerMonths.RemoveRange(entity.BudgetCustomers.SelectMany(x => x.BudgetCustomerMonths));
            _dbContext.BudgetCustomers.RemoveRange(entity.BudgetCustomers);
            _dbContext.Budgets.Remove(entity);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return NoContent();
        }

        [HttpPost("sales-stat/per-month")]
        public async Task<ActionResult<List<BudgetSalesStatMonthDto>>> GetSalesStatPerMonth([FromBody] BudgetSalesStatRequest request)
        {
            var rows = await LoadSalesRowsAsync(request);

            var months = Enumerable.Range(1, MonthCount)
                .Select(month =>
                {
                    var monthRows = rows.Where(x => x.Created.Month == month).ToList();
                    var totalSales = monthRows.Sum(x => x.Sales);
                    var totalTb = monthRows.Sum(x => x.Tb);

                    return new BudgetSalesStatMonthDto
                    {
                        Month = month,
                        TotalSales = ToDecimal(totalSales),
                        TotalTb = ToDecimal(totalTb),
                        AverageAddition = ToDecimal(CalculateAverageAddition(totalSales, totalTb)),
                    };
                })
                .ToList();

            return Ok(months);
        }

        [HttpPost("sales-stat/per-customer")]
        public async Task<ActionResult<List<BudgetSalesStatCustomerDto>>> GetSalesStatPerCustomer([FromBody] BudgetSalesStatRequest request)
        {
            var rows = await LoadSalesRowsAsync(request);

            var customers = rows
                .Where(x => x.CustomerId.HasValue)
                .GroupBy(x => x.CustomerId!.Value)
                .Select(group =>
                {
                    var first = group.First();
                    var totalSales = group.Sum(x => x.Sales);
                    var totalTb = group.Sum(x => x.Tb);

                    return new BudgetSalesStatCustomerDto
                    {
                        CustomerId = group.Key,
                        CustomerName = first.CustomerName,
                        EmployeeId = first.EmployeeId,
                        EmployeeName = first.EmployeeName,
                        TotalSales = ToDecimal(totalSales),
                        TotalTb = ToDecimal(totalTb),
                        AverageAddition = ToDecimal(CalculateAverageAddition(totalSales, totalTb)),
                    };
                })
                .OrderBy(x => x.CustomerName)
                .ToList();

            return Ok(customers);
        }

        [HttpGet("{id:int}/sales-report")]
        public async Task<ActionResult<BudgetSalesReportDto>> GetSalesReport(int id, [FromQuery] bool includeExistingNewCustomers = true)
        {
            var budget = await LoadBudgetDtoAsync(id);
            if (budget is null)
            {
                return NotFound();
            }

            if (!budget.Year.HasValue)
            {
                return BadRequest(new { message = "Budgeten saknar år." });
            }

            var year = budget.Year.Value;
            var yearStart = new DateTime(year, 1, 1);
            var nextYearStart = yearStart.AddYears(1);
            var historyStart = yearStart.AddYears(-3).AddDays(-1);

            var rawOrders = await _dbContext.CustomerOrders
                .AsNoTracking()
                .Where(x => x.Created.HasValue && x.Created.Value >= historyStart && x.Created.Value < nextYearStart)
                .Select(x => new
                {
                    x.CustomerId,
                    CustomerName = x.Customer != null ? x.Customer.Name : x.CustomerName,
                    Category = x.Customer != null ? x.Customer.Category : null,
                    EmployeeId = x.Customer != null ? x.Customer.ResponsibleUserId : null,
                    EmployeeName = x.Customer != null && x.Customer.ResponsibleUser != null ? x.Customer.ResponsibleUser.Initials : null,
                    Created = x.Created!.Value,
                    x.SalesPrice,
                    x.SalesCurrencyRate,
                    x.Edition,
                    x.TotalCostInSalesCurrency,
                    Multiplicator = x.Unit != null ? x.Unit.Multiplicator : null,
                    PurchasePrice = x.SupplierOrder != null ? x.SupplierOrder.PurchasePrice : null,
                    PurchaseCurrencyRate = x.SupplierOrder != null ? x.SupplierOrder.PurchaseCurrencyRate : null,
                    BudgetCountAsNewUntilMonth = x.Customer != null ? x.Customer.BudgetCountAsNewUntilMonth : null,
                })
                .ToListAsync();

            var orders = rawOrders.Select(x =>
            {
                var multiplicator = x.Multiplicator is null or 0 ? 1d : x.Multiplicator.Value;
                var edition = x.Edition ?? 0;
                var salesRate = x.SalesCurrencyRate ?? 0;
                var sales = (x.SalesPrice ?? 0) * salesRate * edition / multiplicator;
                var cost = ((x.PurchasePrice ?? 0) * (x.PurchaseCurrencyRate ?? 0)
                    + (double)(x.TotalCostInSalesCurrency ?? 0) * salesRate)
                    * edition / multiplicator;

                return new SalesReportOrder(
                    x.CustomerId,
                    x.CustomerName,
                    x.Category,
                    x.EmployeeId,
                    x.EmployeeName,
                    x.Created,
                    sales,
                    sales - cost,
                    x.BudgetCountAsNewUntilMonth);
            }).ToList();

            var employees = await _dbContext.LegacyUsers
                .AsNoTracking()
                .Where(x => x.Active && x.IncludeInBudget)
                .OrderBy(x => x.Name)
                .Select(x => new BudgetSalesReportEmployeeDto
                {
                    Id = x.Id,
                    Name = x.Name,
                })
                .ToListAsync();
            var employeeNameById = employees.ToDictionary(x => x.Id, x => x.Name);

            var customerMap = new Dictionary<int, BudgetSalesReportCustomerDto>();
            foreach (var budgetCustomer in budget.Customers.Where(x => x.CustomerId.HasValue))
            {
                var customerId = budgetCustomer.CustomerId!.Value;
                customerMap[customerId] = new BudgetSalesReportCustomerDto
                {
                    Id = customerId,
                    Name = budgetCustomer.CustomerName,
                    EmployeeId = budgetCustomer.EmployeeId,
                    EmployeeName = budgetCustomer.EmployeeName,
                };
            }

            foreach (var order in orders.Where(x => x.CustomerId is > 0))
            {
                var customerId = order.CustomerId!.Value;
                if (!customerMap.ContainsKey(customerId))
                {
                    customerMap[customerId] = new BudgetSalesReportCustomerDto
                    {
                        Id = customerId,
                        Name = order.CustomerName,
                        Category = order.Category,
                        EmployeeId = order.EmployeeId,
                        EmployeeName = order.EmployeeName,
                    };
                }
                else if (string.IsNullOrWhiteSpace(customerMap[customerId].Category))
                {
                    customerMap[customerId].Category = order.Category;
                }
            }

            var customerIds = customerMap.Keys.ToList();
            var customerCategories = await _dbContext.Customers
                .AsNoTracking()
                .Where(x => customerIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Category);
            foreach (var customer in customerMap.Values)
            {
                if (customerCategories.TryGetValue(customer.Id, out var category))
                {
                    customer.Category = category;
                }
            }

            var previousYearStart = yearStart.AddYears(-1);
            var today = SwedishTime.Now;
            var currentYtdEnd = new DateTime(
                year,
                today.Month,
                Math.Min(today.Day, DateTime.DaysInMonth(year, today.Month)))
                .AddDays(1);
            var previousYtdEnd = new DateTime(
                year - 1,
                today.Month,
                Math.Min(today.Day, DateTime.DaysInMonth(year - 1, today.Month)))
                .AddDays(1);
            var previousCustomerIds = orders
                .Where(x => x.Created >= historyStart && x.Created < yearStart.AddDays(-1))
                .Select(x => x.CustomerId.GetValueOrDefault())
                .ToHashSet();

            var currentRows = customerIds.ToDictionary(
                customerId => customerId,
                customerId => new BudgetSalesReportMetricRowDto { CustomerId = customerId });
            var previousRows = customerIds.ToDictionary(
                customerId => customerId,
                customerId => new BudgetSalesReportMetricRowDto { CustomerId = customerId });
            currentRows.TryAdd(0, new BudgetSalesReportMetricRowDto { CustomerId = 0 });
            previousRows.TryAdd(0, new BudgetSalesReportMetricRowDto { CustomerId = 0 });
            var currentActiveCustomers = new HashSet<(int CustomerId, int Month)>();
            var previousActiveCustomers = new HashSet<(int CustomerId, int Month)>();
            var currentYtdActiveCustomers = new HashSet<int>();
            var previousYtdActiveCustomers = new HashSet<int>();
            var currentYtdNewCustomers = new HashSet<int>();
            var currentYtdExistingNewCustomers = new HashSet<int>();
            var currentYearExistingNewCustomers = new HashSet<int>();
            var existingNewCustomers = new HashSet<(int CustomerId, int Month)>();
            var yearEnd = DateOnly.FromDateTime(nextYearStart);
            var yearStartDate = DateOnly.FromDateTime(yearStart);

            foreach (var order in orders.Where(x => x.CustomerId is null or >= 0))
            {
                var customerId = order.CustomerId.GetValueOrDefault();
                var monthIndex = order.Created.Month - 1;
                var sales = (decimal)order.Sales;
                var tb = (decimal)order.Tb;

                if (order.Created >= yearStart && order.Created < nextYearStart)
                {
                    var row = currentRows[customerId];
                    row.SalesMonths[monthIndex] = (row.SalesMonths[monthIndex] ?? 0) + sales;
                    row.TbMonths[monthIndex] = (row.TbMonths[monthIndex] ?? 0) + tb;
                    row.OrderCountMonths[monthIndex]++;
                    if (order.Created < currentYtdEnd)
                    {
                        row.YtdOrderCount++;
                    }
                    currentActiveCustomers.Add((customerId, monthIndex));
                    if (order.Created < currentYtdEnd)
                    {
                        currentYtdActiveCustomers.Add(customerId);
                    }

                    var isNewCustomer = !previousCustomerIds.Contains(customerId);
                    if (isNewCustomer)
                    {
                        row.NewSalesMonths[monthIndex] = (row.NewSalesMonths[monthIndex] ?? 0) + sales;
                        row.NewTbMonths[monthIndex] = (row.NewTbMonths[monthIndex] ?? 0) + tb;
                        row.NewCustomerCountMonths[monthIndex] = 1;
                        if (order.Created < currentYtdEnd)
                        {
                            currentYtdNewCustomers.Add(customerId);
                        }
                    }

                    var countAsNewUntil = order.BudgetCountAsNewUntilMonth;
                    if (includeExistingNewCustomers &&
                        countAsNewUntil > yearStartDate &&
                        countAsNewUntil < yearEnd)
                    {
                        currentYearExistingNewCustomers.Add(customerId);
                        if (order.Created < currentYtdEnd)
                        {
                            currentYtdExistingNewCustomers.Add(customerId);
                        }
                    }

                    if (includeExistingNewCustomers && countAsNewUntil > yearStartDate && countAsNewUntil < yearEnd &&
                        DateOnly.FromDateTime(order.Created) < countAsNewUntil)
                    {
                        row.ExistingNewSalesMonths[monthIndex] = (row.ExistingNewSalesMonths[monthIndex] ?? 0) + sales;
                        row.ExistingNewTbMonths[monthIndex] = (row.ExistingNewTbMonths[monthIndex] ?? 0) + tb;
                        existingNewCustomers.Add((customerId, monthIndex));
                    }
                }
                else if (order.Created >= previousYearStart && order.Created < yearStart)
                {
                    var row = previousRows[customerId];
                    row.SalesMonths[monthIndex] = (row.SalesMonths[monthIndex] ?? 0) + sales;
                    row.TbMonths[monthIndex] = (row.TbMonths[monthIndex] ?? 0) + tb;
                    row.OrderCountMonths[monthIndex]++;
                    if (order.Created < previousYtdEnd)
                    {
                        row.YtdOrderCount++;
                    }
                    previousActiveCustomers.Add((customerId, monthIndex));
                    if (order.Created < previousYtdEnd)
                    {
                        previousYtdActiveCustomers.Add(customerId);
                    }
                }
            }

            foreach (var (customerId, monthIndex) in currentActiveCustomers)
            {
                currentRows[customerId].ActiveCustomerMonths[monthIndex] = 1;
            }

            foreach (var (customerId, monthIndex) in previousActiveCustomers)
            {
                previousRows[customerId].ActiveCustomerMonths[monthIndex] = 1;
            }

            foreach (var customerId in currentYtdActiveCustomers)
            {
                currentRows[customerId].YtdActiveCustomer = 1;
            }

            foreach (var customerId in previousYtdActiveCustomers)
            {
                previousRows[customerId].YtdActiveCustomer = 1;
            }

            foreach (var customerId in currentYtdNewCustomers)
            {
                currentRows[customerId].YtdNewCustomerCount = 1;
            }

            foreach (var customerId in currentYtdExistingNewCustomers)
            {
                currentRows[customerId].YtdExistingNewCustomerCount = 1;
            }

            foreach (var customerId in currentYearExistingNewCustomers)
            {
                currentRows[customerId].YearExistingNewCustomerCount = 1;
            }

            foreach (var (customerId, monthIndex) in existingNewCustomers)
            {
                currentRows[customerId].ExistingNewCustomerCountMonths[monthIndex] = 1;
                currentRows[customerId].NewCustomerCountMonths[monthIndex] = 1;
            }

            var budgetRows = budget.Customers.Select(x => new BudgetSalesReportBudgetRowDto
            {
                CustomerId = x.CustomerId,
                CustomerName = x.CustomerName,
                EmployeeId = x.EmployeeId,
                EmployeeName = x.EmployeeId.HasValue && employeeNameById.TryGetValue(x.EmployeeId.Value, out var employeeName)
                    ? employeeName
                    : x.EmployeeName,
                BudgetCountAsNewUntilMonth = x.BudgetCountAsNewUntilMonth,
                SalesMonths = x.SalesMonths,
                AdditionMonths = x.AdditionMonths,
            }).ToList();

            return Ok(new BudgetSalesReportDto
            {
                BudgetId = budget.Id,
                BudgetName = budget.Name,
                Year = year,
                Employees = employees,
                Customers = customerMap.Values.OrderBy(x => x.Name).ToList(),
                CurrentYear = currentRows.Values.ToList(),
                PreviousYear = previousRows.Values.ToList(),
                BudgetRows = budgetRows,
            });
        }

        [HttpGet("available-customers")]
        public async Task<ActionResult<List<BudgetAvailableCustomerDto>>> GetAvailableCustomers([FromQuery] int? budgetId, [FromQuery] string? search)
        {
            var query = _dbContext.Customers
                .AsNoTracking()
                .Where(x => x.Active);

            if (budgetId is > 0)
            {
                query = query.Where(x => !_dbContext.BudgetCustomers.Any(bc =>
                    bc.BudgetId == budgetId &&
                    !bc.IsRemovedFromBudget &&
                    bc.CustomerId == x.Id));
            }

            var searchText = search?.Trim();
            if (!string.IsNullOrEmpty(searchText))
            {
                query = query.Where(x => x.Name != null && EF.Functions.Like(x.Name, $"%{searchText}%"));
            }

            var customers = await query
                .OrderBy(x => x.Name)
                .Take(AvailableCustomersTake)
                .Select(x => new BudgetAvailableCustomerDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    ResponsibleUserId = x.ResponsibleUserId,
                    ResponsibleUserName = x.ResponsibleUser != null ? x.ResponsibleUser.Initials : null,
                    BudgetCountAsNewUntilMonth = x.BudgetCountAsNewUntilMonth,
                })
                .ToListAsync();

            return Ok(customers);
        }

        [HttpPost("customers/{customerId:int}/active")]
        public async Task<IActionResult> SetCustomerActive(int customerId, [FromBody] SetBudgetCustomerActiveRequest request)
        {
            var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == customerId);
            if (customer is null)
            {
                return NotFound();
            }

            customer.Active = request.Active;
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("customers/{customerId:int}/allocation")]
        public async Task<IActionResult> AllocateCustomer(int customerId, [FromBody] AllocateBudgetCustomerRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var customerExists = await _dbContext.Customers.AnyAsync(x => x.Id == customerId);
            if (!customerExists)
            {
                return NotFound();
            }

            var employeeExists = await _dbContext.LegacyUsers.AnyAsync(x => x.Id == request.EmployeeId && x.Active);
            if (!employeeExists)
            {
                return BadRequest(new { message = "Säljaren finns inte eller är inaktiv." });
            }

            _dbContext.CustomerEmployeeAllocations.Add(new CustomerEmployeeAllocation
            {
                CustomerId = customerId,
                EmployeeId = request.EmployeeId,
                AllocateFromDate = request.AllocateFromDate!.Value.Date,
            });
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("{id:int}/ownership")]
        public async Task<ActionResult<BudgetOwnershipDto>> GetOwnership(int id)
        {
            var currentUser = await _legacyUserResolutionService.ResolveCurrentUserAsync(User);
            if (currentUser is null)
            {
                return CurrentUserMissing();
            }

            var entity = await _dbContext.Budgets
                .AsNoTracking()
                .Include(x => x.OwnedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);

            return entity is null ? NotFound() : Ok(MapOwnership(entity, currentUser.Id));
        }

        [HttpPost("{id:int}/ownership/claim")]
        public async Task<ActionResult<BudgetOwnershipDto>> ClaimOwnership(int id, [FromBody] ClaimBudgetOwnershipRequest? request)
        {
            var currentUser = await _legacyUserResolutionService.ResolveCurrentUserAsync(User);
            if (currentUser is null)
            {
                return CurrentUserMissing();
            }

            var entity = await _dbContext.Budgets
                .Include(x => x.OwnedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            if (IsOwnedByOther(entity, currentUser.Id) && request?.Force != true)
            {
                return Conflict(MapOwnership(entity, currentUser.Id));
            }

            entity.IsOwned = true;
            entity.OwnedByUserId = currentUser.Id;
            entity.OwnedByUserName = currentUser.Initials;
            entity.OwnedDateTime = SwedishTime.Now;
            await _dbContext.SaveChangesAsync();

            entity.OwnedByUser = currentUser;
            return Ok(MapOwnership(entity, currentUser.Id));
        }

        [HttpPost("{id:int}/ownership/release")]
        public async Task<IActionResult> ReleaseOwnership(int id)
        {
            var currentUser = await _legacyUserResolutionService.ResolveCurrentUserAsync(User);
            if (currentUser is null)
            {
                return CurrentUserMissing();
            }

            var entity = await _dbContext.Budgets.FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null)
            {
                return NotFound();
            }

            if (entity.IsOwned && entity.OwnedByUserId == currentUser.Id)
            {
                entity.IsOwned = false;
                await _dbContext.SaveChangesAsync();
            }

            return NoContent();
        }

        private ObjectResult CurrentUserMissing()
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Kunde inte identifiera inloggad användare." });
        }

        private static bool IsOwnedByOther(Budget entity, int currentUserId)
        {
            return entity.IsOwned && entity.OwnedByUserId.HasValue && entity.OwnedByUserId != currentUserId;
        }

        private static BudgetOwnershipDto MapOwnership(Budget entity, int currentUserId) => new()
        {
            IsOwned = entity.IsOwned,
            OwnedByUserId = entity.OwnedByUserId,
            OwnedByUserName = entity.OwnedByUser?.Name ?? entity.OwnedByUserName,
            OwnedDateTime = entity.OwnedDateTime,
            IsOwnedByCurrentUser = entity.IsOwned && entity.OwnedByUserId == currentUserId,
        };

        private async Task<List<BudgetListItemDto>> LoadBudgetListAsync()
        {
            return await _dbContext.Budgets
                .AsNoTracking()
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Id)
                .Select(x => new BudgetListItemDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Year = x.Year,
                    IsActive = x.IsActive,
                    IsLocked = x.IsLocked,
                })
                .ToListAsync();
        }

        private async Task<BudgetDto?> LoadBudgetDtoAsync(int id)
        {
            var entity = await _dbContext.Budgets
                .AsNoTracking()
                .Include(x => x.OwnedByUser)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return null;
            }

            var budgetCustomers = await _dbContext.BudgetCustomers
                .AsNoTracking()
                .AsSplitQuery()
                .Include(x => x.Customer)
                    .ThenInclude(x => x!.ResponsibleUser)
                .Include(x => x.Employee)
                .Include(x => x.BudgetCustomerMonths)
                .Where(x => x.BudgetId == id && !x.IsRemovedFromBudget)
                .ToListAsync();

            var customerIds = budgetCustomers
                .Where(x => x.CustomerId.HasValue)
                .Select(x => x.CustomerId!.Value)
                .Distinct()
                .ToList();

            var now = SwedishTime.Now;
            var nextAllocations = await _dbContext.CustomerEmployeeAllocations
                .AsNoTracking()
                .Where(x => x.CustomerId != null && customerIds.Contains(x.CustomerId.Value) && x.AllocateFromDate >= now)
                .ToListAsync();

            var nextEmployeeByCustomer = nextAllocations
                .GroupBy(x => x.CustomerId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderByDescending(x => x.AllocateFromDate).First().EmployeeId);

            var allocatedEmployeeIds = nextEmployeeByCustomer.Values
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var allocatedEmployeeInitials = await _dbContext.LegacyUsers
                .AsNoTracking()
                .Where(x => allocatedEmployeeIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Initials);

            var customers = budgetCustomers
                .Select(bc =>
                {
                    var responsible = bc.Customer?.ResponsibleUser ?? bc.Employee;
                    int? employeeId = responsible?.Id;
                    var employeeName = responsible?.Initials;

                    if (bc.CustomerId.HasValue &&
                        nextEmployeeByCustomer.TryGetValue(bc.CustomerId.Value, out var nextEmployeeId) &&
                        nextEmployeeId.HasValue)
                    {
                        employeeId = nextEmployeeId;
                        employeeName = allocatedEmployeeInitials.GetValueOrDefault(nextEmployeeId.Value) ?? employeeName;
                    }

                    var month = bc.BudgetCustomerMonths.OrderBy(x => x.Id).FirstOrDefault();

                    return new BudgetCustomerDto
                    {
                        Id = bc.Id,
                        CustomerId = bc.CustomerId,
                        CustomerName = bc.CustomerId.HasValue ? bc.Customer?.Name : $"Nysälj {employeeName}",
                        CustomerActive = bc.Customer?.Active ?? true,
                        BudgetCountAsNewUntilMonth = bc.Customer?.BudgetCountAsNewUntilMonth,
                        EmployeeId = employeeId,
                        EmployeeName = employeeName,
                        TotalSalesBudget = bc.TotalSalesBudget,
                        AdditionBudget = bc.AdditionBudget,
                        IsRemovedFromBudget = bc.IsRemovedFromBudget,
                        SalesMonths = month is null ? new decimal?[MonthCount] : GetSalesMonths(month),
                        AdditionMonths = month is null ? new decimal?[MonthCount] : GetAdditionMonths(month),
                    };
                })
                .OrderBy(x => x.CustomerName)
                .ToList();

            return new BudgetDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Year = entity.Year,
                IsActive = entity.IsActive,
                IsLocked = entity.IsLocked,
                DistributionMonths = GetDistributionMonths(entity),
                CompareBudgetId = entity.CompareBudgetId,
                ComparePrevYear = entity.ComparePrevYear,
                ComparePrevPrevYear = entity.ComparePrevPrevYear,
                IsOwned = entity.IsOwned,
                OwnedDateTime = entity.OwnedDateTime,
                OwnedByUserName = entity.OwnedByUser?.Name ?? entity.OwnedByUserName,
                OwnedByUserId = entity.OwnedByUserId,
                Customers = customers,
            };
        }

        private async Task<ActionResult?> ValidateAsync(SaveBudgetRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.DistributionMonths is not null && request.DistributionMonths.Length != MonthCount)
            {
                return BadRequest(new { message = "Fördelningen måste innehålla 12 månader." });
            }

            var customers = request.Customers.Where(x => x.CustomerId != -1).ToList();

            if (customers.Any(x =>
                (x.SalesMonths is not null && x.SalesMonths.Length != MonthCount) ||
                (x.AdditionMonths is not null && x.AdditionMonths.Length != MonthCount)))
            {
                return BadRequest(new { message = "Månadsvärden måste innehålla 12 månader." });
            }

            if (customers.Any(x => x.CustomerId is null && x.EmployeeId is null))
            {
                return BadRequest(new { message = "Nysäljrader måste ha en säljare." });
            }

            var customerIds = customers
                .Where(x => x.CustomerId.HasValue)
                .Select(x => x.CustomerId!.Value)
                .Distinct()
                .ToList();

            var existingCustomerCount = await _dbContext.Customers.CountAsync(x => customerIds.Contains(x.Id));
            if (existingCustomerCount != customerIds.Count)
            {
                return BadRequest(new { message = "En eller flera kunder finns inte." });
            }

            var employeeIds = customers
                .Where(x => x.CustomerId is null)
                .Select(x => x.EmployeeId!.Value)
                .Distinct()
                .ToList();

            var existingEmployeeCount = await _dbContext.LegacyUsers.CountAsync(x => employeeIds.Contains(x.Id));
            if (existingEmployeeCount != employeeIds.Count)
            {
                return BadRequest(new { message = "En eller flera säljare finns inte." });
            }

            return null;
        }

        private void SyncCustomers(Budget budget, List<BudgetCustomer> existingCustomers, List<SaveBudgetCustomerRequest> requestCustomers)
        {
            // CustomerId -1 is the calculated "Nysälj bef. kund" row and is never persisted.
            var incoming = requestCustomers.Where(x => x.CustomerId != -1).ToList();
            var existingById = existingCustomers.ToDictionary(x => x.Id);
            var keptIds = new HashSet<int>();

            foreach (var item in incoming)
            {
                BudgetCustomer entity;
                if (item.Id > 0 && existingById.TryGetValue(item.Id, out var existing))
                {
                    entity = existing;
                    keptIds.Add(entity.Id);
                    _dbContext.BudgetCustomerMonths.RemoveRange(entity.BudgetCustomerMonths);
                    entity.BudgetCustomerMonths.Clear();
                }
                else
                {
                    entity = new BudgetCustomer { Budget = budget };
                    _dbContext.BudgetCustomers.Add(entity);
                }

                entity.CustomerId = item.CustomerId;
                entity.TotalSalesBudget = item.TotalSalesBudget.HasValue ? Math.Round(item.TotalSalesBudget.Value, 0) : null;
                entity.AdditionBudget = item.AdditionBudget;
                entity.IsRemovedFromBudget = item.IsRemovedFromBudget;

                // Customer rows follow the customer's responsible user, only Nysälj rows store their employee.
                if (item.CustomerId is null)
                {
                    entity.EmployeeId = item.EmployeeId;
                }

                var month = new BudgetCustomerMonth();
                SetSalesMonths(month, item.SalesMonths);
                SetAdditionMonths(month, item.AdditionMonths);
                entity.BudgetCustomerMonths.Add(month);
            }

            foreach (var removed in existingCustomers.Where(x => !keptIds.Contains(x.Id)))
            {
                _dbContext.BudgetCustomerMonths.RemoveRange(removed.BudgetCustomerMonths);
                _dbContext.BudgetCustomers.Remove(removed);
            }
        }

        private async Task DeactivateOtherBudgetsAsync(int? excludeId)
        {
            var others = await _dbContext.Budgets
                .Where(x => x.IsActive && (excludeId == null || x.Id != excludeId))
                .ToListAsync();

            foreach (var other in others)
            {
                other.IsActive = false;
            }
        }

        private static void MapHeader(SaveBudgetRequest request, Budget entity)
        {
            entity.Name = request.Name?.Trim();
            entity.Year = request.Year;
            entity.IsActive = request.IsActive;
            entity.IsLocked = request.IsLocked;
            entity.CompareBudgetId = request.CompareBudgetId;
            entity.ComparePrevYear = request.ComparePrevYear;
            entity.ComparePrevPrevYear = request.ComparePrevPrevYear;

            var months = request.DistributionMonths ?? new decimal?[MonthCount];
            entity.DistributionMonth1 = months[0];
            entity.DistributionMonth2 = months[1];
            entity.DistributionMonth3 = months[2];
            entity.DistributionMonth4 = months[3];
            entity.DistributionMonth5 = months[4];
            entity.DistributionMonth6 = months[5];
            entity.DistributionMonth7 = months[6];
            entity.DistributionMonth8 = months[7];
            entity.DistributionMonth9 = months[8];
            entity.DistributionMonth10 = months[9];
            entity.DistributionMonth11 = months[10];
            entity.DistributionMonth12 = months[11];
        }

        private static decimal?[] GetDistributionMonths(Budget entity) =>
        [
            entity.DistributionMonth1,
            entity.DistributionMonth2,
            entity.DistributionMonth3,
            entity.DistributionMonth4,
            entity.DistributionMonth5,
            entity.DistributionMonth6,
            entity.DistributionMonth7,
            entity.DistributionMonth8,
            entity.DistributionMonth9,
            entity.DistributionMonth10,
            entity.DistributionMonth11,
            entity.DistributionMonth12,
        ];

        private static decimal?[] GetSalesMonths(BudgetCustomerMonth month) =>
        [
            month.SalesMonth1,
            month.SalesMonth2,
            month.SalesMonth3,
            month.SalesMonth4,
            month.SalesMonth5,
            month.SalesMonth6,
            month.SalesMonth7,
            month.SalesMonth8,
            month.SalesMonth9,
            month.SalesMonth10,
            month.SalesMonth11,
            month.SalesMonth12,
        ];

        private static decimal?[] GetAdditionMonths(BudgetCustomerMonth month) =>
        [
            month.AdditionMonth1,
            month.AdditionMonth2,
            month.AdditionMonth3,
            month.AdditionMonth4,
            month.AdditionMonth5,
            month.AdditionMonth6,
            month.AdditionMonth7,
            month.AdditionMonth8,
            month.AdditionMonth9,
            month.AdditionMonth10,
            month.AdditionMonth11,
            month.AdditionMonth12,
        ];

        private static void SetSalesMonths(BudgetCustomerMonth month, decimal?[]? values)
        {
            var v = values ?? new decimal?[MonthCount];
            month.SalesMonth1 = v[0];
            month.SalesMonth2 = v[1];
            month.SalesMonth3 = v[2];
            month.SalesMonth4 = v[3];
            month.SalesMonth5 = v[4];
            month.SalesMonth6 = v[5];
            month.SalesMonth7 = v[6];
            month.SalesMonth8 = v[7];
            month.SalesMonth9 = v[8];
            month.SalesMonth10 = v[9];
            month.SalesMonth11 = v[10];
            month.SalesMonth12 = v[11];
        }

        private static void SetAdditionMonths(BudgetCustomerMonth month, decimal?[]? values)
        {
            var v = values ?? new decimal?[MonthCount];
            month.AdditionMonth1 = v[0];
            month.AdditionMonth2 = v[1];
            month.AdditionMonth3 = v[2];
            month.AdditionMonth4 = v[3];
            month.AdditionMonth5 = v[4];
            month.AdditionMonth6 = v[5];
            month.AdditionMonth7 = v[6];
            month.AdditionMonth8 = v[7];
            month.AdditionMonth9 = v[8];
            month.AdditionMonth10 = v[9];
            month.AdditionMonth11 = v[10];
            month.AdditionMonth12 = v[11];
        }

        private sealed record SalesRow(
            int? CustomerId,
            string? CustomerName,
            int? EmployeeId,
            string? EmployeeName,
            DateTime Created,
            double Sales,
            double Tb);

        private sealed record SalesReportOrder(
            int? CustomerId,
            string? CustomerName,
            string? Category,
            int? EmployeeId,
            string? EmployeeName,
            DateTime Created,
            double Sales,
            double Tb,
            DateOnly? BudgetCountAsNewUntilMonth);

        private async Task<List<SalesRow>> LoadSalesRowsAsync(BudgetSalesStatRequest request)
        {
            var fromDate = new DateTime(request.Year, 1, 1);
            var toDateExclusive = GetStatEndDateExclusive(request.Year, request.Ytd);

            var query = _dbContext.CustomerOrders
                .AsNoTracking()
                .Where(x => x.Created.HasValue && x.Created.Value >= fromDate && x.Created.Value < toDateExclusive);

            var excludeIds = request.ExcludeCustomerIds.Where(x => x > 0).Distinct().ToList();
            if (excludeIds.Count > 0)
            {
                query = query.Where(x => x.CustomerId == null || !excludeIds.Contains(x.CustomerId.Value));
            }

            var employeeIds = request.EmployeeIds.Distinct().ToList();
            if (employeeIds.Count > 0)
            {
                query = query.Where(x =>
                    x.Customer != null &&
                    x.Customer.ResponsibleUserId != null &&
                    employeeIds.Contains(x.Customer.ResponsibleUserId.Value));
            }

            var raw = await query
                .Select(x => new
                {
                    x.CustomerId,
                    CustomerName = x.Customer != null ? x.Customer.Name : x.CustomerName,
                    EmployeeId = x.Customer != null ? x.Customer.ResponsibleUserId : null,
                    EmployeeName = x.Customer != null && x.Customer.ResponsibleUser != null ? x.Customer.ResponsibleUser.Initials : null,
                    Created = x.Created!.Value,
                    x.SalesPrice,
                    x.SalesCurrencyRate,
                    x.Edition,
                    x.TotalCostInSalesCurrency,
                    Multiplicator = x.Unit != null ? x.Unit.Multiplicator : null,
                    PurchasePrice = x.SupplierOrder != null ? x.SupplierOrder.PurchasePrice : null,
                    PurchaseCurrencyRate = x.SupplierOrder != null ? x.SupplierOrder.PurchaseCurrencyRate : null,
                })
                .ToListAsync();

            return raw
                .Select(x =>
                {
                    var multiplicator = x.Multiplicator is null or 0 ? 1d : x.Multiplicator.Value;
                    var edition = x.Edition ?? 0;
                    var salesRate = x.SalesCurrencyRate ?? 0;
                    var sales = (x.SalesPrice ?? 0) * salesRate * edition / multiplicator;
                    var cost = ((x.PurchasePrice ?? 0) * (x.PurchaseCurrencyRate ?? 0)
                        + (double)(x.TotalCostInSalesCurrency ?? 0) * salesRate)
                        * edition / multiplicator;

                    return new SalesRow(x.CustomerId, x.CustomerName, x.EmployeeId, x.EmployeeName, x.Created, sales, sales - cost);
                })
                .ToList();
        }

        private static DateTime GetStatEndDateExclusive(int year, bool ytd)
        {
            if (!ytd)
            {
                return new DateTime(year + 1, 1, 1);
            }

            var today = SwedishTime.Today;
            var day = Math.Min(today.Day, DateTime.DaysInMonth(year, today.Month));
            return new DateTime(year, today.Month, day).AddDays(1);
        }

        private static double CalculateAverageAddition(double totalSales, double totalTb)
        {
            return totalSales != 0 && totalSales != totalTb
                ? 100 * totalTb / (totalSales - totalTb)
                : 0;
        }

        private static decimal ToDecimal(double value)
        {
            return double.IsFinite(value) ? (decimal)Math.Round(value, 4) : 0;
        }
    }
}
