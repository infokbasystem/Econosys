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
    public class PalletFormatsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IPalletFormatOptionsService _palletFormatOptionsService;

        public PalletFormatsController(ApplicationDbContext dbContext, IPalletFormatOptionsService palletFormatOptionsService)
        {
            _dbContext = dbContext;
            _palletFormatOptionsService = palletFormatOptionsService;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<PalletFormatDto>> GetById(int id)
        {
            var entity = await _dbContext.PalletFormats
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            var translations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, entity.TranslationCode);
            return Ok(MapToDto(entity, translations));
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<PalletFormatDto>>> Search()
        {

            var result = await _dbContext.PalletFormats
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .ToListAsync();

            return Ok(result.Select(x => MapToDto(x)).ToList());
        }

        [HttpGet("searchwithprices")]
        public async Task<ActionResult<List<PalletFormatDto>>> SearchWithPrices([FromQuery] int? customerId, [FromQuery] int? supplierId)
        {
            if (!customerId.HasValue && !supplierId.HasValue)
            {
                return BadRequest("At least one of customerId or supplierId must be provided.");
            }

            var items = await _palletFormatOptionsService.GetOptionsAsync(customerId, supplierId);
            return Ok(items);
        }

        [HttpPost]
        public async Task<ActionResult<PalletFormatDto>> Create([FromBody] CreatePalletFormatRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = new PalletFormat
            {
                Name = request.Name,
                M2 = request.M2,
                Active = request.Active,
                Width = request.Width,
                Height = request.Height,
                PalletTypeId = request.PalletTypeId,
                DebitFactor = request.DebitFactor,
                TranslationCode = request.TranslationCode,
                SortNr = request.SortNr,
                CopyTo = request.CopyTo
            };

            _dbContext.PalletFormats.Add(entity);
            await _dbContext.SaveChangesAsync();

            entity.TranslationCode = await EntityTranslationService.UpsertTranslationsAsync(_dbContext, entity.TranslationCode, request.Translations);
            await _dbContext.SaveChangesAsync();

            var translations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, entity.TranslationCode);

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(entity, translations));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<PalletFormatDto>> Update(int id, [FromBody] UpdatePalletFormatRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = await _dbContext.PalletFormats.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            entity.Name = request.Name;
            entity.M2 = request.M2;
            entity.Active = request.Active ?? false;
            entity.Width = request.Width;
            entity.Height = request.Height;
            entity.PalletTypeId = request.PalletTypeId;
            entity.DebitFactor = request.DebitFactor;
            entity.TranslationCode = request.TranslationCode;
            entity.SortNr = request.SortNr;
            entity.CopyTo = request.CopyTo ?? false;

            entity.TranslationCode = await EntityTranslationService.UpsertTranslationsAsync(_dbContext, entity.TranslationCode, request.Translations);

            await _dbContext.SaveChangesAsync();

            var translations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, entity.TranslationCode);

            return Ok(MapToDto(entity, translations));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _dbContext.PalletFormats.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            _dbContext.PalletFormats.Remove(entity);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        private static PalletFormatDto MapToDto(PalletFormat entity, List<EntityTranslationDto>? translations = null, decimal? supplierPrice = null, decimal? customerPrice = null) => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            M2 = entity.M2,
            SupplierPrice = supplierPrice,
            CustomerPrice = customerPrice,
            Active = entity.Active,
            OldDbId = entity.OldDbId,
            Width = entity.Width,
            Height = entity.Height,
            PalletTypeId = entity.PalletTypeId,
            DebitFactor = entity.DebitFactor,
            TranslationCode = entity.TranslationCode,
            Translations = translations ?? new List<EntityTranslationDto>(),
            SortNr = entity.SortNr,
            CopyTo = entity.CopyTo
        };
    }
}
