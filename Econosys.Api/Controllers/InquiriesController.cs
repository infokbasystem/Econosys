using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Econosys.Api.Common;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;

namespace Econosys.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class InquiriesController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        private readonly ILogger<InquiriesController> _logger;

        private readonly ILegacyUserResolutionService _legacyUserResolution;

        public InquiriesController(ApplicationDbContext dbContext, ILogger<InquiriesController> logger, ILegacyUserResolutionService legacyUserResolution)
        {
            _dbContext = dbContext;
            _logger = logger;
            _legacyUserResolution = legacyUserResolution;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<InquiryDto>> GetById(int id)
        {
            var inquiry = await _dbContext.Inquiries
                .AsNoTracking()
                .Include(x => x.CreatedByUser)
                .Include(x => x.EditedByUser)
                .Include(x => x.Customer)
                .Include(x => x.SelectedInquiryRecipient)
                    .ThenInclude(x => x!.Supplier)
                .Include(x => x.SelectedInquiryRecipient)
                    .ThenInclude(x => x!.Language)
                .Include(x => x.Calculation)
                    .ThenInclude(x => x!.PurchaseCurrency)
                .Include(x => x.Calculation)
                    .ThenInclude(x => x!.SalesCurrency)
                .Include(x => x.CustomerDeliveryAddress)
                .Include(x => x.InquiryRows)
                    .ThenInclude(x => x.CalculationRow)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (inquiry is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(inquiry));
        }

        /// <summary>Builds an unsaved inquiry from the saved calculation using legacy defaults.</summary>
        [HttpGet("from-calculation/{calculationId:int}")]
        public async Task<ActionResult<InquiryDto>> FromCalculation(int calculationId, CancellationToken cancellationToken)
        {
            var user = await _legacyUserResolution.ResolveCurrentUserAsync(User);
            if (user is null)
            {
                return Unauthorized(new { message = "Could not map authenticated user to a legacy user by email." });
            }

            var calculation = await _dbContext.Calculations.AsNoTracking()
                .Include(x => x.Product).ThenInclude(x => x!.Material)
                .Include(x => x.Product).ThenInclude(x => x!.Construction)
                .Include(x => x.Product).ThenInclude(x => x!.Varnish)
                .Include(x => x.Supplier).ThenInclude(x => x!.Language)
                .Include(x => x.Customer)
                .Include(x => x.PurchaseCurrency)
                .Include(x => x.SalesCurrency)
                .Include(x => x.CalculationRows)
                .FirstOrDefaultAsync(x => x.Id == calculationId, cancellationToken);
            if (calculation is null)
            {
                return NotFound();
            }
            if (calculation.Product is null || calculation.Supplier is null || calculation.Customer is null)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest,
                    detail: "Calculation must have a product, supplier and customer.");
            }

            var addressQuery = _dbContext.CustomerDeliveryAddresses.AsNoTracking()
                .Where(x => x.CustomerId == calculation.CustomerId);
            var address = calculation.AutoFreightPrice && calculation.CustomerDeliveryAddressId.HasValue
                ? await addressQuery.FirstOrDefaultAsync(x => x.Id == calculation.CustomerDeliveryAddressId, cancellationToken)
                : await addressQuery.OrderBy(x => x.Id).FirstOrDefaultAsync(x => x.IsDefault, cancellationToken);
            var answerDays = await _dbContext.Settings.Where(x => x.CompanyId == calculation.CompanyId)
                .Select(x => x.NrOfInquiryAnswerDays).FirstOrDefaultAsync(cancellationToken);
            var languageCode = calculation.Supplier.Language?.LanguageCode;
            var codes = new[] { calculation.Product.Material?.TranslationCode,
                calculation.Product.Construction?.TranslationCode, calculation.Product.Varnish?.TranslationCode };
            var translations = string.IsNullOrWhiteSpace(languageCode) ? new List<TranslationItem>()
                : await _dbContext.TranslationItems.AsNoTracking()
                    .Where(x => x.LangCode == languageCode && codes.Contains(x.TranslationCode))
                    .OrderBy(x => x.Id).ToListAsync(cancellationToken);

            return Ok(InquiryDraftMapper.Map(calculation, address, user, SwedishTime.Now,
                answerDays, translations.Where(x => x.TranslationCode.HasValue)
                    .GroupBy(x => x.TranslationCode!.Value)
                    .ToDictionary(x => x.Key, x => x.First().Translation)));
        }

        [HttpPost]
        public async Task<ActionResult<InquiryDto>> Create([FromBody] CreateInquiryRequest request)
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

            var requestedRowIds = request.CalculationRowIds.Distinct().ToList();
            var calculationRows = await _dbContext.CalculationRows
                .Where(x => requestedRowIds.Contains(x.Id)
                    && x.CalculationId == request.CalculationId && !x.Archived)
                .ToListAsync();
            if (calculationRows.Count != requestedRowIds.Count)
            {
                return BadRequest(new { message = "Inquiry rows must belong to the calculation and must not be archived." });
            }

            InquiryRecipient? newRecipient = null;
            if (!request.SelectedInquiryRecipientId.HasValue && request.SelectedSupplierId.HasValue)
            {
                var calculation = await _dbContext.Calculations.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == request.CalculationId
                        && x.SupplierId == request.SelectedSupplierId);
                if (calculation is null)
                {
                    return BadRequest(new { message = "Supplier must belong to the calculation." });
                }
                newRecipient = new InquiryRecipient
                {
                    CompanyId = request.CompanyId,
                    SupplierId = request.SelectedSupplierId,
                    YourRef = request.SelectedRecipientYourRef,
                    TermsOfDelivery = request.SelectedRecipientTermsOfDelivery,
                    TermsOfPayment = request.SelectedRecipientTermsOfPayment,
                    LanguageId = request.SelectedRecipientLanguageId,
                    Note = calculation.LoadingInstruction,
                };
            }

            var entity = new Inquiry
            {
                CompanyId = request.CompanyId,
                CalculationId = request.CalculationId,
                AutoGenerated = request.AutoGenerated,
                CustomerId = request.CustomerId,
                Product = request.Product,
                Material = request.Material,
                MaterialThickness = request.MaterialThickness,
                Format = request.Format,
                Color = request.Color,
                Construction = request.Construction,
                CustomerName = request.CustomerName,
                DeliveryAddressName = request.DeliveryAddressName,
                DeliveryAddress = request.DeliveryAddress,
                DeliveryPostalNr = request.DeliveryPostalNr,
                DeliveryPostalAddress = request.DeliveryPostalAddress,
                DeliveryCountry = request.DeliveryCountry,
                OurRef = request.OurRef,
                Preparation = request.Preparation,
                TimeOfDelivery = request.TimeOfDelivery,
                SelectedInquiryRecipientId = request.SelectedInquiryRecipientId,
                AnswerDueDate = request.AnswerDueDate,
                HideCustomerInfoOnPrint = request.HideCustomerInfoOnPrint,
                CustomerDeliveryAddressId = request.CustomerDeliveryAddressId,
                IsFSC = request.IsFSC,
                InquiryRows = calculationRows.Select(row => new InquiryRow
                {
                    CalculationRowId = row.Id,
                    Edition = row.Edition,
                }).ToList(),
            };

            entity.CreatedAt = SwedishTime.Now;
            entity.CreatedBy = legacyUser.Id;
            entity.EditedAt = SwedishTime.Now;
            entity.EditedBy = legacyUser.Id;

            if (newRecipient is not null)
            {
                entity.InquiryRecipients.Add(newRecipient);
            }
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            _dbContext.Inquiries.Add(entity);
            await ApplySelectedInquiryRecipientFieldsAsync(request, entity.SelectedInquiryRecipientId);
            await _dbContext.SaveChangesAsync();
            if (newRecipient is not null)
            {
                entity.SelectedInquiryRecipientId = newRecipient.Id;
                await _dbContext.SaveChangesAsync();
            }
            await transaction.CommitAsync();

            var saved = await _dbContext.Inquiries
                .AsNoTracking()
                .Include(x => x.CreatedByUser)
                .Include(x => x.EditedByUser)
                .Include(x => x.Customer)
                .Include(x => x.SelectedInquiryRecipient)
                    .ThenInclude(x => x!.Supplier)
                .Include(x => x.SelectedInquiryRecipient)
                    .ThenInclude(x => x!.Language)
                .Include(x => x.Calculation)
                    .ThenInclude(x => x!.PurchaseCurrency)
                .Include(x => x.Calculation)
                    .ThenInclude(x => x!.SalesCurrency)
                .Include(x => x.InquiryRows)
                    .ThenInclude(x => x.CalculationRow)
                .FirstAsync(x => x.Id == entity.Id);

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(saved));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<InquiryDto>> Update(int id, [FromBody] UpdateInquiryRequest request)
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

            var entity = await _dbContext.Inquiries
                .Include(x => x.InquiryRows)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            entity.CompanyId = request.CompanyId;
            entity.CalculationId = request.CalculationId;
            entity.AutoGenerated = request.AutoGenerated;
            entity.CustomerId = request.CustomerId;
            entity.Product = request.Product;
            entity.Material = request.Material;
            entity.MaterialThickness = request.MaterialThickness;
            entity.Format = request.Format;
            entity.Color = request.Color;
            entity.Construction = request.Construction;
            entity.CustomerName = request.CustomerName;
            entity.DeliveryAddressName = request.DeliveryAddressName;
            entity.DeliveryAddress = request.DeliveryAddress;
            entity.DeliveryPostalNr = request.DeliveryPostalNr;
            entity.DeliveryPostalAddress = request.DeliveryPostalAddress;
            entity.DeliveryCountry = request.DeliveryCountry;
            entity.OurRef = request.OurRef;
            entity.Preparation = request.Preparation;
            entity.TimeOfDelivery = request.TimeOfDelivery;
            entity.SelectedInquiryRecipientId = request.SelectedInquiryRecipientId;
            entity.AnswerDueDate = request.AnswerDueDate;
            entity.HideCustomerInfoOnPrint = request.HideCustomerInfoOnPrint;
            entity.CustomerDeliveryAddressId = request.CustomerDeliveryAddressId;
            entity.IsFSC = request.IsFSC;

            entity.EditedAt = SwedishTime.Now;
            entity.EditedBy = legacyUser.Id;

            var requestedRowIds = request.InquiryRowIds
                .Where(x => x > 0)
                .ToHashSet();

            var rowsToDelete = entity.InquiryRows
                .Where(x => !requestedRowIds.Contains(x.Id))
                .ToList();

            if (rowsToDelete.Count > 0)
            {
                _dbContext.InquiryRows.RemoveRange(rowsToDelete);
            }

            await ApplySelectedInquiryRecipientFieldsAsync(request, entity.SelectedInquiryRecipientId);
            await _dbContext.SaveChangesAsync();

            var saved = await _dbContext.Inquiries
                .AsNoTracking()
                .Include(x => x.CreatedByUser)
                .Include(x => x.EditedByUser)
                .Include(x => x.Customer)
                .Include(x => x.SelectedInquiryRecipient)
                    .ThenInclude(x => x!.Supplier)
                .Include(x => x.SelectedInquiryRecipient)
                    .ThenInclude(x => x!.Language)
                .Include(x => x.Calculation)
                    .ThenInclude(x => x!.PurchaseCurrency)
                .Include(x => x.Calculation)
                    .ThenInclude(x => x!.SalesCurrency)
                .Include(x => x.InquiryRows)
                    .ThenInclude(x => x.CalculationRow)
                .FirstAsync(x => x.Id == entity.Id);

            return Ok(MapToDto(saved));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _dbContext.Inquiries
                .Include(x => x.InquiryRows)
                .Include(x => x.InquiryRecipients)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            entity.SelectedInquiryRecipientId = null;
            await _dbContext.SaveChangesAsync();
            _dbContext.InquiryRows.RemoveRange(entity.InquiryRows);
            _dbContext.InquiryRecipients.RemoveRange(entity.InquiryRecipients);
            _dbContext.Inquiries.Remove(entity);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return NoContent();
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<InquirySearchItemDto>>> Search([FromBody] SearchInquiriesRequest? request)
        {
            var pagination = request?.Pagination ?? new PaginationRequest
            {
                PageNumber = 1,
                PageSize = 20,
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
                .Select(x => new InquirySearchItemDto
                {
                    Id = x.Inquiry.Id,
                    Product = x.Inquiry.Product,
                    CustomerName = x.Inquiry.Customer != null ? x.Inquiry.Customer.Name : x.Inquiry.CustomerName,
                    SupplierName = x.SelectedSupplier != null ? x.SelectedSupplier.Name : null,
                    Construction = x.Inquiry.Construction,
                    Material = x.Inquiry.Material,
                    Format = x.Inquiry.Format,
                    SellerName = x.Inquiry.Customer != null && x.Inquiry.Customer.ResponsibleUser != null
                        ? x.Inquiry.Customer.ResponsibleUser.Name
                        : null,
                    CreatedByName = x.CreatedByUser != null ? x.CreatedByUser.Name : null,
                    CreatedAt = x.Inquiry.CreatedAt,
                    EditedAt = x.Inquiry.EditedAt,
                })
                .ToListAsync();

            return Ok(new PagedResultDto<InquirySearchItemDto>
            {
                Items = items,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
            });
        }

        private IQueryable<InquirySearchProjection> BuildSearchQuery()
        {
            return _dbContext.Inquiries
                .AsNoTracking()
                .Include(x => x.Customer)
                    .ThenInclude(x => x!.ResponsibleUser)
                .GroupJoin(
                    _dbContext.InquiryRecipients.AsNoTracking(),
                    inquiry => inquiry.SelectedInquiryRecipientId,
                    inquiryRecipient => (int?)inquiryRecipient.Id,
                    (inquiry, inquiryRecipients) => new { inquiry, inquiryRecipients }
                )
                .SelectMany(
                    row => row.inquiryRecipients.DefaultIfEmpty(),
                    (row, selectedInquiryRecipient) => new
                    {
                        Inquiry = row.inquiry,
                        CreatedByUser = row.inquiry.CreatedByUser,
                        SelectedSupplierId = selectedInquiryRecipient != null ? selectedInquiryRecipient.SupplierId : null,
                    }
                )
                .GroupJoin(
                    _dbContext.Suppliers.AsNoTracking(),
                    row => row.SelectedSupplierId,
                    supplier => (int?)supplier.Id,
                    (row, suppliers) => new { row, suppliers }
                )
                .SelectMany(
                    row => row.suppliers.DefaultIfEmpty(),
                    (row, selectedSupplier) => new InquirySearchProjection
                    {
                        Inquiry = row.row.Inquiry,
                        CreatedByUser = row.row.CreatedByUser,
                        SelectedSupplier = selectedSupplier,
                    }
                );
        }

        private static IQueryable<InquirySearchProjection> ApplySearchFilter(IQueryable<InquirySearchProjection> query, string? searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return query;
            }

            var term = searchTerm.Trim().ToLower();

            return query.Where(x =>
                x.Inquiry.Id.ToString().Contains(term) ||
                (x.Inquiry.Product != null && x.Inquiry.Product.ToLower().Contains(term)) ||
                (x.Inquiry.CustomerName != null && x.Inquiry.CustomerName.ToLower().Contains(term)) ||
                (x.Inquiry.Customer != null && x.Inquiry.Customer.Name != null && x.Inquiry.Customer.Name.ToLower().Contains(term)) ||
                (x.SelectedSupplier != null && x.SelectedSupplier.Name != null && x.SelectedSupplier.Name.ToLower().Contains(term)) ||
                (x.Inquiry.Construction != null && x.Inquiry.Construction.ToLower().Contains(term)) ||
                (x.Inquiry.Material != null && x.Inquiry.Material.ToLower().Contains(term)) ||
                (x.Inquiry.Format != null && x.Inquiry.Format.ToLower().Contains(term)) ||
                (x.Inquiry.Customer != null && x.Inquiry.Customer.ResponsibleUser != null && x.Inquiry.Customer.ResponsibleUser.Name != null && x.Inquiry.Customer.ResponsibleUser.Name.ToLower().Contains(term)) ||
                (x.CreatedByUser != null && x.CreatedByUser.Name != null && x.CreatedByUser.Name.ToLower().Contains(term))
            );
        }

        private static IQueryable<InquirySearchProjection> ApplySearchOrdering(IQueryable<InquirySearchProjection> query, List<SortRequest>? orderBy)
        {
            if (orderBy == null || orderBy.Count == 0)
            {
                return query.OrderByDescending(x => x.Inquiry.CreatedAt ?? DateTime.MinValue).ThenByDescending(x => x.Inquiry.Id);
            }

            IOrderedQueryable<InquirySearchProjection>? orderedQuery = null;

            foreach (var sort in orderBy)
            {
                var field = (sort.Field ?? string.Empty).Trim().ToLowerInvariant();
                var isDescending = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);

                orderedQuery = ApplyOrder(orderedQuery ?? query, field, isDescending, orderedQuery != null);
            }

            return orderedQuery ?? query.OrderByDescending(x => x.Inquiry.CreatedAt ?? DateTime.MinValue).ThenByDescending(x => x.Inquiry.Id);
        }

        private static IOrderedQueryable<InquirySearchProjection> ApplyOrder(
            IQueryable<InquirySearchProjection> query,
            string field,
            bool isDescending,
            bool isThenBy)
        {
            return (field, isDescending, isThenBy) switch
            {
                ("id", true, false) => query.OrderByDescending(x => x.Inquiry.Id),
                ("id", false, false) => query.OrderBy(x => x.Inquiry.Id),
                ("id", true, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenByDescending(x => x.Inquiry.Id),
                ("id", false, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenBy(x => x.Inquiry.Id),

                ("product", true, false) => query.OrderByDescending(x => x.Inquiry.Product ?? string.Empty),
                ("product", false, false) => query.OrderBy(x => x.Inquiry.Product ?? string.Empty),
                ("product", true, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenByDescending(x => x.Inquiry.Product ?? string.Empty),
                ("product", false, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenBy(x => x.Inquiry.Product ?? string.Empty),

                ("customername", true, false) => query.OrderByDescending(x => (x.Inquiry.Customer != null ? x.Inquiry.Customer.Name : x.Inquiry.CustomerName) ?? string.Empty),
                ("customername", false, false) => query.OrderBy(x => (x.Inquiry.Customer != null ? x.Inquiry.Customer.Name : x.Inquiry.CustomerName) ?? string.Empty),
                ("customername", true, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenByDescending(x => (x.Inquiry.Customer != null ? x.Inquiry.Customer.Name : x.Inquiry.CustomerName) ?? string.Empty),
                ("customername", false, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenBy(x => (x.Inquiry.Customer != null ? x.Inquiry.Customer.Name : x.Inquiry.CustomerName) ?? string.Empty),

                ("suppliername", true, false) => query.OrderByDescending(x => x.SelectedSupplier != null ? x.SelectedSupplier.Name : string.Empty),
                ("suppliername", false, false) => query.OrderBy(x => x.SelectedSupplier != null ? x.SelectedSupplier.Name : string.Empty),
                ("suppliername", true, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenByDescending(x => x.SelectedSupplier != null ? x.SelectedSupplier.Name : string.Empty),
                ("suppliername", false, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenBy(x => x.SelectedSupplier != null ? x.SelectedSupplier.Name : string.Empty),

                ("construction", true, false) => query.OrderByDescending(x => x.Inquiry.Construction ?? string.Empty),
                ("construction", false, false) => query.OrderBy(x => x.Inquiry.Construction ?? string.Empty),
                ("construction", true, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenByDescending(x => x.Inquiry.Construction ?? string.Empty),
                ("construction", false, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenBy(x => x.Inquiry.Construction ?? string.Empty),

                ("material", true, false) => query.OrderByDescending(x => x.Inquiry.Material ?? string.Empty),
                ("material", false, false) => query.OrderBy(x => x.Inquiry.Material ?? string.Empty),
                ("material", true, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenByDescending(x => x.Inquiry.Material ?? string.Empty),
                ("material", false, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenBy(x => x.Inquiry.Material ?? string.Empty),

                ("format", true, false) => query.OrderByDescending(x => x.Inquiry.Format ?? string.Empty),
                ("format", false, false) => query.OrderBy(x => x.Inquiry.Format ?? string.Empty),
                ("format", true, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenByDescending(x => x.Inquiry.Format ?? string.Empty),
                ("format", false, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenBy(x => x.Inquiry.Format ?? string.Empty),

                ("sellername", true, false) => query.OrderByDescending(x => x.Inquiry.Customer != null && x.Inquiry.Customer.ResponsibleUser != null ? x.Inquiry.Customer.ResponsibleUser.Name : string.Empty),
                ("sellername", false, false) => query.OrderBy(x => x.Inquiry.Customer != null && x.Inquiry.Customer.ResponsibleUser != null ? x.Inquiry.Customer.ResponsibleUser.Name : string.Empty),
                ("sellername", true, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenByDescending(x => x.Inquiry.Customer != null && x.Inquiry.Customer.ResponsibleUser != null ? x.Inquiry.Customer.ResponsibleUser.Name : string.Empty),
                ("sellername", false, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenBy(x => x.Inquiry.Customer != null && x.Inquiry.Customer.ResponsibleUser != null ? x.Inquiry.Customer.ResponsibleUser.Name : string.Empty),

                ("createdbyname", true, false) => query.OrderByDescending(x => x.CreatedByUser != null ? x.CreatedByUser.Name : string.Empty),
                ("createdbyname", false, false) => query.OrderBy(x => x.CreatedByUser != null ? x.CreatedByUser.Name : string.Empty),
                ("createdbyname", true, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenByDescending(x => x.CreatedByUser != null ? x.CreatedByUser.Name : string.Empty),
                ("createdbyname", false, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenBy(x => x.CreatedByUser != null ? x.CreatedByUser.Name : string.Empty),

                ("createdat", true, false) => query.OrderByDescending(x => x.Inquiry.CreatedAt ?? DateTime.MinValue),
                ("createdat", false, false) => query.OrderBy(x => x.Inquiry.CreatedAt ?? DateTime.MinValue),
                ("createdat", true, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenByDescending(x => x.Inquiry.CreatedAt ?? DateTime.MinValue),
                ("createdat", false, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenBy(x => x.Inquiry.CreatedAt ?? DateTime.MinValue),

                ("editedat", true, false) => query.OrderByDescending(x => x.Inquiry.EditedAt ?? DateTime.MinValue),
                ("editedat", false, false) => query.OrderBy(x => x.Inquiry.EditedAt ?? DateTime.MinValue),
                ("editedat", true, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenByDescending(x => x.Inquiry.EditedAt ?? DateTime.MinValue),
                ("editedat", false, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenBy(x => x.Inquiry.EditedAt ?? DateTime.MinValue),

                (_, true, false) => query.OrderByDescending(x => x.Inquiry.CreatedAt ?? DateTime.MinValue).ThenByDescending(x => x.Inquiry.Id),
                (_, false, false) => query.OrderBy(x => x.Inquiry.CreatedAt ?? DateTime.MinValue).ThenBy(x => x.Inquiry.Id),
                (_, true, true) => ((IOrderedQueryable<InquirySearchProjection>)query).ThenByDescending(x => x.Inquiry.CreatedAt ?? DateTime.MinValue).ThenByDescending(x => x.Inquiry.Id),
                _ => ((IOrderedQueryable<InquirySearchProjection>)query).ThenBy(x => x.Inquiry.CreatedAt ?? DateTime.MinValue).ThenBy(x => x.Inquiry.Id),
            };
        }

        private sealed class InquirySearchProjection
        {
            public Inquiry Inquiry { get; set; } = null!;
            public LegacyUser? CreatedByUser { get; set; }
            public Supplier? SelectedSupplier { get; set; }
        }

        private async Task ApplySelectedInquiryRecipientFieldsAsync(CreateInquiryRequest request, int? selectedInquiryRecipientId)
        {
            if (!selectedInquiryRecipientId.HasValue)
            {
                return;
            }

            var recipient = await _dbContext.InquiryRecipients
                .FirstOrDefaultAsync(x => x.Id == selectedInquiryRecipientId.Value);

            if (recipient is null)
            {
                return;
            }

            recipient.YourRef = request.SelectedRecipientYourRef;
            recipient.TermsOfDelivery = request.SelectedRecipientTermsOfDelivery;
            recipient.TermsOfPayment = request.SelectedRecipientTermsOfPayment;
            recipient.LanguageId = request.SelectedRecipientLanguageId;
        }

        private static InquiryDto MapToDto(Inquiry inquiry)
        {
            return new InquiryDto
            {
                Id = inquiry.Id,
                CompanyId = inquiry.CompanyId,
                CalculationId = inquiry.CalculationId,
                PurchaseCurrencyName = inquiry.Calculation?.PurchaseCurrency?.Name,
                SalesCurrencyName = inquiry.Calculation?.SalesCurrency?.Name,
                AutoGenerated = inquiry.AutoGenerated,
                CustomerId = inquiry.CustomerId,
                Product = inquiry.Product,
                Material = inquiry.Material,
                MaterialThickness = inquiry.MaterialThickness,
                Format = inquiry.Format,
                Color = inquiry.Color,
                Construction = inquiry.Construction,
                CustomerName = inquiry.CustomerName,
                DeliveryAddressName = inquiry.DeliveryAddressName,
                DeliveryAddress = inquiry.DeliveryAddress,
                DeliveryPostalNr = inquiry.DeliveryPostalNr,
                DeliveryPostalAddress = inquiry.DeliveryPostalAddress,
                DeliveryCountry = inquiry.DeliveryCountry,
                OurRef = inquiry.OurRef,
                Preparation = inquiry.Preparation,
                TimeOfDelivery = inquiry.TimeOfDelivery,
                SelectedInquiryRecipientId = inquiry.SelectedInquiryRecipientId,
                SelectedSupplierId = inquiry.SelectedInquiryRecipient?.SupplierId,
                SelectedSupplierName = inquiry.SelectedInquiryRecipient?.Supplier?.Name,
                SelectedSupplierEmail = inquiry.SelectedInquiryRecipient?.Supplier?.Email,
                SelectedRecipientYourRef = inquiry.SelectedInquiryRecipient?.YourRef,
                SelectedRecipientTermsOfDelivery = inquiry.SelectedInquiryRecipient?.TermsOfDelivery,
                SelectedRecipientTermsOfPayment = inquiry.SelectedInquiryRecipient?.TermsOfPayment,
                SelectedRecipientLanguageId = inquiry.SelectedInquiryRecipient?.LanguageId,
                SelectedRecipientLanguageName = inquiry.SelectedInquiryRecipient?.Language?.Name,
                AnswerDueDate = inquiry.AnswerDueDate,
                HideCustomerInfoOnPrint = inquiry.HideCustomerInfoOnPrint,
                CreatedAt = inquiry.CreatedAt,
                EditedAt = inquiry.EditedAt,
                CreatedBy = inquiry.CreatedBy,
                CreatedByUserName = inquiry.CreatedByUser?.Name,
                EditedBy = inquiry.EditedBy,
                EditedByUserName = inquiry.EditedByUser?.Name,
                CustomerDeliveryAddressId = inquiry.CustomerDeliveryAddressId,
                IsFSC = inquiry.IsFSC,
                CustomerEmail = inquiry.Customer?.Email,
                InquiryRows = inquiry.InquiryRows
                    .Select(r => new InquiryRowDto
                    {
                        Id = r.Id,
                        InquiryId = r.InquiryId,
                        CalculationRowId = r.CalculationRowId,
                        Edition = r.Edition,
                        PurchasePrice = r.CalculationRow?.PurchasePrice,
                        Freight = r.CalculationRow?.Freight,
                    })
                    .ToList(),
            };
        }
    }
}