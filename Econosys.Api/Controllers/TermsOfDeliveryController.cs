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
    public class TermsOfDeliveryController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public TermsOfDeliveryController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<TermOfDeliveryDto>> GetById(int id)
        {
            var entity = await _dbContext.TermsOfDelivery
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
        public async Task<ActionResult<List<TermOfDeliveryDto>>> Search()
        {
            var items = await _dbContext.TermsOfDelivery
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .ToListAsync();

            return Ok(items.Select(x => MapToDto(x)).ToList());
        }

        [HttpPost]
        public async Task<ActionResult<TermOfDeliveryDto>> Create([FromBody] CreateTermOfDeliveryRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = new TermOfDelivery
            {
                Name = request.Name,
                TranslationCode = request.TranslationCode,
                Active = request.Active,
                IsDefault = request.IsDefault
            };

            _dbContext.TermsOfDelivery.Add(entity);
            await _dbContext.SaveChangesAsync();

            entity.TranslationCode = await EntityTranslationService.UpsertTranslationsAsync(_dbContext, entity.TranslationCode, request.Translations);
            await _dbContext.SaveChangesAsync();

            var translations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, entity.TranslationCode);

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(entity, translations));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<TermOfDeliveryDto>> Update(int id, [FromBody] UpdateTermOfDeliveryRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = await _dbContext.TermsOfDelivery.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            entity.Name = request.Name;
            entity.TranslationCode = request.TranslationCode;
            entity.Active = request.Active ?? false;
            entity.IsDefault = request.IsDefault ?? false;

            entity.TranslationCode = await EntityTranslationService.UpsertTranslationsAsync(_dbContext, entity.TranslationCode, request.Translations);

            await _dbContext.SaveChangesAsync();

            var translations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, entity.TranslationCode);

            return Ok(MapToDto(entity, translations));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _dbContext.TermsOfDelivery.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            _dbContext.TermsOfDelivery.Remove(entity);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        private static TermOfDeliveryDto MapToDto(TermOfDelivery entity, List<EntityTranslationDto>? translations = null) => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            TranslationCode = entity.TranslationCode,
            Translations = translations ?? new List<EntityTranslationDto>(),
            Active = entity.Active,
            IsDefault = entity.IsDefault,
            OldDbId = entity.OldDbId
        };
    }
}
