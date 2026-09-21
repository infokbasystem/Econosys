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
    public class CompanySettingsController : ControllerBase
    {
        private const string JeevesApiKeyPurpose = "Econosys.Jeeves.ApiKey.v1";
        private const string JeevesTestApiKeyPurpose = "Econosys.Jeeves.TestApiKey.v1";
        private readonly ApplicationDbContext _dbContext;
        private readonly IProtectedSecretService _protectedSecretService;

        public CompanySettingsController(ApplicationDbContext dbContext, IProtectedSecretService protectedSecretService)
        {
            _dbContext = dbContext;
            _protectedSecretService = protectedSecretService;
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
            MapCompanyValues(request, company, request.PropertiesToUpdate);

            _dbContext.CompanyInfos.Add(company);
            await _dbContext.SaveChangesAsync();

            Setting? settings = null;
            if (HasAnySettingsValue(request))
            {
                settings = new Setting
                {
                    CompanyId = company.CompanyId
                };

                MapSettingValues(request, settings, request.PropertiesToUpdate, _protectedSecretService);

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

            MapCompanyValues(request, company, request.PropertiesToUpdate);
            MapSettingValues(request, settings, request.PropertiesToUpdate, _protectedSecretService);

            await _dbContext.SaveChangesAsync();

            return Ok(MapToDto(company, settings));
        }

        private static bool ShouldUpdateProperty(IEnumerable<string>? propertiesToUpdate, string propertyName)
        {
            if (propertiesToUpdate is null || !propertiesToUpdate.Any())
            {
                return true;
            }

            return propertiesToUpdate.Any(x => string.Equals(x, propertyName, StringComparison.OrdinalIgnoreCase));
        }

        private static void MapCompanyValues(CreateCompanySettingsRequest source, CompanyInfo target, IEnumerable<string>? propertiesToUpdate)
        {
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.CompanyName))) target.CompanyName = source.CompanyName;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.Address))) target.Address = source.Address;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.PostalAddress))) target.PostalAddress = source.PostalAddress;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.ZipCode))) target.ZipCode = source.ZipCode;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.Telephone1))) target.Telephone1 = source.Telephone1;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.Telephone2))) target.Telephone2 = source.Telephone2;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.Fax1))) target.Fax1 = source.Fax1;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.Fax2))) target.Fax2 = source.Fax2;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.Bank))) target.Bank = source.Bank;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.BIC))) target.BIC = source.BIC;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.IBAN))) target.IBAN = source.IBAN;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.BG))) target.BG = source.BG;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.PG))) target.PG = source.PG;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.VATNr))) target.VATNr = source.VATNr;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.InvoiceMailSWE))) target.InvoiceMailSWE = source.InvoiceMailSWE;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.InvoiceMailENG))) target.InvoiceMailENG = source.InvoiceMailENG;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.DocumentFileBasePath))) target.DocumentFileBasePath = source.DocumentFileBasePath;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.Email))) target.Email = source.Email;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.Web))) target.Web = source.Web;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.MailWrapper))) target.MailWrapper = source.MailWrapper;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.GoogleApiKey))) target.GoogleApiKey = source.GoogleApiKey;
        }

        private static void MapCompanyValues(UpdateCompanySettingsRequest source, CompanyInfo target, IEnumerable<string>? propertiesToUpdate)
        {
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.CompanyName))) target.CompanyName = source.CompanyName;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.Address))) target.Address = source.Address;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.PostalAddress))) target.PostalAddress = source.PostalAddress;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.ZipCode))) target.ZipCode = source.ZipCode;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.Telephone1))) target.Telephone1 = source.Telephone1;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.Telephone2))) target.Telephone2 = source.Telephone2;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.Fax1))) target.Fax1 = source.Fax1;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.Fax2))) target.Fax2 = source.Fax2;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.Bank))) target.Bank = source.Bank;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.BIC))) target.BIC = source.BIC;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.IBAN))) target.IBAN = source.IBAN;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.BG))) target.BG = source.BG;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.PG))) target.PG = source.PG;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.VATNr))) target.VATNr = source.VATNr;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.InvoiceMailSWE))) target.InvoiceMailSWE = source.InvoiceMailSWE;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.InvoiceMailENG))) target.InvoiceMailENG = source.InvoiceMailENG;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.DocumentFileBasePath))) target.DocumentFileBasePath = source.DocumentFileBasePath;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.Email))) target.Email = source.Email;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.Web))) target.Web = source.Web;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.MailWrapper))) target.MailWrapper = source.MailWrapper;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.GoogleApiKey))) target.GoogleApiKey = source.GoogleApiKey;
        }

        private static void MapSettingValues(CreateCompanySettingsRequest source, Setting target, IEnumerable<string>? propertiesToUpdate, IProtectedSecretService protectedSecretService)
        {
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.NrOfInquiryAnswerDays))) target.NrOfInquiryAnswerDays = source.NrOfInquiryAnswerDays;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.VATInfo))) target.VATInfo = source.VATInfo;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.SettingsCompany))) target.Company = source.SettingsCompany;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.InvoiceLastNr))) target.InvoiceLastNr = source.InvoiceLastNr;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.HandlingTimesGoalNrOfDays))) target.HandlingTimesGoalNrOfDays = source.HandlingTimesGoalNrOfDays;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.HandlingTimesGoalMaxNrOfDays))) target.HandlingTimesGoalMaxNrOfDays = source.HandlingTimesGoalMaxNrOfDays;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.HandlingTimesPercentHandledUnderGoalNrOfDays))) target.HandlingTimesPercentHandledUnderGoalNrOfDays = source.HandlingTimesPercentHandledUnderGoalNrOfDays;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.HandlingTimesThresholdNrOfDays))) target.HandlingTimesThresholdNrOfDays = source.HandlingTimesThresholdNrOfDays;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.DefaultCustomerMessage))) target.DefaultCustomerMessage = source.DefaultCustomerMessage;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.EUText))) target.EUText = source.EUText;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.ExportText))) target.ExportText = source.ExportText;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.JeevesApiEndpoint))) target.JeevesApiEndpoint = source.JeevesApiEndpoint;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.JeevesApiKey)) && !string.IsNullOrWhiteSpace(source.JeevesApiKey)) target.JeevesApiKeyProtected = protectedSecretService.Protect(JeevesApiKeyPurpose, source.JeevesApiKey);
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.JeevesTestApiEndpoint))) target.JeevesTestApiEndpoint = source.JeevesTestApiEndpoint;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(CreateCompanySettingsRequest.JeevesTestApiKey)) && !string.IsNullOrWhiteSpace(source.JeevesTestApiKey)) target.JeevesTestApiKeyProtected = protectedSecretService.Protect(JeevesTestApiKeyPurpose, source.JeevesTestApiKey);
        }

        private static void MapSettingValues(UpdateCompanySettingsRequest source, Setting target, IEnumerable<string>? propertiesToUpdate, IProtectedSecretService protectedSecretService)
        {
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.NrOfInquiryAnswerDays))) target.NrOfInquiryAnswerDays = source.NrOfInquiryAnswerDays;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.VATInfo))) target.VATInfo = source.VATInfo;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.SettingsCompany))) target.Company = source.SettingsCompany;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.InvoiceLastNr))) target.InvoiceLastNr = source.InvoiceLastNr;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.HandlingTimesGoalNrOfDays))) target.HandlingTimesGoalNrOfDays = source.HandlingTimesGoalNrOfDays;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.HandlingTimesGoalMaxNrOfDays))) target.HandlingTimesGoalMaxNrOfDays = source.HandlingTimesGoalMaxNrOfDays;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.HandlingTimesPercentHandledUnderGoalNrOfDays))) target.HandlingTimesPercentHandledUnderGoalNrOfDays = source.HandlingTimesPercentHandledUnderGoalNrOfDays;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.HandlingTimesThresholdNrOfDays))) target.HandlingTimesThresholdNrOfDays = source.HandlingTimesThresholdNrOfDays;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.DefaultCustomerMessage))) target.DefaultCustomerMessage = source.DefaultCustomerMessage;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.EUText))) target.EUText = source.EUText;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.ExportText))) target.ExportText = source.ExportText;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.JeevesApiEndpoint))) target.JeevesApiEndpoint = source.JeevesApiEndpoint;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.JeevesApiKey)) && !string.IsNullOrWhiteSpace(source.JeevesApiKey)) target.JeevesApiKeyProtected = protectedSecretService.Protect(JeevesApiKeyPurpose, source.JeevesApiKey);
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.JeevesTestApiEndpoint))) target.JeevesTestApiEndpoint = source.JeevesTestApiEndpoint;
            if (ShouldUpdateProperty(propertiesToUpdate, nameof(UpdateCompanySettingsRequest.JeevesTestApiKey)) && !string.IsNullOrWhiteSpace(source.JeevesTestApiKey)) target.JeevesTestApiKeyProtected = protectedSecretService.Protect(JeevesTestApiKeyPurpose, source.JeevesTestApiKey);
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
                HandlingTimesGoalNrOfDays = settings?.HandlingTimesGoalNrOfDays,
                HandlingTimesGoalMaxNrOfDays = settings?.HandlingTimesGoalMaxNrOfDays,
                HandlingTimesPercentHandledUnderGoalNrOfDays = settings?.HandlingTimesPercentHandledUnderGoalNrOfDays,
                HandlingTimesThresholdNrOfDays = settings?.HandlingTimesThresholdNrOfDays,
                DefaultCustomerMessage = settings?.DefaultCustomerMessage,
                EUText = settings?.EUText,
                ExportText = settings?.ExportText,
                JeevesApiEndpoint = settings?.JeevesApiEndpoint,
                HasJeevesApiKey = !string.IsNullOrWhiteSpace(settings?.JeevesApiKeyProtected),
                JeevesTestApiEndpoint = settings?.JeevesTestApiEndpoint,
                HasJeevesTestApiKey = !string.IsNullOrWhiteSpace(settings?.JeevesTestApiKeyProtected),
                SettingsOldDbId = settings?.OldDbId
            };
        }

        private static bool HasAnySettingsValue(CreateCompanySettingsRequest request)
        {
            return request.NrOfInquiryAnswerDays.HasValue
                || !string.IsNullOrWhiteSpace(request.VATInfo)
                || !string.IsNullOrWhiteSpace(request.SettingsCompany)
                || request.InvoiceLastNr.HasValue
                || request.HandlingTimesGoalNrOfDays.HasValue
                || request.HandlingTimesGoalMaxNrOfDays.HasValue
                || request.HandlingTimesPercentHandledUnderGoalNrOfDays.HasValue
                || request.HandlingTimesThresholdNrOfDays.HasValue
                || !string.IsNullOrWhiteSpace(request.DefaultCustomerMessage)
                || !string.IsNullOrWhiteSpace(request.EUText)
                || !string.IsNullOrWhiteSpace(request.ExportText)
                || !string.IsNullOrWhiteSpace(request.JeevesApiEndpoint)
                || !string.IsNullOrWhiteSpace(request.JeevesApiKey)
                || !string.IsNullOrWhiteSpace(request.JeevesTestApiEndpoint)
                || !string.IsNullOrWhiteSpace(request.JeevesTestApiKey);
        }
    }
}