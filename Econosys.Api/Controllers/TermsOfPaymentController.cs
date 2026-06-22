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
    public class TermsOfPaymentController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public TermsOfPaymentController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<TermOfPaymentDto>> GetById(int id)
        {
            var entity = await _dbContext.TermsOfPayment
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
        public async Task<ActionResult<List<TermOfPaymentDto>>> Search()
        {
            var items = await _dbContext.TermsOfPayment
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .ToListAsync();

            return Ok(items.Select(x => MapToDto(x)).ToList());
        }

        [HttpPost]
        public async Task<ActionResult<TermOfPaymentDto>> Create([FromBody] CreateTermOfPaymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = new TermOfPayment
            {
                Name = request.Name,
                TranslationCode = request.TranslationCode,
                Active = request.Active,
                IsDefault = request.IsDefault,
                TypeOf = request.TypeOf,
                PaymentDays = request.PaymentDays
            };

            _dbContext.TermsOfPayment.Add(entity);
            await _dbContext.SaveChangesAsync();

            entity.TranslationCode = await EntityTranslationService.UpsertTranslationsAsync(_dbContext, entity.TranslationCode, request.Translations);
            await _dbContext.SaveChangesAsync();

            var translations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, entity.TranslationCode);

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(entity, translations));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<TermOfPaymentDto>> Update(int id, [FromBody] UpdateTermOfPaymentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = await _dbContext.TermsOfPayment.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            entity.Name = request.Name;
            entity.TranslationCode = request.TranslationCode;
            entity.Active = request.Active ?? false;
            entity.IsDefault = request.IsDefault ?? false;
            entity.TypeOf = request.TypeOf;
            entity.PaymentDays = request.PaymentDays;

            entity.TranslationCode = await EntityTranslationService.UpsertTranslationsAsync(_dbContext, entity.TranslationCode, request.Translations);

            await _dbContext.SaveChangesAsync();

            var translations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, entity.TranslationCode);

            return Ok(MapToDto(entity, translations));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _dbContext.TermsOfPayment.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            _dbContext.TermsOfPayment.Remove(entity);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        private static TermOfPaymentDto MapToDto(TermOfPayment entity, List<EntityTranslationDto>? translations = null) => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            TranslationCode = entity.TranslationCode,
            Translations = translations ?? new List<EntityTranslationDto>(),
            Active = entity.Active,
            IsDefault = entity.IsDefault,
            TypeOf = entity.TypeOf,
            PaymentDays = entity.PaymentDays,
            OldDbId = entity.OldDbId
        };
    }
}