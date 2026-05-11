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
    public class CustomersController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(ApplicationDbContext dbContext, ILogger<CustomersController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<CustomerDto>> GetById(int id)
        {
            var customer = await _dbContext.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (customer is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(customer));
        }

        [HttpPost]
        public async Task<ActionResult<CustomerDto>> Create([FromBody] CreateCustomerRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var customer = new Customer
            {
                Name = request.Name,
                SortName = request.SortName,
                Address = request.Address,
                PostalNr = request.PostalNr,
                PostalAddress = request.PostalAddress,
                VisitingAddress = request.VisitingAddress,
                Reference = request.Reference,
                Telephone1 = request.Telephone1,
                Telephone2 = request.Telephone2,
                Telephone3 = request.Telephone3,
                Fax = request.Fax,
                Note = request.Note,
                LoadingInstruction = request.LoadingInstruction,
                ResponsibleUserId = request.ResponsibleUserId,
                VAT = request.VAT,
                Active = request.Active,
                Country = request.Country,
                TermsOfDelivery = request.TermsOfDelivery,
                TermsOfPayment = request.TermsOfPayment,
                PaymentDays = request.PaymentDays,
                LanguageId = request.LanguageId,
                VATNr = request.VATNr,
                Email = request.Email,
                QuotationCommunicationTypeId = request.QuotationCommunicationTypeId,
                CustomerOrderCommunicationTypeId = request.CustomerOrderCommunicationTypeId,
                InvoiceCommunicationTypeId = request.InvoiceCommunicationTypeId,
                OrgNr = request.OrgNr,
                EU = request.EU,
                Export = request.Export,
                CreditLimit = request.CreditLimit,
                CurrencyId = request.CurrencyId,
                ExternalKey = request.ExternalKey,
                VATRate = request.VATRate,
                Address2 = request.Address2,
                OneWayPallet = request.OneWayPallet,
                EURPallet = request.EURPallet,
                OldDbId = request.OldDbId,
                SpecialUnitHandling = request.SpecialUnitHandling,
                PrintForDocumentScanning = request.PrintForDocumentScanning,
                PricePerEurPallet = request.PricePerEurPallet,
                LockOrder = request.LockOrder,
                LastCustomerOrderDate = request.LastCustomerOrderDate,
                AccountNrAccountsReceivable = request.AccountNrAccountsReceivable,
                AccountNrEarnings = request.AccountNrEarnings,
                SupportEmployeeId = request.SupportEmployeeId,
                LastActivity = request.LastActivity,
                CountryId = request.CountryId,
                Category = request.Category,
                IsProspect = request.IsProspect,
                InvoicePalletsSeparately = request.InvoicePalletsSeparately,
                InvoiceCostsSeparately = request.InvoiceCostsSeparately,
                InvoiceCostsSeparatelyImmediately = request.InvoiceCostsSeparatelyImmediately,
                BudgetCountAsNewUntilMonth = request.BudgetCountAsNewUntilMonth,
                InvoiceRowsInProductNameOrder = request.InvoiceRowsInProductNameOrder,
                OrderNrPrefix = request.OrderNrPrefix
            };

            _dbContext.Customers.Add(customer);
            await _dbContext.SaveChangesAsync();

            var response = MapToDto(customer);
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<CustomerDto>>> Search([FromBody] SearchCustomersRequest? request)
        {
            IQueryable<Customer> query = _dbContext.Customers.AsNoTracking();

            var pagination = request?.Pagination;

            // Apply filters
            if (!string.IsNullOrWhiteSpace(request?.SearchTerm))
            {
                var searchTerm = request.SearchTerm.ToLower();
                query = query.Where(c =>
                    c.Name != null && c.Name.ToLower().Contains(searchTerm) ||
                    c.Email != null && c.Email.ToLower().Contains(searchTerm) ||
                    c.OrgNr != null && c.OrgNr.ToLower().Contains(searchTerm));
            }

            if (request?.Active.HasValue == true)
            {
                query = query.Where(c => c.Active == request.Active.Value);
            }

            if (request?.IsProspect.HasValue == true)
            {
                query = query.Where(c => c.IsProspect == request.IsProspect.Value);
            }

            if (request?.CountryId.HasValue == true)
            {
                query = query.Where(c => c.CountryId == request.CountryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request?.Category))
            {
                query = query.Where(c => c.Category == request.Category);
            }

            // Apply sorting
            var sortBy = request?.SortBy ?? "Name";
            var sortDescending = request?.SortDescending ?? false;

            query = sortBy.ToLower() switch
            {
                "name" => sortDescending ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
                "email" => sortDescending ? query.OrderByDescending(c => c.Email) : query.OrderBy(c => c.Email),
                "lastactivity" => sortDescending ? query.OrderByDescending(c => c.LastActivity) : query.OrderBy(c => c.LastActivity),
                _ => query.OrderBy(c => c.Name)
            };

            var totalCount = await query.CountAsync();
            List<Customer> items;
            int pageNumber;
            int pageSize;
            int totalPages;

            if (pagination is null)
            {
                items = await query.ToListAsync();
                pageNumber = 1;
                pageSize = totalCount;
                totalPages = totalCount == 0 ? 0 : 1;
            }
            else
            {
                items = await query
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToListAsync();

                pageNumber = pagination.PageNumber;
                pageSize = pagination.PageSize;
                totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pagination.PageSize);
            }

            var dtos = items.Select(MapToDto).ToList();

            return Ok(new PagedResultDto<CustomerDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
            });
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<CustomerDto>> Update(int id, [FromBody] UpdateCustomerRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id);

            if (customer is null)
            {
                return NotFound();
            }

            // Update fields
            if (request.Name != null) customer.Name = request.Name;
            if (request.SortName != null) customer.SortName = request.SortName;
            if (request.Address != null) customer.Address = request.Address;
            if (request.PostalNr != null) customer.PostalNr = request.PostalNr;
            if (request.PostalAddress != null) customer.PostalAddress = request.PostalAddress;
            if (request.VisitingAddress != null) customer.VisitingAddress = request.VisitingAddress;
            if (request.Reference != null) customer.Reference = request.Reference;
            if (request.Telephone1 != null) customer.Telephone1 = request.Telephone1;
            if (request.Telephone2 != null) customer.Telephone2 = request.Telephone2;
            if (request.Telephone3 != null) customer.Telephone3 = request.Telephone3;
            if (request.Fax != null) customer.Fax = request.Fax;
            if (request.Note != null) customer.Note = request.Note;
            if (request.LoadingInstruction != null) customer.LoadingInstruction = request.LoadingInstruction;
            if (request.ResponsibleUserId.HasValue) customer.ResponsibleUserId = request.ResponsibleUserId;
            if (request.VAT.HasValue) customer.VAT = request.VAT.Value;
            if (request.Active.HasValue) customer.Active = request.Active.Value;
            if (request.Country != null) customer.Country = request.Country;
            if (request.TermsOfDelivery != null) customer.TermsOfDelivery = request.TermsOfDelivery;
            if (request.TermsOfPayment != null) customer.TermsOfPayment = request.TermsOfPayment;
            if (request.PaymentDays.HasValue) customer.PaymentDays = request.PaymentDays;
            if (request.LanguageId.HasValue) customer.LanguageId = request.LanguageId;
            if (request.VATNr != null) customer.VATNr = request.VATNr;
            if (request.Email != null) customer.Email = request.Email;
            if (request.QuotationCommunicationTypeId.HasValue) customer.QuotationCommunicationTypeId = request.QuotationCommunicationTypeId;
            if (request.CustomerOrderCommunicationTypeId.HasValue) customer.CustomerOrderCommunicationTypeId = request.CustomerOrderCommunicationTypeId;
            if (request.InvoiceCommunicationTypeId.HasValue) customer.InvoiceCommunicationTypeId = request.InvoiceCommunicationTypeId;
            if (request.OrgNr != null) customer.OrgNr = request.OrgNr;
            if (request.EU.HasValue) customer.EU = request.EU.Value;
            if (request.Export.HasValue) customer.Export = request.Export.Value;
            if (request.CreditLimit.HasValue) customer.CreditLimit = request.CreditLimit;
            if (request.CurrencyId.HasValue) customer.CurrencyId = request.CurrencyId;
            if (request.ExternalKey != null) customer.ExternalKey = request.ExternalKey;
            if (request.VATRate.HasValue) customer.VATRate = request.VATRate;
            if (request.Address2 != null) customer.Address2 = request.Address2;
            if (request.OneWayPallet.HasValue) customer.OneWayPallet = request.OneWayPallet.Value;
            if (request.EURPallet.HasValue) customer.EURPallet = request.EURPallet.Value;
            if (request.OldDbId.HasValue) customer.OldDbId = request.OldDbId;
            if (request.SpecialUnitHandling.HasValue) customer.SpecialUnitHandling = request.SpecialUnitHandling.Value;
            if (request.PrintForDocumentScanning.HasValue) customer.PrintForDocumentScanning = request.PrintForDocumentScanning.Value;
            if (request.PricePerEurPallet.HasValue) customer.PricePerEurPallet = request.PricePerEurPallet;
            if (request.LockOrder.HasValue) customer.LockOrder = request.LockOrder.Value;
            if (request.LastCustomerOrderDate.HasValue) customer.LastCustomerOrderDate = request.LastCustomerOrderDate;
            if (request.AccountNrAccountsReceivable.HasValue) customer.AccountNrAccountsReceivable = request.AccountNrAccountsReceivable;
            if (request.AccountNrEarnings.HasValue) customer.AccountNrEarnings = request.AccountNrEarnings;
            if (request.SupportEmployeeId.HasValue) customer.SupportEmployeeId = request.SupportEmployeeId;
            if (request.LastActivity != null) customer.LastActivity = request.LastActivity;
            if (request.CountryId.HasValue) customer.CountryId = request.CountryId;
            if (request.Category != null) customer.Category = request.Category;
            if (request.IsProspect.HasValue) customer.IsProspect = request.IsProspect.Value;
            if (request.InvoicePalletsSeparately.HasValue) customer.InvoicePalletsSeparately = request.InvoicePalletsSeparately.Value;
            if (request.InvoiceCostsSeparately.HasValue) customer.InvoiceCostsSeparately = request.InvoiceCostsSeparately.Value;
            if (request.InvoiceCostsSeparatelyImmediately.HasValue) customer.InvoiceCostsSeparatelyImmediately = request.InvoiceCostsSeparatelyImmediately.Value;
            if (request.BudgetCountAsNewUntilMonth.HasValue) customer.BudgetCountAsNewUntilMonth = request.BudgetCountAsNewUntilMonth;
            if (request.InvoiceRowsInProductNameOrder.HasValue) customer.InvoiceRowsInProductNameOrder = request.InvoiceRowsInProductNameOrder.Value;
            if (request.OrderNrPrefix != null) customer.OrderNrPrefix = request.OrderNrPrefix;

            await _dbContext.SaveChangesAsync();

            return Ok(MapToDto(customer));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == id);

            if (customer is null)
            {
                return NotFound();
            }

            _dbContext.Customers.Remove(customer);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        private static CustomerDto MapToDto(Customer customer)
        {
            return new CustomerDto
            {
                Id = customer.Id,
                Name = customer.Name,
                SortName = customer.SortName,
                Address = customer.Address,
                PostalNr = customer.PostalNr,
                PostalAddress = customer.PostalAddress,
                VisitingAddress = customer.VisitingAddress,
                Reference = customer.Reference,
                Telephone1 = customer.Telephone1,
                Telephone2 = customer.Telephone2,
                Telephone3 = customer.Telephone3,
                Fax = customer.Fax,
                Note = customer.Note,
                LoadingInstruction = customer.LoadingInstruction,
                ResponsibleUserId = customer.ResponsibleUserId,
                VAT = customer.VAT,
                Active = customer.Active,
                Country = customer.Country,
                TermsOfDelivery = customer.TermsOfDelivery,
                TermsOfPayment = customer.TermsOfPayment,
                PaymentDays = customer.PaymentDays,
                LanguageId = customer.LanguageId,
                VATNr = customer.VATNr,
                Email = customer.Email,
                QuotationCommunicationTypeId = customer.QuotationCommunicationTypeId,
                CustomerOrderCommunicationTypeId = customer.CustomerOrderCommunicationTypeId,
                InvoiceCommunicationTypeId = customer.InvoiceCommunicationTypeId,
                OrgNr = customer.OrgNr,
                EU = customer.EU,
                Export = customer.Export,
                CreditLimit = customer.CreditLimit,
                CurrencyId = customer.CurrencyId,
                ExternalKey = customer.ExternalKey,
                VATRate = customer.VATRate,
                Address2 = customer.Address2,
                OneWayPallet = customer.OneWayPallet,
                EURPallet = customer.EURPallet,
                OldDbId = customer.OldDbId,
                SpecialUnitHandling = customer.SpecialUnitHandling,
                PrintForDocumentScanning = customer.PrintForDocumentScanning,
                PricePerEurPallet = customer.PricePerEurPallet,
                LockOrder = customer.LockOrder,
                LastCustomerOrderDate = customer.LastCustomerOrderDate,
                AccountNrAccountsReceivable = customer.AccountNrAccountsReceivable,
                AccountNrEarnings = customer.AccountNrEarnings,
                SupportEmployeeId = customer.SupportEmployeeId,
                LastActivity = customer.LastActivity,
                CountryId = customer.CountryId,
                Category = customer.Category,
                IsProspect = customer.IsProspect,
                InvoicePalletsSeparately = customer.InvoicePalletsSeparately,
                InvoiceCostsSeparately = customer.InvoiceCostsSeparately,
                InvoiceCostsSeparatelyImmediately = customer.InvoiceCostsSeparatelyImmediately,
                BudgetCountAsNewUntilMonth = customer.BudgetCountAsNewUntilMonth,
                InvoiceRowsInProductNameOrder = customer.InvoiceRowsInProductNameOrder,
                OrderNrPrefix = customer.OrderNrPrefix
            };
        }
    }

    public class CreateCustomerRequest
    {
        public string? Name { get; set; }
        public string? SortName { get; set; }
        public string? Address { get; set; }
        public string? PostalNr { get; set; }
        public string? PostalAddress { get; set; }
        public string? VisitingAddress { get; set; }
        public string? Reference { get; set; }
        public string? Telephone1 { get; set; }
        public string? Telephone2 { get; set; }
        public string? Telephone3 { get; set; }
        public string? Fax { get; set; }
        public string? Note { get; set; }
        public string? LoadingInstruction { get; set; }
        public int? ResponsibleUserId { get; set; }
        public bool VAT { get; set; }
        public bool Active { get; set; }
        public string? Country { get; set; }
        public string? TermsOfDelivery { get; set; }
        public string? TermsOfPayment { get; set; }
        public short? PaymentDays { get; set; }
        public int? LanguageId { get; set; }
        public string? VATNr { get; set; }
        public string? Email { get; set; }
        public int? QuotationCommunicationTypeId { get; set; }
        public int? CustomerOrderCommunicationTypeId { get; set; }
        public int? InvoiceCommunicationTypeId { get; set; }
        public string? OrgNr { get; set; }
        public bool EU { get; set; }
        public bool Export { get; set; }
        public int? CreditLimit { get; set; }
        public int? CurrencyId { get; set; }
        public string? ExternalKey { get; set; }
        public short? VATRate { get; set; }
        public string? Address2 { get; set; }
        public bool OneWayPallet { get; set; }
        public bool EURPallet { get; set; }
        public int? OldDbId { get; set; }
        public bool SpecialUnitHandling { get; set; }
        public bool PrintForDocumentScanning { get; set; }
        public decimal? PricePerEurPallet { get; set; }
        public bool LockOrder { get; set; }
        public DateTime? LastCustomerOrderDate { get; set; }
        public int? AccountNrAccountsReceivable { get; set; }
        public int? AccountNrEarnings { get; set; }
        public int? SupportEmployeeId { get; set; }
        public string? LastActivity { get; set; }
        public int? CountryId { get; set; }
        public string? Category { get; set; }
        public bool IsProspect { get; set; }
        public bool InvoicePalletsSeparately { get; set; }
        public bool InvoiceCostsSeparately { get; set; }
        public bool InvoiceCostsSeparatelyImmediately { get; set; }
        public DateOnly? BudgetCountAsNewUntilMonth { get; set; }
        public bool InvoiceRowsInProductNameOrder { get; set; }
        public string? OrderNrPrefix { get; set; }
    }

    public class UpdateCustomerRequest
    {
        public string? Name { get; set; }
        public string? SortName { get; set; }
        public string? Address { get; set; }
        public string? PostalNr { get; set; }
        public string? PostalAddress { get; set; }
        public string? VisitingAddress { get; set; }
        public string? Reference { get; set; }
        public string? Telephone1 { get; set; }
        public string? Telephone2 { get; set; }
        public string? Telephone3 { get; set; }
        public string? Fax { get; set; }
        public string? Note { get; set; }
        public string? LoadingInstruction { get; set; }
        public int? ResponsibleUserId { get; set; }
        public bool? VAT { get; set; }
        public bool? Active { get; set; }
        public string? Country { get; set; }
        public string? TermsOfDelivery { get; set; }
        public string? TermsOfPayment { get; set; }
        public short? PaymentDays { get; set; }
        public int? LanguageId { get; set; }
        public string? VATNr { get; set; }
        public string? Email { get; set; }
        public int? QuotationCommunicationTypeId { get; set; }
        public int? CustomerOrderCommunicationTypeId { get; set; }
        public int? InvoiceCommunicationTypeId { get; set; }
        public string? OrgNr { get; set; }
        public bool? EU { get; set; }
        public bool? Export { get; set; }
        public int? CreditLimit { get; set; }
        public int? CurrencyId { get; set; }
        public string? ExternalKey { get; set; }
        public short? VATRate { get; set; }
        public string? Address2 { get; set; }
        public bool? OneWayPallet { get; set; }
        public bool? EURPallet { get; set; }
        public int? OldDbId { get; set; }
        public bool? SpecialUnitHandling { get; set; }
        public bool? PrintForDocumentScanning { get; set; }
        public decimal? PricePerEurPallet { get; set; }
        public bool? LockOrder { get; set; }
        public DateTime? LastCustomerOrderDate { get; set; }
        public int? AccountNrAccountsReceivable { get; set; }
        public int? AccountNrEarnings { get; set; }
        public int? SupportEmployeeId { get; set; }
        public string? LastActivity { get; set; }
        public int? CountryId { get; set; }
        public string? Category { get; set; }
        public bool? IsProspect { get; set; }
        public bool? InvoicePalletsSeparately { get; set; }
        public bool? InvoiceCostsSeparately { get; set; }
        public bool? InvoiceCostsSeparatelyImmediately { get; set; }
        public DateOnly? BudgetCountAsNewUntilMonth { get; set; }
        public bool? InvoiceRowsInProductNameOrder { get; set; }
        public string? OrderNrPrefix { get; set; }
    }

    public class SearchCustomersRequest
    {
        public string? SearchTerm { get; set; }
        public bool? Active { get; set; }
        public bool? IsProspect { get; set; }
        public int? CountryId { get; set; }
        public string? Category { get; set; }
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
        public PaginationRequest? Pagination { get; set; }
    }
}
