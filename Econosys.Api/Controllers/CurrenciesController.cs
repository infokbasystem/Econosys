using System.Linq.Expressions;
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
    public class CurrenciesController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CurrenciesController> _logger;

        public CurrenciesController(ApplicationDbContext dbContext, ILogger<CurrenciesController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<CurrencyDto>> GetById(int id)
        {
            var currency = await _dbContext.Currencies
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (currency is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(currency));
        }

        [HttpPost]
        public async Task<ActionResult<CurrencyDto>> Create([FromBody] CreateCurrencyRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var currency = new Currency
            {
                Name = request.Name,
                TranslationCode = request.TranslationCode,
                IsDefault = request.IsDefault,
                RateToSek = request.RateToSek,
                Active = request.Active,
                RateStockValue = request.RateStockValue,
                OldDbId = request.OldDbId,
                SpcsKey = request.SpcsKey,
                WarningTolerancePercent = request.WarningTolerancePercent
            };

            _dbContext.Currencies.Add(currency);
            await _dbContext.SaveChangesAsync();

            var response = MapToDto(currency);
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<CurrencyDto>> Update(int id, [FromBody] UpdateCurrencyRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var currency = await _dbContext.Currencies.FirstOrDefaultAsync(x => x.Id == id);

            if (currency is null)
            {
                return NotFound();
            }

            if (request.Name is not null) currency.Name = request.Name;
            if (request.TranslationCode.HasValue) currency.TranslationCode = request.TranslationCode;
            if (request.IsDefault.HasValue) currency.IsDefault = request.IsDefault.Value;
            if (request.RateToSek.HasValue) currency.RateToSek = request.RateToSek;
            if (request.Active.HasValue) currency.Active = request.Active.Value;
            if (request.RateStockValue.HasValue) currency.RateStockValue = request.RateStockValue;
            if (request.OldDbId.HasValue) currency.OldDbId = request.OldDbId;
            if (request.SpcsKey is not null) currency.SpcsKey = request.SpcsKey;
            if (request.WarningTolerancePercent.HasValue) currency.WarningTolerancePercent = request.WarningTolerancePercent;

            await _dbContext.SaveChangesAsync();

            return Ok(MapToDto(currency));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var currency = await _dbContext.Currencies.FirstOrDefaultAsync(x => x.Id == id);

            if (currency is null)
            {
                return NotFound();
            }

            _dbContext.Currencies.Remove(currency);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<CurrencyDto>>> Search([FromBody] SearchCurrenciesRequest? request)
        {
            IQueryable<Currency> query = _dbContext.Currencies.AsNoTracking();
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
                        _logger.LogWarning(ex, "Invalid currency search condition for field {Field}", condition.Field);
                        return BadRequest(new { message = ex.Message, field = condition.Field });
                    }
                }
            }

            IOrderedQueryable<Currency> orderedQuery;
            try
            {
                orderedQuery = ApplyOrdering(query, request?.OrderBy);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid currency order by request");
                return BadRequest(new { message = ex.Message });
            }

            var totalCount = await query.CountAsync();
            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pagination.PageSize);

            var currencies = (await orderedQuery
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync())
                .Select(MapToDto)
                .ToList();

            return Ok(new PagedResultDto<CurrencyDto>
            {
                Items = currencies,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        private static IOrderedQueryable<Currency> ApplyOrdering(IQueryable<Currency> query, List<SortRequest>? orderBy)
        {
            if (orderBy is null || orderBy.Count == 0)
            {
                return query.OrderBy(x => x.Id);
            }

            IOrderedQueryable<Currency>? ordered = null;

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
                    "translationcode" => ApplyOrder(ordered, query, x => x.TranslationCode, isDescending),
                    "isdefault" => ApplyOrder(ordered, query, x => x.IsDefault, isDescending),
                    "ratetosek" => ApplyOrder(ordered, query, x => x.RateToSek, isDescending),
                    "active" => ApplyOrder(ordered, query, x => x.Active, isDescending),
                    "ratestockvalue" => ApplyOrder(ordered, query, x => x.RateStockValue, isDescending),
                    "olddbid" => ApplyOrder(ordered, query, x => x.OldDbId, isDescending),
                    "spcskey" => ApplyOrder(ordered, query, x => x.SpcsKey, isDescending),
                    "warningtolerancepercent" => ApplyOrder(ordered, query, x => x.WarningTolerancePercent, isDescending),
                    _ => throw new ArgumentException($"Unsupported order by field '{sort.Field}'.")
                };
            }

            return ordered ?? query.OrderBy(x => x.Id);
        }

        private static IOrderedQueryable<Currency> ApplyOrder<TKey>(
            IOrderedQueryable<Currency>? ordered,
            IQueryable<Currency> source,
            Expression<Func<Currency, TKey>> keySelector,
            bool isDescending)
        {
            if (ordered is null)
            {
                return isDescending ? source.OrderByDescending(keySelector) : source.OrderBy(keySelector);
            }

            return isDescending ? ordered.ThenByDescending(keySelector) : ordered.ThenBy(keySelector);
        }

        private static IQueryable<Currency> ApplyCondition(IQueryable<Currency> query, FilterConditionDto condition)
        {
            var field = condition.Field.Trim().ToLowerInvariant();
            var op = condition.Operator.Trim().ToLowerInvariant();

            return field switch
            {
                "id" => ApplyIntCondition(query, x => x.Id, condition, op),
                "name" => ApplyStringCondition(query, x => x.Name, condition, op),
                "translationcode" => ApplyNullableIntCondition(query, x => x.TranslationCode, condition, op),
                "isdefault" => ApplyBoolCondition(query, x => x.IsDefault, condition, op),
                "ratetosek" => ApplyNullableDoubleCondition(query, x => x.RateToSek, condition, op),
                "active" => ApplyBoolCondition(query, x => x.Active, condition, op),
                "ratestockvalue" => ApplyNullableDoubleCondition(query, x => x.RateStockValue, condition, op),
                "olddbid" => ApplyNullableIntCondition(query, x => x.OldDbId, condition, op),
                "spcskey" => ApplyStringCondition(query, x => x.SpcsKey, condition, op),
                "warningtolerancepercent" => ApplyNullableIntCondition(query, x => x.WarningTolerancePercent, condition, op),
                _ => throw new ArgumentException($"Unsupported filter field '{condition.Field}'.")
            };
        }

        private static IQueryable<Currency> ApplyStringCondition(
            IQueryable<Currency> query,
            Expression<Func<Currency, string?>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<Currency, bool>>(
                    Expression.Equal(member, Expression.Constant(value, typeof(string))),
                    parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Currency, bool>>(
                    Expression.NotEqual(member, Expression.Constant(value, typeof(string))),
                    parameter)),
                "contains" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.Contains))),
                "startswith" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.StartsWith))),
                "endswith" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.EndsWith))),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Currency, bool>>(
                    Expression.AndAlso(
                        Expression.NotEqual(member, nullConstant),
                        Expression.Call(
                            Expression.Constant(values),
                            typeof(List<string>).GetMethod(nameof(List<string>.Contains), new[] { typeof(string) })!,
                            member)),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<Currency, bool>>(
                    Expression.Equal(member, nullConstant),
                    parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<Currency, bool>>(
                    Expression.NotEqual(member, nullConstant),
                    parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for string field '{condition.Field}'.")
            };
        }

        private static IQueryable<Currency> ApplyIntCondition(
            IQueryable<Currency> query,
            Expression<Func<Currency, int>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<Currency, bool>>(Expression.Equal(member, constant), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Currency, bool>>(Expression.NotEqual(member, constant), parameter)),
                "gt" => query.Where(Expression.Lambda<Func<Currency, bool>>(Expression.GreaterThan(member, constant), parameter)),
                "gte" => query.Where(Expression.Lambda<Func<Currency, bool>>(Expression.GreaterThanOrEqual(member, constant), parameter)),
                "lt" => query.Where(Expression.Lambda<Func<Currency, bool>>(Expression.LessThan(member, constant), parameter)),
                "lte" => query.Where(Expression.Lambda<Func<Currency, bool>>(Expression.LessThanOrEqual(member, constant), parameter)),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Currency, bool>>(
                    Expression.Call(
                        Expression.Constant(values),
                        typeof(List<int>).GetMethod(nameof(List<int>.Contains), new[] { typeof(int) })!,
                        member),
                    parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for numeric field '{condition.Field}'.")
            };
        }

        private static IQueryable<Currency> ApplyNullableIntCondition(
            IQueryable<Currency> query,
            Expression<Func<Currency, int?>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<Currency, bool>>(
                    Expression.Equal(member, Expression.Constant(value, typeof(int?))),
                    parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Currency, bool>>(
                    Expression.NotEqual(member, Expression.Constant(value, typeof(int?))),
                    parameter)),
                "gt" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThan, value.Value)),
                "gte" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThanOrEqual, value.Value)),
                "lt" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.LessThan, value.Value)),
                "lte" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.LessThanOrEqual, value.Value)),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Currency, bool>>(
                    Expression.AndAlso(
                        hasValue,
                        Expression.Call(
                            Expression.Constant(values),
                            typeof(List<int>).GetMethod(nameof(List<int>.Contains), new[] { typeof(int) })!,
                            memberValue)),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<Currency, bool>>(Expression.Not(hasValue), parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<Currency, bool>>(hasValue, parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for numeric field '{condition.Field}'.")
            };
        }

        private static IQueryable<Currency> ApplyNullableDoubleCondition(
            IQueryable<Currency> query,
            Expression<Func<Currency, double?>> selector,
            FilterConditionDto condition,
            string op)
        {
            var value = GetNullableDoubleValue(condition);
            var values = GetDoubleValues(condition);
            var parameter = selector.Parameters[0];
            var member = selector.Body;
            var hasValue = Expression.Property(member, nameof(Nullable<double>.HasValue));
            var memberValue = Expression.Property(member, nameof(Nullable<double>.Value));

            return op switch
            {
                "eq" => query.Where(Expression.Lambda<Func<Currency, bool>>(
                    Expression.Equal(member, Expression.Constant(value, typeof(double?))),
                    parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Currency, bool>>(
                    Expression.NotEqual(member, Expression.Constant(value, typeof(double?))),
                    parameter)),
                "gt" when value.HasValue => query.Where(BuildNullableDoubleComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThan, value.Value)),
                "gte" when value.HasValue => query.Where(BuildNullableDoubleComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThanOrEqual, value.Value)),
                "lt" when value.HasValue => query.Where(BuildNullableDoubleComparison(parameter, hasValue, memberValue, ExpressionType.LessThan, value.Value)),
                "lte" when value.HasValue => query.Where(BuildNullableDoubleComparison(parameter, hasValue, memberValue, ExpressionType.LessThanOrEqual, value.Value)),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Currency, bool>>(
                    Expression.AndAlso(
                        hasValue,
                        Expression.Call(
                            Expression.Constant(values),
                            typeof(List<double>).GetMethod(nameof(List<double>.Contains), new[] { typeof(double) })!,
                            memberValue)),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<Currency, bool>>(Expression.Not(hasValue), parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<Currency, bool>>(hasValue, parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for numeric field '{condition.Field}'.")
            };
        }

        private static IQueryable<Currency> ApplyBoolCondition(
            IQueryable<Currency> query,
            Expression<Func<Currency, bool>> selector,
            FilterConditionDto condition,
            string op)
        {
            var value = GetRequiredBoolValue(condition);
            var parameter = selector.Parameters[0];
            var member = selector.Body;
            var constant = Expression.Constant(value);

            return op switch
            {
                "eq" => query.Where(Expression.Lambda<Func<Currency, bool>>(Expression.Equal(member, constant), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Currency, bool>>(Expression.NotEqual(member, constant), parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for boolean field '{condition.Field}'.")
            };
        }

        private static Expression<Func<Currency, bool>> BuildStringMethodCall(
            ParameterExpression parameter,
            Expression member,
            string value,
            string methodName)
        {
            var method = typeof(string).GetMethod(methodName, new[] { typeof(string) })!;
            var nullConstant = Expression.Constant(null, typeof(string));

            return Expression.Lambda<Func<Currency, bool>>(
                Expression.AndAlso(
                    Expression.NotEqual(member, nullConstant),
                    Expression.Call(member, method, Expression.Constant(value))),
                parameter);
        }

        private static Expression<Func<Currency, bool>> BuildNullableIntComparison(
            ParameterExpression parameter,
            MemberExpression hasValue,
            MemberExpression memberValue,
            ExpressionType comparison,
            int value)
        {
            var comparisonExpression = Expression.MakeBinary(comparison, memberValue, Expression.Constant(value));

            return Expression.Lambda<Func<Currency, bool>>(
                Expression.AndAlso(hasValue, comparisonExpression),
                parameter);
        }

        private static Expression<Func<Currency, bool>> BuildNullableDoubleComparison(
            ParameterExpression parameter,
            MemberExpression hasValue,
            MemberExpression memberValue,
            ExpressionType comparison,
            double value)
        {
            var comparisonExpression = Expression.MakeBinary(comparison, memberValue, Expression.Constant(value));

            return Expression.Lambda<Func<Currency, bool>>(
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

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
            {
                return number;
            }

            throw new ArgumentException($"Filter field '{condition.Field}' requires an integer value.");
        }

        private static double? GetNullableDoubleValue(FilterConditionDto condition)
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

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out number))
            {
                return number;
            }

            throw new ArgumentException($"Filter field '{condition.Field}' requires a numeric value.");
        }

        private static bool GetRequiredBoolValue(FilterConditionDto condition)
        {
            if (!condition.Value.HasValue)
            {
                throw new ArgumentException($"Filter field '{condition.Field}' requires a boolean value.");
            }

            var value = condition.Value.Value;
            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
                _ => throw new ArgumentException($"Filter field '{condition.Field}' requires a boolean value.")
            };
        }

        private static List<string> GetStringValues(FilterConditionDto condition)
        {
            if (condition.Values is null || condition.Values.Count == 0)
            {
                return new List<string>();
            }

            return condition.Values
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString())
                .Where(x => x is not null)
                .Cast<string>()
                .ToList();
        }

        private static List<int> GetIntValues(FilterConditionDto condition)
        {
            if (condition.Values is null || condition.Values.Count == 0)
            {
                return new List<int>();
            }

            var values = new List<int>();
            foreach (var value in condition.Values)
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                {
                    values.Add(number);
                    continue;
                }

                if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
                {
                    values.Add(number);
                }
            }

            return values;
        }

        private static List<double> GetDoubleValues(FilterConditionDto condition)
        {
            if (condition.Values is null || condition.Values.Count == 0)
            {
                return new List<double>();
            }

            var values = new List<double>();
            foreach (var value in condition.Values)
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
                {
                    values.Add(number);
                    continue;
                }

                if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out number))
                {
                    values.Add(number);
                }
            }

            return values;
        }

        private static CurrencyDto MapToDto(Currency source)
        {
            return new CurrencyDto
            {
                Id = source.Id,
                Name = source.Name,
                TranslationCode = source.TranslationCode,
                IsDefault = source.IsDefault,
                RateToSek = source.RateToSek,
                Active = source.Active,
                RateStockValue = source.RateStockValue,
                OldDbId = source.OldDbId,
                SpcsKey = source.SpcsKey,
                WarningTolerancePercent = source.WarningTolerancePercent
            };
        }
    }
}