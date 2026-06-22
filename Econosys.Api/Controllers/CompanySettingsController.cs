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
    public class CompanySettingsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public CompanySettingsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("{companyId:int}")]
        public async Task<ActionResult<CompanySettingsDto>> GetByCompanyId(int companyId)
        {
            var company = await _dbContext.CompanyInfos
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CompanyId == companyId);

            if (company is null)
            {
                return NotFound();
            }

            var settings = await _dbContext.Settings
                .AsNoTracking()
                .Where(x => x.CompanyId == companyId)
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync();

            return Ok(MapToDto(company, settings));
        }

        [HttpPost]
        public async Task<ActionResult<CompanySettingsDto>> Create([FromBody] CreateCompanySettingsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var company = new CompanyInfo();
            MapCompanyValues(request, company);

            _dbContext.CompanyInfos.Add(company);
            await _dbContext.SaveChangesAsync();

            Setting? settings = null;
            if (HasAnySettingsValue(request))
            {
                settings = new Setting
                {
                    CompanyId = company.CompanyId
                };

                MapSettingValues(request, settings);

                _dbContext.Settings.Add(settings);
                await _dbContext.SaveChangesAsync();
            }

            var response = MapToDto(company, settings);
            return CreatedAtAction(nameof(GetByCompanyId), new { companyId = response.CompanyId }, response);
        }

        [HttpPut("{companyId:int}")]
        public async Task<ActionResult<CompanySettingsDto>> Update(int companyId, [FromBody] UpdateCompanySettingsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var company = await _dbContext.CompanyInfos
                .FirstOrDefaultAsync(x => x.CompanyId == companyId);

            if (company is null)
            {
                return NotFound();
            }

            var settings = await _dbContext.Settings
                .Where(x => x.CompanyId == companyId)
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync();

            if (settings is null)
            {
                settings = new Setting
                {
                    CompanyId = companyId
                };

                _dbContext.Settings.Add(settings);
            }

            MapCompanyValues(request, company);
            MapSettingValues(request, settings);

            await _dbContext.SaveChangesAsync();

            return Ok(MapToDto(company, settings));
        }

        private static void MapCompanyValues(CreateCompanySettingsRequest source, CompanyInfo target)
        {
            target.CompanyName = source.CompanyName;
            target.Address = source.Address;
            target.PostalAddress = source.PostalAddress;
            target.ZipCode = source.ZipCode;
            target.Telephone1 = source.Telephone1;
            target.Telephone2 = source.Telephone2;
            target.Fax1 = source.Fax1;
            target.Fax2 = source.Fax2;
            target.Bank = source.Bank;
            target.BIC = source.BIC;
            target.IBAN = source.IBAN;
            target.BG = source.BG;
            target.PG = source.PG;
            target.VATNr = source.VATNr;
            target.InvoiceMailSWE = source.InvoiceMailSWE;
            target.InvoiceMailENG = source.InvoiceMailENG;
            target.DocumentFileBasePath = source.DocumentFileBasePath;
            target.Email = source.Email;
            target.Web = source.Web;
            target.MailWrapper = source.MailWrapper;
            target.GoogleApiKey = source.GoogleApiKey;
        }

        private static void MapCompanyValues(UpdateCompanySettingsRequest source, CompanyInfo target)
        {
            target.CompanyName = source.CompanyName;
            target.Address = source.Address;
            target.PostalAddress = source.PostalAddress;
            target.ZipCode = source.ZipCode;
            target.Telephone1 = source.Telephone1;
            target.Telephone2 = source.Telephone2;
            target.Fax1 = source.Fax1;
            target.Fax2 = source.Fax2;
            target.Bank = source.Bank;
            target.BIC = source.BIC;
            target.IBAN = source.IBAN;
            target.BG = source.BG;
            target.PG = source.PG;
            target.VATNr = source.VATNr;
            target.InvoiceMailSWE = source.InvoiceMailSWE;
            target.InvoiceMailENG = source.InvoiceMailENG;
            target.DocumentFileBasePath = source.DocumentFileBasePath;
            target.Email = source.Email;
            target.Web = source.Web;
            target.MailWrapper = source.MailWrapper;
            target.GoogleApiKey = source.GoogleApiKey;
        }

        private static void MapSettingValues(CreateCompanySettingsRequest source, Setting target)
        {
            target.NrOfInquiryAnswerDays = source.NrOfInquiryAnswerDays;
            target.VATInfo = source.VATInfo;
            target.Company = source.SettingsCompany;
            target.InvoiceLastNr = source.InvoiceLastNr;
            target.DefaultCustomerMessage = source.DefaultCustomerMessage;
            target.EUText = source.EUText;
            target.ExportText = source.ExportText;
        }

        private static void MapSettingValues(UpdateCompanySettingsRequest source, Setting target)
        {
            target.NrOfInquiryAnswerDays = source.NrOfInquiryAnswerDays;
            target.VATInfo = source.VATInfo;
            target.Company = source.SettingsCompany;
            target.InvoiceLastNr = source.InvoiceLastNr;
            target.DefaultCustomerMessage = source.DefaultCustomerMessage;
            target.EUText = source.EUText;
            target.ExportText = source.ExportText;
        }

        private static CompanySettingsDto MapToDto(CompanyInfo company, Setting? settings)
        {
            return new CompanySettingsDto
            {
                CompanyId = company.CompanyId,
                SettingsId = settings?.Id,
                CompanyName = company.CompanyName,
                Address = company.Address,
                PostalAddress = company.PostalAddress,
                ZipCode = company.ZipCode,
                Telephone1 = company.Telephone1,
                Telephone2 = company.Telephone2,
                Fax1 = company.Fax1,
                Fax2 = company.Fax2,
                Bank = company.Bank,
                BIC = company.BIC,
                IBAN = company.IBAN,
                BG = company.BG,
                PG = company.PG,
                VATNr = company.VATNr,
                CompanyOldDbId = company.OldDbId,
                InvoiceMailSWE = company.InvoiceMailSWE,
                InvoiceMailENG = company.InvoiceMailENG,
                DocumentFileBasePath = company.DocumentFileBasePath,
                Email = company.Email,
                Web = company.Web,
                MailWrapper = company.MailWrapper,
                GoogleApiKey = company.GoogleApiKey,
                NrOfInquiryAnswerDays = settings?.NrOfInquiryAnswerDays,
                VATInfo = settings?.VATInfo,
                SettingsCompany = settings?.Company,
                InvoiceLastNr = settings?.InvoiceLastNr,
                DefaultCustomerMessage = settings?.DefaultCustomerMessage,
                EUText = settings?.EUText,
                ExportText = settings?.ExportText,
                SettingsOldDbId = settings?.OldDbId
            };
        }

        private static bool HasAnySettingsValue(CreateCompanySettingsRequest request)
        {
            return request.NrOfInquiryAnswerDays.HasValue
                || !string.IsNullOrWhiteSpace(request.VATInfo)
                || !string.IsNullOrWhiteSpace(request.SettingsCompany)
                || request.InvoiceLastNr.HasValue
                || !string.IsNullOrWhiteSpace(request.DefaultCustomerMessage)
                || !string.IsNullOrWhiteSpace(request.EUText)
                || !string.IsNullOrWhiteSpace(request.ExportText);
        }
    }
}