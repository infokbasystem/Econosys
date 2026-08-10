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
    public class SupplierOrdersController : ControllerBase
    {
        private static readonly Dictionary<string, PropertyInfo> FieldMap = BuildFieldMap();

        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<SupplierOrdersController> _logger;
        private readonly ILegacyUserResolutionService _legacyUserResolution;
        private readonly IPalletFormatOptionsService _palletFormatOptionsService;

        public SupplierOrdersController(
            ApplicationDbContext dbContext,
            ILogger<SupplierOrdersController> logger,
            ILegacyUserResolutionService legacyUserResolution,
            IPalletFormatOptionsService palletFormatOptionsService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _legacyUserResolution = legacyUserResolution;
            _palletFormatOptionsService = palletFormatOptionsService;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<SupplierOrderDto>> GetById(int id)
        {
            var supplierOrder = await BuildDetailsQuery()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (supplierOrder is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(supplierOrder));
        }

        [HttpPost]
        public async Task<ActionResult<SupplierOrderDto>> Create([FromBody] CreateSupplierOrderRequest request)
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

            var entity = new SupplierOrder();
            ApplyCreateRequestToEntity(entity, request);

            entity.Created ??= SwedishTime.Now;
            entity.CreatedBy ??= legacyUser.Id;
            entity.Edited = SwedishTime.Now;
            entity.EditedBy = legacyUser.Id;

            _dbContext.SupplierOrders.Add(entity);
            await _dbContext.SaveChangesAsync();

            await SyncOrderCostsAsync(entity.Id, request.OrderCosts);
            await _dbContext.SaveChangesAsync();

            var saved = await BuildDetailsQuery().FirstAsync(x => x.Id == entity.Id);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(saved));
        }

        [HttpGet("{id:int}/form-options")]
        public async Task<ActionResult<SupplierOrderFormOptionsDto>> GetFormOptions(int id)
        {
            var context = await _dbContext.SupplierOrders
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.CustomerId,
                    x.SupplierId,
                    x.SupplierFactoryId,
                    x.InventoryId,
                })
                .FirstOrDefaultAsync();

            if (context is null)
            {
                return NotFound();
            }

            var users = await _dbContext.LegacyUsers
                .AsNoTracking()
                .Where(x => x.Active && x.Name != null && x.Name != "")
                .OrderBy(x => x.Name)
                .Select(x => new FilterOptionDto<string>
                {
                    Id = x.Name!,
                    Name = x.Name!,
                    IsActive = x.Active,
                })
                .ToListAsync();

            var costs = await _dbContext.Costs
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new CostDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    IsActive = x.IsActive,
                    AccountDomestic = x.AccountDomestic,
                    AccountEU = x.AccountEU,
                    AccountExport = x.AccountExport,
                    AddAutomaicIfEconopackIsTransportReponsible = x.AddAutomaicIfEconopackIsTransportReponsible,
                    CostTypeText = x.CostTypeText,
                    DmtFixed = x.DmtFixed,
                    DmtPercent = x.DmtPercent,
                    IsCalculation = x.IsCalculation,
                    IsCustomerDefault = x.IsCustomerDefault,
                    IsNrOf = x.IsNrOf,
                    IsSupplier = x.IsSupplier,
                    IsDebitDefault = x.IsDebitDefault,
                    DoPrintCustomerOrderDefault = x.DoPrintCustomerOrderDefault,
                    DoPrintQuotationDefault = x.DoPrintQuotationDefault,
                    DoPrintScrapToolsTextOnCustomerOrder = x.DoPrintScrapToolsTextOnCustomerOrder,
                    DoPrintSupplierOrderDefault = x.DoPrintSupplierOrderDefault,
                    ProvisionPercent = x.ProvisionPercent,
                    TranslationCodeCustomerOrderKnown = x.TranslationCodeCustomerOrderKnown,
                    TranslationCodeCustomerOrderUnknown = x.TranslationCodeCustomerOrderUnknown,
                    TranslationCodeInvoiceRow = x.TranslationCodeInvoiceRow,
                    TranslationCodeQuotationKnown = x.TranslationCodeQuotationKnown,
                    TranslationCodeQuotationUnknown = x.TranslationCodeQuotationUnknown,
                    TranslationCodeSupplierOrderKnown = x.TranslationCodeSupplierOrderKnown,
                    TranslationCodeSupplierOrderUnknown = x.TranslationCodeSupplierOrderUnknown,
                })
                .ToListAsync();

            var currencies = await _dbContext.Currencies
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new CurrencyDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    TranslationCode = x.TranslationCode,
                    IsDefault = x.IsDefault,
                    RateToSek = x.RateToSek,
                    Active = x.Active,
                    RateStockValue = x.RateStockValue,
                    OldDbId = x.OldDbId,
                    SpcsKey = x.SpcsKey,
                    WarningTolerancePercent = x.WarningTolerancePercent,
                })
                .ToListAsync();

            var palletFormats = await _palletFormatOptionsService.GetOptionsAsync(context.CustomerId, context.SupplierId);

            var supplierFactories = context.SupplierId.HasValue
                ? await _dbContext.SupplierFactories
                    .AsNoTracking()
                    .Where(x => x.SupplierId == context.SupplierId)
                    .OrderBy(x => x.Name)
                    .ThenBy(x => x.Id)
                    .Select(x => new FilterOptionDto<int>
                    {
                        Id = x.Id,
                        Name = !string.IsNullOrWhiteSpace(x.Name) ? x.Name! : $"Fabrik {x.Id}",
                        IsActive = true,
                    })
                    .ToListAsync()
                : new List<FilterOptionDto<int>>();

            if (context.SupplierFactoryId.HasValue && supplierFactories.All(x => x.Id != context.SupplierFactoryId.Value))
            {
                var selectedFactory = await _dbContext.SupplierFactories
                    .AsNoTracking()
                    .Where(x => x.Id == context.SupplierFactoryId.Value)
                    .Select(x => new { x.Id, x.Name })
                    .FirstOrDefaultAsync();

                supplierFactories.Add(new FilterOptionDto<int>
                {
                    Id = context.SupplierFactoryId.Value,
                    Name = !string.IsNullOrWhiteSpace(selectedFactory?.Name)
                        ? selectedFactory!.Name!
                        : $"Fabrik {context.SupplierFactoryId.Value}",
                    IsActive = true,
                });
                supplierFactories = supplierFactories.OrderBy(x => x.Id).ToList();
            }

            var inventories = await _dbContext.Inventories
                .AsNoTracking()
                .Where(x => x.IsInventory)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Id)
                .Select(x => new FilterOptionDto<int>
                {
                    Id = x.Id,
                    Name = !string.IsNullOrWhiteSpace(x.Name) ? x.Name! : $"Lager {x.Id}",
                    IsActive = true,
                })
                .ToListAsync();

            if (context.InventoryId.HasValue && inventories.All(x => x.Id != context.InventoryId.Value))
            {
                var selectedInventory = await _dbContext.Inventories
                    .AsNoTracking()
                    .Where(x => x.Id == context.InventoryId.Value)
                    .Select(x => new { x.Id, x.Name })
                    .FirstOrDefaultAsync();

                inventories.Add(new FilterOptionDto<int>
                {
                    Id = context.InventoryId.Value,
                    Name = !string.IsNullOrWhiteSpace(selectedInventory?.Name)
                        ? selectedInventory!.Name!
                        : $"Lager {context.InventoryId.Value}",
                    IsActive = true,
                });
                inventories = inventories.OrderBy(x => x.Id).ToList();
            }

            return Ok(new SupplierOrderFormOptionsDto
            {
                Users = users,
                Costs = costs,
                Currencies = currencies,
                PalletFormats = palletFormats,
                SupplierFactories = supplierFactories,
                Inventories = inventories,
            });
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<SupplierOrderDto>> Update(int id, [FromBody] UpdateSupplierOrderRequest request)
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

            var entity = await _dbContext.SupplierOrders.FirstOrDefaultAsync(x => x.Id == id);
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

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _dbContext.SupplierOrders.FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null)
            {
                return NotFound();
            }

            var customerOrders = await _dbContext.CustomerOrders
                .Where(x => x.SupplierOrderId == id)
                .ToListAsync();
            foreach (var customerOrder in customerOrders)
            {
                customerOrder.SupplierOrderId = null;
            }

            var orderCosts = await _dbContext.OrderCosts
                .Where(x => x.SupplierOrderId == id)
                .ToListAsync();
            foreach (var orderCost in orderCosts)
            {
                orderCost.SupplierOrderId = null;
            }

            var deliveriesToCustomer = await _dbContext.DeliveryToCustomers
                .Where(x => x.SupplierOrderId == id)
                .ToListAsync();
            foreach (var row in deliveriesToCustomer)
            {
                row.SupplierOrderId = null;
            }

            var deliveriesToStock = await _dbContext.DeliveryToStocks
                .Where(x => x.SupplierOrderId == id)
                .ToListAsync();
            foreach (var row in deliveriesToStock)
            {
                row.SupplierOrderId = null;
            }

            var stockTakingItems = await _dbContext.StockTakingItems
                .Where(x => x.SupplierOrderId == id)
                .ToListAsync();
            foreach (var row in stockTakingItems)
            {
                row.SupplierOrderId = null;
            }

            var documentFiles = await _dbContext.DocumentFiles
                .Where(x => x.SupplierOrderId == id)
                .ToListAsync();
            foreach (var row in documentFiles)
            {
                row.SupplierOrderId = null;
            }

            var documentFileRelations = await _dbContext.DocumentFileRelations
                .Where(x => x.SupplierOrderId == id)
                .ToListAsync();
            foreach (var row in documentFileRelations)
            {
                row.SupplierOrderId = null;
            }

            var basedOnReferences = await _dbContext.SupplierOrders
                .Where(x => x.BasedOnSupplierOrderId == id)
                .ToListAsync();
            foreach (var row in basedOnReferences)
            {
                row.BasedOnSupplierOrderId = null;
            }

            var editionReferences = await _dbContext.SupplierOrders
                .Where(x => x.EditionTakenFromSupplierOrderId == id)
                .ToListAsync();
            foreach (var row in editionReferences)
            {
                row.EditionTakenFromSupplierOrderId = null;
            }

            _dbContext.SupplierOrders.Remove(entity);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<SupplierOrderDto>>> Search([FromBody] SearchSupplierOrdersRequest? request)
        {
            IQueryable<SupplierOrder> query = _dbContext.SupplierOrders
                .AsNoTracking()
                .Include(x => x.Customer);
            var pagination = request?.Pagination ?? new PaginationRequest();

            var searchTerm = request?.SearchTerm?.Trim();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var likePattern = $"%{EscapeLikePattern(searchTerm)}%";
                var hasNumericId = int.TryParse(searchTerm, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idValue);

                query = query.Where(x =>
                    (hasNumericId && x.Id == idValue) ||
                    (x.SupplierOrderNr != null && EF.Functions.Like(x.SupplierOrderNr, likePattern)) ||
                    (x.CustomerOrderNr != null && EF.Functions.Like(x.CustomerOrderNr, likePattern)) ||
                    (x.Customer != null && x.Customer.Name != null && EF.Functions.Like(x.Customer.Name, likePattern)));
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
                        _logger.LogWarning(ex, "Invalid supplier order search condition for field {Field}", condition.Field);
                        return BadRequest(new { message = ex.Message, field = condition.Field });
                    }
                }
            }

            IOrderedQueryable<SupplierOrder> orderedQuery;
            try
            {
                orderedQuery = ApplyOrdering(query, request?.OrderBy);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid supplier order order by request");
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

            return Ok(new PagedResultDto<SupplierOrderDto>
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

            foreach (var property in typeof(SupplierOrder).GetProperties(BindingFlags.Public | BindingFlags.Instance))
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

        private static IOrderedQueryable<SupplierOrder> ApplyOrdering(
            IQueryable<SupplierOrder> query,
            List<SortRequest>? orderBy)
        {
            if (orderBy is null || orderBy.Count == 0)
            {
                return query.OrderBy(x => x.Id);
            }

            IOrderedQueryable<SupplierOrder>? ordered = null;

            foreach (var sort in orderBy)
            {
                var isDescending = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);

                if (string.Equals(sort.Field, "customername", StringComparison.OrdinalIgnoreCase))
                {
                    ordered = ApplySort(ordered, query, x => x.Customer != null ? x.Customer.Name : null, isDescending);
                    continue;
                }

                var property = ResolveProperty(sort.Field);
                ordered = ApplySort(ordered, query, property, isDescending);
            }

            return ordered ?? query.OrderBy(x => x.Id);
        }

        private static IOrderedQueryable<SupplierOrder> ApplySort(
            IOrderedQueryable<SupplierOrder>? ordered,
            IQueryable<SupplierOrder> source,
            PropertyInfo property,
            bool isDescending)
        {
            var parameter = Expression.Parameter(typeof(SupplierOrder), "x");
            var propertyExpression = Expression.Property(parameter, property);
            var lambda = Expression.Lambda(propertyExpression, parameter);

            var methodName = ordered is null
                ? (isDescending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy))
                : (isDescending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy));

            var method = typeof(Queryable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(SupplierOrder), property.PropertyType);

            var result = method.Invoke(null, new object[] { ordered ?? source, lambda });
            return (IOrderedQueryable<SupplierOrder>)result!;
        }

        private static IOrderedQueryable<SupplierOrder> ApplySort<TKey>(
            IOrderedQueryable<SupplierOrder>? ordered,
            IQueryable<SupplierOrder> source,
            Expression<Func<SupplierOrder, TKey>> keySelector,
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

        private static IQueryable<SupplierOrder> ApplyCondition(IQueryable<SupplierOrder> query, FilterConditionDto condition)
        {
            var property = ResolveProperty(condition.Field);
            var op = condition.Operator.Trim().ToLowerInvariant();

            var parameter = Expression.Parameter(typeof(SupplierOrder), "x");
            var member = Expression.Property(parameter, property);
            var body = BuildConditionBody(member, property.PropertyType, condition, op);

            var lambda = Expression.Lambda<Func<SupplierOrder, bool>>(body, parameter);
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

        private static SupplierOrderDto MapToDto(SupplierOrder source)
        {
            return new SupplierOrderDto
            {
                Id = source.Id,
                SupplierOrderNr = source.SupplierOrderNr,
                BasedOnSupplierOrderId = source.BasedOnSupplierOrderId,
                InquiryId = source.InquiryId,
                QuotationId = source.QuotationId,
                InquiryOldId = source.InquiryOldId,
                SupplierId = source.SupplierId,
                SupplierName = source.SupplierName,
                Address = source.Address,
                PostalNr = source.PostalNr,
                PostalAddress = source.PostalAddress,
                Country = source.Country,
                CustomerId = source.CustomerId,
                CustomerName = source.Customer?.Name,
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
                Message = source.Message,
                GoodsMarking = source.GoodsMarking,
                Preparation = source.Preparation,
                UnitId = source.UnitId,
                UnitName = source.Unit?.Name,
                SelectedCalculationRowId = source.SelectedCalculationRowId,
                Edition = source.Edition,
                PurchasePrice = source.PurchasePrice,
                PurchaseCurrencyId = source.PurchaseCurrencyId,
                PurchaseCurrencyName = source.PurchaseCurrency?.Name,
                Confirmed = source.Confirmed,
                PuchDrawingAccepted = source.PuchDrawingAccepted,
                PrintBasisAccepted = source.PrintBasisAccepted,
                HideCustomerInfoOnPrint = source.HideCustomerInfoOnPrint,
                Created = source.Created,
                Edited = source.Edited,
                CreatedBy = source.CreatedBy,
                CreatedByUserName = null,
                EditedBy = source.EditedBy,
                EditedByUserName = null,
                ProductCode = source.ProductCode,
                ConfirmedDeliveryDate = source.ConfirmedDeliveryDate,
                PurchaseCurrencyRate = source.PurchaseCurrencyRate,
                Customer2 = source.Customer2,
                Address2 = source.Address2,
                OneWayPallet = source.OneWayPallet,
                EurPallet = source.EurPallet,
                InventoryId = source.InventoryId,
                LoadingInstruction = source.LoadingInstruction,
                OldDbId = source.OldDbId,
                IsNonStopPallet = source.IsNonStopPallet,
                SupplierFactoryId = source.SupplierFactoryId,
                CustomerOrderNr = source.CustomerOrderNr,
                EconopackTransportResponsible = source.EconopackTransportResponsible,
                ConfirmedDeliveryDateWeekMode = source.ConfirmedDeliveryDateWeekMode,
                ProducedEdition = source.ProducedEdition,
                DeliveryDate = source.DeliveryDate,
                DeliveryDateWeekMode = source.DeliveryDateWeekMode,
                CustomerDeliveryAddressId = source.CustomerDeliveryAddressId,
                IsParcelDelivery = source.IsParcelDelivery,
                PalletFormatId = source.PalletFormatId,
                BlockOrderCalculationId = source.BlockOrderCalculationId,
                HideCustomerNameOnPrint = source.HideCustomerNameOnPrint,
                HideProductNameOnPrint = source.HideProductNameOnPrint,
                IsFSC = source.IsFSC,
                EditionTakenFromSupplierOrderId = source.EditionTakenFromSupplierOrderId,
                EmailSentDateTime = source.EmailSentDateTime,
                PackagingType = source.PackagingType,
                CalculationId = source.CalculationId,
                OrderCosts = source.OrderCosts
                    .OrderBy(x => x.Id)
                    .Select(MapOrderCostToDto)
                    .ToList(),
            };
        }

        private IQueryable<SupplierOrder> BuildDetailsQuery()
        {
            return _dbContext.SupplierOrders
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Unit)
                .Include(x => x.PurchaseCurrency)
                .Include(x => x.OrderCosts)
                    .ThenInclude(x => x.CustomerOrder)
                .Include(x => x.OrderCosts)
                    .ThenInclude(x => x.InvoiceRows);
        }

        private async Task SyncOrderCostsAsync(int supplierOrderId, List<UpsertSupplierOrderOrderCostRequest>? requestedCosts)
        {
            var requested = requestedCosts ?? new List<UpsertSupplierOrderOrderCostRequest>();

            var requestedIds = requested
                .Where(x => x.Id.HasValue && x.Id.Value > 0)
                .Select(x => x.Id!.Value)
                .ToHashSet();

            var existing = await _dbContext.OrderCosts
                .Where(x => x.SupplierOrderId == supplierOrderId)
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
                    var entity = new OrderCost { SupplierOrderId = supplierOrderId };
                    ApplyOrderCostRequest(entity, req);
                    _dbContext.OrderCosts.Add(entity);
                }
            }
        }

        private static void ApplyOrderCostRequest(OrderCost entity, UpsertSupplierOrderOrderCostRequest req)
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

        private static SupplierOrderOrderCostDto MapOrderCostToDto(OrderCost source)
        {
            var invoiceRow = source.InvoiceRows
                .Where(x => x.InvoiceId.HasValue)
                .OrderByDescending(x => x.InvoiceId)
                .FirstOrDefault();

            return new SupplierOrderOrderCostDto
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
                OutPriceSEK = source.OutPrice.HasValue && source.CustomerOrder?.SalesCurrencyRate is > 0
                    ? source.OutPrice * (decimal)source.CustomerOrder.SalesCurrencyRate.Value
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

        private static void ApplyCreateRequestToEntity(SupplierOrder entity, CreateSupplierOrderRequest request)
        {
            entity.SupplierOrderNr = request.SupplierOrderNr;
            entity.BasedOnSupplierOrderId = request.BasedOnSupplierOrderId;
            entity.SupplierId = request.SupplierId;
            entity.SupplierFactoryId = request.SupplierFactoryId;
            entity.InventoryId = request.InventoryId;
            entity.SupplierName = request.SupplierName;
            entity.CustomerId = request.CustomerId;
            entity.Date = request.Date;
            entity.TimeOfDelivery = request.TimeOfDelivery;
            entity.CustomerOrderNr = request.CustomerOrderNr;
            entity.DeliveryAddressName = request.DeliveryAddressName;
            entity.DeliveryAddress = request.DeliveryAddress;
            entity.DeliveryPostalNr = request.DeliveryPostalNr;
            entity.DeliveryPostalAddress = request.DeliveryPostalAddress;
            entity.DeliveryCountry = request.DeliveryCountry;
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
            entity.GoodsMarking = request.GoodsMarking;
            entity.HideCustomerInfoOnPrint = request.HideCustomerInfoOnPrint;
            entity.HideCustomerNameOnPrint = request.HideCustomerNameOnPrint;
            entity.HideProductNameOnPrint = request.HideProductNameOnPrint;
            entity.IsFSC = request.IsFSC;
            entity.Confirmed = request.Confirmed;
            entity.EconopackTransportResponsible = request.EconopackTransportResponsible;
            entity.DeliveryDate = request.DeliveryDate;
            entity.DeliveryDateWeekMode = request.DeliveryDateWeekMode;
            entity.ConfirmedDeliveryDate = request.ConfirmedDeliveryDate;
            entity.ConfirmedDeliveryDateWeekMode = request.ConfirmedDeliveryDateWeekMode;
            entity.PackagingType = request.PackagingType;
            entity.PalletFormatId = request.PalletFormatId;
            entity.EurPallet = request.EurPallet;
        }

        private static void ApplyUpdateRequestToEntity(SupplierOrder entity, UpdateSupplierOrderRequest request)
        {
            // Supplier/customer linkage is intentionally stable after create.
            entity.SupplierOrderNr = request.SupplierOrderNr;
            entity.BasedOnSupplierOrderId = request.BasedOnSupplierOrderId;
            entity.SupplierFactoryId = request.SupplierFactoryId;
            entity.InventoryId = request.InventoryId;
            entity.Date = request.Date;
            entity.TimeOfDelivery = request.TimeOfDelivery;
            entity.CustomerOrderNr = request.CustomerOrderNr;
            entity.DeliveryAddressName = request.DeliveryAddressName;
            entity.DeliveryAddress = request.DeliveryAddress;
            entity.DeliveryPostalNr = request.DeliveryPostalNr;
            entity.DeliveryPostalAddress = request.DeliveryPostalAddress;
            entity.DeliveryCountry = request.DeliveryCountry;
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
            entity.GoodsMarking = request.GoodsMarking;
            entity.HideCustomerInfoOnPrint = request.HideCustomerInfoOnPrint;
            entity.HideCustomerNameOnPrint = request.HideCustomerNameOnPrint;
            entity.HideProductNameOnPrint = request.HideProductNameOnPrint;
            entity.IsFSC = request.IsFSC;
            entity.Confirmed = request.Confirmed;
            entity.EconopackTransportResponsible = request.EconopackTransportResponsible;
            entity.DeliveryDate = request.DeliveryDate;
            entity.DeliveryDateWeekMode = request.DeliveryDateWeekMode;
            entity.ConfirmedDeliveryDate = request.ConfirmedDeliveryDate;
            entity.ConfirmedDeliveryDateWeekMode = request.ConfirmedDeliveryDateWeekMode;
            entity.PackagingType = request.PackagingType;
            entity.PalletFormatId = request.PalletFormatId;
            entity.EurPallet = request.EurPallet;
            entity.ProducedEdition = request.ProducedEdition;
        }
    }
}