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
    public class OrderCostsController : ControllerBase
    {
        private static readonly Dictionary<string, PropertyInfo> FieldMap = BuildFieldMap();

        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<OrderCostsController> _logger;

        public OrderCostsController(ApplicationDbContext dbContext, ILogger<OrderCostsController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<OrderCostDto>> GetById(int id)
        {
            var orderCost = await _dbContext.OrderCosts
                .AsNoTracking()
                .Include(x => x.SupplierOrder)
                .Include(x => x.CustomerOrder)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (orderCost is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(orderCost));
        }

        [HttpPost]
        public async Task<ActionResult<OrderCostDto>> Create([FromBody] CreateOrderCostRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var orderCost = new OrderCost
            {
                QuotationId = request.QuotationId,
                QuotationRowId = request.QuotationRowId,
                CalculationRowId = request.CalculationRowId,
                CustomerOrderId = request.CustomerOrderId,
                SupplierOrderId = request.SupplierOrderId,
                CostId = request.CostId,
                NrOf = request.NrOf,
                InPrice = request.InPrice,
                InPriceCurrencyId = request.InPriceCurrencyId,
                OutPrice = request.OutPrice,
                DoDebit = request.DoDebit,
                Note = request.Note,
                SupplierId = request.SupplierId,
                DoPrintOnCustomerOrder = request.DoPrintOnCustomerOrder,
                DoPrintOnSupplierOrder = request.DoPrintOnSupplierOrder,
                DoPrintOnQuotation = request.DoPrintOnQuotation,
                SupplierName = request.SupplierName,
                InPriceAttested = request.InPriceAttested,
                CreatedById = request.CreatedById,
                CreatedDateTime = request.CreatedDateTime,
                EditedById = request.EditedById,
                EditedDateTime = request.EditedDateTime,
                AttestedInPriceCurrencyRate = request.AttestedInPriceCurrencyRate,
                AttestedOutPriceCurrencyRate = request.AttestedOutPriceCurrencyRate,
                AtttestedBySignature = request.AtttestedBySignature,
                AttestedDateTime = request.AttestedDateTime,
                DoInvoiceSeparately = request.DoInvoiceSeparately,
                DoInvoiceSeparatelyImmediately = request.DoInvoiceSeparatelyImmediately,
                IsCostInvoicedSeparately = request.IsCostInvoicedSeparately
            };

            _dbContext.OrderCosts.Add(orderCost);
            await _dbContext.SaveChangesAsync();

            var response = MapToDto(orderCost);
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<OrderCostDto>> Update(int id, [FromBody] UpdateOrderCostRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var orderCost = await _dbContext.OrderCosts
                .Include(x => x.SupplierOrder)
                .Include(x => x.CustomerOrder)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (orderCost is null)
            {
                return NotFound();
            }

            if (request.QuotationId.HasValue) orderCost.QuotationId = request.QuotationId;
            if (request.QuotationRowId.HasValue) orderCost.QuotationRowId = request.QuotationRowId;
            if (request.CalculationRowId.HasValue) orderCost.CalculationRowId = request.CalculationRowId;
            if (request.CustomerOrderId.HasValue) orderCost.CustomerOrderId = request.CustomerOrderId;
            if (request.SupplierOrderId.HasValue) orderCost.SupplierOrderId = request.SupplierOrderId;
            if (request.CostId.HasValue) orderCost.CostId = request.CostId;
            if (request.NrOf.HasValue) orderCost.NrOf = request.NrOf;
            if (request.InPrice.HasValue) orderCost.InPrice = request.InPrice;
            if (request.InPriceCurrencyId.HasValue) orderCost.InPriceCurrencyId = request.InPriceCurrencyId;
            if (request.OutPrice.HasValue) orderCost.OutPrice = request.OutPrice;
            if (request.DoDebit.HasValue) orderCost.DoDebit = request.DoDebit.Value;
            if (request.Note != null) orderCost.Note = request.Note;
            if (request.SupplierId.HasValue) orderCost.SupplierId = request.SupplierId;
            if (request.DoPrintOnCustomerOrder.HasValue) orderCost.DoPrintOnCustomerOrder = request.DoPrintOnCustomerOrder.Value;
            if (request.DoPrintOnSupplierOrder.HasValue) orderCost.DoPrintOnSupplierOrder = request.DoPrintOnSupplierOrder.Value;
            if (request.DoPrintOnQuotation.HasValue) orderCost.DoPrintOnQuotation = request.DoPrintOnQuotation.Value;
            if (request.SupplierName != null) orderCost.SupplierName = request.SupplierName;
            if (request.InPriceAttested.HasValue) orderCost.InPriceAttested = request.InPriceAttested;
            if (request.CreatedById.HasValue) orderCost.CreatedById = request.CreatedById;
            if (request.CreatedDateTime.HasValue) orderCost.CreatedDateTime = request.CreatedDateTime;
            if (request.EditedById.HasValue) orderCost.EditedById = request.EditedById;
            if (request.EditedDateTime.HasValue) orderCost.EditedDateTime = request.EditedDateTime;
            if (request.AttestedInPriceCurrencyRate.HasValue) orderCost.AttestedInPriceCurrencyRate = request.AttestedInPriceCurrencyRate;
            if (request.AttestedOutPriceCurrencyRate.HasValue) orderCost.AttestedOutPriceCurrencyRate = request.AttestedOutPriceCurrencyRate;
            if (request.AtttestedBySignature != null) orderCost.AtttestedBySignature = request.AtttestedBySignature;
            if (request.AttestedDateTime.HasValue) orderCost.AttestedDateTime = request.AttestedDateTime;
            if (request.DoInvoiceSeparately.HasValue) orderCost.DoInvoiceSeparately = request.DoInvoiceSeparately.Value;
            if (request.DoInvoiceSeparatelyImmediately.HasValue) orderCost.DoInvoiceSeparatelyImmediately = request.DoInvoiceSeparatelyImmediately.Value;
            if (request.IsCostInvoicedSeparately.HasValue) orderCost.IsCostInvoicedSeparately = request.IsCostInvoicedSeparately.Value;

            await _dbContext.SaveChangesAsync();

            return Ok(MapToDto(orderCost));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var orderCost = await _dbContext.OrderCosts.FirstOrDefaultAsync(x => x.Id == id);

            if (orderCost is null)
            {
                return NotFound();
            }

            _dbContext.OrderCosts.Remove(orderCost);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<OrderCostDto>>> Search([FromBody] SearchOrderCostsRequest? request)
        {
            IQueryable<OrderCost> query = _dbContext.OrderCosts
                .AsNoTracking()
                .Include(x => x.SupplierOrder)
                .Include(x => x.CustomerOrder);
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
                        _logger.LogWarning(ex, "Invalid order cost search condition for field {Field}", condition.Field);
                        return BadRequest(new { message = ex.Message, field = condition.Field });
                    }
                }
            }

            IOrderedQueryable<OrderCost> orderedQuery;
            try
            {
                orderedQuery = ApplyOrdering(query, request?.OrderBy);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid order cost order by request");
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

            return Ok(new PagedResultDto<OrderCostDto>
            {
                Items = items,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        private static Dictionary<string, PropertyInfo> BuildFieldMap()
        {
            var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in typeof(OrderCost).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!IsSearchableProperty(property))
                {
                    continue;
                }

                map[property.Name.ToLowerInvariant()] = property;

                var columnAttribute = property.GetCustomAttribute<ColumnAttribute>();
                if (!string.IsNullOrWhiteSpace(columnAttribute?.Name))
                {
                    map[columnAttribute.Name.ToLowerInvariant()] = property;
                }
            }

            return map;
        }

        private static bool IsSearchableProperty(PropertyInfo property)
        {
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            return propertyType == typeof(string)
                || propertyType == typeof(int)
                || propertyType == typeof(double)
                || propertyType == typeof(decimal)
                || propertyType == typeof(bool)
                || propertyType == typeof(DateTime);
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

        private static IOrderedQueryable<OrderCost> ApplyOrdering(
            IQueryable<OrderCost> query,
            List<SortRequest>? orderBy)
        {
            if (orderBy is null || orderBy.Count == 0)
            {
                return query.OrderBy(x => x.Id);
            }

            IOrderedQueryable<OrderCost>? ordered = null;

            foreach (var sort in orderBy)
            {
                var property = ResolveProperty(sort.Field);
                var isDescending = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
                ordered = ApplySort(ordered, query, property, isDescending);
            }

            return ordered ?? query.OrderBy(x => x.Id);
        }

        private static IOrderedQueryable<OrderCost> ApplySort(
            IOrderedQueryable<OrderCost>? ordered,
            IQueryable<OrderCost> source,
            PropertyInfo property,
            bool isDescending)
        {
            var parameter = Expression.Parameter(typeof(OrderCost), "x");
            var propertyExpression = Expression.Property(parameter, property);
            var lambda = Expression.Lambda(propertyExpression, parameter);

            var methodName = ordered is null
                ? (isDescending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy))
                : (isDescending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy));

            var method = typeof(Queryable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(OrderCost), property.PropertyType);

            var result = method.Invoke(null, new object[] { ordered ?? source, lambda });
            return (IOrderedQueryable<OrderCost>)result!;
        }

        private static IQueryable<OrderCost> ApplyCondition(IQueryable<OrderCost> query, FilterConditionDto condition)
        {
            var property = ResolveProperty(condition.Field);
            var op = condition.Operator.Trim().ToLowerInvariant();

            var parameter = Expression.Parameter(typeof(OrderCost), "x");
            var member = Expression.Property(parameter, property);
            var body = BuildConditionBody(member, property.PropertyType, condition, op);

            var lambda = Expression.Lambda<Func<OrderCost, bool>>(body, parameter);
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
                || type == typeof(double)
                || type == typeof(decimal)
                || type == typeof(DateTime);
        }

        private static OrderCostDto MapToDto(OrderCost source)
        {
            return new OrderCostDto
            {
                Id = source.Id,
                QuotationId = source.QuotationId,
                QuotationRowId = source.QuotationRowId,
                CalculationRowId = source.CalculationRowId,
                CustomerOrderId = source.CustomerOrderId,
                SupplierOrderId = source.SupplierOrderId,
                CostId = source.CostId,
                NrOf = source.NrOf,
                InPrice = source.InPrice,
                InPriceCurrencyId = source.InPriceCurrencyId,
                OutPrice = source.OutPrice,
                DoDebit = source.DoDebit,
                Note = source.Note,
                SupplierId = source.SupplierId,
                DoPrintOnCustomerOrder = source.DoPrintOnCustomerOrder,
                DoPrintOnSupplierOrder = source.DoPrintOnSupplierOrder,
                DoPrintOnQuotation = source.DoPrintOnQuotation,
                SupplierName = source.SupplierName,
                InPriceAttested = source.InPriceAttested,
                CreatedById = source.CreatedById,
                CreatedDateTime = source.CreatedDateTime,
                EditedById = source.EditedById,
                EditedDateTime = source.EditedDateTime,
                AttestedInPriceCurrencyRate = source.AttestedInPriceCurrencyRate,
                AttestedOutPriceCurrencyRate = source.AttestedOutPriceCurrencyRate,
                AtttestedBySignature = source.AtttestedBySignature,
                AttestedDateTime = source.AttestedDateTime,
                DoInvoiceSeparately = source.DoInvoiceSeparately,
                DoInvoiceSeparatelyImmediately = source.DoInvoiceSeparatelyImmediately,
                IsCostInvoicedSeparately = source.IsCostInvoicedSeparately,
                InPriceSEK = source.InPrice.HasValue && source.SupplierOrder?.PurchaseCurrencyRate is > 0
                    ? source.InPrice * (decimal)source.SupplierOrder.PurchaseCurrencyRate.Value
                    : source.InPrice,
                InPriceAttestedSEK = source.InPriceAttested.HasValue && source.SupplierOrder?.PurchaseCurrencyRate is > 0
                    ? source.InPriceAttested * (decimal)source.SupplierOrder.PurchaseCurrencyRate.Value
                    : source.InPriceAttested,
                OutPriceSEK = source.OutPrice.HasValue && source.CustomerOrder?.SalesCurrencyRate is > 0
                    ? source.OutPrice * (decimal)source.CustomerOrder.SalesCurrencyRate.Value
                    : source.OutPrice,
                Markup = ComputeMarkup(
                    source.InPrice, source.SupplierOrder?.PurchaseCurrencyRate,
                    source.OutPrice, source.CustomerOrder?.SalesCurrencyRate)
            };
        }

        private static decimal? ComputeMarkup(
            decimal? inPrice, double? purchaseCurrencyRate,
            decimal? outPrice, double? salesCurrencyRate)
        {
            var inPriceSek = inPrice.HasValue && purchaseCurrencyRate is > 0
                ? inPrice.Value * (decimal)purchaseCurrencyRate.Value
                : inPrice;
            var outPriceSek = outPrice.HasValue && salesCurrencyRate is > 0
                ? outPrice.Value * (decimal)salesCurrencyRate.Value
                : outPrice;

            if (inPriceSek is null or 0 || outPriceSek is null)
                return null;

            return (outPriceSek.Value - inPriceSek.Value) / inPriceSek.Value;
        }
    }
}
