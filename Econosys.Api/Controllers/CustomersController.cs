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
        public async Task<ActionResult<CustomerDetailsDto>> GetById(int id)
        {
            var customer = await _dbContext.Customers
                .AsNoTracking()
                .Include(x => x.DeliveryAddresses)
                .Include(x => x.CustomerContactPersons)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (customer is null)
            {
                return NotFound();
            }

            return Ok(MapToDetailsDto(customer));
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

            customer.Name = request.Name;
            customer.SortName = request.SortName;
            customer.Address = request.Address;
            customer.PostalNr = request.PostalNr;
            customer.PostalAddress = request.PostalAddress;
            customer.VisitingAddress = request.VisitingAddress;
            customer.Reference = request.Reference;
            customer.Telephone1 = request.Telephone1;
            customer.Telephone2 = request.Telephone2;
            customer.Telephone3 = request.Telephone3;
            customer.Fax = request.Fax;
            customer.Note = request.Note;
            customer.LoadingInstruction = request.LoadingInstruction;
            customer.ResponsibleUserId = request.ResponsibleUserId;
            customer.VAT = request.VAT ?? false;
            customer.Active = request.Active ?? false;
            customer.Country = request.Country;
            customer.TermsOfDelivery = request.TermsOfDelivery;
            customer.TermsOfPayment = request.TermsOfPayment;
            customer.PaymentDays = request.PaymentDays;
            customer.LanguageId = request.LanguageId;
            customer.VATNr = request.VATNr;
            customer.Email = request.Email;
            customer.QuotationCommunicationTypeId = request.QuotationCommunicationTypeId;
            customer.CustomerOrderCommunicationTypeId = request.CustomerOrderCommunicationTypeId;
            customer.InvoiceCommunicationTypeId = request.InvoiceCommunicationTypeId;
            customer.OrgNr = request.OrgNr;
            customer.EU = request.EU ?? false;
            customer.Export = request.Export ?? false;
            customer.CreditLimit = request.CreditLimit;
            customer.CurrencyId = request.CurrencyId;
            customer.ExternalKey = request.ExternalKey;
            customer.VATRate = request.VATRate;
            customer.Address2 = request.Address2;
            customer.OneWayPallet = request.OneWayPallet ?? false;
            customer.EURPallet = request.EURPallet ?? false;
            customer.SpecialUnitHandling = request.SpecialUnitHandling ?? false;
            customer.PrintForDocumentScanning = request.PrintForDocumentScanning ?? false;
            customer.PricePerEurPallet = request.PricePerEurPallet;
            customer.LockOrder = request.LockOrder ?? false;
            customer.LastCustomerOrderDate = request.LastCustomerOrderDate;
            customer.AccountNrAccountsReceivable = request.AccountNrAccountsReceivable;
            customer.AccountNrEarnings = request.AccountNrEarnings;
            customer.SupportEmployeeId = request.SupportEmployeeId;
            customer.LastActivity = request.LastActivity;
            customer.CountryId = request.CountryId;
            customer.Category = request.Category;
            customer.IsProspect = request.IsProspect ?? false;
            customer.InvoicePalletsSeparately = request.InvoicePalletsSeparately ?? false;
            customer.InvoiceCostsSeparately = request.InvoiceCostsSeparately ?? false;
            customer.InvoiceCostsSeparatelyImmediately = request.InvoiceCostsSeparatelyImmediately ?? false;
            customer.BudgetCountAsNewUntilMonth = request.BudgetCountAsNewUntilMonth;
            customer.InvoiceRowsInProductNameOrder = request.InvoiceRowsInProductNameOrder ?? false;
            customer.OrderNrPrefix = request.OrderNrPrefix;

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

        private static CustomerDetailsDto MapToDetailsDto(Customer customer)
        {
            return new CustomerDetailsDto
            {
                Customer = MapToDto(customer),
                DeliveryAddresses = customer.DeliveryAddresses
                    .OrderBy(x => x.Name)
                    .ThenBy(x => x.Address)
                    .Select(x => new CustomerDeliveryAddressDto
                    {
                        Id = x.Id,
                        Name = x.Name,
                        Address = x.Address,
                        PostalNr = x.PostalNr,
                        PostalAddress = x.PostalAddress,
                        Country = x.Country,
                    })
                    .ToList(),
                ContactPersons = customer.CustomerContactPersons
                    .OrderBy(x => x.CustomerContactPersonName)
                    .ThenBy(x => x.ContactPerson)
                    .Select(x => new CustomerContactPersonDto
                    {
                        Id = x.Id,
                        CustomerContactPersonName = x.CustomerContactPersonName,
                        ContactPerson = x.ContactPerson,
                        Name = !string.IsNullOrWhiteSpace(x.CustomerContactPersonName)
                            ? x.CustomerContactPersonName
                            : x.ContactPerson,
                        Email = x.Email,
                        Telephone = x.Telephone,
                        Cellphone = x.Cellphone,
                        MailQuotation = x.MailQuotation,
                        MailCustomerOrder = x.MailCustomerOrder,
                        MailInvoice = x.MailInvoice,
                        MailTransportOrder = x.MailTransportOrder,
                        MailGeneralInfo = x.MailGeneralInfo,
                        MailCallOffConfirmation = x.MailCallOffConfirmation,
                        Title = x.Title,
                        OldDbId = x.OldDbId,
                    })
                    .ToList(),
            };
        }

    }
}
