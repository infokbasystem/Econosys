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

            return Ok(MapToDto(entity));
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<VarnishDto>>> Search()
        {
            var items = await _dbContext.Varnishes
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .ToListAsync();

            return Ok(items.Select(MapToDto).ToList());
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
                Active = request.Active,
                OldDbId = request.OldDbId
            };

            _dbContext.Varnishes.Add(entity);
            await _dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(entity));
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

            if (request.Name is not null) entity.Name = request.Name;
            if (request.TranslationCode.HasValue) entity.TranslationCode = request.TranslationCode;
            if (request.Active.HasValue) entity.Active = request.Active.Value;
            if (request.OldDbId.HasValue) entity.OldDbId = request.OldDbId;

            await _dbContext.SaveChangesAsync();

            return Ok(MapToDto(entity));
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

        private static VarnishDto MapToDto(Varnish entity) => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            TranslationCode = entity.TranslationCode,
            Active = entity.Active,
            OldDbId = entity.OldDbId
        };
    }
}
