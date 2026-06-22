using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;

namespace Econosys.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class QuotationsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<QuotationsController> _logger;

        private readonly ILegacyUserResolutionService _legacyUserResolution;
        private readonly IPalletFormatOptionsService _palletFormatOptionsService;

        public QuotationsController(
            ApplicationDbContext dbContext,
            ILogger<QuotationsController> logger,
            ILegacyUserResolutionService legacyUserResolution,
            IPalletFormatOptionsService palletFormatOptionsService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _legacyUserResolution = legacyUserResolution;
            _palletFormatOptionsService = palletFormatOptionsService;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<QuotationDto>> GetById(int id)
        {
            var quotation = await BuildDetailsQuery()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (quotation is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(quotation));
        }

        [HttpGet("{id:int}/form-options")]
        public async Task<ActionResult<QuotationFormOptionsDto>> GetFormOptions(int id)
        {
            var quotationContext = await _dbContext.Quotations
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.CustomerId,
                    SupplierId = x.Inquiry != null && x.Inquiry.SelectedInquiryRecipient != null
                        ? x.Inquiry.SelectedInquiryRecipient.SupplierId
                        : (int?)null,
                })
                .FirstOrDefaultAsync();

            if (quotationContext is null)
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

            var palletFormats = await _palletFormatOptionsService.GetOptionsAsync(quotationContext.CustomerId, quotationContext.SupplierId);

            return Ok(new QuotationFormOptionsDto
            {
                Users = users,
                Costs = costs,
                Currencies = currencies,
                PalletFormats = palletFormats,
            });
        }

        [HttpPost]
        public async Task<ActionResult<QuotationDto>> Create([FromBody] CreateQuotationRequest request)
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

            var entity = new Quotation();
            // ApplyRequestToEntity(entity, request);

            entity.CreatedAt ??= SwedishTime.Now;
            entity.CreatedBy ??= legacyUser.Id;
            entity.EditedAt = SwedishTime.Now;
            entity.EditedBy = legacyUser.Id;

            _dbContext.Quotations.Add(entity);
            await _dbContext.SaveChangesAsync();

            await SyncQuotationRowsAsync(entity, request.QuotationRowIds);
            await SyncOrderCostsAsync(entity.Id, request.OrderCosts);
            await _dbContext.SaveChangesAsync();

            var saved = await BuildDetailsQuery().FirstAsync(x => x.Id == entity.Id);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(saved));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<QuotationDto>> Update(int id, [FromBody] UpdateQuotationRequest request)
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

            var entity = await _dbContext.Quotations
                .Include(x => x.QuotationRows)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            entity.Date = request.Date;
            entity.DeliveryAddress = request.DeliveryAddress;
            entity.DeliveryAddressName = request.DeliveryAddressName;
            entity.DeliveryCountry = request.DeliveryCountry;
            entity.DeliveryPostalAddress = request.DeliveryPostalAddress;
            entity.DeliveryPostalNr = request.DeliveryPostalNr;
            entity.HideDeliveryAddressOnPrint = request.HideDeliveryAddressOnPrint;
            entity.TermsOfDelivery = request.TermsOfDelivery;
            entity.TermsOfPayment = request.TermsOfPayment;
            entity.TimeOfDelivery = request.TimeOfDelivery;
            entity.YourReference = request.YourReference;
            entity.OurReference = request.OurReference;
            entity.Message = request.Message;
            entity.PalletFormatId = request.PalletFormatId;
            entity.EurPallet = request.EurPallet;
            entity.NrOfEurPallet = request.NrOfEurPallet;


            entity.EditedAt = SwedishTime.Now;
            entity.EditedBy = legacyUser.Id;

            await SyncQuotationRowsAsync(entity, request.QuotationRowIds);
            await SyncOrderCostsAsync(entity.Id, request.OrderCosts);
            await _dbContext.SaveChangesAsync();

            var saved = await BuildDetailsQuery().FirstAsync(x => x.Id == entity.Id);
            return Ok(MapToDto(saved));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _dbContext.Quotations
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            var quotationRows = await _dbContext.QuotationRows
                .Where(x => x.QuotationId == id)
                .ToListAsync();

            var quotationRowIds = quotationRows
                .Select(x => x.Id)
                .ToHashSet();

            var orderCosts = await _dbContext.OrderCosts
                .Where(x => x.QuotationId == id || (x.QuotationRowId.HasValue && quotationRowIds.Contains(x.QuotationRowId.Value)))
                .ToListAsync();

            foreach (var orderCost in orderCosts)
            {
                if (orderCost.QuotationId == id)
                {
                    orderCost.QuotationId = null;
                }

                if (orderCost.QuotationRowId.HasValue && quotationRowIds.Contains(orderCost.QuotationRowId.Value))
                {
                    orderCost.QuotationRowId = null;
                }
            }

            var customerOrders = await _dbContext.CustomerOrders
                .Where(x => x.QuotationId == id)
                .ToListAsync();

            foreach (var customerOrder in customerOrders)
            {
                customerOrder.QuotationId = null;
            }

            var supplierOrders = await _dbContext.SupplierOrders
                .Where(x => x.QuotationId == id)
                .ToListAsync();

            foreach (var supplierOrder in supplierOrders)
            {
                supplierOrder.QuotationId = null;
            }

            if (quotationRows.Count > 0)
            {
                _dbContext.QuotationRows.RemoveRange(quotationRows);
            }

            _dbContext.Quotations.Remove(entity);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<QuotationSearchItemDto>>> Search([FromBody] SearchQuotationsRequest? request)
        {
            var pagination = request?.Pagination ?? new PaginationRequest
            {
                PageNumber = 1,
                PageSize = 25,
            };

            var query = BuildSearchQuery();
            query = ApplySearchFilter(query, request?.SearchTerm);

            var totalCount = await query.CountAsync();
            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pagination.PageSize);

            var orderedQuery = ApplySearchOrdering(query, request?.OrderBy);

            var items = await orderedQuery
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .Select(x => new QuotationSearchItemDto
                {
                    Id = x.Id,
                    Product = x.Product,
                    CustomerName = x.CustomerName,
                    SupplierName = x.SupplierName,
                    Construction = x.Construction,
                    Material = x.Material,
                    Format = x.Format,
                    SellerName = x.SellerName,
                    CreatedByName = x.CreatedByName,
                    CreatedAt = x.CreatedAt,
                    EditedAt = x.EditedAt,
                })
                .ToListAsync();

            return Ok(new PagedResultDto<QuotationSearchItemDto>
            {
                Items = items,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
            });
        }


        private IQueryable<QuotationSearchProjection> BuildSearchQuery()
        {
            return _dbContext.Quotations
                .AsNoTracking()
                .Select(x => new QuotationSearchProjection
                {
                    Id = x.Id,
                    Product = x.Product,
                    CustomerName = x.Customer != null ? x.Customer.Name : x.CustomerName,
                    SupplierName = x.Inquiry != null && x.Inquiry.SelectedInquiryRecipient != null && x.Inquiry.SelectedInquiryRecipient.Supplier != null
                        ? x.Inquiry.SelectedInquiryRecipient.Supplier.Name
                        : null,
                    Construction = x.Construction,
                    Material = x.Material,
                    Format = x.Format,
                    SellerName = x.Inquiry != null && x.Inquiry.Customer != null && x.Inquiry.Customer.ResponsibleUser != null
                        ? x.Inquiry.Customer.ResponsibleUser.Name
                        : null,
                    CreatedByName = x.CreatedByUser != null ? x.CreatedByUser.Name : null,
                    CreatedAt = x.CreatedAt,
                    EditedAt = x.EditedAt,
                });
        }

        private static IQueryable<QuotationSearchProjection> ApplySearchFilter(IQueryable<QuotationSearchProjection> query, string? searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return query;
            }

            var term = searchTerm.Trim().ToLower();

            return query.Where(x =>
                x.Id.ToString().Contains(term) ||
                (x.Product != null && x.Product.ToLower().Contains(term)) ||
                (x.CustomerName != null && x.CustomerName.ToLower().Contains(term)) ||
                (x.SupplierName != null && x.SupplierName.ToLower().Contains(term)) ||
                (x.Construction != null && x.Construction.ToLower().Contains(term)) ||
                (x.Material != null && x.Material.ToLower().Contains(term)) ||
                (x.Format != null && x.Format.ToLower().Contains(term)) ||
                (x.SellerName != null && x.SellerName.ToLower().Contains(term)) ||
                (x.CreatedByName != null && x.CreatedByName.ToLower().Contains(term))
            );
        }

        private static IQueryable<QuotationSearchProjection> ApplySearchOrdering(IQueryable<QuotationSearchProjection> query, List<SortRequest>? orderBy)
        {
            if (orderBy == null || orderBy.Count == 0)
            {
                return query.OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue).ThenByDescending(x => x.Id);
            }

            IOrderedQueryable<QuotationSearchProjection>? orderedQuery = null;

            foreach (var sort in orderBy)
            {
                var field = (sort.Field ?? string.Empty).Trim().ToLowerInvariant();
                var isDescending = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);

                orderedQuery = ApplyOrder(orderedQuery ?? query, field, isDescending, orderedQuery != null);
            }

            return orderedQuery ?? query.OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue).ThenByDescending(x => x.Id);
        }

        private static IOrderedQueryable<QuotationSearchProjection> ApplyOrder(
            IQueryable<QuotationSearchProjection> query,
            string field,
            bool isDescending,
            bool isThenBy)
        {
            return (field, isDescending, isThenBy) switch
            {
                ("id", true, false) => query.OrderByDescending(x => x.Id),
                ("id", false, false) => query.OrderBy(x => x.Id),
                ("id", true, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenByDescending(x => x.Id),
                ("id", false, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenBy(x => x.Id),

                ("product", true, false) => query.OrderByDescending(x => x.Product ?? string.Empty),
                ("product", false, false) => query.OrderBy(x => x.Product ?? string.Empty),
                ("product", true, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenByDescending(x => x.Product ?? string.Empty),
                ("product", false, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenBy(x => x.Product ?? string.Empty),

                ("customername", true, false) => query.OrderByDescending(x => x.CustomerName ?? string.Empty),
                ("customername", false, false) => query.OrderBy(x => x.CustomerName ?? string.Empty),
                ("customername", true, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenByDescending(x => x.CustomerName ?? string.Empty),
                ("customername", false, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenBy(x => x.CustomerName ?? string.Empty),

                ("suppliername", true, false) => query.OrderByDescending(x => x.SupplierName ?? string.Empty),
                ("suppliername", false, false) => query.OrderBy(x => x.SupplierName ?? string.Empty),
                ("suppliername", true, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenByDescending(x => x.SupplierName ?? string.Empty),
                ("suppliername", false, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenBy(x => x.SupplierName ?? string.Empty),

                ("construction", true, false) => query.OrderByDescending(x => x.Construction ?? string.Empty),
                ("construction", false, false) => query.OrderBy(x => x.Construction ?? string.Empty),
                ("construction", true, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenByDescending(x => x.Construction ?? string.Empty),
                ("construction", false, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenBy(x => x.Construction ?? string.Empty),

                ("material", true, false) => query.OrderByDescending(x => x.Material ?? string.Empty),
                ("material", false, false) => query.OrderBy(x => x.Material ?? string.Empty),
                ("material", true, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenByDescending(x => x.Material ?? string.Empty),
                ("material", false, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenBy(x => x.Material ?? string.Empty),

                ("format", true, false) => query.OrderByDescending(x => x.Format ?? string.Empty),
                ("format", false, false) => query.OrderBy(x => x.Format ?? string.Empty),
                ("format", true, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenByDescending(x => x.Format ?? string.Empty),
                ("format", false, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenBy(x => x.Format ?? string.Empty),

                ("sellername", true, false) => query.OrderByDescending(x => x.SellerName ?? string.Empty),
                ("sellername", false, false) => query.OrderBy(x => x.SellerName ?? string.Empty),
                ("sellername", true, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenByDescending(x => x.SellerName ?? string.Empty),
                ("sellername", false, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenBy(x => x.SellerName ?? string.Empty),

                ("createdbyname", true, false) => query.OrderByDescending(x => x.CreatedByName ?? string.Empty),
                ("createdbyname", false, false) => query.OrderBy(x => x.CreatedByName ?? string.Empty),
                ("createdbyname", true, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenByDescending(x => x.CreatedByName ?? string.Empty),
                ("createdbyname", false, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenBy(x => x.CreatedByName ?? string.Empty),

                ("createdat", true, false) => query.OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue),
                ("createdat", false, false) => query.OrderBy(x => x.CreatedAt ?? DateTime.MinValue),
                ("createdat", true, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenByDescending(x => x.CreatedAt ?? DateTime.MinValue),
                ("createdat", false, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenBy(x => x.CreatedAt ?? DateTime.MinValue),

                ("editedat", true, false) => query.OrderByDescending(x => x.EditedAt ?? DateTime.MinValue),
                ("editedat", false, false) => query.OrderBy(x => x.EditedAt ?? DateTime.MinValue),
                ("editedat", true, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenByDescending(x => x.EditedAt ?? DateTime.MinValue),
                ("editedat", false, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenBy(x => x.EditedAt ?? DateTime.MinValue),

                (_, true, false) => query.OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue).ThenByDescending(x => x.Id),
                (_, false, false) => query.OrderBy(x => x.CreatedAt ?? DateTime.MinValue).ThenBy(x => x.Id),
                (_, true, true) => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenByDescending(x => x.CreatedAt ?? DateTime.MinValue).ThenByDescending(x => x.Id),
                _ => ((IOrderedQueryable<QuotationSearchProjection>)query).ThenBy(x => x.CreatedAt ?? DateTime.MinValue).ThenBy(x => x.Id),
            };
        }

        private sealed class QuotationSearchProjection
        {
            public int Id { get; set; }
            public string? Product { get; set; }
            public string? CustomerName { get; set; }
            public string? SupplierName { get; set; }
            public string? Construction { get; set; }
            public string? Material { get; set; }
            public string? Format { get; set; }
            public string? SellerName { get; set; }
            public string? CreatedByName { get; set; }
            public DateTime? CreatedAt { get; set; }
            public DateTime? EditedAt { get; set; }
        }

        private IQueryable<Quotation> BuildDetailsQuery()
        {
            return _dbContext.Quotations
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Inquiry)
                    .ThenInclude(x => x!.SelectedInquiryRecipient)
                        .ThenInclude(x => x!.Supplier)
                .Include(x => x.Inquiry)
                    .ThenInclude(x => x!.Customer)
                        .ThenInclude(x => x!.ResponsibleUser)
                .Include(x => x.SalesCurrency)
                .Include(x => x.Unit)
                .Include(x => x.CreatedByUser)
                .Include(x => x.EditedByUser)
                .Include(x => x.PurchaseCurrency)
                .Include(x => x.CustomerDeliveryAddress)
                .Include(x => x.PalletFormat)
                .Include(x => x.QuotationRows)
                    .ThenInclude(x => x.CalculationRow)
                        .ThenInclude(x => x!.Calculation)
                            .ThenInclude(x => x!.SalesCurrency)
                .Include(x => x.QuotationRows)
                    .ThenInclude(x => x.CalculationRow)
                        .ThenInclude(x => x!.Calculation)
                            .ThenInclude(x => x!.Unit)
                .Include(x => x.OrderCosts)
                    .ThenInclude(x => x.CustomerOrder)
                .Include(x => x.OrderCosts)
                    .ThenInclude(x => x.InvoiceRows);
        }

        private async Task SyncQuotationRowsAsync(Quotation quotation, IEnumerable<int>? requestedRowIds)
        {
            var targetIds = (requestedRowIds ?? Enumerable.Empty<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToHashSet();

            var currentRows = await _dbContext.QuotationRows
                .Where(x => x.QuotationId == quotation.Id)
                .ToListAsync();

            var rowsToDelete = currentRows
                .Where(x => !targetIds.Contains(x.Id))
                .ToList();

            if (rowsToDelete.Count > 0)
            {
                _dbContext.QuotationRows.RemoveRange(rowsToDelete);
            }

            var currentIds = currentRows
                .Where(x => x.Id > 0)
                .Select(x => x.Id)
                .ToHashSet();

            var idsToAttach = targetIds
                .Where(id => !currentIds.Contains(id))
                .ToList();

            if (idsToAttach.Count == 0)
            {
                return;
            }

            var rowsToAttach = await _dbContext.QuotationRows
                .Where(x => idsToAttach.Contains(x.Id))
                .ToListAsync();

            foreach (var row in rowsToAttach)
            {
                row.QuotationId = quotation.Id;
                if (row.CompanyId <= 0)
                {
                    row.CompanyId = quotation.CompanyId;
                }
            }
        }

        private async Task SyncOrderCostsAsync(int quotationId, List<UpsertQuotationOrderCostRequest>? requestedCosts)
        {
            var requested = requestedCosts ?? new List<UpsertQuotationOrderCostRequest>();

            var requestedIds = requested
                .Where(x => x.Id.HasValue && x.Id.Value > 0)
                .Select(x => x.Id!.Value)
                .ToHashSet();

            var existing = await _dbContext.OrderCosts
                .Where(x => x.QuotationId == quotationId)
                .ToListAsync();

            var toDelete = existing.Where(x => !requestedIds.Contains(x.Id)).ToList();
            if (toDelete.Count > 0)
                _dbContext.OrderCosts.RemoveRange(toDelete);

            foreach (var req in requested)
            {
                if (req.Id.HasValue && req.Id.Value > 0)
                {
                    var entity = existing.FirstOrDefault(x => x.Id == req.Id.Value);
                    if (entity != null)
                        ApplyOrderCostRequest(entity, req);
                }
                else if (req.CostId.HasValue)
                {
                    var entity = new OrderCost { QuotationId = quotationId };
                    ApplyOrderCostRequest(entity, req);
                    _dbContext.OrderCosts.Add(entity);
                }
            }
        }

        private static void ApplyOrderCostRequest(OrderCost entity, UpsertQuotationOrderCostRequest req)
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

        private static QuotationOrderCostDto MapOrderCostToDto(OrderCost source)
        {
            var invoiceRow = source.InvoiceRows
                .Where(x => x.InvoiceId.HasValue)
                .OrderByDescending(x => x.InvoiceId)
                .FirstOrDefault();

            return new QuotationOrderCostDto
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

        private static QuotationDto MapToDto(Quotation quotation)
        {
            return new QuotationDto
            {
                Id = quotation.Id,
                CompanyId = quotation.CompanyId,
                CalculationId = quotation.CalculationId,
                InquiryId = quotation.InquiryId,
                CustomerId = quotation.CustomerId,
                CustomerName = quotation.CustomerName,
                Address = quotation.Address,
                PostalNr = quotation.PostalNr,
                PostalAddress = quotation.PostalAddress,
                Country = quotation.Country,
                DeliveryAddressName = quotation.DeliveryAddressName,
                DeliveryAddress = quotation.DeliveryAddress,
                DeliveryPostalNr = quotation.DeliveryPostalNr,
                DeliveryPostalAddress = quotation.DeliveryPostalAddress,
                DeliveryCountry = quotation.DeliveryCountry,
                Date = quotation.Date,
                YourReference = quotation.YourReference,
                OurReference = quotation.OurReference,
                Product = quotation.Product,
                Material = quotation.Material,
                MaterialThickness = quotation.MaterialThickness,
                Format = quotation.Format,
                Color = quotation.Color,
                Construction = quotation.Construction,
                TimeOfDelivery = quotation.TimeOfDelivery,
                TermsOfDelivery = quotation.TermsOfDelivery,
                TermsOfPayment = quotation.TermsOfPayment,
                PaymentDays = quotation.PaymentDays,
                Message = quotation.Message,
                SalesCurrencyId = quotation.SalesCurrencyId,
                UnitId = quotation.UnitId,
                PartOfPrintEdition = quotation.PartOfPrintEdition,
                PartOfPrintEditionValue = quotation.PartOfPrintEditionValue,
                PartOfPunchEdition = quotation.PartOfPunchEdition,
                PartOfPunchEditionValue = quotation.PartOfPunchEditionValue,
                ChangeOfSheet = quotation.ChangeOfSheet,
                NrOfChangeOfSheet = quotation.NrOfChangeOfSheet,
                ChangeOfSheetValue = quotation.ChangeOfSheetValue,
                ChangeOfCliche = quotation.ChangeOfCliche,
                NrOfChangeOfCliche = quotation.NrOfChangeOfCliche,
                ChangeOfClicheValue = quotation.ChangeOfClicheValue,
                ChangeOfColor = quotation.ChangeOfColor,
                NrOfChangeOfColor = quotation.NrOfChangeOfColor,
                ChangeOfColorValue = quotation.ChangeOfColorValue,
                PmsColor = quotation.PmsColor,
                PmsColorValue = quotation.PmsColorValue,
                EurPallet = quotation.EurPallet,
                NrOfEurPallet = quotation.NrOfEurPallet,
                EurPalletValue = quotation.EurPalletValue,
                OtherCost = quotation.OtherCost,
                OtherCostName = quotation.OtherCostName,
                OtherCostValue = quotation.OtherCostValue,
                CreatedAt = quotation.CreatedAt,
                EditedAt = quotation.EditedAt,
                CreatedBy = quotation.CreatedBy,
                CreatedByUserName = quotation.CreatedByUser?.Name,
                EditedBy = quotation.EditedBy,
                EditedByUserName = quotation.EditedByUser?.Name,
                Printed = quotation.Printed,
                HideDeliveryAddressOnPrint = quotation.HideDeliveryAddressOnPrint,
                ProductMessage = quotation.ProductMessage,
                NoteInternal = quotation.NoteInternal,
                SalesCurrencyRate = quotation.SalesCurrencyRate,
                PurchaseCurrencyId = quotation.PurchaseCurrencyId,
                PurchaseCurrencyRate = quotation.PurchaseCurrencyRate,
                Address2 = quotation.Address2,
                AutoGenerated = quotation.AutoGenerated,
                CustomerDeliveryAddressId = quotation.CustomerDeliveryAddressId,
                PalletFormatId = quotation.PalletFormatId,
                IsFsc = quotation.IsFsc,
                CustomerEmail = quotation.Customer?.Email,
                SelectedSupplierId = quotation.Inquiry?.SelectedInquiryRecipient?.SupplierId,
                SelectedSupplierName = quotation.Inquiry?.SelectedInquiryRecipient?.Supplier?.Name,
                SellerName = quotation.Inquiry?.Customer?.ResponsibleUser?.Name,
                QuotationRows = quotation.QuotationRows
                    .Select(row => new QuotationRowDto
                    {
                        Id = row.Id,
                        QuotationId = row.QuotationId,
                        CalculationRowId = row.CalculationRowId,
                        Edition = row.Edition,
                        Price = row.Price,
                        SalesCurrencyName = row.CalculationRow?.Calculation?.SalesCurrency?.Name,
                        UnitName = row.CalculationRow?.Calculation?.Unit?.Name,
                        UnitMultiplicator = row.CalculationRow?.Calculation?.Unit?.Multiplicator,
                    })
                    .ToList(),
                OrderCosts = quotation.OrderCosts
                    .OrderBy(x => x.Id)
                    .Select(MapOrderCostToDto)
                    .ToList(),
            };
        }
    }
}