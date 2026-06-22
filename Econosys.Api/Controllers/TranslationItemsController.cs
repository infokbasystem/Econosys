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
    public class TranslationItemsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<TranslationItemsController> _logger;

        public TranslationItemsController(ApplicationDbContext dbContext, ILogger<TranslationItemsController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<TranslationItemDto>> GetById(int id)
        {
            var entity = await _dbContext.TranslationItems
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(entity));
        }

        [HttpPost]
        public async Task<ActionResult<TranslationItemDto>> Create([FromBody] CreateTranslationItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = new TranslationItem
            {
                TranslationCode = request.TranslationCode,
                LangCode = request.LangCode,
                Translation = request.Translation,
                OldDbId = null,
                TranslationHtml = request.TranslationHtml
            };

            _dbContext.TranslationItems.Add(entity);
            await _dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(entity));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<TranslationItemDto>> Update(int id, [FromBody] UpdateTranslationItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = await _dbContext.TranslationItems.FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null)
            {
                return NotFound();
            }

            entity.TranslationCode = request.TranslationCode;
            entity.LangCode = request.LangCode;
            entity.Translation = request.Translation;
            entity.TranslationHtml = request.TranslationHtml;

            await _dbContext.SaveChangesAsync();

            return Ok(MapToDto(entity));
        }

        [HttpPost("batch-upsert")]
        public async Task<ActionResult<List<TranslationItemDto>>> BatchUpsert([FromBody] BatchUpsertTranslationItemsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var items = request?.Items ?? new List<BatchUpsertTranslationItemRequestItem>();
            if (items.Count == 0)
            {
                return Ok(new List<TranslationItemDto>());
            }

            var normalizedItems = new List<BatchUpsertTranslationItemRequestItem>(items.Count);
            foreach (var item in items)
            {
                if (item.TranslationCode is null)
                {
                    return BadRequest(new { message = "TranslationCode is required for batch upsert." });
                }

                var normalizedLangCode = (item.LangCode ?? string.Empty).Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(normalizedLangCode))
                {
                    return BadRequest(new { message = "LangCode is required for batch upsert." });
                }

                normalizedItems.Add(new BatchUpsertTranslationItemRequestItem
                {
                    Id = item.Id,
                    TranslationCode = item.TranslationCode,
                    LangCode = normalizedLangCode,
                    Translation = item.Translation,
                    TranslationHtml = item.TranslationHtml
                });
            }

            var ids = normalizedItems
                .Where(x => x.Id.HasValue && x.Id.Value > 0)
                .Select(x => x.Id!.Value)
                .Distinct()
                .ToList();

            var existingById = ids.Count == 0
                ? new Dictionary<int, TranslationItem>()
                : await _dbContext.TranslationItems
                    .Where(x => ids.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id);

            var codes = normalizedItems
                .Where(x => x.TranslationCode.HasValue)
                .Select(x => x.TranslationCode!.Value)
                .Distinct()
                .ToList();

            var langs = normalizedItems
                .Select(x => x.LangCode)
                .Where(x => x is not null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingByCodeAndLang = (codes.Count == 0 || langs.Count == 0)
                ? new Dictionary<(int TranslationCode, string LangCode), TranslationItem>()
                : (await _dbContext.TranslationItems
                    .Where(x => x.TranslationCode.HasValue
                        && codes.Contains(x.TranslationCode.Value)
                        && x.LangCode != null
                        && langs.Contains(x.LangCode))
                    .ToListAsync())
                    .GroupBy(x => (x.TranslationCode!.Value, x.LangCode!.Trim().ToUpperInvariant()))
                    .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Id).First());

            var upserted = new List<TranslationItem>(normalizedItems.Count);

            foreach (var item in normalizedItems)
            {
                var translationCode = item.TranslationCode!.Value;
                var langCode = item.LangCode!;

                TranslationItem? entity = null;

                if (item.Id.HasValue && item.Id.Value > 0)
                {
                    existingById.TryGetValue(item.Id.Value, out entity);
                }

                if (entity is null)
                {
                    existingByCodeAndLang.TryGetValue((translationCode, langCode), out entity);
                }

                if (entity is null)
                {
                    entity = new TranslationItem();
                    _dbContext.TranslationItems.Add(entity);
                }

                entity.TranslationCode = translationCode;
                entity.LangCode = langCode;
                entity.Translation = item.Translation;
                entity.TranslationHtml = item.TranslationHtml;

                upserted.Add(entity);
            }

            await _dbContext.SaveChangesAsync();

            return Ok(upserted.Select(MapToDto).ToList());
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _dbContext.TranslationItems.FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null)
            {
                return NotFound();
            }

            _dbContext.TranslationItems.Remove(entity);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<TranslationItemDto>>> Search([FromBody] SearchTranslationItemsRequest? request)
        {
            IQueryable<TranslationItem> query = _dbContext.TranslationItems.AsNoTracking();
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
                        _logger.LogWarning(ex, "Invalid translation item search condition for field {Field}", condition.Field);
                        return BadRequest(new { message = ex.Message, field = condition.Field });
                    }
                }
            }

            IOrderedQueryable<TranslationItem> orderedQuery;
            try
            {
                orderedQuery = ApplyOrdering(query, request?.OrderBy);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid translation item order by request");
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

            return Ok(new PagedResultDto<TranslationItemDto>
            {
                Items = items,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        private static IOrderedQueryable<TranslationItem> ApplyOrdering(IQueryable<TranslationItem> query, List<SortRequest>? orderBy)
        {
            if (orderBy is null || orderBy.Count == 0)
            {
                return query.OrderBy(x => x.Id);
            }

            IOrderedQueryable<TranslationItem>? ordered = null;

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
                    "translationcode" => ApplyOrder(ordered, query, x => x.TranslationCode, isDescending),
                    "langcode" => ApplyOrder(ordered, query, x => x.LangCode, isDescending),
                    "translation" => ApplyOrder(ordered, query, x => x.Translation, isDescending),
                    "olddbid" => ApplyOrder(ordered, query, x => x.OldDbId, isDescending),
                    "translationhtml" => ApplyOrder(ordered, query, x => x.TranslationHtml, isDescending),
                    _ => throw new ArgumentException($"Unsupported order by field '{sort.Field}'.")
                };
            }

            return ordered ?? query.OrderBy(x => x.Id);
        }

        private static IOrderedQueryable<TranslationItem> ApplyOrder<TKey>(
            IOrderedQueryable<TranslationItem>? ordered,
            IQueryable<TranslationItem> source,
            Expression<Func<TranslationItem, TKey>> keySelector,
            bool isDescending)
        {
            if (ordered is null)
            {
                return isDescending ? source.OrderByDescending(keySelector) : source.OrderBy(keySelector);
            }

            return isDescending ? ordered.ThenByDescending(keySelector) : ordered.ThenBy(keySelector);
        }

        private static IQueryable<TranslationItem> ApplyCondition(IQueryable<TranslationItem> query, FilterConditionDto condition)
        {
            var field = condition.Field.Trim().ToLowerInvariant();
            var op = condition.Operator.Trim().ToLowerInvariant();

            return field switch
            {
                "id" => ApplyIntCondition(query, x => x.Id, condition, op),
                "translationcode" => ApplyNullableIntCondition(query, x => x.TranslationCode, condition, op),
                "langcode" => ApplyStringCondition(query, x => x.LangCode, condition, op),
                "translation" => ApplyStringCondition(query, x => x.Translation, condition, op),
                "olddbid" => ApplyNullableIntCondition(query, x => x.OldDbId, condition, op),
                "translationhtml" => ApplyStringCondition(query, x => x.TranslationHtml, condition, op),
                _ => throw new ArgumentException($"Unsupported filter field '{condition.Field}'.")
            };
        }

        private static IQueryable<TranslationItem> ApplyStringCondition(
            IQueryable<TranslationItem> query,
            Expression<Func<TranslationItem, string?>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<TranslationItem, bool>>(
                    Expression.Equal(member, Expression.Constant(value, typeof(string))),
                    parameter)),
                "neq" => query.Where(Expression.Lambda<Func<TranslationItem, bool>>(
                    Expression.NotEqual(member, Expression.Constant(value, typeof(string))),
                    parameter)),
                "contains" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.Contains))),
                "startswith" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.StartsWith))),
                "endswith" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.EndsWith))),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<TranslationItem, bool>>(
                    Expression.AndAlso(
                        Expression.NotEqual(member, nullConstant),
                        Expression.Call(
                            Expression.Constant(values),
                            typeof(List<string>).GetMethod(nameof(List<string>.Contains), new[] { typeof(string) })!,
                            member)),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<TranslationItem, bool>>(
                    Expression.Equal(member, nullConstant),
                    parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<TranslationItem, bool>>(
                    Expression.NotEqual(member, nullConstant),
                    parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for string field '{condition.Field}'.")
            };
        }

        private static IQueryable<TranslationItem> ApplyIntCondition(
            IQueryable<TranslationItem> query,
            Expression<Func<TranslationItem, int>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<TranslationItem, bool>>(Expression.Equal(member, constant), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<TranslationItem, bool>>(Expression.NotEqual(member, constant), parameter)),
                "gt" => query.Where(Expression.Lambda<Func<TranslationItem, bool>>(Expression.GreaterThan(member, constant), parameter)),
                "gte" => query.Where(Expression.Lambda<Func<TranslationItem, bool>>(Expression.GreaterThanOrEqual(member, constant), parameter)),
                "lt" => query.Where(Expression.Lambda<Func<TranslationItem, bool>>(Expression.LessThan(member, constant), parameter)),
                "lte" => query.Where(Expression.Lambda<Func<TranslationItem, bool>>(Expression.LessThanOrEqual(member, constant), parameter)),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<TranslationItem, bool>>(
                    Expression.Call(
                        Expression.Constant(values),
                        typeof(List<int>).GetMethod(nameof(List<int>.Contains), new[] { typeof(int) })!,
                        member),
                    parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for numeric field '{condition.Field}'.")
            };
        }

        private static IQueryable<TranslationItem> ApplyNullableIntCondition(
            IQueryable<TranslationItem> query,
            Expression<Func<TranslationItem, int?>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<TranslationItem, bool>>(
                    Expression.Equal(member, Expression.Constant(value, typeof(int?))),
                    parameter)),
                "neq" => query.Where(Expression.Lambda<Func<TranslationItem, bool>>(
                    Expression.NotEqual(member, Expression.Constant(value, typeof(int?))),
                    parameter)),
                "gt" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThan, value.Value)),
                "gte" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThanOrEqual, value.Value)),
                "lt" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.LessThan, value.Value)),
                "lte" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.LessThanOrEqual, value.Value)),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<TranslationItem, bool>>(
                    Expression.AndAlso(
                        hasValue,
                        Expression.Call(
                            Expression.Constant(values),
                            typeof(List<int>).GetMethod(nameof(List<int>.Contains), new[] { typeof(int) })!,
                            memberValue)),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<TranslationItem, bool>>(Expression.Not(hasValue), parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<TranslationItem, bool>>(hasValue, parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for numeric field '{condition.Field}'.")
            };
        }

        private static Expression<Func<TranslationItem, bool>> BuildStringMethodCall(
            ParameterExpression parameter,
            Expression member,
            string value,
            string methodName)
        {
            var method = typeof(string).GetMethod(methodName, new[] { typeof(string) })!;
            var nullConstant = Expression.Constant(null, typeof(string));

            return Expression.Lambda<Func<TranslationItem, bool>>(
                Expression.AndAlso(
                    Expression.NotEqual(member, nullConstant),
                    Expression.Call(member, method, Expression.Constant(value))),
                parameter);
        }

        private static Expression<Func<TranslationItem, bool>> BuildNullableIntComparison(
            ParameterExpression parameter,
            MemberExpression hasValue,
            MemberExpression memberValue,
            ExpressionType comparison,
            int value)
        {
            var comparisonExpression = Expression.MakeBinary(comparison, memberValue, Expression.Constant(value));

            return Expression.Lambda<Func<TranslationItem, bool>>(
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

        private static TranslationItemDto MapToDto(TranslationItem source)
        {
            return new TranslationItemDto
            {
                Id = source.Id,
                TranslationCode = source.TranslationCode,
                LangCode = source.LangCode,
                Translation = source.Translation,
                OldDbId = source.OldDbId,
                TranslationHtml = source.TranslationHtml
            };
        }
    }
}