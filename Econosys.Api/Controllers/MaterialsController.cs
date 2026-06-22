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
    public class MaterialsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<MaterialsController> _logger;

        public MaterialsController(ApplicationDbContext dbContext, ILogger<MaterialsController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<MaterialDto>> GetById(int id)
        {
            var entity = await _dbContext.Materials
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            var translations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, entity.TranslationCode);
            return Ok(MapToDto(entity, translations));
        }

        [HttpPost]
        public async Task<ActionResult<MaterialDto>> Create([FromBody] CreateMaterialRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = new Material
            {
                Name = request.Name,
                TranslationCode = request.TranslationCode,
                Active = request.Active,
                MaterialGroup = request.MaterialGroup,
                Color1 = request.Color1,
                Color2 = request.Color2,
                Layer1Material = request.Layer1Material,
                Layer1Weight = request.Layer1Weight,
                Layer1Thickness = request.Layer1Thickness,
                Layer2Material = request.Layer2Material,
                Layer2Weight = request.Layer2Weight,
                Layer2Thickness = request.Layer2Thickness,
                Layer3Material = request.Layer3Material,
                Layer3Weight = request.Layer3Weight,
                Layer3Thickness = request.Layer3Thickness,
                Layer4Material = request.Layer4Material,
                Layer4Weight = request.Layer4Weight,
                Layer4Thickness = request.Layer4Thickness,
                Layer5Material = request.Layer5Material,
                Layer5Weight = request.Layer5Weight,
                Layer5Thickness = request.Layer5Thickness,
                ThicknessMm = request.ThicknessMm,
                WeightGr = request.WeightGr,
                MaterialText = request.MaterialText,
                PackagingFeeCategoryId = request.PackagingFeeCategoryId
            };

            _dbContext.Materials.Add(entity);
            await _dbContext.SaveChangesAsync();

            entity.TranslationCode = await EntityTranslationService.UpsertTranslationsAsync(_dbContext, entity.TranslationCode, request.Translations);
            await _dbContext.SaveChangesAsync();

            var translations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, entity.TranslationCode);

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(entity, translations));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<MaterialDto>> Update(int id, [FromBody] UpdateMaterialRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = await _dbContext.Materials.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            entity.Name = request.Name;
            entity.TranslationCode = request.TranslationCode;
            entity.Active = request.Active ?? false;
            entity.MaterialGroup = request.MaterialGroup;
            entity.Color1 = request.Color1;
            entity.Color2 = request.Color2;
            entity.Layer1Material = request.Layer1Material;
            entity.Layer1Weight = request.Layer1Weight;
            entity.Layer1Thickness = request.Layer1Thickness;
            entity.Layer2Material = request.Layer2Material;
            entity.Layer2Weight = request.Layer2Weight;
            entity.Layer2Thickness = request.Layer2Thickness;
            entity.Layer3Material = request.Layer3Material;
            entity.Layer3Weight = request.Layer3Weight;
            entity.Layer3Thickness = request.Layer3Thickness;
            entity.Layer4Material = request.Layer4Material;
            entity.Layer4Weight = request.Layer4Weight;
            entity.Layer4Thickness = request.Layer4Thickness;
            entity.Layer5Material = request.Layer5Material;
            entity.Layer5Weight = request.Layer5Weight;
            entity.Layer5Thickness = request.Layer5Thickness;
            entity.ThicknessMm = request.ThicknessMm;
            entity.WeightGr = request.WeightGr;
            entity.MaterialText = request.MaterialText;
            entity.PackagingFeeCategoryId = request.PackagingFeeCategoryId;

            entity.TranslationCode = await EntityTranslationService.UpsertTranslationsAsync(_dbContext, entity.TranslationCode, request.Translations);

            await _dbContext.SaveChangesAsync();

            var translations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, entity.TranslationCode);

            return Ok(MapToDto(entity, translations));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _dbContext.Materials.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            _dbContext.Materials.Remove(entity);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Delete conflict for material {MaterialId}", id);

                var details = new List<string>();
                var productCount = await _dbContext.Products
                    .AsNoTracking()
                    .CountAsync(x => x.MaterialId == id);

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
                    message = "Materialet kan inte raderas eftersom det refereras av annan data.",
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
        public async Task<ActionResult<PagedResultDto<MaterialDto>>> Search([FromBody] SearchMaterialsRequest? request)
        {
            IQueryable<Material> query = _dbContext.Materials.AsNoTracking();
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
                        _logger.LogWarning(ex, "Invalid material search condition for field {Field}", condition.Field);
                        return BadRequest(new { message = ex.Message, field = condition.Field });
                    }
                }
            }

            IOrderedQueryable<Material> orderedQuery;
            try
            {
                orderedQuery = ApplyOrdering(query, request?.OrderBy);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid material order by request");
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

            return Ok(new PagedResultDto<MaterialDto>
            {
                Items = items,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        private static IOrderedQueryable<Material> ApplyOrdering(IQueryable<Material> query, List<SortRequest>? orderBy)
        {
            if (orderBy is null || orderBy.Count == 0)
            {
                return query.OrderBy(x => x.Id);
            }

            IOrderedQueryable<Material>? ordered = null;

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
                    "materialgroup" => ApplyOrder(ordered, query, x => x.MaterialGroup, isDescending),
                    "color1" => ApplyOrder(ordered, query, x => x.Color1, isDescending),
                    "color2" => ApplyOrder(ordered, query, x => x.Color2, isDescending),
                    "thicknessmm" => ApplyOrder(ordered, query, x => x.ThicknessMm, isDescending),
                    "weightgr" => ApplyOrder(ordered, query, x => x.WeightGr, isDescending),
                    "materialtext" => ApplyOrder(ordered, query, x => x.MaterialText, isDescending),
                    "olddbid" => ApplyOrder(ordered, query, x => x.OldDbId, isDescending),
                    "packagingfeecategoryid" => ApplyOrder(ordered, query, x => x.PackagingFeeCategoryId, isDescending),
                    _ => throw new ArgumentException($"Unsupported order by field '{sort.Field}'.")
                };
            }

            return ordered ?? query.OrderBy(x => x.Id);
        }

        private static IOrderedQueryable<Material> ApplyOrder<TKey>(
            IOrderedQueryable<Material>? ordered,
            IQueryable<Material> source,
            Expression<Func<Material, TKey>> keySelector,
            bool isDescending)
        {
            if (ordered is null)
            {
                return isDescending ? source.OrderByDescending(keySelector) : source.OrderBy(keySelector);
            }

            return isDescending ? ordered.ThenByDescending(keySelector) : ordered.ThenBy(keySelector);
        }

        private static IQueryable<Material> ApplyCondition(IQueryable<Material> query, FilterConditionDto condition)
        {
            var field = condition.Field.Trim().ToLowerInvariant();
            var op = condition.Operator.Trim().ToLowerInvariant();

            return field switch
            {
                "id" => ApplyIntCondition(query, x => x.Id, condition, op),
                "name" => ApplyStringCondition(query, x => x.Name, condition, op),
                "translationcode" => ApplyNullableIntCondition(query, x => x.TranslationCode, condition, op),
                "active" => ApplyBoolCondition(query, x => x.Active, condition, op),
                "materialgroup" => ApplyNullableIntCondition(query, x => x.MaterialGroup, condition, op),
                "color1" => ApplyStringCondition(query, x => x.Color1, condition, op),
                "color2" => ApplyStringCondition(query, x => x.Color2, condition, op),
                "layer1material" => ApplyStringCondition(query, x => x.Layer1Material, condition, op),
                "layer1weight" => ApplyNullableIntCondition(query, x => x.Layer1Weight, condition, op),
                "layer1thickness" => ApplyStringCondition(query, x => x.Layer1Thickness, condition, op),
                "layer2material" => ApplyStringCondition(query, x => x.Layer2Material, condition, op),
                "layer2weight" => ApplyNullableIntCondition(query, x => x.Layer2Weight, condition, op),
                "layer2thickness" => ApplyStringCondition(query, x => x.Layer2Thickness, condition, op),
                "layer3material" => ApplyStringCondition(query, x => x.Layer3Material, condition, op),
                "layer3weight" => ApplyNullableIntCondition(query, x => x.Layer3Weight, condition, op),
                "layer3thickness" => ApplyStringCondition(query, x => x.Layer3Thickness, condition, op),
                "layer4material" => ApplyStringCondition(query, x => x.Layer4Material, condition, op),
                "layer4weight" => ApplyNullableIntCondition(query, x => x.Layer4Weight, condition, op),
                "layer4thickness" => ApplyStringCondition(query, x => x.Layer4Thickness, condition, op),
                "layer5material" => ApplyStringCondition(query, x => x.Layer5Material, condition, op),
                "layer5weight" => ApplyNullableIntCondition(query, x => x.Layer5Weight, condition, op),
                "layer5thickness" => ApplyStringCondition(query, x => x.Layer5Thickness, condition, op),
                "thicknessmm" => ApplyNullableDoubleCondition(query, x => x.ThicknessMm, condition, op),
                "weightgr" => ApplyNullableIntCondition(query, x => x.WeightGr, condition, op),
                "materialtext" => ApplyStringCondition(query, x => x.MaterialText, condition, op),
                "olddbid" => ApplyNullableIntCondition(query, x => x.OldDbId, condition, op),
                "packagingfeecategoryid" => ApplyNullableIntCondition(query, x => x.PackagingFeeCategoryId, condition, op),
                _ => throw new ArgumentException($"Unsupported filter field '{condition.Field}'.")
            };
        }

        private static IQueryable<Material> ApplyStringCondition(
            IQueryable<Material> query,
            Expression<Func<Material, string?>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<Material, bool>>(
                    Expression.Equal(member, Expression.Constant(value, typeof(string))), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Material, bool>>(
                    Expression.NotEqual(member, Expression.Constant(value, typeof(string))), parameter)),
                "contains" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.Contains))),
                "startswith" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.StartsWith))),
                "endswith" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.EndsWith))),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Material, bool>>(
                    Expression.AndAlso(
                        Expression.NotEqual(member, nullConstant),
                        Expression.Call(
                            Expression.Constant(values),
                            typeof(List<string>).GetMethod(nameof(List<string>.Contains), new[] { typeof(string) })!,
                            member)),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<Material, bool>>(
                    Expression.Equal(member, nullConstant), parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<Material, bool>>(
                    Expression.NotEqual(member, nullConstant), parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for string field '{condition.Field}'.")
            };
        }

        private static IQueryable<Material> ApplyIntCondition(
            IQueryable<Material> query,
            Expression<Func<Material, int>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<Material, bool>>(Expression.Equal(member, constant), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Material, bool>>(Expression.NotEqual(member, constant), parameter)),
                "gt" => query.Where(Expression.Lambda<Func<Material, bool>>(Expression.GreaterThan(member, constant), parameter)),
                "gte" => query.Where(Expression.Lambda<Func<Material, bool>>(Expression.GreaterThanOrEqual(member, constant), parameter)),
                "lt" => query.Where(Expression.Lambda<Func<Material, bool>>(Expression.LessThan(member, constant), parameter)),
                "lte" => query.Where(Expression.Lambda<Func<Material, bool>>(Expression.LessThanOrEqual(member, constant), parameter)),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Material, bool>>(
                    Expression.Call(
                        Expression.Constant(values),
                        typeof(List<int>).GetMethod(nameof(List<int>.Contains), new[] { typeof(int) })!,
                        member),
                    parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for numeric field '{condition.Field}'.")
            };
        }

        private static IQueryable<Material> ApplyNullableIntCondition(
            IQueryable<Material> query,
            Expression<Func<Material, int?>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<Material, bool>>(
                    Expression.Equal(member, Expression.Constant(value, typeof(int?))), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Material, bool>>(
                    Expression.NotEqual(member, Expression.Constant(value, typeof(int?))), parameter)),
                "gt" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThan, value.Value)),
                "gte" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThanOrEqual, value.Value)),
                "lt" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.LessThan, value.Value)),
                "lte" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.LessThanOrEqual, value.Value)),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Material, bool>>(
                    Expression.AndAlso(
                        hasValue,
                        Expression.Call(
                            Expression.Constant(values),
                            typeof(List<int>).GetMethod(nameof(List<int>.Contains), new[] { typeof(int) })!,
                            memberValue)),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<Material, bool>>(Expression.Not(hasValue), parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<Material, bool>>(hasValue, parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for numeric field '{condition.Field}'.")
            };
        }

        private static IQueryable<Material> ApplyNullableDoubleCondition(
            IQueryable<Material> query,
            Expression<Func<Material, double?>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<Material, bool>>(
                    Expression.Equal(member, Expression.Constant(value, typeof(double?))), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Material, bool>>(
                    Expression.NotEqual(member, Expression.Constant(value, typeof(double?))), parameter)),
                "gt" when value.HasValue => query.Where(BuildNullableDoubleComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThan, value.Value)),
                "gte" when value.HasValue => query.Where(BuildNullableDoubleComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThanOrEqual, value.Value)),
                "lt" when value.HasValue => query.Where(BuildNullableDoubleComparison(parameter, hasValue, memberValue, ExpressionType.LessThan, value.Value)),
                "lte" when value.HasValue => query.Where(BuildNullableDoubleComparison(parameter, hasValue, memberValue, ExpressionType.LessThanOrEqual, value.Value)),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Material, bool>>(
                    Expression.AndAlso(
                        hasValue,
                        Expression.Call(
                            Expression.Constant(values),
                            typeof(List<double>).GetMethod(nameof(List<double>.Contains), new[] { typeof(double) })!,
                            memberValue)),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<Material, bool>>(Expression.Not(hasValue), parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<Material, bool>>(hasValue, parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for numeric field '{condition.Field}'.")
            };
        }

        private static IQueryable<Material> ApplyBoolCondition(
            IQueryable<Material> query,
            Expression<Func<Material, bool>> selector,
            FilterConditionDto condition,
            string op)
        {
            var value = GetRequiredBoolValue(condition);
            var parameter = selector.Parameters[0];
            var member = selector.Body;
            var constant = Expression.Constant(value);

            return op switch
            {
                "eq" => query.Where(Expression.Lambda<Func<Material, bool>>(Expression.Equal(member, constant), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Material, bool>>(Expression.NotEqual(member, constant), parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for boolean field '{condition.Field}'.")
            };
        }

        private static Expression<Func<Material, bool>> BuildStringMethodCall(
            ParameterExpression parameter,
            Expression member,
            string value,
            string methodName)
        {
            var method = typeof(string).GetMethod(methodName, new[] { typeof(string) })!;
            var nullConstant = Expression.Constant(null, typeof(string));

            return Expression.Lambda<Func<Material, bool>>(
                Expression.AndAlso(
                    Expression.NotEqual(member, nullConstant),
                    Expression.Call(member, method, Expression.Constant(value))),
                parameter);
        }

        private static Expression<Func<Material, bool>> BuildNullableIntComparison(
            ParameterExpression parameter,
            MemberExpression hasValue,
            MemberExpression memberValue,
            ExpressionType comparison,
            int value)
        {
            return Expression.Lambda<Func<Material, bool>>(
                Expression.AndAlso(hasValue, Expression.MakeBinary(comparison, memberValue, Expression.Constant(value))),
                parameter);
        }

        private static Expression<Func<Material, bool>> BuildNullableDoubleComparison(
            ParameterExpression parameter,
            MemberExpression hasValue,
            MemberExpression memberValue,
            ExpressionType comparison,
            double value)
        {
            return Expression.Lambda<Func<Material, bool>>(
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

        private static double? GetNullableDoubleValue(FilterConditionDto condition)
        {
            if (!condition.Value.HasValue) return null;
            var value = condition.Value.Value;
            if (value.ValueKind == JsonValueKind.Null) return null;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return number;
            throw new ArgumentException($"Filter field '{condition.Field}' requires a numeric value.");
        }

        private static List<double> GetDoubleValues(FilterConditionDto condition)
        {
            if (condition.Values is null) return new List<double>();
            return condition.Values
                .Where(v => v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out _))
                .Select(v => v.GetDouble())
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

        private static MaterialDto MapToDto(Material entity, List<EntityTranslationDto>? translations = null) => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            TranslationCode = entity.TranslationCode,
            Translations = translations ?? new List<EntityTranslationDto>(),
            Active = entity.Active,
            MaterialGroup = entity.MaterialGroup,
            Color1 = entity.Color1,
            Color2 = entity.Color2,
            Layer1Material = entity.Layer1Material,
            Layer1Weight = entity.Layer1Weight,
            Layer1Thickness = entity.Layer1Thickness,
            Layer2Material = entity.Layer2Material,
            Layer2Weight = entity.Layer2Weight,
            Layer2Thickness = entity.Layer2Thickness,
            Layer3Material = entity.Layer3Material,
            Layer3Weight = entity.Layer3Weight,
            Layer3Thickness = entity.Layer3Thickness,
            Layer4Material = entity.Layer4Material,
            Layer4Weight = entity.Layer4Weight,
            Layer4Thickness = entity.Layer4Thickness,
            Layer5Material = entity.Layer5Material,
            Layer5Weight = entity.Layer5Weight,
            Layer5Thickness = entity.Layer5Thickness,
            ThicknessMm = entity.ThicknessMm,
            WeightGr = entity.WeightGr,
            MaterialText = entity.MaterialText,
            OldDbId = entity.OldDbId,
            PackagingFeeCategoryId = entity.PackagingFeeCategoryId
        };
    }
}
