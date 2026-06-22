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
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(ApplicationDbContext dbContext, ILogger<ProductsController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ProductDto>> GetById(int id)
        {
            var entity = await _dbContext.Products
                .AsNoTracking()
                .Include(x => x.Construction)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(entity));
        }

        [HttpPost]
        public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = new Product
            {                
                Name = request.Name,
                NoteInternal = request.NoteInternal,
                NoteExternal = request.NoteExternal,
                NoteExtra = request.NoteExtra,
                MaterialId = request.MaterialId,
                MaterialThickness = request.MaterialThickness,
                LengthMm = request.LengthMm,
                WidthMm = request.WidthMm,
                HeightMm = request.HeightMm,
                Format = request.Format,
                NrOfColors = request.NrOfColors,
                ConstructionId = request.ConstructionId,
                VarnishId = request.VarnishId,
                VarnishOther = request.VarnishOther,
                NetWeightPer1000 = request.NetWeightPer1000,
                CreatedAt = request.CreatedAt,
                EditedAt = request.EditedAt,
                CreatedBy = request.CreatedBy,
                EditedBy = request.EditedBy,
                ProductCode = request.ProductCode,
                MaterialGroup = request.MaterialGroup,
                Active = request.Active,
                IsServicePackaging = request.IsServicePackaging,
                LastSupplierOrderCreated = request.LastSupplierOrderCreated,
                LastCustomerOrderCreated = request.LastCustomerOrderCreated
            };

            _dbContext.Products.Add(entity);
            await _dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(entity));
        }
        [HttpPut("{id:int}")]
        public async Task<ActionResult<ProductDto>> Update(int id, [FromBody] UpdateProductRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = await _dbContext.Products.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            entity.Name = request.Name;
            entity.NoteInternal = request.NoteInternal;
            entity.NoteExternal = request.NoteExternal;
            entity.NoteExtra = request.NoteExtra;
            entity.MaterialId = request.MaterialId;
            entity.MaterialThickness = request.MaterialThickness;
            entity.LengthMm = request.LengthMm;
            entity.WidthMm = request.WidthMm;
            entity.HeightMm = request.HeightMm;
            entity.Format = request.Format;
            entity.NrOfColors = request.NrOfColors;
            entity.ConstructionId = request.ConstructionId;
            entity.VarnishId = request.VarnishId;
            entity.VarnishOther = request.VarnishOther;
            entity.NetWeightPer1000 = request.NetWeightPer1000;
            entity.CreatedAt = request.CreatedAt;
            entity.EditedAt = request.EditedAt;
            entity.CreatedBy = request.CreatedBy;
            entity.EditedBy = request.EditedBy;
            entity.ProductCode = request.ProductCode;
            entity.MaterialGroup = request.MaterialGroup;
            entity.Active = request.Active ?? false;
            entity.IsServicePackaging = request.IsServicePackaging;
            entity.LastSupplierOrderCreated = request.LastSupplierOrderCreated;
            entity.LastCustomerOrderCreated = request.LastCustomerOrderCreated;

            await _dbContext.SaveChangesAsync();

            return Ok(MapToDto(entity));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _dbContext.Products.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            _dbContext.Products.Remove(entity);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<ProductDto>>> Search([FromBody] SearchProductsRequest? request)
        {
            IQueryable<Product> query = _dbContext.Products
                .AsNoTracking()
                .Include(x => x.Construction);
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
                        _logger.LogWarning(ex, "Invalid product search condition for field {Field}", condition.Field);
                        return BadRequest(new { message = ex.Message, field = condition.Field });
                    }
                }
            }

            IOrderedQueryable<Product> orderedQuery;
            try
            {
                orderedQuery = ApplyOrdering(query, request?.OrderBy);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid product order by request");
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

            return Ok(new PagedResultDto<ProductDto>
            {
                Items = items,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        private static IOrderedQueryable<Product> ApplyOrdering(IQueryable<Product> query, List<SortRequest>? orderBy)
        {
            if (orderBy is null || orderBy.Count == 0)
            {
                return query.OrderBy(x => x.Id);
            }

            IOrderedQueryable<Product>? ordered = null;

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
                    "noteinternal" => ApplyOrder(ordered, query, x => x.NoteInternal, isDescending),
                    "noteexternal" => ApplyOrder(ordered, query, x => x.NoteExternal, isDescending),
                    "noteextra" => ApplyOrder(ordered, query, x => x.NoteExtra, isDescending),
                    "materialid" => ApplyOrder(ordered, query, x => x.MaterialId, isDescending),
                    "materialthickness" => ApplyOrder(ordered, query, x => x.MaterialThickness, isDescending),
                    "lengthmm" => ApplyOrder(ordered, query, x => x.LengthMm, isDescending),
                    "widthmm" => ApplyOrder(ordered, query, x => x.WidthMm, isDescending),
                    "heightmm" => ApplyOrder(ordered, query, x => x.HeightMm, isDescending),
                    "format" => ApplyOrder(ordered, query, x => x.Format, isDescending),
                    "nrofcolors" => ApplyOrder(ordered, query, x => x.NrOfColors, isDescending),
                    "constructionid" => ApplyOrder(ordered, query, x => x.ConstructionId, isDescending),
                    "varnishid" => ApplyOrder(ordered, query, x => x.VarnishId, isDescending),
                    "varnishother" => ApplyOrder(ordered, query, x => x.VarnishOther, isDescending),
                    "netweightper1000" => ApplyOrder(ordered, query, x => x.NetWeightPer1000, isDescending),
                    "createdat" => ApplyOrder(ordered, query, x => x.CreatedAt, isDescending),
                    "editedat" => ApplyOrder(ordered, query, x => x.EditedAt, isDescending),
                    "createdby" => ApplyOrder(ordered, query, x => x.CreatedBy, isDescending),
                    "editedby" => ApplyOrder(ordered, query, x => x.EditedBy, isDescending),
                    "productcode" => ApplyOrder(ordered, query, x => x.ProductCode, isDescending),
                    "olddbid" => ApplyOrder(ordered, query, x => x.OldDbId, isDescending),
                    "materialgroup" => ApplyOrder(ordered, query, x => x.MaterialGroup, isDescending),
                    "active" => ApplyOrder(ordered, query, x => x.Active, isDescending),
                    "isservicepackaging" => ApplyOrder(ordered, query, x => x.IsServicePackaging, isDescending),
                    "lastsupplierordercreated" => ApplyOrder(ordered, query, x => x.LastSupplierOrderCreated, isDescending),
                    "lastcustomerordercreated" => ApplyOrder(ordered, query, x => x.LastCustomerOrderCreated, isDescending),
                    _ => throw new ArgumentException($"Unsupported order by field '{sort.Field}'.")
                };
            }

            return ordered ?? query.OrderBy(x => x.Id);
        }

        private static IOrderedQueryable<Product> ApplyOrder<TKey>(
            IOrderedQueryable<Product>? ordered,
            IQueryable<Product> source,
            Expression<Func<Product, TKey>> keySelector,
            bool isDescending)
        {
            if (ordered is null)
            {
                return isDescending ? source.OrderByDescending(keySelector) : source.OrderBy(keySelector);
            }

            return isDescending ? ordered.ThenByDescending(keySelector) : ordered.ThenBy(keySelector);
        }

        private static IQueryable<Product> ApplyCondition(IQueryable<Product> query, FilterConditionDto condition)
        {
            var field = condition.Field.Trim().ToLowerInvariant();
            var op = condition.Operator.Trim().ToLowerInvariant();

            return field switch
            {
                "id" => ApplyIntCondition(query, x => x.Id, condition, op),
                "name" => ApplyStringCondition(query, x => x.Name, condition, op),
                "noteinternal" => ApplyStringCondition(query, x => x.NoteInternal, condition, op),
                "noteexternal" => ApplyStringCondition(query, x => x.NoteExternal, condition, op),
                "noteextra" => ApplyStringCondition(query, x => x.NoteExtra, condition, op),
                "materialid" => ApplyNullableIntCondition(query, x => x.MaterialId, condition, op),
                "materialthickness" => ApplyStringCondition(query, x => x.MaterialThickness, condition, op),
                "lengthmm" => ApplyNullableIntCondition(query, x => x.LengthMm, condition, op),
                "widthmm" => ApplyNullableIntCondition(query, x => x.WidthMm, condition, op),
                "heightmm" => ApplyNullableIntCondition(query, x => x.HeightMm, condition, op),
                "format" => ApplyStringCondition(query, x => x.Format, condition, op),
                "nrofcolors" => ApplyStringCondition(query, x => x.NrOfColors, condition, op),
                "constructionid" => ApplyNullableIntCondition(query, x => x.ConstructionId, condition, op),
                "varnishid" => ApplyNullableIntCondition(query, x => x.VarnishId, condition, op),
                "varnishother" => ApplyStringCondition(query, x => x.VarnishOther, condition, op),
                "netweightper1000" => ApplyNullableIntCondition(query, x => x.NetWeightPer1000, condition, op),
                "createdat" => ApplyNullableDateTimeCondition(query, x => x.CreatedAt, condition, op),
                "editedat" => ApplyNullableDateTimeCondition(query, x => x.EditedAt, condition, op),
                "createdby" => ApplyNullableIntCondition(query, x => x.CreatedBy, condition, op),
                "editedby" => ApplyNullableIntCondition(query, x => x.EditedBy, condition, op),
                "productcode" => ApplyStringCondition(query, x => x.ProductCode, condition, op),
                "olddbid" => ApplyNullableIntCondition(query, x => x.OldDbId, condition, op),
                "materialgroup" => ApplyNullableIntCondition(query, x => x.MaterialGroup, condition, op),
                "active" => ApplyBoolCondition(query, x => x.Active, condition, op),
                "isservicepackaging" => ApplyNullableBoolCondition(query, x => x.IsServicePackaging, condition, op),
                "lastsupplierordercreated" => ApplyNullableDateTimeCondition(query, x => x.LastSupplierOrderCreated, condition, op),
                "lastcustomerordercreated" => ApplyNullableDateTimeCondition(query, x => x.LastCustomerOrderCreated, condition, op),
                _ => throw new ArgumentException($"Unsupported filter field '{condition.Field}'.")
            };
        }

        private static IQueryable<Product> ApplyStringCondition(
            IQueryable<Product> query,
            Expression<Func<Product, string?>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<Product, bool>>(
                    Expression.Equal(member, Expression.Constant(value, typeof(string))),
                    parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Product, bool>>(
                    Expression.NotEqual(member, Expression.Constant(value, typeof(string))),
                    parameter)),
                "contains" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.Contains))),
                "startswith" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.StartsWith))),
                "endswith" when value is not null => query.Where(BuildStringMethodCall(parameter, member, value, nameof(string.EndsWith))),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Product, bool>>(
                    Expression.AndAlso(
                        Expression.NotEqual(member, nullConstant),
                        Expression.Call(
                            Expression.Constant(values),
                            typeof(List<string>).GetMethod(nameof(List<string>.Contains), new[] { typeof(string) })!,
                            member)),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<Product, bool>>(
                    Expression.Equal(member, nullConstant),
                    parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<Product, bool>>(
                    Expression.NotEqual(member, nullConstant),
                    parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for string field '{condition.Field}'.")
            };
        }

        private static IQueryable<Product> ApplyIntCondition(
            IQueryable<Product> query,
            Expression<Func<Product, int>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<Product, bool>>(Expression.Equal(member, constant), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Product, bool>>(Expression.NotEqual(member, constant), parameter)),
                "gt" => query.Where(Expression.Lambda<Func<Product, bool>>(Expression.GreaterThan(member, constant), parameter)),
                "gte" => query.Where(Expression.Lambda<Func<Product, bool>>(Expression.GreaterThanOrEqual(member, constant), parameter)),
                "lt" => query.Where(Expression.Lambda<Func<Product, bool>>(Expression.LessThan(member, constant), parameter)),
                "lte" => query.Where(Expression.Lambda<Func<Product, bool>>(Expression.LessThanOrEqual(member, constant), parameter)),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Product, bool>>(
                    Expression.Call(
                        Expression.Constant(values),
                        typeof(List<int>).GetMethod(nameof(List<int>.Contains), new[] { typeof(int) })!,
                        member),
                    parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for numeric field '{condition.Field}'.")
            };
        }

        private static IQueryable<Product> ApplyNullableIntCondition(
            IQueryable<Product> query,
            Expression<Func<Product, int?>> selector,
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
                "eq" => query.Where(Expression.Lambda<Func<Product, bool>>(
                    Expression.Equal(member, Expression.Constant(value, typeof(int?))),
                    parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Product, bool>>(
                    Expression.NotEqual(member, Expression.Constant(value, typeof(int?))),
                    parameter)),
                "gt" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThan, value.Value)),
                "gte" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThanOrEqual, value.Value)),
                "lt" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.LessThan, value.Value)),
                "lte" when value.HasValue => query.Where(BuildNullableIntComparison(parameter, hasValue, memberValue, ExpressionType.LessThanOrEqual, value.Value)),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Product, bool>>(
                    Expression.AndAlso(
                        hasValue,
                        Expression.Call(
                            Expression.Constant(values),
                            typeof(List<int>).GetMethod(nameof(List<int>.Contains), new[] { typeof(int) })!,
                            memberValue)),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<Product, bool>>(Expression.Not(hasValue), parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<Product, bool>>(hasValue, parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for numeric field '{condition.Field}'.")
            };
        }

        private static IQueryable<Product> ApplyBoolCondition(
            IQueryable<Product> query,
            Expression<Func<Product, bool>> selector,
            FilterConditionDto condition,
            string op)
        {
            var value = GetRequiredBoolValue(condition);
            var parameter = selector.Parameters[0];
            var member = selector.Body;
            var constant = Expression.Constant(value);

            return op switch
            {
                "eq" => query.Where(Expression.Lambda<Func<Product, bool>>(Expression.Equal(member, constant), parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Product, bool>>(Expression.NotEqual(member, constant), parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for boolean field '{condition.Field}'.")
            };
        }

        private static IQueryable<Product> ApplyNullableBoolCondition(
            IQueryable<Product> query,
            Expression<Func<Product, bool?>> selector,
            FilterConditionDto condition,
            string op)
        {
            var parameter = selector.Parameters[0];
            var member = selector.Body;
            var hasValue = Expression.Property(member, nameof(Nullable<bool>.HasValue));

            return op switch
            {
                "eq" => query.Where(Expression.Lambda<Func<Product, bool>>(
                    Expression.Equal(member, Expression.Constant(GetNullableBoolValue(condition), typeof(bool?))),
                    parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Product, bool>>(
                    Expression.NotEqual(member, Expression.Constant(GetNullableBoolValue(condition), typeof(bool?))),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<Product, bool>>(Expression.Not(hasValue), parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<Product, bool>>(hasValue, parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for boolean field '{condition.Field}'.")
            };
        }

        private static IQueryable<Product> ApplyNullableDateTimeCondition(
            IQueryable<Product> query,
            Expression<Func<Product, DateTime?>> selector,
            FilterConditionDto condition,
            string op)
        {
            var value = GetNullableDateTimeValue(condition);
            var values = GetDateTimeValues(condition);
            var parameter = selector.Parameters[0];
            var member = selector.Body;
            var hasValue = Expression.Property(member, nameof(Nullable<DateTime>.HasValue));
            var memberValue = Expression.Property(member, nameof(Nullable<DateTime>.Value));

            return op switch
            {
                "eq" => query.Where(Expression.Lambda<Func<Product, bool>>(
                    Expression.Equal(member, Expression.Constant(value, typeof(DateTime?))),
                    parameter)),
                "neq" => query.Where(Expression.Lambda<Func<Product, bool>>(
                    Expression.NotEqual(member, Expression.Constant(value, typeof(DateTime?))),
                    parameter)),
                "gt" when value.HasValue => query.Where(BuildNullableDateTimeComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThan, value.Value)),
                "gte" when value.HasValue => query.Where(BuildNullableDateTimeComparison(parameter, hasValue, memberValue, ExpressionType.GreaterThanOrEqual, value.Value)),
                "lt" when value.HasValue => query.Where(BuildNullableDateTimeComparison(parameter, hasValue, memberValue, ExpressionType.LessThan, value.Value)),
                "lte" when value.HasValue => query.Where(BuildNullableDateTimeComparison(parameter, hasValue, memberValue, ExpressionType.LessThanOrEqual, value.Value)),
                "in" when values.Count > 0 => query.Where(Expression.Lambda<Func<Product, bool>>(
                    Expression.AndAlso(
                        hasValue,
                        Expression.Call(
                            Expression.Constant(values),
                            typeof(List<DateTime>).GetMethod(nameof(List<DateTime>.Contains), new[] { typeof(DateTime) })!,
                            memberValue)),
                    parameter)),
                "isnull" => query.Where(Expression.Lambda<Func<Product, bool>>(Expression.Not(hasValue), parameter)),
                "isnotnull" => query.Where(Expression.Lambda<Func<Product, bool>>(hasValue, parameter)),
                _ => throw new ArgumentException($"Unsupported operator '{condition.Operator}' for date field '{condition.Field}'.")
            };
        }

        private static Expression<Func<Product, bool>> BuildStringMethodCall(
            ParameterExpression parameter,
            Expression member,
            string value,
            string methodName)
        {
            var method = typeof(string).GetMethod(methodName, new[] { typeof(string) })!;
            var nullConstant = Expression.Constant(null, typeof(string));

            return Expression.Lambda<Func<Product, bool>>(
                Expression.AndAlso(
                    Expression.NotEqual(member, nullConstant),
                    Expression.Call(member, method, Expression.Constant(value))),
                parameter);
        }

        private static Expression<Func<Product, bool>> BuildNullableIntComparison(
            ParameterExpression parameter,
            MemberExpression hasValue,
            MemberExpression memberValue,
            ExpressionType comparison,
            int value)
        {
            var comparisonExpression = Expression.MakeBinary(comparison, memberValue, Expression.Constant(value));
            return Expression.Lambda<Func<Product, bool>>(Expression.AndAlso(hasValue, comparisonExpression), parameter);
        }

        private static Expression<Func<Product, bool>> BuildNullableDateTimeComparison(
            ParameterExpression parameter,
            MemberExpression hasValue,
            MemberExpression memberValue,
            ExpressionType comparison,
            DateTime value)
        {
            var comparisonExpression = Expression.MakeBinary(comparison, memberValue, Expression.Constant(value));
            return Expression.Lambda<Func<Product, bool>>(Expression.AndAlso(hasValue, comparisonExpression), parameter);
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
            if (condition.Values is null)
            {
                return new List<string>();
            }

            return condition.Values
                .Where(v => v.ValueKind == JsonValueKind.String)
                .Select(v => v.GetString()!)
                .ToList();
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

            throw new ArgumentException($"Filter field '{condition.Field}' requires an integer value.");
        }

        private static List<int> GetIntValues(FilterConditionDto condition)
        {
            if (condition.Values is null)
            {
                return new List<int>();
            }

            return condition.Values
                .Select(v =>
                {
                    if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var parsed))
                    {
                        return (int?)parsed;
                    }

                    return null;
                })
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
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
                _ => throw new ArgumentException($"Filter field '{condition.Field}' requires a boolean value.")
            };
        }

        private static bool? GetNullableBoolValue(FilterConditionDto condition)
        {
            if (!condition.Value.HasValue)
            {
                return null;
            }

            var value = condition.Value.Value;
            return value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new ArgumentException($"Filter field '{condition.Field}' requires a boolean value.")
            };
        }

        private static DateTime? GetNullableDateTimeValue(FilterConditionDto condition)
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

            if (value.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException($"Filter field '{condition.Field}' requires an ISO-8601 datetime string value.");
            }

            var raw = value.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (DateTime.TryParse(raw, out var parsed))
            {
                return parsed;
            }

            throw new ArgumentException($"Filter field '{condition.Field}' requires a valid datetime value.");
        }

        private static List<DateTime> GetDateTimeValues(FilterConditionDto condition)
        {
            if (condition.Values is null)
            {
                return new List<DateTime>();
            }

            return condition.Values
                .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : null)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s =>
                {
                    if (DateTime.TryParse(s, out var parsed))
                    {
                        return (DateTime?)parsed;
                    }

                    return null;
                })
                .Where(dt => dt.HasValue)
                .Select(dt => dt!.Value)
                .ToList();
        }

        private static ProductDto MapToDto(Product entity) => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            NoteInternal = entity.NoteInternal,
            NoteExternal = entity.NoteExternal,
            NoteExtra = entity.NoteExtra,
            MaterialId = entity.MaterialId,
            MaterialThickness = entity.MaterialThickness,
            LengthMm = entity.LengthMm,
            WidthMm = entity.WidthMm,
            HeightMm = entity.HeightMm,
            Format = entity.Format,
            NrOfColors = entity.NrOfColors,
            ConstructionId = entity.ConstructionId,
            VarnishId = entity.VarnishId,
            VarnishOther = entity.VarnishOther,
            NetWeightPer1000 = entity.NetWeightPer1000,
            CreatedAt = entity.CreatedAt,
            EditedAt = entity.EditedAt,
            CreatedBy = entity.CreatedBy,
            EditedBy = entity.EditedBy,
            ProductCode = entity.ProductCode,
            OldDbId = entity.OldDbId,
            MaterialGroup = entity.MaterialGroup,
            Active = entity.Active,
            IsServicePackaging = entity.IsServicePackaging,
            LastSupplierOrderCreated = entity.LastSupplierOrderCreated,
            LastCustomerOrderCreated = entity.LastCustomerOrderCreated,
            Construction = entity.Construction is null
                ? null
                : new ConstructionLookupDto
                {
                    Id = entity.Construction.Id,
                    Name = entity.Construction.Name
                }
        };
    }
}
