using System.Text.Json;
using System.Linq.Expressions;
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
    public class InventoriesController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<InventoriesController> _logger;

        public InventoriesController(ApplicationDbContext dbContext, ILogger<InventoriesController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<InventoryDto>> GetById(int id)
        {
            var inventory = await _dbContext.Inventories
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (inventory is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(inventory));
        }

        [HttpPost]
        public async Task<ActionResult<InventoryDto>> Create([FromBody] CreateInventoryRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var inventory = new Inventory
            {
                Name = request.Name,
                AccountNr = request.AccountNr,
                IsInventory = request.IsInventory,
                Email = request.Email,
                IsOmlast = request.IsOmlast,
                Address = request.Address,
                PostalNr = request.PostalNr,
                PostalAddress = request.PostalAddress,
                CountryCode = request.CountryCode,
                Country = request.Country,
                PostalNrText = request.PostalNrText,
                NoInventoryValue = request.NoInventoryValue,
                PositionId = request.PositionId,
                AddressExtra = request.AddressExtra
            };

            _dbContext.Inventories.Add(inventory);
            await _dbContext.SaveChangesAsync();

            var response = MapToDto(inventory);
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<InventoryDto>> Update(int id, [FromBody] CreateInventoryRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var inventory = await _dbContext.Inventories.FirstOrDefaultAsync(x => x.Id == id);
            if (inventory is null)
            {
                return NotFound();
            }

            inventory.Name = request.Name;
            inventory.AccountNr = request.AccountNr;
            inventory.IsInventory = request.IsInventory;
            inventory.Email = request.Email;
            inventory.IsOmlast = request.IsOmlast;
            inventory.Address = request.Address;
            inventory.PostalNr = request.PostalNr;
            inventory.PostalAddress = request.PostalAddress;
            inventory.CountryCode = request.CountryCode;
            inventory.Country = request.Country;
            inventory.PostalNrText = request.PostalNrText;
            inventory.NoInventoryValue = request.NoInventoryValue;
            // inventory.PositionId = request.PositionId;
            inventory.AddressExtra = request.AddressExtra;

            await _dbContext.SaveChangesAsync();

            return Ok(MapToDto(inventory));
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<InventoryDto>>> Search([FromBody] SearchInventoriesRequest? request)
        {
            IQueryable<Inventory> query = _dbContext.Inventories.AsNoTracking();
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
                        _logger.LogWarning(ex, "Invalid inventory search condition for field {Field}", condition.Field);
                        return BadRequest(new { message = ex.Message, field = condition.Field });
                    }
                }
            }

            IOrderedQueryable<Inventory> orderedQuery;
            try
            {
                orderedQuery = ApplyOrdering(query, request?.OrderBy);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid inventory order by request");
                return BadRequest(new { message = ex.Message });
            }

            var totalCount = await query.CountAsync();
            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pagination.PageSize);

            var inventories = (await orderedQuery
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync())
                .Select(MapToDto)
                .ToList();

            return Ok(new PagedResultDto<InventoryDto>
            {
                Items = inventories,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        private static IOrderedQueryable<Inventory> ApplyOrdering(
            IQueryable<Inventory> query,
            List<SortRequest>? orderBy)
        {
            if (orderBy is null || orderBy.Count == 0)
            {
                return query.OrderBy(x => x.Id);
            }

            IOrderedQueryable<Inventory>? ordered = null;

            foreach (var sort in orderBy)
            {
                var field = sort.Field?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(field))
                {
                    throw new ArgumentException("OrderBy field is required.");
                }

                var isDescending = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);

                ordered = field switch
                {
                    "id" => ApplyOrder(ordered, query, x => x.Id, isDescending),
                    "name" => ApplyOrder(ordered, query, x => x.Name, isDescending),
                    "accountnr" => ApplyOrder(ordered, query, x => x.AccountNr, isDescending),
                    "olddbid" => ApplyOrder(ordered, query, x => x.OldDbId, isDescending),
                    "isinventory" => ApplyOrder(ordered, query, x => x.IsInventory, isDescending),
                    "email" => ApplyOrder(ordered, query, x => x.Email, isDescending),
                    "isomlast" => ApplyOrder(ordered, query, x => x.IsOmlast, isDescending),
                    "address" => ApplyOrder(ordered, query, x => x.Address, isDescending),
                    "postalnr" => ApplyOrder(ordered, query, x => x.PostalNr, isDescending),
                    "postaladdress" => ApplyOrder(ordered, query, x => x.PostalAddress, isDescending),
                    "countrycode" => ApplyOrder(ordered, query, x => x.CountryCode, isDescending),
                    "country" => ApplyOrder(ordered, query, x => x.Country, isDescending),
                    "postalnrtext" => ApplyOrder(ordered, query, x => x.PostalNrText, isDescending),
                    "noinventoryvalue" => ApplyOrder(ordered, query, x => x.NoInventoryValue, isDescending),
                    "positionid" => ApplyOrder(ordered, query, x => x.PositionId, isDescending),
                    "addressextra" => ApplyOrder(ordered, query, x => x.AddressExtra, isDescending),
                    _ => throw new ArgumentException($"Unsupported order by field '{sort.Field}'.")
                };
            }

            return ordered ?? query.OrderBy(x => x.Id);
        }

        private static IOrderedQueryable<Inventory> ApplyOrder<TKey>(
            IOrderedQueryable<Inventory>? ordered,
            IQueryable<Inventory> source,
            Expression<Func<Inventory, TKey>> keySelector,
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

        private static IQueryable<Inventory> ApplyCondition(IQueryable<Inventory> query, FilterConditionDto condition)
        {
            var field = condition.Field.Trim().ToLowerInvariant();
            var op = condition.Operator.Trim().ToLowerInvariant();

            return field switch
            {
                "id" => ApplyIntCondition(query, x => x.Id, condition, op),
                "name" => ApplyStringCondition(query, x => x.Name, condition, op),
                "accountnr" => ApplyNullableIntCondition(query, x => x.AccountNr, condition, op),
                "olddbid" => ApplyNullableIntCondition(query, x => x.OldDbId, condition, op),
                "isinventory" => ApplyBoolCondition(query, x => x.IsInventory, condition, op),
                "email" => ApplyStringCondition(query, x => x.Email, condition, op),
                "isomlast" => ApplyBoolCondition(query, x => x.IsOmlast, condition, op),
                "address" => ApplyStringCondition(query, x => x.Address, condition, op),
                "postalnr" => ApplyNullableIntCondition(query, x => x.PostalNr, condition, op),
                "postaladdress" => ApplyStringCondition(query, x => x.PostalAddress, condition, op),
                "countrycode" => ApplyStringCondition(query, x => x.CountryCode, condition, op),
                "country" => ApplyStringCondition(query, x => x.Country, condition, op),
                "postalnrtext" => ApplyStringCondition(query, x => x.PostalNrText, condition, op),
                "noinventoryvalue" => ApplyBoolCondition(query, x => x.NoInventoryValue, condition, op),
                "positionid" => ApplyNullableIntCondition(query, x => x.PositionId, condition, op),
                "addressextra" => ApplyStringCondition(query, x => x.AddressExtra, condition, op),
                _ => throw new ArgumentException($"Unsupported filter field '{condition.Field}'.")
            };
        }

        private static IQueryable<Inventory> ApplyStringCondition(
            IQueryable<Inventory> query,
            Expression<Func<Inventory, string?>> selector,
            FilterConditionDto condition,
            string op)
        {
            var value = GetSingleStringValue(condition);
            var values = GetStringValues(condition);
            var parameter = selector.Parameters[0];
            var member = selector.Body;
            var nullConstant = Expression.Constant(null, typeof(string));

            return op switch
            {
                "eq" => query.Where(Expression.Lambda<Func<Inventory, bool>>(
                    Expression.Equal(member, Expression.Constant(value, typeof(string))),
                    parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Inventory, bool>>(
                    Expression.NotEqual(member, Expression.Constant(value, typeof(string))),
                    parameter)),
                "contains" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.Contains))),
                "startswith" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.StartsWith))),
                "endswith" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.EndsWith))),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Inventory, bool>>(
                    Expression.AndAlso(
                        Expression.NotEqual(member, nullConstant),
                        Expression.Call(
                            Expression.Constant(values),
                            typeof(List<string>).GetMethod(nameof(List<string>.Contains), new[] { typeof(string) })!,
                            member)),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<Inventory, bool>>(
                    Expression.Equal(member, nullConstant),
                    parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<Inventory, bool>>(
                    Expression.NotEqual(member, nullConstant),
                    parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for string field '{condition.Field}'.")
            };
        }

        private static IQueryable<Inventory> ApplyIntCondition(
            IQueryable<Inventory> query,
            Expression<Func<Inventory, int>> selector,
            FilterConditionDto condition,
            string op)
        {
            var value = GetRequiredIntValue(condition);
            var values = GetIntValues(condition);
            var parameter = selector.Parameters[0];
            var member = selector.Body;
            var constant = Expression.Constant(value);

            return op switch
            {
                "eq" => query.Where(Expression.Lambda<Func<Inventory, bool>>(Expression.Equal(member, constant), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Inventory, bool>>(Expression.NotEqual(member, constant), parameter)),
                "gt" => query.Where(Expression.Lambda<Func<Inventory, bool>>(Expression.GreaterThan(member, constant), parameter)),
                "gte" => query.Where(Expression.Lambda<Func<Inventory, bool>>(Expression.GreaterThanOrEqual(member, constant), parameter)),
                "lt" => query.Where(Expression.Lambda<Func<Inventory, bool>>(Expression.LessThan(member, constant), parameter)),
                "lte" => query.Where(Expression.Lambda<Func<Inventory, bool>>(Expression.LessThanOrEqual(member, constant), parameter)),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Inventory, bool>>(
                    Expression.Call(
                        Expression.Constant(values),
                        typeof(List<int>).GetMethod(nameof(List<int>.Contains), new[] { typeof(int) })!,
                        member),
                    parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for numeric field '{condition.Field}'.")
            };
        }

        private static IQueryable<Inventory> ApplyNullableIntCondition(
            IQueryable<Inventory> query,
            Expression<Func<Inventory, int?>> selector,
            FilterConditionDto condition,
            string op)
        {
            var value = GetNullableIntValue(condition);
            var values = GetIntValues(condition);
            var parameter = selector.Parameters[0];
            var member = selector.Body;
            var hasValue = Expression.Property(member, nameof(Nullable<int>.HasValue));
            var memberValue = Expression.Property(member, nameof(Nullable<int>.Value));

            return op switch
            {
                "eq" => query.Where(Expression.Lambda<Func<Inventory, bool>>(
                    Expression.Equal(member, Expression.Constant(value, typeof(int?))),
                    parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Inventory, bool>>(
                    Expression.NotEqual(member, Expression.Constant(value, typeof(int?))),
                    parameter)),
                "gt" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThan, value.Value)),
                "gte" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThanOrEqual, value.Value)),
                "lt" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.LessThan, value.Value)),
                "lte" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.LessThanOrEqual, value.Value)),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Inventory, bool>>(
                    Expression.AndAlso(
                        hasValue,
                        Expression.Call(
                            Expression.Constant(values),
                            typeof(List<int>).GetMethod(nameof(List<int>.Contains), new[] { typeof(int) })!,
                            memberValue)),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<Inventory, bool>>(Expression.Not(hasValue), parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<Inventory, bool>>(hasValue, parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for numeric field '{condition.Field}'.")
            };
        }

        private static IQueryable<Inventory> ApplyBoolCondition(
            IQueryable<Inventory> query,
            Expression<Func<Inventory, bool>> selector,
            FilterConditionDto condition,
            string op)
        {
            var value = GetRequiredBoolValue(condition);
            var parameter = selector.Parameters[0];
            var member = selector.Body;
            var constant = Expression.Constant(value);

            return op switch
            {
                "eq" => query.Where(Expression.Lambda<Func<Inventory, bool>>(Expression.Equal(member, constant), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Inventory, bool>>(Expression.NotEqual(member, constant), parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for boolean field '{condition.Field}'.")
            };
        }

        private static Expression<Func<Inventory, bool>> BuildStringMethodCall(
            ParameterExpression parameter,
            Expression member,
            string value,
            string methodName)
        {
            var method = typeof(string).GetMethod(methodName, new[] { typeof(string) })!;
            var nullConstant = Expression.Constant(null, typeof(string));

            return Expression.Lambda<Func<Inventory, bool>>(
                Expression.AndAlso(
                    Expression.NotEqual(member, nullConstant),
                    Expression.Call(member, method, Expression.Constant(value))),
                parameter);
        }

        private static Expression<Func<Inventory, bool>> BuildNullableIntComparison(
            ParameterExpression parameter,
            MemberExpression hasValue,
            MemberExpression memberValue,
            ExpressionType comparison,
            int value)
        {
            var comparisonExpression = Expression.MakeBinary(comparison, memberValue, Expression.Constant(value));

            return Expression.Lambda<Func<Inventory, bool>>(
                Expression.AndAlso(hasValue, comparisonExpression),
                parameter);
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

        private static int GetRequiredIntValue(FilterConditionDto condition)
        {
            var value = GetNullableIntValue(condition);
            if (!value.HasValue)
            {
                throw new ArgumentException($"Filter field '{condition.Field}' requires an integer value.");
            }

            return value.Value;
        }

        private static int? GetNullableIntValue(FilterConditionDto condition)
        {
            if (!condition.Value.HasValue)
            {
                return null;
            }

            var value = condition.Value.Value;

            if (value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }

            throw new ArgumentException($"Filter field '{condition.Field}' requires an integer value.");
        }

        private static bool GetRequiredBoolValue(FilterConditionDto condition)
        {
            if (!condition.Value.HasValue)
            {
                throw new ArgumentException($"Filter field '{condition.Field}' requires a boolean value.");
            }

            var value = condition.Value.Value;

            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }

            throw new ArgumentException($"Filter field '{condition.Field}' requires a boolean value.");
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

        private static List<int> GetIntValues(FilterConditionDto condition)
        {
            var results = new List<int>();

            foreach (var value in GetValues(condition))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                {
                    results.Add(number);
                    continue;
                }

                if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
                {
                    results.Add(parsed);
                    continue;
                }

                throw new ArgumentException($"Filter field '{condition.Field}' requires integer values.");
            }

            return results;
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

        private static InventoryDto MapToDto(Inventory inventory)
        {
            return new InventoryDto
            {
                Id = inventory.Id,
                Name = inventory.Name,
                AccountNr = inventory.AccountNr,
                OldDbId = inventory.OldDbId,
                IsInventory = inventory.IsInventory,
                Email = inventory.Email,
                IsOmlast = inventory.IsOmlast,
                Address = inventory.Address,
                PostalNr = inventory.PostalNr,
                PostalAddress = inventory.PostalAddress,
                CountryCode = inventory.CountryCode,
                Country = inventory.Country,
                PostalNrText = inventory.PostalNrText,
                NoInventoryValue = inventory.NoInventoryValue,
                PositionId = inventory.PositionId,
                AddressExtra = inventory.AddressExtra
            };
        }
    }
}