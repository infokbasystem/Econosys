using System.Linq.Expressions;
using System.Text.Json;
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
    public class UnitsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<UnitsController> _logger;

        public UnitsController(ApplicationDbContext dbContext, ILogger<UnitsController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<UnitDto>> GetById(int id)
        {
            var unit = await _dbContext.Units
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (unit is null)
            {
                return NotFound();
            }

            var translations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, unit.TranslationCode);
            return Ok(MapToDto(unit, translations));
        }

        [HttpPost]
        public async Task<ActionResult<UnitDto>> Create([FromBody] CreateUnitRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var unit = new Unit
            {
                Name = request.Name,
                TranslationCode = request.TranslationCode,
                Multiplicator = request.Multiplicator,
                IsDefault = request.IsDefault,
                Active = request.Active,
                NrOfCalcDecimals = request.NrOfCalcDecimals,
                NrOfCalcDecimalsQty = request.NrOfCalcDecimalsQty
            };

            _dbContext.Units.Add(unit);
            await _dbContext.SaveChangesAsync();

            unit.TranslationCode = await EntityTranslationService.UpsertTranslationsAsync(_dbContext, unit.TranslationCode, request.Translations);
            await _dbContext.SaveChangesAsync();

            var translations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, unit.TranslationCode);

            var response = MapToDto(unit, translations);
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<UnitDto>> Update(int id, [FromBody] UpdateUnitRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var unit = await _dbContext.Units.FirstOrDefaultAsync(x => x.Id == id);

            if (unit is null)
            {
                return NotFound();
            }

            unit.Name = request.Name;
            unit.TranslationCode = request.TranslationCode;
            unit.Multiplicator = request.Multiplicator;
            unit.IsDefault = request.IsDefault ?? false;
            unit.Active = request.Active ?? false;
            unit.NrOfCalcDecimals = request.NrOfCalcDecimals;
            unit.NrOfCalcDecimalsQty = request.NrOfCalcDecimalsQty;

            unit.TranslationCode = await EntityTranslationService.UpsertTranslationsAsync(_dbContext, unit.TranslationCode, request.Translations);

            await _dbContext.SaveChangesAsync();

            var translations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, unit.TranslationCode);

            return Ok(MapToDto(unit, translations));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var unit = await _dbContext.Units.FirstOrDefaultAsync(x => x.Id == id);

            if (unit is null)
            {
                return NotFound();
            }

            _dbContext.Units.Remove(unit);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<UnitDto>>> Search([FromBody] SearchUnitsRequest? request)
        {
            IQueryable<Unit> query = _dbContext.Units.AsNoTracking();
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
                        _logger.LogWarning(ex, "Invalid unit search condition for field {Field}", condition.Field);
                        return BadRequest(new { message = ex.Message, field = condition.Field });
                    }
                }
            }

            IOrderedQueryable<Unit> orderedQuery;
            try
            {
                orderedQuery = ApplyOrdering(query, request?.OrderBy);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid unit order by request");
                return BadRequest(new { message = ex.Message });
            }

            var totalCount = await query.CountAsync();
            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pagination.PageSize);

            var units = (await orderedQuery
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync())
                .Select(x => MapToDto(x))
                .ToList();

            return Ok(new PagedResultDto<UnitDto>
            {
                Items = units,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        private static IOrderedQueryable<Unit> ApplyOrdering(IQueryable<Unit> query, List<SortRequest>? orderBy)
        {
            if (orderBy is null || orderBy.Count == 0)
            {
                return query.OrderBy(x => x.Id);
            }

            IOrderedQueryable<Unit>? ordered = null;

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
                    "multiplicator" => ApplyOrder(ordered, query, x => x.Multiplicator, isDescending),
                    "isdefault" => ApplyOrder(ordered, query, x => x.IsDefault, isDescending),
                    "active" => ApplyOrder(ordered, query, x => x.Active, isDescending),
                    "nrofcalcdecimals" => ApplyOrder(ordered, query, x => x.NrOfCalcDecimals, isDescending),
                    "nrofcalcdecimalsqty" => ApplyOrder(ordered, query, x => x.NrOfCalcDecimalsQty, isDescending),
                    "olddbid" => ApplyOrder(ordered, query, x => x.OldDbId, isDescending),
                    _ => throw new ArgumentException($"Unsupported order by field '{sort.Field}'.")
                };
            }

            return ordered ?? query.OrderBy(x => x.Id);
        }

        private static IOrderedQueryable<Unit> ApplyOrder<TKey>(
            IOrderedQueryable<Unit>? ordered,
            IQueryable<Unit> source,
            Expression<Func<Unit, TKey>> keySelector,
            bool isDescending)
        {
            if (ordered is null)
            {
                return isDescending ? source.OrderByDescending(keySelector) : source.OrderBy(keySelector);
            }

            return isDescending ? ordered.ThenByDescending(keySelector) : ordered.ThenBy(keySelector);
        }

        private static IQueryable<Unit> ApplyCondition(IQueryable<Unit> query, FilterConditionDto condition)
        {
            var field = condition.Field.Trim().ToLowerInvariant();
            var op = condition.Operator.Trim().ToLowerInvariant();

            return field switch
            {
                "id" => ApplyIntCondition(query, x => x.Id, condition, op),
                "name" => ApplyStringCondition(query, x => x.Name, condition, op),
                "translationcode" => ApplyNullableIntCondition(query, x => x.TranslationCode, condition, op),
                "multiplicator" => ApplyNullableShortCondition(query, x => x.Multiplicator, condition, op),
                "isdefault" => ApplyBoolCondition(query, x => x.IsDefault, condition, op),
                "active" => ApplyBoolCondition(query, x => x.Active, condition, op),
                "nrofcalcdecimals" => ApplyNullableShortCondition(query, x => x.NrOfCalcDecimals, condition, op),
                "nrofcalcdecimalsqty" => ApplyNullableShortCondition(query, x => x.NrOfCalcDecimalsQty, condition, op),
                "olddbid" => ApplyNullableIntCondition(query, x => x.OldDbId, condition, op),
                _ => throw new ArgumentException($"Unsupported filter field '{condition.Field}'.")
            };
        }

        private static IQueryable<Unit> ApplyStringCondition(
            IQueryable<Unit> query,
            Expression<Func<Unit, string?>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<Unit, bool>>(
                    Expression.Equal(member, Expression.Constant(value, typeof(string))),
                    parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Unit, bool>>(
                    Expression.NotEqual(member, Expression.Constant(value, typeof(string))),
                    parameter)),
                "contains" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.Contains))),
                "startswith" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.StartsWith))),
                "endswith" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.EndsWith))),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Unit, bool>>(
                    Expression.AndAlso(
                        Expression.NotEqual(member, nullConstant),
                        Expression.Call(
                            Expression.Constant(values),
                            typeof(List<string>).GetMethod(nameof(List<string>.Contains), new[] { typeof(string) })!,
                            member)),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<Unit, bool>>(
                    Expression.Equal(member, nullConstant),
                    parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<Unit, bool>>(
                    Expression.NotEqual(member, nullConstant),
                    parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for string field '{condition.Field}'.")
            };
        }

        private static IQueryable<Unit> ApplyIntCondition(
            IQueryable<Unit> query,
            Expression<Func<Unit, int>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<Unit, bool>>(Expression.Equal(member, constant), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Unit, bool>>(Expression.NotEqual(member, constant), parameter)),
                "gt" => query.Where(Expression.Lambda<Func<Unit, bool>>(Expression.GreaterThan(member, constant), parameter)),
                "gte" => query.Where(Expression.Lambda<Func<Unit, bool>>(Expression.GreaterThanOrEqual(member, constant), parameter)),
                "lt" => query.Where(Expression.Lambda<Func<Unit, bool>>(Expression.LessThan(member, constant), parameter)),
                "lte" => query.Where(Expression.Lambda<Func<Unit, bool>>(Expression.LessThanOrEqual(member, constant), parameter)),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Unit, bool>>(
                    Expression.Call(
                        Expression.Constant(values),
                        typeof(List<int>).GetMethod(nameof(List<int>.Contains), new[] { typeof(int) })!,
                        member),
                    parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for numeric field '{condition.Field}'.")
            };
        }

        private static IQueryable<Unit> ApplyNullableIntCondition(
            IQueryable<Unit> query,
            Expression<Func<Unit, int?>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<Unit, bool>>(
                    Expression.Equal(member, Expression.Constant(value, typeof(int?))),
                    parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Unit, bool>>(
                    Expression.NotEqual(member, Expression.Constant(value, typeof(int?))),
                    parameter)),
                "gt" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThan, value.Value)),
                "gte" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThanOrEqual, value.Value)),
                "lt" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.LessThan, value.Value)),
                "lte" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.LessThanOrEqual, value.Value)),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Unit, bool>>(
                    Expression.AndAlso(
                        hasValue,
                        Expression.Call(
                            Expression.Constant(values),
                            typeof(List<int>).GetMethod(nameof(List<int>.Contains), new[] { typeof(int) })!,
                            memberValue)),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<Unit, bool>>(Expression.Not(hasValue), parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<Unit, bool>>(hasValue, parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for numeric field '{condition.Field}'.")
            };
        }

        private static IQueryable<Unit> ApplyNullableShortCondition(
            IQueryable<Unit> query,
            Expression<Func<Unit, short?>> selector,
            FilterConditionDto condition,
            string op)
        {
            var value = GetNullableShortValue(condition);
            var values = GetShortValues(condition);
            var parameter = selector.Parameters[0];
            var member = selector.Body;
            var hasValue = Expression.Property(member, nameof(Nullable<short>.HasValue));
            var memberValue = Expression.Property(member, nameof(Nullable<short>.Value));

            return op switch
            {
                "eq" => query.Where(Expression.Lambda<Func<Unit, bool>>(
                    Expression.Equal(member, Expression.Constant(value, typeof(short?))),
                    parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Unit, bool>>(
                    Expression.NotEqual(member, Expression.Constant(value, typeof(short?))),
                    parameter)),
                "gt" when value.HasValue => query.Where(BuildNullableShortComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThan, value.Value)),
                "gte" when value.HasValue => query.Where(BuildNullableShortComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThanOrEqual, value.Value)),
                "lt" when value.HasValue => query.Where(BuildNullableShortComparison(parameter, hasValue, memberValue, ExpressionType.LessThan, value.Value)),
                "lte" when value.HasValue => query.Where(BuildNullableShortComparison(parameter, hasValue, memberValue, ExpressionType.LessThanOrEqual, value.Value)),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Unit, bool>>(
                    Expression.AndAlso(
                        hasValue,
                        Expression.Call(
                            Expression.Constant(values),
                            typeof(List<short>).GetMethod(nameof(List<short>.Contains), new[] { typeof(short) })!,
                            memberValue)),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<Unit, bool>>(Expression.Not(hasValue), parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<Unit, bool>>(hasValue, parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for numeric field '{condition.Field}'.")
            };
        }

        private static IQueryable<Unit> ApplyBoolCondition(
            IQueryable<Unit> query,
            Expression<Func<Unit, bool>> selector,
            FilterConditionDto condition,
            string op)
        {
            var value = GetRequiredBoolValue(condition);
            var parameter = selector.Parameters[0];
            var member = selector.Body;
            var constant = Expression.Constant(value);

            return op switch
            {
                "eq" => query.Where(Expression.Lambda<Func<Unit, bool>>(Expression.Equal(member, constant), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Unit, bool>>(Expression.NotEqual(member, constant), parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for boolean field '{condition.Field}'.")
            };
        }

        private static Expression<Func<Unit, bool>> BuildStringMethodCall(
            ParameterExpression parameter,
            Expression member,
            string value,
            string methodName)
        {
            var method = typeof(string).GetMethod(methodName, new[] { typeof(string) })!;
            var nullConstant = Expression.Constant(null, typeof(string));

            return Expression.Lambda<Func<Unit, bool>>(
                Expression.AndAlso(
                    Expression.NotEqual(member, nullConstant),
                    Expression.Call(member, method, Expression.Constant(value))),
                parameter);
        }

        private static Expression<Func<Unit, bool>> BuildNullableIntComparison(
            ParameterExpression parameter,
            MemberExpression hasValue,
            MemberExpression memberValue,
            ExpressionType comparison,
            int value)
        {
            var comparisonExpression = Expression.MakeBinary(comparison, memberValue, Expression.Constant(value));

            return Expression.Lambda<Func<Unit, bool>>(
                Expression.AndAlso(hasValue, comparisonExpression),
                parameter);
        }

        private static Expression<Func<Unit, bool>> BuildNullableShortComparison(
            ParameterExpression parameter,
            MemberExpression hasValue,
            MemberExpression memberValue,
            ExpressionType comparison,
            short value)
        {
            var comparisonExpression = Expression.MakeBinary(comparison, memberValue, Expression.Constant(value));

            return Expression.Lambda<Func<Unit, bool>>(
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

        private static short? GetNullableShortValue(FilterConditionDto condition)
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

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt16(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && short.TryParse(value.GetString(), out number))
            {
                return number;
            }

            throw new ArgumentException($"Filter field '{condition.Field}' requires a short value.");
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

        private static List<short> GetShortValues(FilterConditionDto condition)
        {
            if (condition.Values is null || condition.Values.Count == 0)
            {
                return new List<short>();
            }

            var values = new List<short>();
            foreach (var value in condition.Values)
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt16(out var number))
                {
                    values.Add(number);
                    continue;
                }

                if (value.ValueKind == JsonValueKind.String && short.TryParse(value.GetString(), out number))
                {
                    values.Add(number);
                }
            }

            return values;
        }

        private static UnitDto MapToDto(Unit source, List<EntityTranslationDto>? translations = null)
        {
            return new UnitDto
            {
                Id = source.Id,
                Name = source.Name,
                TranslationCode = source.TranslationCode,
            Translations = translations ?? new List<EntityTranslationDto>(),
                Multiplicator = source.Multiplicator,
                IsDefault = source.IsDefault,
                Active = source.Active,
                NrOfCalcDecimals = source.NrOfCalcDecimals,
                NrOfCalcDecimalsQty = source.NrOfCalcDecimalsQty,
                OldDbId = source.OldDbId
            };
        }
    }
}