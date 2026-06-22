using System.Linq.Expressions;
using System.Text.RegularExpressions;
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
    public class ConstructionsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<ConstructionsController> _logger;

        public ConstructionsController(ApplicationDbContext dbContext, ILogger<ConstructionsController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ConstructionDto>> GetById(int id)
        {
            var entity = await _dbContext.Constructions
                .AsNoTracking()
                .Include(x => x.Products)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            var translations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, entity.TranslationCode);
            return Ok(MapToDto(entity, translations));
        }

        [HttpPost]
        public async Task<ActionResult<ConstructionDto>> Create([FromBody] CreateConstructionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = new Construction
            {
                Name = request.Name,
                TranslationCode = request.TranslationCode,
                Active = request.Active,
                IsPackaging = request.IsPackaging
            };

            _dbContext.Constructions.Add(entity);
            await _dbContext.SaveChangesAsync();

            entity.TranslationCode = await EntityTranslationService.UpsertTranslationsAsync(_dbContext, entity.TranslationCode, request.Translations);
            await _dbContext.SaveChangesAsync();

            var translations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, entity.TranslationCode);

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(entity, translations));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ConstructionDto>> Update(int id, [FromBody] UpdateConstructionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = await _dbContext.Constructions.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            entity.Name = request.Name;
            entity.TranslationCode = request.TranslationCode;
            entity.Active = request.Active ?? false;
            entity.IsPackaging = request.IsPackaging;

            entity.TranslationCode = await EntityTranslationService.UpsertTranslationsAsync(_dbContext, entity.TranslationCode, request.Translations);

            await _dbContext.SaveChangesAsync();

            var translations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, entity.TranslationCode);

            return Ok(MapToDto(entity, translations));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _dbContext.Constructions.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            _dbContext.Constructions.Remove(entity);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Delete conflict for construction {ConstructionId}", id);

                var details = new List<string>();
                var productCount = await _dbContext.Products
                    .AsNoTracking()
                    .CountAsync(x => x.ConstructionId == id);

                if (productCount > 0)
                {
                    details.Add($"Anvands av {productCount} produkt(er).");
                }

                var table = TryExtractSqlConflictTable(ex);
                if (!string.IsNullOrWhiteSpace(table))
                {
                    details.Add($"Konflikt i tabell {table}.");
                }

                if (details.Count == 0)
                {
                    details.Add("Posten ar relaterad till annan data och kan inte raderas.");
                }

                return Conflict(new
                {
                    message = "Konstruktionen kan inte raderas eftersom den refereras av annan data.",
                    details
                });
            }

            return NoContent();
        }

        private static string? TryExtractSqlConflictTable(DbUpdateException ex)
        {
            var fullMessage = ex.InnerException?.Message ?? ex.Message;
            if (string.IsNullOrWhiteSpace(fullMessage))
            {
                return null;
            }

            var match = Regex.Match(fullMessage, "table '([^']+)'", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            return match.Groups[1].Value;
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<ConstructionDto>>> Search([FromBody] SearchConstructionsRequest? request)
        {
            IQueryable<Construction> query = _dbContext.Constructions
                .AsNoTracking()
                .Include(x => x.Products);
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
                        _logger.LogWarning(ex, "Invalid construction search condition for field {Field}", condition.Field);
                        return BadRequest(new { message = ex.Message, field = condition.Field });
                    }
                }
            }

            IOrderedQueryable<Construction> orderedQuery;
            try
            {
                orderedQuery = ApplyOrdering(query, request?.OrderBy);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid construction order by request");
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
                .Select(x => MapToDto(x))
                .ToList();

            return Ok(new PagedResultDto<ConstructionDto>
            {
                Items = items,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        private static IOrderedQueryable<Construction> ApplyOrdering(IQueryable<Construction> query, List<SortRequest>? orderBy)
        {
            if (orderBy is null || orderBy.Count == 0)
            {
                return query.OrderBy(x => x.Id);
            }

            IOrderedQueryable<Construction>? ordered = null;

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
                    "active" => ApplyOrder(ordered, query, x => x.Active, isDescending),
                    "olddbid" => ApplyOrder(ordered, query, x => x.OldDbId, isDescending),
                    "ispackaging" => ApplyOrder(ordered, query, x => x.IsPackaging, isDescending),
                    _ => throw new ArgumentException($"Unsupported order by field '{sort.Field}'.")
                };
            }

            return ordered ?? query.OrderBy(x => x.Id);
        }

        private static IOrderedQueryable<Construction> ApplyOrder<TKey>(
            IOrderedQueryable<Construction>? ordered,
            IQueryable<Construction> source,
            Expression<Func<Construction, TKey>> keySelector,
            bool isDescending)
        {
            if (ordered is null)
            {
                return isDescending ? source.OrderByDescending(keySelector) : source.OrderBy(keySelector);
            }

            return isDescending ? ordered.ThenByDescending(keySelector) : ordered.ThenBy(keySelector);
        }

        private static IQueryable<Construction> ApplyCondition(IQueryable<Construction> query, FilterConditionDto condition)
        {
            var field = condition.Field.Trim().ToLowerInvariant();
            var op = condition.Operator.Trim().ToLowerInvariant();

            return field switch
            {
                "id" => ApplyIntCondition(query, x => x.Id, condition, op),
                "name" => ApplyStringCondition(query, x => x.Name, condition, op),
                "translationcode" => ApplyNullableIntCondition(query, x => x.TranslationCode, condition, op),
                "active" => ApplyBoolCondition(query, x => x.Active, condition, op),
                "olddbid" => ApplyNullableIntCondition(query, x => x.OldDbId, condition, op),
                "ispackaging" => ApplyNullableBoolCondition(query, x => x.IsPackaging, condition, op),
                _ => throw new ArgumentException($"Unsupported filter field '{condition.Field}'.")
            };
        }

        private static IQueryable<Construction> ApplyStringCondition(
            IQueryable<Construction> query,
            Expression<Func<Construction, string?>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<Construction, bool>>(
                    Expression.Equal(member, Expression.Constant(value, typeof(string))), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Construction, bool>>(
                    Expression.NotEqual(member, Expression.Constant(value, typeof(string))), parameter)),
                "contains" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.Contains))),
                "startswith" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.StartsWith))),
                "endswith" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.EndsWith))),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Construction, bool>>(
                    Expression.AndAlso(
                        Expression.NotEqual(member, nullConstant),
                        Expression.Call(
                            Expression.Constant(values),
                            typeof(List<string>).GetMethod(nameof(List<string>.Contains), new[] { typeof(string) })!,
                            member)),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<Construction, bool>>(
                    Expression.Equal(member, nullConstant), parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<Construction, bool>>(
                    Expression.NotEqual(member, nullConstant), parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for string field '{condition.Field}'.")
            };
        }

        private static IQueryable<Construction> ApplyIntCondition(
            IQueryable<Construction> query,
            Expression<Func<Construction, int>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<Construction, bool>>(Expression.Equal(member, constant), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Construction, bool>>(Expression.NotEqual(member, constant), parameter)),
                "gt" => query.Where(Expression.Lambda<Func<Construction, bool>>(Expression.GreaterThan(member, constant), parameter)),
                "gte" => query.Where(Expression.Lambda<Func<Construction, bool>>(Expression.GreaterThanOrEqual(member, constant), parameter)),
                "lt" => query.Where(Expression.Lambda<Func<Construction, bool>>(Expression.LessThan(member, constant), parameter)),
                "lte" => query.Where(Expression.Lambda<Func<Construction, bool>>(Expression.LessThanOrEqual(member, constant), parameter)),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Construction, bool>>(
                    Expression.Call(
                        Expression.Constant(values),
                        typeof(List<int>).GetMethod(nameof(List<int>.Contains), new[] { typeof(int) })!,
                        member),
                    parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for numeric field '{condition.Field}'.")
            };
        }

        private static IQueryable<Construction> ApplyNullableIntCondition(
            IQueryable<Construction> query,
            Expression<Func<Construction, int?>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<Construction, bool>>(
                    Expression.Equal(member, Expression.Constant(value, typeof(int?))), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Construction, bool>>(
                    Expression.NotEqual(member, Expression.Constant(value, typeof(int?))), parameter)),
                "gt" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThan, value.Value)),
                "gte" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThanOrEqual, value.Value)),
                "lt" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.LessThan, value.Value)),
                "lte" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.LessThanOrEqual, value.Value)),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Construction, bool>>(
                    Expression.AndAlso(
                        hasValue,
                        Expression.Call(
                            Expression.Constant(values),
                            typeof(List<int>).GetMethod(nameof(List<int>.Contains), new[] { typeof(int) })!,
                            memberValue)),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<Construction, bool>>(Expression.Not(hasValue), parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<Construction, bool>>(hasValue, parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for numeric field '{condition.Field}'.")
            };
        }

        private static IQueryable<Construction> ApplyBoolCondition(
            IQueryable<Construction> query,
            Expression<Func<Construction, bool>> selector,
            FilterConditionDto condition,
            string op)
        {
            var value = GetRequiredBoolValue(condition);
            var parameter = selector.Parameters[0];
            var member = selector.Body;
            var constant = Expression.Constant(value);

            return op switch
            {
                "eq" => query.Where(Expression.Lambda<Func<Construction, bool>>(Expression.Equal(member, constant), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Construction, bool>>(Expression.NotEqual(member, constant), parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for boolean field '{condition.Field}'.")
            };
        }

        private static IQueryable<Construction> ApplyNullableBoolCondition(
            IQueryable<Construction> query,
            Expression<Func<Construction, bool?>> selector,
            FilterConditionDto condition,
            string op)
        {
            var parameter = selector.Parameters[0];
            var member = selector.Body;
            var hasValue = Expression.Property(member, nameof(Nullable<bool>.HasValue));

            return op switch
            {
                "eq" => query.Where(Expression.Lambda<Func<Construction, bool>>(
                    Expression.Equal(member, Expression.Constant(GetNullableBoolValue(condition), typeof(bool?))), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Construction, bool>>(
                    Expression.NotEqual(member, Expression.Constant(GetNullableBoolValue(condition), typeof(bool?))), parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<Construction, bool>>(Expression.Not(hasValue), parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<Construction, bool>>(hasValue, parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for boolean field '{condition.Field}'.")
            };
        }

        private static Expression<Func<Construction, bool>> BuildStringMethodCall(
            ParameterExpression parameter,
            Expression member,
            string value,
            string methodName)
        {
            var method = typeof(string).GetMethod(methodName, new[] { typeof(string) })!;
            var nullConstant = Expression.Constant(null, typeof(string));

            return Expression.Lambda<Func<Construction, bool>>(
                Expression.AndAlso(
                    Expression.NotEqual(member, nullConstant),
                    Expression.Call(member, method, Expression.Constant(value))),
                parameter);
        }

        private static Expression<Func<Construction, bool>> BuildNullableIntComparison(
            ParameterExpression parameter,
            MemberExpression hasValue,
            MemberExpression memberValue,
            ExpressionType comparison,
            int value)
        {
            return Expression.Lambda<Func<Construction, bool>>(
                Expression.AndAlso(hasValue, Expression.MakeBinary(comparison, memberValue, Expression.Constant(value))),
                parameter);
        }

        private static string? GetSingleStringValue(FilterConditionDto condition)
        {
            if (!condition.Value.HasValue) return null;
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
            if (condition.Values is null) return new List<string>();
            return condition.Values
                .Where(v => v.ValueKind == JsonValueKind.String)
                .Select(v => v.GetString()!)
                .ToList();
        }

        private static int GetRequiredIntValue(FilterConditionDto condition)
        {
            var value = GetNullableIntValue(condition);
            if (!value.HasValue)
                throw new ArgumentException($"Filter field '{condition.Field}' requires an integer value.");
            return value.Value;
        }

        private static int? GetNullableIntValue(FilterConditionDto condition)
        {
            if (!condition.Value.HasValue) return null;
            var value = condition.Value.Value;
            if (value.ValueKind == JsonValueKind.Null) return null;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
            throw new ArgumentException($"Filter field '{condition.Field}' requires an integer value.");
        }

        private static List<int> GetIntValues(FilterConditionDto condition)
        {
            if (condition.Values is null) return new List<int>();
            return condition.Values
                .Where(v => v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out _))
                .Select(v => v.GetInt32())
                .ToList();
        }

        private static bool GetRequiredBoolValue(FilterConditionDto condition)
        {
            if (!condition.Value.HasValue)
                throw new ArgumentException($"Filter field '{condition.Field}' requires a boolean value.");
            var value = condition.Value.Value;
            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new ArgumentException($"Filter field '{condition.Field}' requires a boolean value.")
            };
        }

        private static bool? GetNullableBoolValue(FilterConditionDto condition)
        {
            if (!condition.Value.HasValue) return null;
            var value = condition.Value.Value;
            return value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new ArgumentException($"Filter field '{condition.Field}' requires a boolean value.")
            };
        }

        private static ConstructionDto MapToDto(Construction entity, List<EntityTranslationDto>? translations = null) => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            TranslationCode = entity.TranslationCode,
            Translations = translations ?? new List<EntityTranslationDto>(),
            Active = entity.Active,
            OldDbId = entity.OldDbId,
            IsPackaging = entity.IsPackaging,
            Products = entity.Products
                .OrderBy(x => x.Id)
                .Select(x => new ProductLookupDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    ProductCode = x.ProductCode
                })
                .ToList()
        };
    }
}
