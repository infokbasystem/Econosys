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
    public class StockTakingsController : ControllerBase
    {
        private static readonly Dictionary<string, PropertyInfo> FieldMap = BuildFieldMap();

        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<StockTakingsController> _logger;

        public StockTakingsController(ApplicationDbContext dbContext, ILogger<StockTakingsController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<StockTakingDto>> GetById(int id)
        {
            var stockTaking = await _dbContext.StockTakings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (stockTaking is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(stockTaking));
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<StockTakingDto>>> Search([FromBody] SearchStockTakingsRequest? request)
        {
            IQueryable<StockTaking> query = _dbContext.StockTakings.AsNoTracking();
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
                        _logger.LogWarning(ex, "Invalid stock taking search condition for field {Field}", condition.Field);
                        return BadRequest(new { message = ex.Message, field = condition.Field });
                    }
                }
            }

            IOrderedQueryable<StockTaking> orderedQuery;
            try
            {
                orderedQuery = ApplyOrdering(query, request?.OrderBy);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid stock taking order by request");
                return BadRequest(new { message = ex.Message });
            }

            var totalCount = await query.CountAsync();
            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pagination.PageSize);

            var items = (await orderedQuery
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync())
                .Select(MapToDto)
                .ToList();

            return Ok(new PagedResultDto<StockTakingDto>
            {
                Items = items,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        [HttpGet("history")]
        public async Task<ActionResult<List<StockTakingHistoryDto>>> GetHistory()
        {
            var stockTakings = await _dbContext.StockTakings
                .AsNoTracking()
                .OrderByDescending(x => x.StockTakingDate)
                .ThenByDescending(x => x.Id)
                .Select(x => new StockTakingHistoryDto
                {
                    Id = x.Id,
                    StockTakingDate = x.StockTakingDate
                })
                .ToListAsync();

            if (stockTakings.Count == 0)
            {
                return Ok(stockTakings);
            }

            var stockTakingIds = stockTakings.Select(x => x.Id).ToList();
            var itemRows = await _dbContext.StockTakingItems
                .AsNoTracking()
                .Where(x => x.StockTakingId.HasValue && stockTakingIds.Contains(x.StockTakingId.Value))
                .Select(x => new
                {
                    StockTakingId = x.StockTakingId!.Value,
                    InventoryName = x.SupplierOrder != null && x.SupplierOrder.Inventory != null
                        ? x.SupplierOrder.Inventory.Name
                        : null
                })
                .ToListAsync();

            var rowsByStockTaking = itemRows.GroupBy(x => x.StockTakingId).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var stockTaking in stockTakings)
            {
                if (!rowsByStockTaking.TryGetValue(stockTaking.Id, out var rows))
                {
                    continue;
                }

                stockTaking.ItemCount = rows.Count;
                stockTaking.InventoryNames = string.Join(
                    ", ",
                    rows.Select(x => x.InventoryName)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase));
            }

            return Ok(stockTakings);
        }

        [HttpGet("{id:int}/aggregate")]
        public async Task<ActionResult<StockTakingAggregateDto>> GetAggregate(int id)
        {
            var stockTaking = await BuildAggregateDtoAsync(id);
            return stockTaking is null ? NotFound() : Ok(stockTaking);
        }

        [HttpPost("calculate")]
        public async Task<ActionResult<StockTakingDraftDto>> Calculate([FromBody] CalculateStockTakingRequestDto request)
        {
            var stockTakingDate = NormalizeStockTakingDate(request.StockTakingDate!.Value);
            if (request.InventoryId != 0 && !await _dbContext.Inventories.AnyAsync(x => x.Id == request.InventoryId && x.IsInventory))
            {
                return BadRequest(new { message = "Valt lager finns inte eller är inte ett lager." });
            }

            var items = await CalculateStockTakingItemsAsync(stockTakingDate, request.InventoryId);
            return Ok(new StockTakingDraftDto
            {
                StockTakingDate = stockTakingDate,
                InventoryId = request.InventoryId,
                Items = items
            });
        }

        [HttpPost]
        public async Task<ActionResult<StockTakingAggregateDto>> Create([FromBody] CreateStockTakingRequestDto request)
        {
            var stockTakingDate = NormalizeStockTakingDate(request.StockTakingDate!.Value);
            var countedItems = request.Items
                .Where(x => x.NrOfItems.HasValue || x.NrOfPallets.HasValue)
                .ToList();

            if (countedItems.Count == 0)
            {
                return BadRequest(new { message = "Ange minst ett lagervärde innan inventeringen sparas." });
            }

            if (countedItems.Select(x => x.SupplierOrderId).Distinct().Count() != countedItems.Count)
            {
                return BadRequest(new { message = "Samma leverantörsorder kan inte förekomma flera gånger." });
            }

            var calculatedItems = await CalculateStockTakingItemsAsync(stockTakingDate, 0);
            var calculatedBySupplierOrder = calculatedItems.ToDictionary(x => x.SupplierOrderId);
            if (countedItems.Any(x => !calculatedBySupplierOrder.ContainsKey(x.SupplierOrderId)))
            {
                return BadRequest(new { message = "En eller flera rader tillhör inte längre den aktuella inventeringen." });
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            var stockTaking = new StockTaking
            {
                CompanyId = 1,
                StockTakingDate = stockTakingDate
            };

            _dbContext.StockTakings.Add(stockTaking);
            await _dbContext.SaveChangesAsync();

            var stockTakingItems = countedItems.Select(item =>
            {
                var calculated = calculatedBySupplierOrder[item.SupplierOrderId];
                return new StockTakingItem
                {
                    CompanyId = 1,
                    StockTakingId = stockTaking.Id,
                    SupplierOrderId = item.SupplierOrderId,
                    NrOfItems = item.NrOfItems,
                    NrOfPallets = item.NrOfPallets,
                    DiffNrOfItems = item.NrOfItems.HasValue ? item.NrOfItems.Value - calculated.CalculatedNrOfItems!.Value : null,
                    DiffNrOfPallets = item.NrOfPallets.HasValue ? item.NrOfPallets.Value - calculated.CalculatedNrOfPallets!.Value : null
                };
            }).ToList();

            _dbContext.StockTakingItems.AddRange(stockTakingItems);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            var result = await BuildAggregateDtoAsync(stockTaking.Id);
            return CreatedAtAction(nameof(GetAggregate), new { id = stockTaking.Id }, result);
        }

        private async Task<StockTakingAggregateDto?> BuildAggregateDtoAsync(int id)
        {
            var stockTaking = await _dbContext.StockTakings
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new { x.Id, x.StockTakingDate })
                .FirstOrDefaultAsync();

            if (stockTaking is null)
            {
                return null;
            }

            var items = await _dbContext.StockTakingItems
                .AsNoTracking()
                .Where(x => x.StockTakingId == id)
                .OrderBy(x => x.SupplierOrder != null ? x.SupplierOrder.SupplierOrderNr : null)
                .ThenBy(x => x.Id)
                .Select(x => new StockTakingLineDto
                {
                    Id = x.Id,
                    SupplierOrderId = x.SupplierOrderId ?? 0,
                    SupplierOrderNr = x.SupplierOrder != null ? x.SupplierOrder.SupplierOrderNr ?? string.Empty : string.Empty,
                    ProductName = x.SupplierOrder != null ? x.SupplierOrder.Product ?? string.Empty : string.Empty,
                    CustomerName = x.SupplierOrder != null && x.SupplierOrder.Customer != null
                        ? x.SupplierOrder.Customer.Name ?? string.Empty
                        : string.Empty,
                    Edition = x.SupplierOrder != null ? x.SupplierOrder.Edition : null,
                    InventoryName = x.SupplierOrder != null && x.SupplierOrder.Inventory != null
                        ? x.SupplierOrder.Inventory.Name ?? string.Empty
                        : string.Empty,
                    CalculatedNrOfItems = 0,
                    CalculatedNrOfPallets = 0,
                    NrOfItems = x.NrOfItems,
                    NrOfPallets = x.NrOfPallets,
                    DiffNrOfItems = x.DiffNrOfItems,
                    DiffNrOfPallets = x.DiffNrOfPallets
                })
                .ToListAsync();

            return new StockTakingAggregateDto
            {
                Id = stockTaking.Id,
                StockTakingDate = stockTaking.StockTakingDate,
                InventoryNames = string.Join(
                    ", ",
                    items.Select(x => x.InventoryName)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)),
                Items = items
            };
        }

        private async Task<List<StockTakingLineDto>> CalculateStockTakingItemsAsync(DateTime stockTakingDate, int inventoryId)
        {
            var orders = await _dbContext.SupplierOrders
                .AsNoTracking()
                .Where(order =>
                    (!order.CustomerOrders.Any() || order.CustomerOrders.Any(customerOrder => !customerOrder.Completed))
                    && order.DeliveryToStocks.Sum(delivery => delivery.NrOfItems ?? 0d) > 0
                    && (inventoryId == 0 || order.InventoryId == inventoryId))
                .Select(order => new
                {
                    SupplierOrderId = order.Id,
                    SupplierOrderNr = order.SupplierOrderNr,
                    ProductName = order.Product,
                    Edition = order.Edition,
                    CustomerName = order.Customer != null ? order.Customer.Name : null,
                    InventoryName = order.Inventory != null ? order.Inventory.Name : null
                })
                .ToListAsync();

            if (orders.Count == 0)
            {
                return new List<StockTakingLineDto>();
            }

            var supplierOrderIds = orders.Select(x => x.SupplierOrderId).ToList();
            var snapshotRows = await _dbContext.StockTakingItems
                .AsNoTracking()
                .Where(item => item.SupplierOrderId.HasValue
                    && supplierOrderIds.Contains(item.SupplierOrderId.Value)
                    && item.StockTaking != null
                    && item.StockTaking.StockTakingDate < stockTakingDate)
                .Select(item => new
                {
                    SupplierOrderId = item.SupplierOrderId!.Value,
                    item.Id,
                    StockTakingDate = item.StockTaking!.StockTakingDate!.Value,
                    item.NrOfItems,
                    item.NrOfPallets
                })
                .ToListAsync();

            var latestSnapshotByOrder = snapshotRows
                .GroupBy(x => x.SupplierOrderId)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderByDescending(x => x.StockTakingDate).ThenByDescending(x => x.Id).First());

            var deliveredToRows = await _dbContext.DeliveryToStocks
                .AsNoTracking()
                .Where(delivery => delivery.SupplierOrderId.HasValue
                    && supplierOrderIds.Contains(delivery.SupplierOrderId.Value)
                    && delivery.DeliveryStatus == (int)DeliveryStatus.Delivered
                    && delivery.DeliveryDate.HasValue
                    && delivery.DeliveryDate.Value < stockTakingDate)
                .Select(delivery => new
                {
                    SupplierOrderId = delivery.SupplierOrderId!.Value,
                    DeliveryDate = delivery.DeliveryDate!.Value,
                    delivery.NrOfItems,
                    delivery.NrOfPallets
                })
                .ToListAsync();

            var deliveredFromRows = await (
                from delivery in _dbContext.DeliveryFromStocks.AsNoTracking()
                join customerOrder in _dbContext.CustomerOrders.AsNoTracking()
                    on delivery.CustomerOrderId equals customerOrder.Id
                where customerOrder.SupplierOrderId.HasValue
                    && supplierOrderIds.Contains(customerOrder.SupplierOrderId.Value)
                    && delivery.DeliveryStatus == (int)DeliveryStatus.Delivered
                    && delivery.DeliveryDate.HasValue
                    && delivery.DeliveryDate.Value < stockTakingDate
                select new
                {
                    SupplierOrderId = customerOrder.SupplierOrderId!.Value,
                    DeliveryDate = delivery.DeliveryDate!.Value,
                    delivery.NrOfItems,
                    delivery.NrOfPallets
                })
                .ToListAsync();

            var deliveredToByOrder = deliveredToRows.GroupBy(x => x.SupplierOrderId).ToDictionary(x => x.Key, x => x.ToList());
            var deliveredFromByOrder = deliveredFromRows.GroupBy(x => x.SupplierOrderId).ToDictionary(x => x.Key, x => x.ToList());
            var result = new List<StockTakingLineDto>(orders.Count);

            foreach (var order in orders)
            {
                latestSnapshotByOrder.TryGetValue(order.SupplierOrderId, out var snapshot);
                deliveredToByOrder.TryGetValue(order.SupplierOrderId, out var deliveredTo);
                deliveredFromByOrder.TryGetValue(order.SupplierOrderId, out var deliveredFrom);

                var deliveredItemsToStock = deliveredTo?
                    .Where(x => snapshot is null || x.DeliveryDate > snapshot.StockTakingDate)
                    .Sum(x => x.NrOfItems ?? 0d) ?? 0d;
                var deliveredItemsFromStock = deliveredFrom?
                    .Where(x => snapshot is null || x.DeliveryDate > snapshot.StockTakingDate)
                    .Sum(x => x.NrOfItems ?? 0d) ?? 0d;
                var deliveredPalletsToStock = deliveredTo?
                    .Where(x => snapshot is null || x.DeliveryDate > snapshot.StockTakingDate)
                    .Sum(x => x.NrOfPallets ?? 0) ?? 0;
                var deliveredPalletsFromStock = deliveredFrom?
                    .Where(x => snapshot is null || x.DeliveryDate > snapshot.StockTakingDate)
                    .Sum(x => x.NrOfPallets ?? 0) ?? 0;

                var calculatedItems = Convert.ToInt32((snapshot?.NrOfItems ?? 0) + deliveredItemsToStock - deliveredItemsFromStock);
                var calculatedPallets = (snapshot?.NrOfPallets ?? 0) + deliveredPalletsToStock - deliveredPalletsFromStock;
                if (calculatedItems <= 0 && calculatedPallets <= 0)
                {
                    continue;
                }

                result.Add(new StockTakingLineDto
                {
                    SupplierOrderId = order.SupplierOrderId,
                    SupplierOrderNr = order.SupplierOrderNr ?? string.Empty,
                    ProductName = order.ProductName ?? string.Empty,
                    CustomerName = order.CustomerName ?? string.Empty,
                    Edition = order.Edition,
                    InventoryName = order.InventoryName ?? string.Empty,
                    CalculatedNrOfItems = calculatedItems,
                    CalculatedNrOfPallets = calculatedPallets,
                    LastStockTakingDate = snapshot?.StockTakingDate
                });
            }

            return result.OrderBy(x => x.SupplierOrderNr, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static DateTime NormalizeStockTakingDate(DateTime value)
        {
            return DateTime.SpecifyKind(value.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Unspecified);
        }

        private static Dictionary<string, PropertyInfo> BuildFieldMap()
        {
            var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in typeof(StockTaking).GetProperties(BindingFlags.Public | BindingFlags.Instance))
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

        private static IOrderedQueryable<StockTaking> ApplyOrdering(
            IQueryable<StockTaking> query,
            List<SortRequest>? orderBy)
        {
            if (orderBy is null || orderBy.Count == 0)
            {
                return query.OrderBy(x => x.Id);
            }

            IOrderedQueryable<StockTaking>? ordered = null;

            foreach (var sort in orderBy)
            {
                var property = ResolveProperty(sort.Field);
                var isDescending = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
                ordered = ApplySort(ordered, query, property, isDescending);
            }

            return ordered ?? query.OrderBy(x => x.Id);
        }

        private static IOrderedQueryable<StockTaking> ApplySort(
            IOrderedQueryable<StockTaking>? ordered,
            IQueryable<StockTaking> source,
            PropertyInfo property,
            bool isDescending)
        {
            var parameter = Expression.Parameter(typeof(StockTaking), "x");
            var propertyExpression = Expression.Property(parameter, property);
            var lambda = Expression.Lambda(propertyExpression, parameter);

            var methodName = ordered is null
                ? (isDescending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy))
                : (isDescending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy));

            var method = typeof(Queryable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(StockTaking), property.PropertyType);

            var result = method.Invoke(null, new object[] { ordered ?? source, lambda });
            return (IOrderedQueryable<StockTaking>)result!;
        }

        private static IQueryable<StockTaking> ApplyCondition(IQueryable<StockTaking> query, FilterConditionDto condition)
        {
            var property = ResolveProperty(condition.Field);
            var op = condition.Operator.Trim().ToLowerInvariant();

            var parameter = Expression.Parameter(typeof(StockTaking), "x");
            var member = Expression.Property(parameter, property);
            var body = BuildConditionBody(member, property.PropertyType, condition, op);

            var lambda = Expression.Lambda<Func<StockTaking, bool>>(body, parameter);
            return query.Where(lambda);
        }

        private static Expression BuildConditionBody(
            MemberExpression member,
            Type memberType,
            FilterConditionDto condition,
            string op)
        {
            var underlyingType = Nullable.GetUnderlyingType(memberType) ?? memberType;
            var supportsNull = !memberType.IsValueType || Nullable.GetUnderlyingType(memberType) is not null;

            if (op == "isnull")
            {
                if (!supportsNull)
                {
                    throw new ArgumentException($"Operator '{condition.Operator}' is not valid for non-nullable field '{condition.Field}'.");
                }

                return Expression.Equal(member, Expression.Constant(null, memberType));
            }

            if (op == "isnotnull")
            {
                if (!supportsNull)
                {
                    throw new ArgumentException($"Operator '{condition.Operator}' is not valid for non-nullable field '{condition.Field}'.");
                }

                return Expression.NotEqual(member, Expression.Constant(null, memberType));
            }

            if (underlyingType == typeof(string))
            {
                return BuildStringCondition(member, condition, op);
            }

            if (underlyingType == typeof(bool))
            {
                var value = GetRequiredSingleValue(condition, underlyingType);
                return BuildComparableCondition(member, memberType, value, condition, op, allowRange: false);
            }

            if (IsSupportedComparableType(underlyingType))
            {
                if (op == "in")
                {
                    return BuildInCondition(member, memberType, underlyingType, condition);
                }

                var value = GetRequiredSingleValue(condition, underlyingType);
                return BuildComparableCondition(member, memberType, value, condition, op, allowRange: true);
            }

            throw new ArgumentException($"Unsupported field type for '{condition.Field}'.");
        }

        private static Expression BuildStringCondition(MemberExpression member, FilterConditionDto condition, string op)
        {
            var value = GetSingleStringValue(condition);
            var nullConstant = Expression.Constant(null, typeof(string));

            return op switch
            {
                "eq" => Expression.Equal(member, Expression.Constant(value, typeof(string))),
                "neq" => Expression.NotEqual(member, Expression.Constant(value, typeof(string))),
                "contains" when value is not null => BuildStringMethodCall(member, value, nameof(string.Contains)),
                "startswith" when value is not null => BuildStringMethodCall(member, value, nameof(string.StartsWith)),
                "endswith" when value is not null => BuildStringMethodCall(member, value, nameof(string.EndsWith)),
                "in" => BuildStringInCondition(member, GetStringValues(condition)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for string field '{condition.Field}'.")
            };

            Expression BuildStringMethodCall(Expression left, string right, string methodName)
            {
                var method = typeof(string).GetMethod(methodName, new[] { typeof(string) })!;
                return Expression.AndAlso(
                    Expression.NotEqual(left, nullConstant),
                    Expression.Call(left, method, Expression.Constant(right)));
            }
        }

        private static Expression BuildStringInCondition(MemberExpression member, List<string> values)
        {
            if (values.Count == 0)
            {
                throw new ArgumentException("Operator 'in' requires at least one value.");
            }

            var nullConstant = Expression.Constant(null, typeof(string));
            var containsMethod = typeof(List<string>).GetMethod(nameof(List<string>.Contains), new[] { typeof(string) })!;

            return Expression.AndAlso(
                Expression.NotEqual(member, nullConstant),
                Expression.Call(Expression.Constant(values), containsMethod, member));
        }

        private static Expression BuildInCondition(
            MemberExpression member,
            Type memberType,
            Type underlyingType,
            FilterConditionDto condition)
        {
            var values = GetValues(condition)
                .Select(value => ConvertJsonValue(value, underlyingType, condition.Field))
                .ToList();

            if (values.Count == 0)
            {
                throw new ArgumentException($"Operator 'in' for field '{condition.Field}' requires values.");
            }

            var listType = typeof(List<>).MakeGenericType(underlyingType);
            var list = Activator.CreateInstance(listType)!;
            var addMethod = listType.GetMethod("Add")!;

            foreach (var value in values)
            {
                addMethod.Invoke(list, new[] { value });
            }

            var containsMethod = listType.GetMethod("Contains", new[] { underlyingType })!;

            if (Nullable.GetUnderlyingType(memberType) is not null)
            {
                var hasValue = Expression.Property(member, nameof(Nullable<int>.HasValue));
                var memberValue = Expression.Property(member, nameof(Nullable<int>.Value));
                return Expression.AndAlso(
                    hasValue,
                    Expression.Call(Expression.Constant(list), containsMethod, memberValue));
            }

            return Expression.Call(Expression.Constant(list), containsMethod, member);
        }

        private static Expression BuildComparableCondition(
            MemberExpression member,
            Type memberType,
            object value,
            FilterConditionDto condition,
            string op,
            bool allowRange)
        {
            var constant = BuildConstant(memberType, value);

            if (Nullable.GetUnderlyingType(memberType) is not null && op is "gt" or "gte" or "lt" or "lte")
            {
                var hasValue = Expression.Property(member, nameof(Nullable<int>.HasValue));
                var memberValue = Expression.Property(member, nameof(Nullable<int>.Value));
                var valueConstant = Expression.Constant(value, value.GetType());
                var comparison = BuildComparison(memberValue, valueConstant, condition, op, allowRange);
                return Expression.AndAlso(hasValue, comparison);
            }

            return BuildComparison(member, constant, condition, op, allowRange);
        }

        private static Expression BuildComparison(
            Expression left,
            Expression right,
            FilterConditionDto condition,
            string op,
            bool allowRange)
        {
            return op switch
            {
                "eq" => Expression.Equal(left, right),
                "neq" => Expression.NotEqual(left, right),
                "gt" when allowRange => Expression.GreaterThan(left, right),
                "gte" when allowRange => Expression.GreaterThanOrEqual(left, right),
                "lt" when allowRange => Expression.LessThan(left, right),
                "lte" when allowRange => Expression.LessThanOrEqual(left, right),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for field '{condition.Field}'.")
            };
        }

        private static Expression BuildConstant(Type targetType, object value)
        {
            var nonNullableTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;
            var constant = Expression.Constant(value, nonNullableTarget);

            if (constant.Type == targetType)
            {
                return constant;
            }

            return Expression.Convert(constant, targetType);
        }

        private static object GetRequiredSingleValue(FilterConditionDto condition, Type targetType)
        {
            if (!condition.Value.HasValue)
            {
                throw new ArgumentException($"Filter field '{condition.Field}' requires a value.");
            }

            return ConvertJsonValue(condition.Value.Value, targetType, condition.Field)
                ?? throw new ArgumentException($"Filter field '{condition.Field}' requires a non-null value.");
        }

        private static string? GetSingleStringValue(FilterConditionDto condition)
        {
            if (!condition.Value.HasValue)
            {
                return null;
            }

            var value = condition.Value.Value;
            return value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
                JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
                _ => throw new ArgumentException($"Filter field '{condition.Field}' requires a simple value.")
            };
        }

        private static List<string> GetStringValues(FilterConditionDto condition)
        {
            return GetValues(condition)
                .Select(value => value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Number => value.GetRawText(),
                    JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
                    JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
                    JsonValueKind.Null => null,
                    _ => throw new ArgumentException($"Filter field '{condition.Field}' requires a flat array of values.")
                })
                .Where(value => value is not null)
                .Cast<string>()
                .ToList();
        }

        private static IEnumerable<JsonElement> GetValues(FilterConditionDto condition)
        {
            if (condition.Values is { Count: > 0 })
            {
                return condition.Values;
            }

            if (condition.Value.HasValue && condition.Value.Value.ValueKind == JsonValueKind.Array)
            {
                return condition.Value.Value.EnumerateArray().ToList();
            }

            return Array.Empty<JsonElement>();
        }

        private static object? ConvertJsonValue(JsonElement element, Type targetType, string field)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            try
            {
                if (targetType == typeof(string))
                {
                    return element.ValueKind switch
                    {
                        JsonValueKind.String => element.GetString(),
                        JsonValueKind.Number => element.GetRawText(),
                        JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
                        JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
                        _ => throw new ArgumentException()
                    };
                }

                if (targetType == typeof(int))
                {
                    if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var intNumber))
                    {
                        return intNumber;
                    }

                    if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var intParsed))
                    {
                        return intParsed;
                    }
                }

                if (targetType == typeof(DateTime))
                {
                    if (element.ValueKind == JsonValueKind.String && DateTime.TryParse(element.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime))
                    {
                        return dateTime;
                    }
                }
            }
            catch (Exception)
            {
                throw new ArgumentException($"Filter field '{field}' has an invalid value.");
            }

            throw new ArgumentException($"Filter field '{field}' has an invalid value.");
        }

        private static bool IsSupportedComparableType(Type type)
        {
            return type == typeof(int)
                || type == typeof(DateTime);
        }

        private static StockTakingDto MapToDto(StockTaking source)
        {
            return new StockTakingDto
            {
                Id = source.Id,
                StockTakingDate = source.StockTakingDate,
                OldDbId = source.OldDbId
            };
        }
    }
}
