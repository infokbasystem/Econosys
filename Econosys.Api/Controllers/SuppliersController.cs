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

        [HttpGet("{id:int}/details")]
        public async Task<ActionResult<SupplierDetailsDto>> GetDetailsById(int id)
        {
            var supplier = await _dbContext.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (supplier is null)
            {
                return NotFound();
            }

            var contactPersons = await BuildContactPersonsAsync(id);
            var factories = await BuildFactoriesAsync(id);
            var languages = await BuildLanguagesAsync();

            return Ok(new SupplierDetailsDto
            {
                Supplier = MapToDto(supplier),
                ContactPersons = contactPersons,
                Factories = factories,
                Languages = languages,
            });
        }

        [HttpGet("form-options")]
        public async Task<ActionResult<SupplierFormOptionsDto>> GetFormOptions()
        {
            var currencies = await _dbContext.Currencies
                .AsNoTracking()
                .Where(x => x.Active)
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

            var termsOfDelivery = await _dbContext.TermsOfDelivery
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

            var termsOfPayment = await _dbContext.TermsOfPayment
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

            var languages = await BuildLanguagesAsync();

            return Ok(new SupplierFormOptionsDto
            {
                Currencies = currencies,
                TermsOfDelivery = termsOfDelivery,
                TermsOfPayment = termsOfPayment,
                Languages = languages,
                InquiryCommunicationTypes = BuildCommunicationTypes(),
                SupplierOrderCommunicationTypes = BuildCommunicationTypes(),
            });
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
                EconopackTransportResponsible = request.EconopackTransportResponsible,
                PrintForDocumentScanning = request.PrintForDocumentScanning,
                PricePerEurPallet = request.PricePerEurPallet,
                FscDefault = request.FscDefault,
                SupplierOrderTemplateNr = request.SupplierOrderTemplateNr
            };

            _dbContext.Suppliers.Add(supplier);
            await _dbContext.SaveChangesAsync();

            if (request.ContactPersons is not null)
            {
                await SyncContactPersonsAsync(supplier.Id, request.ContactPersons);
            }

            if (request.Factories is not null)
            {
                await SyncFactoriesAsync(supplier.Id, request.Factories);
            }

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

            supplier.Name = request.Name;
            supplier.SortName = request.SortName;
            supplier.Reference = request.Reference;
            supplier.ContactPerson = request.ContactPerson;
            supplier.Address = request.Address;
            supplier.PostalNr = request.PostalNr;
            supplier.PostalAddress = request.PostalAddress;
            supplier.VisitingAddress = request.VisitingAddress;
            supplier.Country = request.Country;
            supplier.Telephone1 = request.Telephone1;
            supplier.Telephone2 = request.Telephone2;
            supplier.Telephone3 = request.Telephone3;
            supplier.Fax = request.Fax;
            supplier.Note = request.Note;
            supplier.Active = request.Active ?? false;
            supplier.LanguageId = request.LanguageId;
            supplier.TermsOfDelivery = request.TermsOfDelivery;
            supplier.TermsOfPayment = request.TermsOfPayment;
            supplier.Email = request.Email;
            supplier.InquiryCommunicationTypeId = request.InquiryCommunicationTypeId;
            supplier.SupplierOrderCommunicationTypeId = request.SupplierOrderCommunicationTypeId;
            supplier.CostCenter = request.CostCenter;
            supplier.CurrencyId = request.CurrencyId;
            supplier.Address2 = request.Address2;
            supplier.EconopackTransportResponsible = request.EconopackTransportResponsible ?? false;
            supplier.PrintForDocumentScanning = request.PrintForDocumentScanning ?? false;
            supplier.PricePerEurPallet = request.PricePerEurPallet;
            supplier.FscDefault = request.FscDefault ?? false;
            supplier.SupplierOrderTemplateNr = request.SupplierOrderTemplateNr;

            if (request.ContactPersons is not null)
            {
                await SyncContactPersonsAsync(supplier.Id, request.ContactPersons);
            }

            if (request.Factories is not null)
            {
                await SyncFactoriesAsync(supplier.Id, request.Factories);
            }

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

        private async Task<List<SupplierContactPersonDto>> BuildContactPersonsAsync(int supplierId)
        {
            return await _dbContext.SupplierContactPersons
                .AsNoTracking()
                .Where(x => x.SupplierId == supplierId)
                .OrderBy(x => x.SupplierContactPersonName)
                .ThenBy(x => x.ContactPerson)
                .Select(x => new SupplierContactPersonDto
                {
                    Id = x.Id,
                    SupplierContactPersonName = x.SupplierContactPersonName,
                    ContactPerson = x.ContactPerson,
                    Name = !string.IsNullOrWhiteSpace(x.SupplierContactPersonName)
                        ? x.SupplierContactPersonName
                        : x.ContactPerson,
                    Email = x.Email,
                    Telephone = x.Telephone,
                    Cellphone = x.Cellphone,
                    MailInquiry = x.MailInquiry,
                    MailSupplierOrder = x.MailSupplierOrder,
                    DoMailTransportOrder = x.DoMailTransportOrder,
                    Title = x.Title,
                    OldDbId = x.OldDbId,
                })
                .ToListAsync();
        }

        private async Task<List<SupplierFactoryDto>> BuildFactoriesAsync(int supplierId)
        {
            return await _dbContext.SupplierFactories
                .AsNoTracking()
                .Where(x => x.SupplierId == supplierId)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.Name)
                .Select(x => new SupplierFactoryDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Address = x.Address,
                    PostalNr = x.PostalNr,
                    City = x.City,
                    Country = x.Country,
                    CountryCode = x.CountryCode,
                    AddressExtra = x.AddressExtra,
                    IsDefault = x.IsDefault,
                    PositionId = x.PositionId,
                    ViaInventoryId = x.ViaInventoryId,
                })
                .ToListAsync();
        }

        private async Task<List<FilterOptionDto<int>>> BuildLanguagesAsync()
        {
            return await _dbContext.Languages
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new FilterOptionDto<int>
                {
                    Id = x.Id,
                    Name = x.Name ?? string.Empty,
                    IsActive = true,
                })
                .ToListAsync();
        }

        private static List<FilterOptionDto<int>> BuildCommunicationTypes()
        {
            return new List<FilterOptionDto<int>>
            {
                new()
                {
                    Id = 1,
                    Name = "Epost",
                    IsActive = true,
                },
            };
        }

        private async Task SyncContactPersonsAsync(int supplierId, IEnumerable<SupplierContactPersonRequest> requests)
        {
            var incoming = requests.ToList();
            var existing = await _dbContext.SupplierContactPersons
                .Where(x => x.SupplierId == supplierId)
                .ToListAsync();

            var incomingIds = incoming
                .Select(x => x.Id)
                .Where(x => x.HasValue && x.Value > 0)
                .Select(x => x!.Value)
                .ToHashSet();

            foreach (var entity in existing.Where(x => !incomingIds.Contains(x.Id)).ToList())
            {
                _dbContext.SupplierContactPersons.Remove(entity);
            }

            foreach (var item in incoming)
            {
                var entity = item.Id.HasValue && item.Id.Value > 0
                    ? existing.FirstOrDefault(x => x.Id == item.Id.Value)
                    : null;

                if (entity is null)
                {
                    entity = new SupplierContactPerson
                    {
                        SupplierId = supplierId,
                    };

                    _dbContext.SupplierContactPersons.Add(entity);
                }

                entity.SupplierId = supplierId;
                entity.SupplierContactPersonName = item.SupplierContactPersonName;
                entity.ContactPerson = item.ContactPerson;
                entity.Email = item.Email;
                entity.Telephone = item.Telephone;
                entity.Cellphone = item.Cellphone;
                entity.MailInquiry = item.MailInquiry;
                entity.MailSupplierOrder = item.MailSupplierOrder;
                entity.DoMailTransportOrder = item.DoMailTransportOrder;
                entity.Title = item.Title;
            }
        }

        private async Task SyncFactoriesAsync(int supplierId, IEnumerable<SupplierFactoryRequest> requests)
        {
            var incoming = requests.ToList();
            var existing = await _dbContext.SupplierFactories
                .Where(x => x.SupplierId == supplierId)
                .ToListAsync();

            var incomingIds = incoming
                .Select(x => x.Id)
                .Where(x => x.HasValue && x.Value > 0)
                .Select(x => x!.Value)
                .ToHashSet();

            foreach (var entity in existing.Where(x => !incomingIds.Contains(x.Id)).ToList())
            {
                _dbContext.SupplierFactories.Remove(entity);
            }

            foreach (var item in incoming)
            {
                var entity = item.Id.HasValue && item.Id.Value > 0
                    ? existing.FirstOrDefault(x => x.Id == item.Id.Value)
                    : null;

                if (entity is null)
                {
                    entity = new SupplierFactory
                    {
                        SupplierId = supplierId,
                    };

                    _dbContext.SupplierFactories.Add(entity);
                }

                entity.SupplierId = supplierId;
                entity.Name = item.Name;
                entity.Address = item.Address;
                entity.PostalNr = item.PostalNr;
                entity.City = item.City;
                entity.Country = item.Country;
                entity.CountryCode = item.CountryCode;
                entity.AddressExtra = item.AddressExtra;
                entity.IsDefault = item.IsDefault;
                entity.PositionId = item.PositionId;
                entity.ViaInventoryId = item.ViaInventoryId;
            }
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
        public List<SupplierContactPersonRequest>? ContactPersons { get; set; }
        public List<SupplierFactoryRequest>? Factories { get; set; }
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
        public List<SupplierContactPersonRequest>? ContactPersons { get; set; }
        public List<SupplierFactoryRequest>? Factories { get; set; }
    }

    public class SupplierContactPersonRequest
    {
        public int? Id { get; set; }
        public string? SupplierContactPersonName { get; set; }
        public string? ContactPerson { get; set; }
        public string? Email { get; set; }
        public string? Telephone { get; set; }
        public string? Cellphone { get; set; }
        public bool MailInquiry { get; set; }
        public bool MailSupplierOrder { get; set; }
        public bool DoMailTransportOrder { get; set; }
        public string? Title { get; set; }
    }

    public class SupplierFactoryRequest
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? PostalNr { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? CountryCode { get; set; }
        public string? AddressExtra { get; set; }
        public bool IsDefault { get; set; }
        public int? PositionId { get; set; }
        public int? ViaInventoryId { get; set; }
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
