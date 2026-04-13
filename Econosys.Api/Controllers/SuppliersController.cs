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
    public class SuppliersController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public SuppliersController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<SupplierDto>> GetById(int id)
        {
            var supplier = await _dbContext.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (supplier is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(supplier));
        }

        [HttpPost]
        public async Task<ActionResult<SupplierDto>> Create([FromBody] CreateSupplierRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var supplier = new Supplier
            {
                Name = request.Name,
                SortName = request.SortName,
                Reference = request.Reference,
                ContactPerson = request.ContactPerson,
                Address = request.Address,
                PostalNr = request.PostalNr,
                PostalAddress = request.PostalAddress,
                VisitingAddress = request.VisitingAddress,
                Country = request.Country,
                Telephone1 = request.Telephone1,
                Telephone2 = request.Telephone2,
                Telephone3 = request.Telephone3,
                Fax = request.Fax,
                Note = request.Note,
                Active = request.Active,
                LanguageId = request.LanguageId,
                TermsOfDelivery = request.TermsOfDelivery,
                TermsOfPayment = request.TermsOfPayment,
                Email = request.Email,
                InquiryCommunicationTypeId = request.InquiryCommunicationTypeId,
                SupplierOrderCommunicationTypeId = request.SupplierOrderCommunicationTypeId,
                CostCenter = request.CostCenter,
                CurrencyId = request.CurrencyId,
                Address2 = request.Address2,
                OldDbId = request.OldDbId,
                EconopackTransportResponsible = request.EconopackTransportResponsible,
                PrintForDocumentScanning = request.PrintForDocumentScanning,
                PricePerEurPallet = request.PricePerEurPallet,
                FscDefault = request.FscDefault,
                SupplierOrderTemplateNr = request.SupplierOrderTemplateNr
            };

            _dbContext.Suppliers.Add(supplier);
            await _dbContext.SaveChangesAsync();

            var response = MapToDto(supplier);
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<SupplierDto>>> Search([FromBody] SearchSuppliersRequest? request)
        {
            IQueryable<Supplier> query = _dbContext.Suppliers.AsNoTracking();
            var pagination = request?.Pagination ?? new PaginationRequest();

            if (!string.IsNullOrWhiteSpace(request?.SearchTerm))
            {
                var searchTerm = request.SearchTerm.ToLower();
                query = query.Where(s =>
                    s.Name != null && s.Name.ToLower().Contains(searchTerm) ||
                    s.Email != null && s.Email.ToLower().Contains(searchTerm) ||
                    s.Reference != null && s.Reference.ToLower().Contains(searchTerm));
            }

            if (request?.Active.HasValue == true)
            {
                query = query.Where(s => s.Active == request.Active.Value);
            }

            if (request?.CurrencyId.HasValue == true)
            {
                query = query.Where(s => s.CurrencyId == request.CurrencyId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request?.Country))
            {
                query = query.Where(s => s.Country == request.Country);
            }

            var sortBy = request?.SortBy ?? "Name";
            var sortDescending = request?.SortDescending ?? false;

            query = sortBy.ToLower() switch
            {
                "name" => sortDescending ? query.OrderByDescending(s => s.Name) : query.OrderBy(s => s.Name),
                "email" => sortDescending ? query.OrderByDescending(s => s.Email) : query.OrderBy(s => s.Email),
                "country" => sortDescending ? query.OrderByDescending(s => s.Country) : query.OrderBy(s => s.Country),
                _ => query.OrderBy(s => s.Name)
            };

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            var dtos = items.Select(MapToDto).ToList();

            return Ok(new PagedResultDto<SupplierDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize
            });
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<SupplierDto>> Update(int id, [FromBody] UpdateSupplierRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(x => x.Id == id);

            if (supplier is null)
            {
                return NotFound();
            }

            if (request.Name != null) supplier.Name = request.Name;
            if (request.SortName != null) supplier.SortName = request.SortName;
            if (request.Reference != null) supplier.Reference = request.Reference;
            if (request.ContactPerson != null) supplier.ContactPerson = request.ContactPerson;
            if (request.Address != null) supplier.Address = request.Address;
            if (request.PostalNr != null) supplier.PostalNr = request.PostalNr;
            if (request.PostalAddress != null) supplier.PostalAddress = request.PostalAddress;
            if (request.VisitingAddress != null) supplier.VisitingAddress = request.VisitingAddress;
            if (request.Country != null) supplier.Country = request.Country;
            if (request.Telephone1 != null) supplier.Telephone1 = request.Telephone1;
            if (request.Telephone2 != null) supplier.Telephone2 = request.Telephone2;
            if (request.Telephone3 != null) supplier.Telephone3 = request.Telephone3;
            if (request.Fax != null) supplier.Fax = request.Fax;
            if (request.Note != null) supplier.Note = request.Note;
            if (request.Active.HasValue) supplier.Active = request.Active.Value;
            if (request.LanguageId.HasValue) supplier.LanguageId = request.LanguageId;
            if (request.TermsOfDelivery != null) supplier.TermsOfDelivery = request.TermsOfDelivery;
            if (request.TermsOfPayment != null) supplier.TermsOfPayment = request.TermsOfPayment;
            if (request.Email != null) supplier.Email = request.Email;
            if (request.InquiryCommunicationTypeId.HasValue) supplier.InquiryCommunicationTypeId = request.InquiryCommunicationTypeId;
            if (request.SupplierOrderCommunicationTypeId.HasValue) supplier.SupplierOrderCommunicationTypeId = request.SupplierOrderCommunicationTypeId;
            if (request.CostCenter != null) supplier.CostCenter = request.CostCenter;
            if (request.CurrencyId.HasValue) supplier.CurrencyId = request.CurrencyId;
            if (request.Address2 != null) supplier.Address2 = request.Address2;
            if (request.OldDbId.HasValue) supplier.OldDbId = request.OldDbId;
            if (request.EconopackTransportResponsible.HasValue) supplier.EconopackTransportResponsible = request.EconopackTransportResponsible.Value;
            if (request.PrintForDocumentScanning.HasValue) supplier.PrintForDocumentScanning = request.PrintForDocumentScanning.Value;
            if (request.PricePerEurPallet.HasValue) supplier.PricePerEurPallet = request.PricePerEurPallet;
            if (request.FscDefault.HasValue) supplier.FscDefault = request.FscDefault.Value;
            if (request.SupplierOrderTemplateNr.HasValue) supplier.SupplierOrderTemplateNr = request.SupplierOrderTemplateNr;

            await _dbContext.SaveChangesAsync();

            return Ok(MapToDto(supplier));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(x => x.Id == id);

            if (supplier is null)
            {
                return NotFound();
            }

            _dbContext.Suppliers.Remove(supplier);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        private static SupplierDto MapToDto(Supplier supplier)
        {
            return new SupplierDto
            {
                Id = supplier.Id,
                Name = supplier.Name,
                SortName = supplier.SortName,
                Reference = supplier.Reference,
                ContactPerson = supplier.ContactPerson,
                Address = supplier.Address,
                PostalNr = supplier.PostalNr,
                PostalAddress = supplier.PostalAddress,
                VisitingAddress = supplier.VisitingAddress,
                Country = supplier.Country,
                Telephone1 = supplier.Telephone1,
                Telephone2 = supplier.Telephone2,
                Telephone3 = supplier.Telephone3,
                Fax = supplier.Fax,
                Note = supplier.Note,
                Active = supplier.Active,
                LanguageId = supplier.LanguageId,
                TermsOfDelivery = supplier.TermsOfDelivery,
                TermsOfPayment = supplier.TermsOfPayment,
                Email = supplier.Email,
                InquiryCommunicationTypeId = supplier.InquiryCommunicationTypeId,
                SupplierOrderCommunicationTypeId = supplier.SupplierOrderCommunicationTypeId,
                CostCenter = supplier.CostCenter,
                CurrencyId = supplier.CurrencyId,
                Address2 = supplier.Address2,
                OldDbId = supplier.OldDbId,
                EconopackTransportResponsible = supplier.EconopackTransportResponsible,
                PrintForDocumentScanning = supplier.PrintForDocumentScanning,
                PricePerEurPallet = supplier.PricePerEurPallet,
                FscDefault = supplier.FscDefault,
                SupplierOrderTemplateNr = supplier.SupplierOrderTemplateNr
            };
        }
    }

    public class CreateSupplierRequest
    {
        public string? Name { get; set; }
        public string? SortName { get; set; }
        public string? Reference { get; set; }
        public string? ContactPerson { get; set; }
        public string? Address { get; set; }
        public string? PostalNr { get; set; }
        public string? PostalAddress { get; set; }
        public string? VisitingAddress { get; set; }
        public string? Country { get; set; }
        public string? Telephone1 { get; set; }
        public string? Telephone2 { get; set; }
        public string? Telephone3 { get; set; }
        public string? Fax { get; set; }
        public string? Note { get; set; }
        public bool Active { get; set; }
        public int? LanguageId { get; set; }
        public string? TermsOfDelivery { get; set; }
        public string? TermsOfPayment { get; set; }
        public string? Email { get; set; }
        public int? InquiryCommunicationTypeId { get; set; }
        public int? SupplierOrderCommunicationTypeId { get; set; }
        public string? CostCenter { get; set; }
        public int? CurrencyId { get; set; }
        public string? Address2 { get; set; }
        public int? OldDbId { get; set; }
        public bool EconopackTransportResponsible { get; set; }
        public bool PrintForDocumentScanning { get; set; }
        public decimal? PricePerEurPallet { get; set; }
        public bool FscDefault { get; set; }
        public int? SupplierOrderTemplateNr { get; set; }
    }

    public class UpdateSupplierRequest
    {
        public string? Name { get; set; }
        public string? SortName { get; set; }
        public string? Reference { get; set; }
        public string? ContactPerson { get; set; }
        public string? Address { get; set; }
        public string? PostalNr { get; set; }
        public string? PostalAddress { get; set; }
        public string? VisitingAddress { get; set; }
        public string? Country { get; set; }
        public string? Telephone1 { get; set; }
        public string? Telephone2 { get; set; }
        public string? Telephone3 { get; set; }
        public string? Fax { get; set; }
        public string? Note { get; set; }
        public bool? Active { get; set; }
        public int? LanguageId { get; set; }
        public string? TermsOfDelivery { get; set; }
        public string? TermsOfPayment { get; set; }
        public string? Email { get; set; }
        public int? InquiryCommunicationTypeId { get; set; }
        public int? SupplierOrderCommunicationTypeId { get; set; }
        public string? CostCenter { get; set; }
        public int? CurrencyId { get; set; }
        public string? Address2 { get; set; }
        public int? OldDbId { get; set; }
        public bool? EconopackTransportResponsible { get; set; }
        public bool? PrintForDocumentScanning { get; set; }
        public decimal? PricePerEurPallet { get; set; }
        public bool? FscDefault { get; set; }
        public int? SupplierOrderTemplateNr { get; set; }
    }

    public class SearchSuppliersRequest
    {
        public string? SearchTerm { get; set; }
        public bool? Active { get; set; }
        public int? CurrencyId { get; set; }
        public string? Country { get; set; }
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
        public PaginationRequest? Pagination { get; set; }
    }
}
