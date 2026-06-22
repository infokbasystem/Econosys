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
    public class CostsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public CostsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<CostDto>> GetById(int id)
        {
            var cost = await _dbContext.Costs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (cost is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(cost));
        }

        [HttpPost]
        public async Task<ActionResult<CostDto>> Create([FromBody] CreateCostRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var nextTranslationCode = await EntityTranslationService.GetNextTranslationCodeAsync(_dbContext);

            var cost = new Cost
            {
                Name = (request.Name ?? string.Empty).Trim(),
                IsActive = request.IsActive,
                IsNrOf = request.IsNrOf,
                IsCalculation = request.IsCalculation,
                IsSupplier = request.IsSupplier,
                AccountDomestic = request.AccountDomestic,
                AccountEU = request.AccountEU,
                AccountExport = request.AccountExport,
                TranslationCodeSupplierOrderUnknown = nextTranslationCode++,
                TranslationCodeSupplierOrderKnown = nextTranslationCode++,
                TranslationCodeQuotationUnknown = nextTranslationCode++,
                TranslationCodeQuotationKnown = nextTranslationCode++,
                TranslationCodeCustomerOrderUnknown = nextTranslationCode++,
                TranslationCodeCustomerOrderKnown = nextTranslationCode++,
                TranslationCodeInvoiceRow = nextTranslationCode++,
                IsDebitDefault = request.IsDebitDefault,
                DoPrintSupplierOrderDefault = request.DoPrintSupplierOrderDefault,
                DoPrintQuotationDefault = request.DoPrintQuotationDefault,
                DoPrintCustomerOrderDefault = request.DoPrintCustomerOrderDefault,
                IsCustomerDefault = request.IsCustomerDefault,
                DoPrintScrapToolsTextOnCustomerOrder = request.DoPrintScrapToolsTextOnCustomerOrder,
                AddAutomaicIfEconopackIsTransportReponsible = request.AddAutomaicIfEconopackIsTransportReponsible,
                DmtPercent = request.DmtPercent,
                DmtFixed = request.DmtFixed,
                ProvisionPercent = request.ProvisionPercent,
                CostTypeText = (request.CostTypeText ?? string.Empty).Trim(),
            };

            _dbContext.Costs.Add(cost);
            await _dbContext.SaveChangesAsync();

            var response = MapToDto(cost);
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }

        [HttpPost("search")]
        public async Task<ActionResult<IReadOnlyList<CostDto>>> Search()
        {
            var items = await _dbContext.Costs
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

            return Ok(items);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<CostDto>> Update(int id, [FromBody] UpdateCostRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var cost = await _dbContext.Costs.FirstOrDefaultAsync(x => x.Id == id);

            if (cost is null)
            {
                return NotFound();
            }

            cost.Name = (request.Name ?? string.Empty).Trim();
            cost.IsActive = request.IsActive ?? false;
            cost.IsNrOf = request.IsNrOf ?? false;
            cost.IsCalculation = request.IsCalculation ?? false;
            cost.IsSupplier = request.IsSupplier ?? false;
            cost.AccountDomestic = request.AccountDomestic;
            cost.AccountEU = request.AccountEU;
            cost.AccountExport = request.AccountExport;
            cost.IsDebitDefault = request.IsDebitDefault ?? false;
            cost.DoPrintSupplierOrderDefault = request.DoPrintSupplierOrderDefault ?? false;
            cost.DoPrintQuotationDefault = request.DoPrintQuotationDefault ?? false;
            cost.DoPrintCustomerOrderDefault = request.DoPrintCustomerOrderDefault ?? false;
            cost.IsCustomerDefault = request.IsCustomerDefault ?? false;
            cost.DoPrintScrapToolsTextOnCustomerOrder = request.DoPrintScrapToolsTextOnCustomerOrder ?? false;
            cost.AddAutomaicIfEconopackIsTransportReponsible = request.AddAutomaicIfEconopackIsTransportReponsible ?? false;
            cost.DmtPercent = request.DmtPercent;
            cost.DmtFixed = request.DmtFixed;
            cost.ProvisionPercent = request.ProvisionPercent;
            cost.CostTypeText = (request.CostTypeText ?? string.Empty).Trim();

            await _dbContext.SaveChangesAsync();
            return Ok(MapToDto(cost));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var cost = await _dbContext.Costs.FirstOrDefaultAsync(x => x.Id == id);

            if (cost is null)
            {
                return NotFound();
            }

            _dbContext.Costs.Remove(cost);

            try
            {
                await _dbContext.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateException)
            {
                return Conflict(new
                {
                    message = "Kostnaden kan inte raderas eftersom den anvands i andra poster."
                });
            }
        }

        private static CostDto MapToDto(Models.Cost cost)
        {
            return new CostDto
            {
                Id = cost.Id,
                Name = cost.Name,
                IsActive = cost.IsActive,
                IsNrOf = cost.IsNrOf,
                IsCalculation = cost.IsCalculation,
                IsSupplier = cost.IsSupplier,
                AccountDomestic = cost.AccountDomestic,
                AccountEU = cost.AccountEU,
                AccountExport = cost.AccountExport,
                TranslationCodeSupplierOrderUnknown = cost.TranslationCodeSupplierOrderUnknown,
                TranslationCodeSupplierOrderKnown = cost.TranslationCodeSupplierOrderKnown,
                TranslationCodeQuotationUnknown = cost.TranslationCodeQuotationUnknown,
                TranslationCodeQuotationKnown = cost.TranslationCodeQuotationKnown,
                TranslationCodeCustomerOrderUnknown = cost.TranslationCodeCustomerOrderUnknown,
                TranslationCodeCustomerOrderKnown = cost.TranslationCodeCustomerOrderKnown,
                TranslationCodeInvoiceRow = cost.TranslationCodeInvoiceRow,
                IsDebitDefault = cost.IsDebitDefault,
                DoPrintSupplierOrderDefault = cost.DoPrintSupplierOrderDefault,
                DoPrintQuotationDefault = cost.DoPrintQuotationDefault,
                DoPrintCustomerOrderDefault = cost.DoPrintCustomerOrderDefault,
                IsCustomerDefault = cost.IsCustomerDefault,
                DoPrintScrapToolsTextOnCustomerOrder = cost.DoPrintScrapToolsTextOnCustomerOrder,
                AddAutomaicIfEconopackIsTransportReponsible = cost.AddAutomaicIfEconopackIsTransportReponsible,
                DmtPercent = cost.DmtPercent,
                DmtFixed = cost.DmtFixed,
                ProvisionPercent = cost.ProvisionPercent,
                CostTypeText = cost.CostTypeText,
            };
        }
    }
}