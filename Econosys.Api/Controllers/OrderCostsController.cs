using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
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
    public class OrderCostsController : ControllerBase
    {
        private const string SearchCollation = "Finnish_Swedish_CI_AS";
        private static readonly Dictionary<string, PropertyInfo> FieldMap = BuildFieldMap();

        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<OrderCostsController> _logger;
        private readonly ILegacyUserResolutionService _legacyUserResolution;

        public OrderCostsController(
            ApplicationDbContext dbContext,
            ILogger<OrderCostsController> logger,
            ILegacyUserResolutionService legacyUserResolution)
        {
            _dbContext = dbContext;
            _logger = logger;
            _legacyUserResolution = legacyUserResolution;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<OrderCostDto>> GetById(int id)
        {
            var orderCost = await _dbContext.OrderCosts
                .AsNoTracking()
                .Include(x => x.SupplierOrder!)
                    .ThenInclude(x => x.PurchaseCurrency)
                .Include(x => x.CustomerOrder!)
                    .ThenInclude(x => x.SalesCurrency)
                .Include(x => x.Cost)
                .Include(x => x.InPriceCurrency)
                .Include(x => x.CreatedByUser)
                .Include(x => x.EditedByUser)
                .Include(x => x.InvoiceRows)
                    .ThenInclude(x => x.Invoice)
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

            var legacyUser = await _legacyUserResolution.ResolveCurrentUserAsync(User);
            if (legacyUser is null)
            {
                return Unauthorized(new { message = "Could not map authenticated user to a legacy user by email." });
            }

            var nowUtc = SwedishTime.Now;

            var orderCost = new OrderCost
            {
                CreatedById = legacyUser.Id,
                CreatedDateTime = nowUtc,
                EditedById = legacyUser.Id,
                EditedDateTime = nowUtc,
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
                .Include(x => x.SupplierOrder!)
                    .ThenInclude(x => x.PurchaseCurrency)
                .Include(x => x.CustomerOrder!)
                    .ThenInclude(x => x.SalesCurrency)
                .Include(x => x.Cost)
                .Include(x => x.InPriceCurrency)
                .Include(x => x.CreatedByUser)
                .Include(x => x.EditedByUser)
                .Include(x => x.InvoiceRows)
                    .ThenInclude(x => x.Invoice)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (orderCost is null)
            {
                return NotFound();
            }

            var legacyUser = await _legacyUserResolution.ResolveCurrentUserAsync(User);
            if (legacyUser is null)
            {
                return Unauthorized(new { message = "Could not map authenticated user to a legacy user by email." });
            }

            var nowUtc = SwedishTime.Now;
            orderCost.EditedById = legacyUser.Id;
            orderCost.EditedDateTime = nowUtc;

            var updatedFields = BuildUpdatedFieldSet(request.UpdatedFields);
            var useExplicitFieldUpdates = updatedFields.Count > 0;

            if (useExplicitFieldUpdates)
            {
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.QuotationId))) orderCost.QuotationId = request.QuotationId;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.QuotationRowId))) orderCost.QuotationRowId = request.QuotationRowId;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.CalculationRowId))) orderCost.CalculationRowId = request.CalculationRowId;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.CustomerOrderId))) orderCost.CustomerOrderId = request.CustomerOrderId;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.SupplierOrderId))) orderCost.SupplierOrderId = request.SupplierOrderId;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.CostId))) orderCost.CostId = request.CostId;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.NrOf))) orderCost.NrOf = request.NrOf;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.InPrice))) orderCost.InPrice = request.InPrice;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.InPriceCurrencyId))) orderCost.InPriceCurrencyId = request.InPriceCurrencyId;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.OutPrice))) orderCost.OutPrice = request.OutPrice;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.DoDebit))) orderCost.DoDebit = request.DoDebit ?? false;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.Note))) orderCost.Note = request.Note;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.SupplierId))) orderCost.SupplierId = request.SupplierId;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.DoPrintOnCustomerOrder))) orderCost.DoPrintOnCustomerOrder = request.DoPrintOnCustomerOrder ?? false;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.DoPrintOnSupplierOrder))) orderCost.DoPrintOnSupplierOrder = request.DoPrintOnSupplierOrder ?? false;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.DoPrintOnQuotation))) orderCost.DoPrintOnQuotation = request.DoPrintOnQuotation ?? false;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.SupplierName))) orderCost.SupplierName = request.SupplierName;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.InPriceAttested))) orderCost.InPriceAttested = request.InPriceAttested;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.DoInvoiceSeparately))) orderCost.DoInvoiceSeparately = request.DoInvoiceSeparately ?? false;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.DoInvoiceSeparatelyImmediately))) orderCost.DoInvoiceSeparatelyImmediately = request.DoInvoiceSeparatelyImmediately ?? false;
                if (updatedFields.Contains(nameof(UpdateOrderCostRequest.IsCostInvoicedSeparately))) orderCost.IsCostInvoicedSeparately = request.IsCostInvoicedSeparately ?? false;
            }
            else
            {
                orderCost.QuotationId = request.QuotationId;
                orderCost.QuotationRowId = request.QuotationRowId;
                orderCost.CalculationRowId = request.CalculationRowId;
                orderCost.CustomerOrderId = request.CustomerOrderId;
                orderCost.SupplierOrderId = request.SupplierOrderId;
                orderCost.CostId = request.CostId;
                orderCost.NrOf = request.NrOf;
                orderCost.InPrice = request.InPrice;
                orderCost.InPriceCurrencyId = request.InPriceCurrencyId;
                orderCost.OutPrice = request.OutPrice;
                orderCost.DoDebit = request.DoDebit ?? false;
                orderCost.Note = request.Note;
                orderCost.SupplierId = request.SupplierId;
                orderCost.DoPrintOnCustomerOrder = request.DoPrintOnCustomerOrder ?? false;
                orderCost.DoPrintOnSupplierOrder = request.DoPrintOnSupplierOrder ?? false;
                orderCost.DoPrintOnQuotation = request.DoPrintOnQuotation ?? false;
                orderCost.SupplierName = request.SupplierName;
                orderCost.DoInvoiceSeparately = request.DoInvoiceSeparately ?? false;
                orderCost.DoInvoiceSeparatelyImmediately = request.DoInvoiceSeparatelyImmediately ?? false;
                orderCost.IsCostInvoicedSeparately = request.IsCostInvoicedSeparately ?? false;

                if (request.InPriceAttested.HasValue)
                {
                    var (purchaseRate, salesRate) = await ResolveCalculationCurrencyRatesAsync(orderCost.CustomerOrderId);
                    orderCost.InPriceAttested = request.InPriceAttested;
                    orderCost.AttestedDateTime = nowUtc;
                    orderCost.AtttestedBySignature = legacyUser?.Initials;
                    orderCost.AttestedInPriceCurrencyRate = purchaseRate;
                    orderCost.AttestedOutPriceCurrencyRate = salesRate;
                }
                else
                {
                    orderCost.InPriceAttested = null;
                    orderCost.AttestedDateTime = null;
                    orderCost.AtttestedBySignature = null;
                    orderCost.AttestedInPriceCurrencyRate = null;
                    orderCost.AttestedOutPriceCurrencyRate = null;
                }
            }

            await _dbContext.SaveChangesAsync();

            return Ok(MapToDto(orderCost));
        }

        [HttpPost("{id:int}/attest")]
        public async Task<ActionResult<OrderCostDto>> Attest(int id)
        {
            var orderCost = await _dbContext.OrderCosts
                .Include(x => x.SupplierOrder!)
                    .ThenInclude(x => x.PurchaseCurrency)
                .Include(x => x.CustomerOrder!)
                    .ThenInclude(x => x.SalesCurrency)
                .Include(x => x.Cost)
                .Include(x => x.InPriceCurrency)
                .Include(x => x.CreatedByUser)
                .Include(x => x.EditedByUser)
                .Include(x => x.InvoiceRows)
                    .ThenInclude(x => x.Invoice)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (orderCost is null)
            {
                return NotFound();
            }

            var legacyUser = await _legacyUserResolution.ResolveCurrentUserAsync(User);
            if (legacyUser is null)
            {
                return Unauthorized(new { message = "Could not map authenticated user to a legacy user by email." });
            }

            var nowUtc = SwedishTime.Now;
            var (purchaseRate, salesRate) = await ResolveCalculationCurrencyRatesAsync(orderCost.CustomerOrderId);

            orderCost.AttestedDateTime = nowUtc;
            orderCost.AtttestedBySignature = legacyUser.Name;
            orderCost.AttestedInPriceCurrencyRate = purchaseRate;
            orderCost.AttestedOutPriceCurrencyRate = salesRate;

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
                .AsNoTracking();

            var mode = string.IsNullOrWhiteSpace(request?.Mode)
                ? "all"
                : request!.Mode!.Trim().ToLowerInvariant();

            if (mode == "notattested")
            {
                query = query.Where(x => x.InPriceAttested == null && x.AttestedDateTime == null && x.AtttestedBySignature == null && x.InPrice != null);
            }
            else if (mode == "all")
            {
                if (request?.StartDate is DateTime startDate)
                {
                    query = query.Where(x => x.CreatedDateTime.HasValue && x.CreatedDateTime.Value >= startDate);
                }

                if (request?.EndDate is DateTime endDate)
                {
                    query = query.Where(x => x.CreatedDateTime.HasValue && x.CreatedDateTime.Value <= endDate);
                }
            }

            if (!string.IsNullOrWhiteSpace(request?.SearchText))
            {
                var searchText = request.SearchText.Trim();

                query = query.Where(x =>
                    (x.CustomerOrder != null && x.CustomerOrder.CustomerName != null && x.CustomerOrder.CustomerName.Contains(searchText)) ||
                    ((x.SupplierOrder != null && x.SupplierOrder.SupplierName != null && x.SupplierOrder.SupplierName.Contains(searchText)) ||
                     (x.SupplierName != null && x.SupplierName.Contains(searchText))) ||
                    ((x.CustomerOrder != null && x.CustomerOrder.CustomerOrderNr != null && x.CustomerOrder.CustomerOrderNr.Contains(searchText)) ||
                     (x.SupplierOrder != null && x.SupplierOrder.CustomerOrderNr != null && x.SupplierOrder.CustomerOrderNr.Contains(searchText))) ||
                    (x.Cost != null && x.Cost.Name != null && x.Cost.Name.Contains(searchText)));
            }

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

            var stopwatch = Stopwatch.StartNew();
            var totalCount = await query.CountAsync();
            var countElapsedMs = stopwatch.ElapsedMilliseconds;
            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pagination.PageSize);

            var projectedRows = await orderedQuery
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .Select(source => new
                {
                    source.Id,
                    source.QuotationId,
                    source.QuotationRowId,
                    source.CalculationRowId,
                    source.CustomerOrderId,
                    source.SupplierOrderId,
                    source.CostId,
                    source.NrOf,
                    source.InPrice,
                    source.InPriceCurrencyId,
                    source.OutPrice,
                    source.DoDebit,
                    source.Note,
                    source.SupplierId,
                    source.DoPrintOnCustomerOrder,
                    source.DoPrintOnSupplierOrder,
                    source.DoPrintOnQuotation,
                    source.SupplierName,
                    source.InPriceAttested,
                    source.CreatedById,
                    source.CreatedDateTime,
                    source.EditedById,
                    source.EditedDateTime,
                    source.AttestedInPriceCurrencyRate,
                    source.AttestedOutPriceCurrencyRate,
                    source.AtttestedBySignature,
                    source.AttestedDateTime,
                    source.DoInvoiceSeparately,
                    source.DoInvoiceSeparatelyImmediately,
                    source.IsCostInvoicedSeparately,
                    SupplierPurchaseCurrencyRate = source.SupplierOrder != null ? source.SupplierOrder.PurchaseCurrencyRate : null,
                    CustomerSalesCurrencyRate = source.CustomerOrder != null ? source.CustomerOrder.SalesCurrencyRate : null,
                    CreatedByName = source.CreatedByUser != null
                        ? EF.Functions.Collate(source.CreatedByUser.Name ?? string.Empty, SearchCollation)
                        : EF.Functions.Collate(string.Empty, SearchCollation),
                    CustomerOrderNr = source.CustomerOrder != null
                        ? EF.Functions.Collate(source.CustomerOrder.CustomerOrderNr ?? string.Empty, SearchCollation)
                        : source.SupplierOrder != null
                            ? EF.Functions.Collate(source.SupplierOrder.CustomerOrderNr ?? string.Empty, SearchCollation)
                            : EF.Functions.Collate(string.Empty, SearchCollation),
                    OrderSupplierName = source.SupplierOrder != null
                        ? EF.Functions.Collate(source.SupplierOrder.SupplierName ?? string.Empty, SearchCollation)
                        : EF.Functions.Collate(source.SupplierName ?? string.Empty, SearchCollation),
                    OrderCustomerName = source.CustomerOrder != null
                        ? EF.Functions.Collate(source.CustomerOrder.CustomerName ?? string.Empty, SearchCollation)
                        : EF.Functions.Collate(string.Empty, SearchCollation),
                    CostName = source.Cost != null
                        ? EF.Functions.Collate(source.Cost.Name ?? string.Empty, SearchCollation)
                        : EF.Functions.Collate(string.Empty, SearchCollation),
                    InPriceCurrencyName = source.InPriceCurrency != null
                        ? EF.Functions.Collate(source.InPriceCurrency.Name ?? string.Empty, SearchCollation)
                        : source.SupplierOrder != null && source.SupplierOrder.PurchaseCurrency != null
                            ? EF.Functions.Collate(source.SupplierOrder.PurchaseCurrency.Name ?? string.Empty, SearchCollation)
                            : EF.Functions.Collate(string.Empty, SearchCollation),
                    OutPriceCurrencyName = source.CustomerOrder != null && source.CustomerOrder.SalesCurrency != null
                        ? EF.Functions.Collate(source.CustomerOrder.SalesCurrency.Name ?? string.Empty, SearchCollation)
                        : EF.Functions.Collate(string.Empty, SearchCollation),
                    AttestedByName = EF.Functions.Collate(source.AtttestedBySignature ?? string.Empty, SearchCollation),
                    InvoiceId = source.InvoiceRows
                        .Where(x => x.InvoiceId.HasValue)
                        .OrderByDescending(x => x.InvoiceId)
                        .Select(x => x.InvoiceId)
                        .FirstOrDefault(),
                    InvoiceNumber = source.InvoiceRows
                        .Where(x => x.InvoiceId.HasValue)
                        .OrderByDescending(x => x.InvoiceId)
                        .Select(x => x.Invoice != null ? x.Invoice.InvoiceNumber : null)
                        .FirstOrDefault()
                })
                .ToListAsync();
            var fetchElapsedMs = stopwatch.ElapsedMilliseconds;

            var items = projectedRows
                .Select(row =>
                {
                    var inPriceSek = row.InPrice.HasValue && row.SupplierPurchaseCurrencyRate is > 0
                        ? row.InPrice.Value * (decimal)row.SupplierPurchaseCurrencyRate.Value
                        : row.InPrice;

                    var inPriceAttestedSek = row.InPriceAttested.HasValue && row.SupplierPurchaseCurrencyRate is > 0
                        ? row.InPriceAttested.Value * (decimal)row.SupplierPurchaseCurrencyRate.Value
                        : row.InPriceAttested;

                    var outPriceSek = row.OutPrice.HasValue && row.CustomerSalesCurrencyRate is > 0
                        ? row.OutPrice.Value * (decimal)row.CustomerSalesCurrencyRate.Value
                        : row.OutPrice;

                    return new OrderCostDto
                    {
                        Id = row.Id,
                        QuotationId = row.QuotationId,
                        QuotationRowId = row.QuotationRowId,
                        CalculationRowId = row.CalculationRowId,
                        CustomerOrderId = row.CustomerOrderId,
                        SupplierOrderId = row.SupplierOrderId,
                        CostId = row.CostId,
                        NrOf = row.NrOf,
                        InPrice = row.InPrice,
                        InPriceCurrencyId = row.InPriceCurrencyId,
                        OutPrice = row.OutPrice,
                        DoDebit = row.DoDebit,
                        Note = row.Note,
                        SupplierId = row.SupplierId,
                        DoPrintOnCustomerOrder = row.DoPrintOnCustomerOrder,
                        DoPrintOnSupplierOrder = row.DoPrintOnSupplierOrder,
                        DoPrintOnQuotation = row.DoPrintOnQuotation,
                        SupplierName = row.SupplierName,
                        InPriceAttested = row.InPriceAttested,
                        CreatedById = row.CreatedById,
                        CreatedDateTime = row.CreatedDateTime,
                        EditedById = row.EditedById,
                        EditedDateTime = row.EditedDateTime,
                        AttestedInPriceCurrencyRate = row.AttestedInPriceCurrencyRate,
                        AttestedOutPriceCurrencyRate = row.AttestedOutPriceCurrencyRate,
                        AtttestedBySignature = row.AtttestedBySignature,
                        AttestedDateTime = row.AttestedDateTime,
                        DoInvoiceSeparately = row.DoInvoiceSeparately,
                        DoInvoiceSeparatelyImmediately = row.DoInvoiceSeparatelyImmediately,
                        IsCostInvoicedSeparately = row.IsCostInvoicedSeparately,
                        InPriceSEK = inPriceSek,
                        InPriceAttestedSEK = inPriceAttestedSek,
                        OutPriceSEK = outPriceSek,
                        Markup = inPriceSek is null or 0 || outPriceSek is null
                            ? null
                            : (outPriceSek.Value - inPriceSek.Value) / inPriceSek.Value,
                        CreatedByName = row.CreatedByName ?? string.Empty,
                        CustomerOrderNr = row.CustomerOrderNr ?? string.Empty,
                        OrderSupplierName = row.OrderSupplierName ?? string.Empty,
                        OrderCustomerName = row.OrderCustomerName ?? string.Empty,
                        CostName = NormalizeOrderCostName(row.CostName) ?? string.Empty,
                        InPriceCurrencyName = row.InPriceCurrencyName ?? string.Empty,
                        OutPriceCurrencyName = row.OutPriceCurrencyName ?? string.Empty,
                        AttestedByName = row.AttestedByName ?? string.Empty,
                        InvoiceId = row.InvoiceId,
                        InvoiceNumber = row.InvoiceNumber
                    };
                })
                .ToList();

            stopwatch.Stop();
            _logger.LogInformation(
                "Order cost search completed in {ElapsedMilliseconds} ms. Count {CountElapsedMs} ms, fetch {FetchElapsedMs} ms. Page {PageNumber} of {TotalPages} returned {ItemCount} items from {TotalCount} total.",
                stopwatch.ElapsedMilliseconds,
                countElapsedMs,
                fetchElapsedMs - countElapsedMs,
                pagination.PageNumber,
                totalPages,
                items.Count,
                totalCount);

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

        private static HashSet<string> BuildUpdatedFieldSet(IEnumerable<string>? updatedFields)
        {
            return updatedFields is null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(
                    updatedFields
                        .Where(field => !string.IsNullOrWhiteSpace(field))
                        .Select(field => field.Trim()),
                    StringComparer.OrdinalIgnoreCase);
        }

        private async Task<(decimal? PurchaseRate, decimal? SalesRate)> ResolveCalculationCurrencyRatesAsync(int? customerOrderId)
        {
            if (!customerOrderId.HasValue)
            {
                return (null, null);
            }

            var calculationId = await _dbContext.CustomerOrders
                .AsNoTracking()
                .Where(x => x.Id == customerOrderId.Value)
                .Select(x => x.CalculationId)
                .FirstOrDefaultAsync();

            if (!calculationId.HasValue)
            {
                return (null, null);
            }

            var rates = await _dbContext.Calculations
                .AsNoTracking()
                .Where(x => x.Id == calculationId.Value)
                .Select(x => new
                {
                    x.PurchaseCurrencyRate,
                    x.SalesCurrencyRate,
                })
                .FirstOrDefaultAsync();

            return ((decimal?)rates?.PurchaseCurrencyRate, (decimal?)rates?.SalesCurrencyRate);
        }

        private static OrderCostDto MapToDto(OrderCost source)
        {
            var createdByName = source.CreatedByUser?.Name ?? string.Empty;
            var editedByName = source.EditedByUser?.Name ?? string.Empty;

            var invoiceRow = source.InvoiceRows
                .Where(x => x.InvoiceId.HasValue)
                .OrderByDescending(x => x.InvoiceId)
                .FirstOrDefault();

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
                ,
                CreatedByName = createdByName,
                EditedByName = editedByName,
                CustomerOrderNr = source.CustomerOrder?.CustomerOrderNr ?? source.SupplierOrder?.CustomerOrderNr ?? string.Empty,
                OrderSupplierName = source.SupplierOrder?.SupplierName ?? source.SupplierName ?? string.Empty,
                OrderCustomerName = source.CustomerOrder?.CustomerName ?? string.Empty,
                CostName = NormalizeOrderCostName(source.Cost?.Name) ?? string.Empty,
                InPriceCurrencyName = source.InPriceCurrency?.Name ?? source.SupplierOrder?.PurchaseCurrency?.Name ?? string.Empty,
                OutPriceCurrencyName = source.CustomerOrder?.SalesCurrency?.Name ?? string.Empty,
                AttestedByName = source.AtttestedBySignature ?? string.Empty,
                InvoiceId = invoiceRow?.InvoiceId,
                InvoiceNumber = invoiceRow?.Invoice?.InvoiceNumber
            };
        }

        private static string? NormalizeOrderCostName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            var span = name.AsSpan();
            var index = 0;

            while (index < span.Length && char.IsWhiteSpace(span[index]))
            {
                index++;
            }

            var digitsStart = index;
            while (index < span.Length && char.IsDigit(span[index]))
            {
                index++;
            }

            if (index == digitsStart)
            {
                return name;
            }

            var whitespaceStart = index;
            while (index < span.Length && char.IsWhiteSpace(span[index]))
            {
                index++;
            }

            if (index == whitespaceStart || index >= span.Length)
            {
                return name;
            }

            return name[index..].Trim();
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
