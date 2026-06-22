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
    public class VarnishesController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public VarnishesController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<VarnishDto>> GetById(int id)
        {
            var entity = await _dbContext.Varnishes
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
        public async Task<ActionResult<List<VarnishDto>>> Search()
        {
            var items = await _dbContext.Varnishes
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .ToListAsync();

            return Ok(items.Select(x => MapToDto(x)).ToList());
        }

        [HttpPost]
        public async Task<ActionResult<VarnishDto>> Create([FromBody] CreateVarnishRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = new Varnish
            {
                Name = request.Name,
                TranslationCode = request.TranslationCode,
                Active = request.Active
            };

            _dbContext.Varnishes.Add(entity);
            await _dbContext.SaveChangesAsync();

            entity.TranslationCode = await EntityTranslationService.UpsertTranslationsAsync(_dbContext, entity.TranslationCode, request.Translations);
            await _dbContext.SaveChangesAsync();

            var translations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, entity.TranslationCode);

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(entity, translations));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<VarnishDto>> Update(int id, [FromBody] UpdateVarnishRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = await _dbContext.Varnishes.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            entity.Name = request.Name;
            entity.TranslationCode = request.TranslationCode;
            entity.Active = request.Active ?? false;

            entity.TranslationCode = await EntityTranslationService.UpsertTranslationsAsync(_dbContext, entity.TranslationCode, request.Translations);

            await _dbContext.SaveChangesAsync();

            var translations = await EntityTranslationService.BuildCompletedTranslationsAsync(_dbContext, entity.TranslationCode);

            return Ok(MapToDto(entity, translations));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _dbContext.Varnishes.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            _dbContext.Varnishes.Remove(entity);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        private static VarnishDto MapToDto(Varnish entity, List<EntityTranslationDto>? translations = null) => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            TranslationCode = entity.TranslationCode,
            Translations = translations ?? new List<EntityTranslationDto>(),
            Active = entity.Active,
            OldDbId = entity.OldDbId
        };
    }
}
