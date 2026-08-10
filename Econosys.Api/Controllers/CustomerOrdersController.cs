using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Econosys.Api.Common;
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
    public class CustomerOrdersController : ControllerBase
    {
        private static readonly Dictionary<string, PropertyInfo> FieldMap = BuildFieldMap();

        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CustomerOrdersController> _logger;
        private readonly ILegacyUserResolutionService _legacyUserResolution;

        public CustomerOrdersController(
            ApplicationDbContext dbContext,
            ILogger<CustomerOrdersController> logger,
            ILegacyUserResolutionService legacyUserResolution)
        {
            _dbContext = dbContext;
            _logger = logger;
            _legacyUserResolution = legacyUserResolution;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<CustomerOrderDto>> GetById(int id)
        {
            var customerOrder = await BuildDetailsQuery()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (customerOrder is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(customerOrder));
        }

        [HttpPost]
        public async Task<ActionResult<CustomerOrderDto>> Create([FromBody] CreateCustomerOrderRequest request)
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

            var entity = new CustomerOrder();
            ApplyCreateRequestToEntity(entity, request);

            entity.Created ??= SwedishTime.Now;
            entity.CreatedBy ??= legacyUser.Id;
            entity.Edited = SwedishTime.Now;
            entity.EditedBy = legacyUser.Id;

            _dbContext.CustomerOrders.Add(entity);
            await _dbContext.SaveChangesAsync();

            await SyncOrderCostsAsync(entity.Id, request.OrderCosts);
            await _dbContext.SaveChangesAsync();

            var saved = await BuildDetailsQuery().FirstAsync(x => x.Id == entity.Id);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(saved));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<CustomerOrderDto>> Update(int id, [FromBody] UpdateCustomerOrderRequest request)
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

            if (request.Id > 0 && request.Id != id)
            {
                return BadRequest(new { message = "Request id does not match route id." });
            }

            var entity = await _dbContext.CustomerOrders.FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null)
            {
                return NotFound();
            }

            ApplyUpdateRequestToEntity(entity, request);
            entity.Edited = SwedishTime.Now;
            entity.EditedBy = legacyUser.Id;

            await SyncOrderCostsAsync(entity.Id, request.OrderCosts);
            await _dbContext.SaveChangesAsync();

            var saved = await BuildDetailsQuery().FirstAsync(x => x.Id == entity.Id);
            return Ok(MapToDto(saved));
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<CustomerOrderDto>>> Search([FromBody] SearchCustomerOrdersRequest? request)
        {
            IQueryable<CustomerOrder> query = _dbContext.CustomerOrders
                .AsNoTracking()
                .Include(x => x.SupplierOrder);
            var pagination = request?.Pagination ?? new PaginationRequest();

            var searchTerm = request?.SearchTerm?.Trim();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var likePattern = $"%{EscapeLikePattern(searchTerm)}%";
                var hasNumericId = int.TryParse(searchTerm, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idValue);

                query = query.Where(x =>
                    (hasNumericId && x.Id == idValue) ||
                    (x.CustomerOrderNr != null && EF.Functions.Like(x.CustomerOrderNr, likePattern)) ||
                    (x.SupplierOrder != null && x.SupplierOrder.SupplierOrderNr != null && EF.Functions.Like(x.SupplierOrder.SupplierOrderNr, likePattern)) ||
                    (x.CustomerName != null && EF.Functions.Like(x.CustomerName, likePattern)) ||
                    (x.Product != null && EF.Functions.Like(x.Product, likePattern)));
            }

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
                        _logger.LogWarning(ex, "Invalid customer order search condition for field {Field}", condition.Field);
                        return BadRequest(new { message = ex.Message, field = condition.Field });
                    }
                }
            }

            IOrderedQueryable<CustomerOrder> orderedQuery;
            try
            {
                orderedQuery = ApplyOrdering(query, request?.OrderBy);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid customer order order by request");
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

            return Ok(new PagedResultDto<CustomerOrderDto>
            {
                Items = items,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        private static string EscapeLikePattern(string value)
        {
            return value
                .Replace("[", "[[")
                .Replace("%", "[%]")
                .Replace("_", "[_]");
        }

        private static Dictionary<string, PropertyInfo> BuildFieldMap()
        {
            var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in typeof(CustomerOrder).GetProperties(BindingFlags.Public | BindingFlags.Instance))
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

        private static IOrderedQueryable<CustomerOrder> ApplyOrdering(
            IQueryable<CustomerOrder> query,
            List<SortRequest>? orderBy)
        {
            if (orderBy is null || orderBy.Count == 0)
            {
                return query.OrderBy(x => x.Id);
            }

            IOrderedQueryable<CustomerOrder>? ordered = null;

            foreach (var sort in orderBy)
            {
                var isDescending = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);

                if (string.Equals(sort.Field, "supplierordernr", StringComparison.OrdinalIgnoreCase))
                {
                    ordered = ApplySort(ordered, query, x => x.SupplierOrder != null ? x.SupplierOrder.SupplierOrderNr : null, isDescending);
                    continue;
                }

                var property = ResolveProperty(sort.Field);
                ordered = ApplySort(ordered, query, property, isDescending);
            }

            return ordered ?? query.OrderBy(x => x.Id);
        }

        private static IOrderedQueryable<CustomerOrder> ApplySort(
            IOrderedQueryable<CustomerOrder>? ordered,
            IQueryable<CustomerOrder> source,
            PropertyInfo property,
            bool isDescending)
        {
            var parameter = Expression.Parameter(typeof(CustomerOrder), "x");
            var propertyExpression = Expression.Property(parameter, property);
            var lambda = Expression.Lambda(propertyExpression, parameter);

            var methodName = ordered is null
                ? (isDescending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy))
                : (isDescending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy));

            var method = typeof(Queryable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(CustomerOrder), property.PropertyType);

            var result = method.Invoke(null, new object[] { ordered ?? source, lambda });
            return (IOrderedQueryable<CustomerOrder>)result!;
        }

        private static IOrderedQueryable<CustomerOrder> ApplySort<TKey>(
            IOrderedQueryable<CustomerOrder>? ordered,
            IQueryable<CustomerOrder> source,
            Expression<Func<CustomerOrder, TKey>> keySelector,
            bool isDescending)
        {
            if (ordered is null)
            {
                return isDescending
                    ? source.OrderByDescending(keySelector)
                    : source.OrderBy(keySelector);
            }

            return isDescending
                ? ordered.ThenByDescending(keySelector)
                : ordered.ThenBy(keySelector);
        }

        private static IQueryable<CustomerOrder> ApplyCondition(IQueryable<CustomerOrder> query, FilterConditionDto condition)
        {
            var property = ResolveProperty(condition.Field);
            var op = condition.Operator.Trim().ToLowerInvariant();

            var parameter = Expression.Parameter(typeof(CustomerOrder), "x");
            var member = Expression.Property(parameter, property);
            var body = BuildConditionBody(member, property.PropertyType, condition, op);

            var lambda = Expression.Lambda<Func<CustomerOrder, bool>>(body, parameter);
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

                if (targetType == typeof(short))
                {
                    if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var intNumber) && intNumber >= short.MinValue && intNumber <= short.MaxValue)
                    {
                        return (short)intNumber;
                    }

                    if (element.ValueKind == JsonValueKind.String && short.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var shortParsed))
                    {
                        return shortParsed;
                    }
                }

                if (targetType == typeof(double))
                {
                    if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var doubleNumber))
                    {
                        return doubleNumber;
                    }

                    if (element.ValueKind == JsonValueKind.String && double.TryParse(element.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleParsed))
                    {
                        return doubleParsed;
                    }
                }

                if (targetType == typeof(decimal))
                {
                    if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var decimalNumber))
                    {
                        return decimalNumber;
                    }

                    if (element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var decimalParsed))
                    {
                        return decimalParsed;
                    }
                }

                if (targetType == typeof(bool))
                {
                    if (element.ValueKind == JsonValueKind.True)
                    {
                        return true;
                    }

                    if (element.ValueKind == JsonValueKind.False)
                    {
                        return false;
                    }

                    if (element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out var boolParsed))
                    {
                        return boolParsed;
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
                || type == typeof(short)
                || type == typeof(double)
                || type == typeof(decimal)
                || type == typeof(DateTime);
        }

        private IQueryable<CustomerOrder> BuildDetailsQuery()
        {
            return _dbContext.CustomerOrders
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Unit)
                .Include(x => x.SalesCurrency)
                .Include(x => x.SupplierOrder)
                .Include(x => x.OrderCosts)
                    .ThenInclude(x => x.InvoiceRows);
        }

        private async Task SyncOrderCostsAsync(int customerOrderId, List<UpsertCustomerOrderOrderCostRequest>? requestedCosts)
        {
            var requested = requestedCosts ?? new List<UpsertCustomerOrderOrderCostRequest>();

            var requestedIds = requested
                .Where(x => x.Id.HasValue && x.Id.Value > 0)
                .Select(x => x.Id!.Value)
                .ToHashSet();

            var existing = await _dbContext.OrderCosts
                .Where(x => x.CustomerOrderId == customerOrderId)
                .ToListAsync();

            var toDelete = existing.Where(x => !requestedIds.Contains(x.Id)).ToList();
            if (toDelete.Count > 0)
            {
                _dbContext.OrderCosts.RemoveRange(toDelete);
            }

            foreach (var req in requested)
            {
                if (req.Id.HasValue && req.Id.Value > 0)
                {
                    var entity = existing.FirstOrDefault(x => x.Id == req.Id.Value);
                    if (entity != null)
                    {
                        ApplyOrderCostRequest(entity, req);
                    }
                }
                else if (req.CostId.HasValue)
                {
                    var entity = new OrderCost { CustomerOrderId = customerOrderId };
                    ApplyOrderCostRequest(entity, req);
                    _dbContext.OrderCosts.Add(entity);
                }
            }
        }

        private static void ApplyOrderCostRequest(OrderCost entity, UpsertCustomerOrderOrderCostRequest req)
        {
            entity.CustomerOrderId = req.CustomerOrderId;
            entity.SupplierOrderId = req.SupplierOrderId;
            entity.CostId = req.CostId;
            entity.DoDebit = req.DoDebit;
            entity.NrOf = req.NrOf;
            entity.InPrice = req.InPrice;
            entity.InPriceAttested = req.InPriceAttested;
            entity.OutPrice = req.OutPrice;
            entity.Note = req.Note;
            entity.SupplierName = req.SupplierName;
            entity.DoPrintOnQuotation = req.DoPrintOnQuotation;
            entity.DoPrintOnCustomerOrder = req.DoPrintOnCustomerOrder;
            entity.DoPrintOnSupplierOrder = req.DoPrintOnSupplierOrder;
            entity.DoInvoiceSeparately = req.DoInvoiceSeparately;
            entity.DoInvoiceSeparatelyImmediately = req.DoInvoiceSeparatelyImmediately;
            entity.IsCostInvoicedSeparately = req.IsCostInvoicedSeparately;
        }

        private static CustomerOrderOrderCostDto MapOrderCostToDto(OrderCost source, double? salesCurrencyRate)
        {
            var invoiceRow = source.InvoiceRows
                .Where(x => x.InvoiceId.HasValue)
                .OrderByDescending(x => x.InvoiceId)
                .FirstOrDefault();

            return new CustomerOrderOrderCostDto
            {
                Id = source.Id,
                CustomerOrderId = source.CustomerOrderId,
                SupplierOrderId = source.SupplierOrderId,
                CostId = source.CostId,
                DoDebit = source.DoDebit,
                NrOf = source.NrOf,
                InPrice = source.InPrice,
                InPriceAttested = source.InPriceAttested,
                OutPrice = source.OutPrice,
                OutPriceSEK = source.OutPrice.HasValue && salesCurrencyRate is > 0
                    ? source.OutPrice * (decimal)salesCurrencyRate.Value
                    : source.OutPrice,
                Markup = source.InPrice.HasValue && source.OutPrice.HasValue && source.InPrice != 0
                    ? Math.Round((source.OutPrice.Value - source.InPrice.Value) / source.InPrice.Value * 100, 2)
                    : null,
                Note = source.Note,
                SupplierName = source.SupplierName,
                DoPrintOnQuotation = source.DoPrintOnQuotation,
                DoPrintOnCustomerOrder = source.DoPrintOnCustomerOrder,
                DoPrintOnSupplierOrder = source.DoPrintOnSupplierOrder,
                DoInvoiceSeparately = source.DoInvoiceSeparately,
                DoInvoiceSeparatelyImmediately = source.DoInvoiceSeparatelyImmediately,
                IsCostInvoicedSeparately = source.IsCostInvoicedSeparately,
                InvoiceId = invoiceRow?.InvoiceId,
                InvoiceRowId = invoiceRow?.Id,
            };
        }

        private static void ApplyCreateRequestToEntity(CustomerOrder entity, CreateCustomerOrderRequest request)
        {
            entity.QuotationId = request.QuotationId;
            entity.SupplierOrderId = request.SupplierOrderId;
            entity.CustomerOrderNr = request.CustomerOrderNr;
            entity.CustomerId = request.CustomerId;
            entity.CustomerName = request.CustomerName;
            entity.DeliveryAddressName = request.DeliveryAddressName;
            entity.DeliveryAddress = request.DeliveryAddress;
            entity.DeliveryPostalNr = request.DeliveryPostalNr;
            entity.DeliveryPostalAddress = request.DeliveryPostalAddress;
            entity.DeliveryCountry = request.DeliveryCountry;
            entity.Date = request.Date;
            entity.TimeOfDelivery = request.TimeOfDelivery;
            entity.YourReference = request.YourReference;
            entity.OurReference = request.OurReference;
            entity.TermsOfDelivery = request.TermsOfDelivery;
            entity.TermsOfPayment = request.TermsOfPayment;
            entity.Message = request.Message;
            entity.Product = request.Product;
            entity.Material = request.Material;
            entity.Format = request.Format;
            entity.Color = request.Color;
            entity.Construction = request.Construction;
            entity.IsFSC = request.IsFSC;
            entity.SalesCurrencyId = request.SalesCurrencyId;
            entity.SalesCurrencyRate = request.SalesCurrencyRate;
            entity.UnitId = request.UnitId;
            entity.Edition = request.Edition;
            entity.SalesPrice = request.SalesPrice;
            entity.PalletFormatId = request.PalletFormatId;
            entity.EurPallet = request.EurPallet;
        }

        private static void ApplyUpdateRequestToEntity(CustomerOrder entity, UpdateCustomerOrderRequest request)
        {
            entity.CustomerOrderNr = request.CustomerOrderNr;
            entity.DeliveryAddressName = request.DeliveryAddressName;
            entity.DeliveryAddress = request.DeliveryAddress;
            entity.DeliveryPostalNr = request.DeliveryPostalNr;
            entity.DeliveryPostalAddress = request.DeliveryPostalAddress;
            entity.DeliveryCountry = request.DeliveryCountry;
            entity.Date = request.Date;
            entity.TimeOfDelivery = request.TimeOfDelivery;
            entity.YourReference = request.YourReference;
            entity.OurReference = request.OurReference;
            entity.TermsOfDelivery = request.TermsOfDelivery;
            entity.TermsOfPayment = request.TermsOfPayment;
            entity.Message = request.Message;
            entity.Product = request.Product;
            entity.Material = request.Material;
            entity.Format = request.Format;
            entity.Color = request.Color;
            entity.Construction = request.Construction;
            entity.IsFSC = request.IsFSC;
            entity.SalesCurrencyId = request.SalesCurrencyId;
            entity.SalesCurrencyRate = request.SalesCurrencyRate;
            entity.UnitId = request.UnitId;
            entity.Edition = request.Edition;
            entity.SalesPrice = request.SalesPrice;
            entity.PalletFormatId = request.PalletFormatId;
            entity.EurPallet = request.EurPallet;
        }

        private static CustomerOrderDto MapToDto(CustomerOrder source)
        {
            return new CustomerOrderDto
            {
                Id = source.Id,
                QuotationId = source.QuotationId,
                SupplierOrderId = source.SupplierOrderId,
                SupplierOrderNr = source.SupplierOrder?.SupplierOrderNr,
                CustomerOrderNr = source.CustomerOrderNr,
                CustomerId = source.CustomerId,
                CustomerName = source.CustomerName ?? source.Customer?.Name,
                Address = source.Address,
                PostalNr = source.PostalNr,
                PostalAddress = source.PostalAddress,
                Country = source.Country,
                DeliveryAddressName = source.DeliveryAddressName,
                DeliveryAddress = source.DeliveryAddress,
                DeliveryPostalNr = source.DeliveryPostalNr,
                DeliveryPostalAddress = source.DeliveryPostalAddress,
                DeliveryCountry = source.DeliveryCountry,
                Date = source.Date,
                YourReference = source.YourReference,
                OurReference = source.OurReference,
                Product = source.Product,
                Material = source.Material,
                MaterialThickness = source.MaterialThickness,
                Format = source.Format,
                Color = source.Color,
                Construction = source.Construction,
                TimeOfDelivery = source.TimeOfDelivery,
                TermsOfDelivery = source.TermsOfDelivery,
                TermsOfPayment = source.TermsOfPayment,
                PaymentDays = source.PaymentDays,
                Message = source.Message,
                GoodsMarking = source.GoodsMarking,
                SalesCurrencyId = source.SalesCurrencyId,
                SalesCurrencyName = source.SalesCurrency?.Name,
                UnitId = source.UnitId,
                UnitName = source.Unit?.Name,
                SelectedCalculationRowId = source.SelectedCalculationRowId,
                Edition = source.Edition,
                SalesPrice = source.SalesPrice,
                OrderConfirmationPrinted = source.OrderConfirmationPrinted,
                PartOfPrintEdition = source.PartOfPrintEdition,
                PartOfPrintEditionValue = source.PartOfPrintEditionValue,
                PartOfPunchEdition = source.PartOfPunchEdition,
                PartOfPunchEditionValue = source.PartOfPunchEditionValue,
                ChangeOfSheet = source.ChangeOfSheet,
                NrOfChangeOfSheet = source.NrOfChangeOfSheet,
                ChangeOfSheetValue = source.ChangeOfSheetValue,
                ChangeOfCliche = source.ChangeOfCliche,
                NrOfChangeOfCliche = source.NrOfChangeOfCliche,
                ChangeOfClichemValue = source.ChangeOfClichemValue,
                ChangeOfColor = source.ChangeOfColor,
                NrOfChangeOfColor = source.NrOfChangeOfColor,
                ChangeOfColorValue = source.ChangeOfColorValue,
                PMSColor = source.PMSColor,
                PMSColorValue = source.PMSColorValue,
                EurPallet = source.EurPallet,
                NrOfEurPallet = source.NrOfEurPallet,
                EurPalletValue = source.EurPalletValue,
                OtherCost = source.OtherCost,
                OtherCostDescription = source.OtherCostDescription,
                OtherCostValue = source.OtherCostValue,
                Created = source.Created,
                Edited = source.Edited,
                CreatedBy = source.CreatedBy,
                CreatedByUserName = null,
                EditedBy = source.EditedBy,
                EditedByUserName = null,
                YourOrderNr = source.YourOrderNr,
                Completed = source.Completed,
                ProductMessage = source.ProductMessage,
                SalesCurrencyRate = source.SalesCurrencyRate,
                Customer2 = source.Customer2,
                Address2 = source.Address2,
                OldDbId = source.OldDbId,
                ResponsibleUserId = source.ResponsibleUserId,
                CompletedDate = source.CompletedDate,
                HasFreightCost = source.HasFreightCost,
                FreightCost = source.FreightCost,
                IsCallOff = source.IsCallOff,
                IsReadyForLoading = source.IsReadyForLoading,
                DeliveryDate = source.DeliveryDate,
                DeliveryDateWeekMode = source.DeliveryDateWeekMode,
                LogisticsInfoInternal = source.LogisticsInfoInternal,
                CustomerDeliveryAddressId = source.CustomerDeliveryAddressId,
                SupplierPricePerEurPallet = source.SupplierPricePerEurPallet,
                SupplierPricePerEurPalletCurrencyId = source.SupplierPricePerEurPalletCurrencyId,
                SupplierPricePerEurPalletCurrencyRate = source.SupplierPricePerEurPalletCurrencyRate,
                PalletFormatId = source.PalletFormatId,
                IsFSC = source.IsFSC,
                TotalCostInSalesCurrency = source.TotalCostInSalesCurrency,
                InvoicingInfo = source.InvoicingInfo,
                CalculationId = source.CalculationId,
                OrderCosts = source.OrderCosts
                    .OrderBy(x => x.Id)
                    .Select(x => MapOrderCostToDto(x, source.SalesCurrencyRate))
                    .ToList(),
            };
        }
    }
}
