using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
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
    public class DeliveriesController : ControllerBase
    {
        private static readonly string[] AllowedDeliveryTypes =
        {
            "DeliveryToCustomer",
            "DeliveryToStock",
            "DeliveryFromStock"
        };

        private static readonly Dictionary<string, PropertyInfo> FieldMap = BuildFieldMap();

        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<DeliveriesController> _logger;

        public DeliveriesController(ApplicationDbContext dbContext, ILogger<DeliveriesController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("{type}/{id:int}")]
        public async Task<ActionResult<DeliveryDto>> GetByTypeAndId(string type, int id)
        {
            var normalizedType = NormalizeType(type);

            var result = normalizedType switch
            {
                "DeliveryToCustomer" => await QueryToCustomer()
                    .FirstOrDefaultAsync(x => x.Id == id),
                "DeliveryToStock" => await QueryToStock()
                    .FirstOrDefaultAsync(x => x.Id == id),
                "DeliveryFromStock" => await QueryFromStock()
                    .FirstOrDefaultAsync(x => x.Id == id),
                _ => null
            };

            return result is null ? NotFound() : Ok(result);
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<DeliveryDto>>> Search([FromBody] SearchDeliveriesRequestDto? request)
        {
            var pagination = request?.Pagination ?? new PaginationRequest();

            IQueryable<DeliveryDto> query = QueryToCustomer()
                .Concat(QueryToStock())
                .Concat(QueryFromStock());

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
                        _logger.LogWarning(ex, "Invalid delivery search condition for field {Field}", condition.Field);
                        return BadRequest(new { message = ex.Message, field = condition.Field });
                    }
                }
            }

            IOrderedQueryable<DeliveryDto> ordered;
            try
            {
                ordered = ApplyOrdering(query, request?.OrderBy);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid delivery order by request");
                return BadRequest(new { message = ex.Message });
            }

            var totalCount = await query.CountAsync();
            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pagination.PageSize);

            var items = await ordered
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            return Ok(new PagedResultDto<DeliveryDto>
            {
                Items = items,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        private IQueryable<DeliveryDto> QueryToCustomer()
        {
            return _dbContext.DeliveryToCustomers
                .AsNoTracking()
                .Select(x => new DeliveryDto
                {
                    Id = x.Id,
                    Type = "DeliveryToCustomer",
                    SupplierOrderId = x.SupplierOrderId,
                    CustomerOrderId = x.CustomerOrderId,
                    DeliveryDate = x.DeliveryDate,
                    NrOfItems = x.NrOfItems,
                    ToInvoice = x.ToInvoice,
                    NrOfPallets = x.NrOfPallets,
                    CallOff = x.CallOff,
                    OldDbId = x.OldDbId,
                    SpcsRefNr = x.SpcsRefNr,
                    SupplierInvoiceNr = x.SupplierInvoiceNr,
                    SupplierInvoiceCost = x.SupplierInvoiceCost,
                    DeliveryStatus = x.DeliveryStatus,
                    PalletFormatId = x.PalletFormatId,
                    PalletIsStackable = x.PalletIsStackable,
                    PalletWidth = x.PalletWidth,
                    PalletHeight = x.PalletHeight,
                    PalletLength = x.PalletLength,
                    EditionPerPallet = x.EditionPerPallet,
                    PalletCalcFactor = x.PalletCalcFactor,
                    IsSlattPallet = x.IsSlattPallet,
                    ParentDeliveryId = x.ParentDeliveryId,
                    DoNotInvoice = x.DoNotInvoice,
                    InfoOk = x.InfoOk,
                    WeightMissingEmailSentDateTime = x.WeightMissingEmailSentDateTime,
                    CostMissingEmailSentDateTime = x.CostMissingEmailSentDateTime,
                    IsPalletInvoicedSeparately = x.IsPalletInvoicedSeparately
                });
        }

        private IQueryable<DeliveryDto> QueryToStock()
        {
            return _dbContext.DeliveryToStocks
                .AsNoTracking()
                .Select(x => new DeliveryDto
                {
                    Id = x.Id,
                    Type = "DeliveryToStock",
                    SupplierOrderId = x.SupplierOrderId,
                    DeliveryDate = x.DeliveryDate,
                    NrOfItems = x.NrOfItems,
                    NrOfPallets = x.NrOfPallets,
                    DeliveryNr = x.DeliveryNr,
                    InventoryId = x.InventoryId,
                    OldDbId = x.OldDbId,
                    SpcsRefNr = x.SpcsRefNr,
                    SupplierInvoiceNr = x.SupplierInvoiceNr,
                    SupplierInvoiceCost = x.SupplierInvoiceCost,
                    DeliveryStatus = x.DeliveryStatus,
                    PalletFormatId = x.PalletFormatId,
                    PalletIsStackable = x.PalletIsStackable,
                    PalletWidth = x.PalletWidth,
                    PalletHeight = x.PalletHeight,
                    PalletLength = x.PalletLength,
                    EditionPerPallet = x.EditionPerPallet,
                    PalletCalcFactor = x.PalletCalcFactor,
                    IsSlattPallet = x.IsSlattPallet,
                    ParentDeliveryId = x.ParentDeliveryId,
                    InfoOk = x.InfoOk,
                    IsAdjustment = x.IsAdjustment,
                    AdjustedFromDeliveryId = x.AdjustedFromDeliveryId,
                    WeightMissingEmailSentDateTime = x.WeightMissingEmailSentDateTime,
                    CostMissingEmailSentDateTime = x.CostMissingEmailSentDateTime
                });
        }

        private IQueryable<DeliveryDto> QueryFromStock()
        {
            return _dbContext.DeliveryFromStocks
                .AsNoTracking()
                .Select(x => new DeliveryDto
                {
                    Id = x.Id,
                    Type = "DeliveryFromStock",
                    CustomerOrderId = x.CustomerOrderId,
                    DeliveryDate = x.DeliveryDate,
                    NrOfItems = x.NrOfItems,
                    ToInvoice = x.ToInvoice,
                    NrOfPallets = x.NrOfPallets,
                    CallOff = x.CallOff,
                    InventoryId = x.InventoryId,
                    OldDbId = x.OldDbId,
                    DeliveryStatus = x.DeliveryStatus,
                    PalletFormatId = x.PalletFormatId,
                    PalletIsStackable = x.PalletIsStackable,
                    PalletWidth = x.PalletWidth,
                    PalletHeight = x.PalletHeight,
                    PalletLength = x.PalletLength,
                    EditionPerPallet = x.EditionPerPallet,
                    PalletCalcFactor = x.PalletCalcFactor,
                    IsSlattPallet = x.IsSlattPallet,
                    ParentDeliveryId = x.ParentDeliveryId,
                    DeliveryToStockId = x.DeliveryToStockId,
                    DoNotInvoice = x.DoNotInvoice,
                    InfoOk = x.InfoOk,
                    WeightMissingEmailSentDateTime = x.WeightMissingEmailSentDateTime,
                    CostMissingEmailSentDateTime = x.CostMissingEmailSentDateTime,
                    NrOfBunt = x.NrOfBunt,
                    NrOfYtterforpackning = x.NrOfYtterforpackning,
                    IsPalletInvoicedSeparately = x.IsPalletInvoicedSeparately
                });
        }

        private static Dictionary<string, PropertyInfo> BuildFieldMap()
        {
            var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in typeof(DeliveryDto).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                map[property.Name.ToLowerInvariant()] = property;
            }

            map["lngdeliverytocustomer_id"] = map["id"];
            map["lngdeliverytostock_id"] = map["id"];
            map["lngdeliveryfromstock_id"] = map["id"];
            map["dtedeliverydate"] = map["deliverydate"];
            map["lngsupplierorder_id"] = map["supplierorderid"];
            map["lngcustomerorder_id"] = map["customerorderid"];
            map["lngnrofitems"] = map["nrofitems"];
            map["lngnrofpallets"] = map["nrofpallets"];

            return map;
        }

        private static string NormalizeType(string type)
        {
            var value = type.Trim().ToLowerInvariant();
            return value switch
            {
                "deliverytocustomer" or "tocustomer" => "DeliveryToCustomer",
                "deliverytostock" or "tostock" => "DeliveryToStock",
                "deliveryfromstock" or "fromstock" => "DeliveryFromStock",
                _ => string.Empty
            };
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

        private static IOrderedQueryable<DeliveryDto> ApplyOrdering(
            IQueryable<DeliveryDto> query,
            List<SortRequest>? orderBy)
        {
            if (orderBy is null || orderBy.Count == 0)
            {
                return query.OrderBy(x => x.Type).ThenBy(x => x.Id);
            }

            IOrderedQueryable<DeliveryDto>? ordered = null;

            foreach (var sort in orderBy)
            {
                var property = ResolveProperty(sort.Field);
                var isDescending = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
                ordered = ApplySort(ordered, query, property, isDescending);
            }

            return ordered ?? query.OrderBy(x => x.Type).ThenBy(x => x.Id);
        }

        private static IOrderedQueryable<DeliveryDto> ApplySort(
            IOrderedQueryable<DeliveryDto>? ordered,
            IQueryable<DeliveryDto> source,
            PropertyInfo property,
            bool isDescending)
        {
            var parameter = Expression.Parameter(typeof(DeliveryDto), "x");
            var propertyExpression = Expression.Property(parameter, property);
            var lambda = Expression.Lambda(propertyExpression, parameter);

            var methodName = ordered is null
                ? (isDescending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy))
                : (isDescending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy));

            var method = typeof(Queryable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(DeliveryDto), property.PropertyType);

            var result = method.Invoke(null, new object[] { ordered ?? source, lambda });
            return (IOrderedQueryable<DeliveryDto>)result!;
        }

        private static IQueryable<DeliveryDto> ApplyCondition(IQueryable<DeliveryDto> query, FilterConditionDto condition)
        {
            var property = ResolveProperty(condition.Field);
            var op = condition.Operator.Trim().ToLowerInvariant();

            var parameter = Expression.Parameter(typeof(DeliveryDto), "x");
            var member = Expression.Property(parameter, property);
            var body = BuildConditionBody(member, property.PropertyType, condition, op);

            var lambda = Expression.Lambda<Func<DeliveryDto, bool>>(body, parameter);
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
            if (string.Equals(condition.Field, nameof(DeliveryDto.Type), StringComparison.OrdinalIgnoreCase))
            {
                return BuildTypeCondition(member, condition, op);
            }

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

        private static Expression BuildTypeCondition(MemberExpression member, FilterConditionDto condition, string op)
        {
            var nullConstant = Expression.Constant(null, typeof(string));

            return op switch
            {
                "eq" => Expression.Equal(member, Expression.Constant(NormalizeRequiredTypeValue(GetSingleStringValue(condition), condition.Field), typeof(string))),
                "neq" => Expression.NotEqual(member, Expression.Constant(NormalizeRequiredTypeValue(GetSingleStringValue(condition), condition.Field), typeof(string))),
                "in" => BuildStringInCondition(member, GetNormalizedTypeValues(condition)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for field '{condition.Field}'. Use eq, neq, or in.")
            };

            static List<string> GetNormalizedTypeValues(FilterConditionDto condition)
            {
                var values = GetStringValues(condition)
                    .Select(v => NormalizeRequiredTypeValue(v, condition.Field))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (values.Count == 0)
                {
                    throw new ArgumentException($"Filter field '{condition.Field}' requires at least one valid type value.");
                }

                return values;
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

        private static string NormalizeRequiredTypeValue(string? value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"Filter field '{field}' requires a non-empty value.");
            }

            var normalized = NormalizeType(value);
            if (string.IsNullOrEmpty(normalized))
            {
                throw new ArgumentException($"Filter field '{field}' has invalid type '{value}'. Allowed values: {string.Join(", ", AllowedDeliveryTypes)}.");
            }

            return normalized;
        }
    }
}