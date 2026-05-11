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
    public class PalletFormatsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public PalletFormatsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
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

            return Ok(MapToDto(entity));
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<PalletFormatDto>>> Search()
        {
            var items = await _dbContext.PalletFormats
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .ToListAsync();

            return Ok(items.Select(MapToDto).ToList());
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
                OldDbId = request.OldDbId,
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

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(entity));
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

            if (request.Name is not null) entity.Name = request.Name;
            if (request.M2.HasValue) entity.M2 = request.M2;
            if (request.Active.HasValue) entity.Active = request.Active.Value;
            if (request.OldDbId.HasValue) entity.OldDbId = request.OldDbId;
            if (request.Width.HasValue) entity.Width = request.Width;
            if (request.Height.HasValue) entity.Height = request.Height;
            if (request.PalletTypeId.HasValue) entity.PalletTypeId = request.PalletTypeId;
            if (request.DebitFactor.HasValue) entity.DebitFactor = request.DebitFactor;
            if (request.TranslationCode.HasValue) entity.TranslationCode = request.TranslationCode;
            if (request.SortNr.HasValue) entity.SortNr = request.SortNr;
            if (request.CopyTo.HasValue) entity.CopyTo = request.CopyTo.Value;

            await _dbContext.SaveChangesAsync();

            return Ok(MapToDto(entity));
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

        private static PalletFormatDto MapToDto(PalletFormat entity) => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            M2 = entity.M2,
            Active = entity.Active,
            OldDbId = entity.OldDbId,
            Width = entity.Width,
            Height = entity.Height,
            PalletTypeId = entity.PalletTypeId,
            DebitFactor = entity.DebitFactor,
            TranslationCode = entity.TranslationCode,
            SortNr = entity.SortNr,
            CopyTo = entity.CopyTo
        };
    }
}
