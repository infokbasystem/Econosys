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
    public class PalletTypesController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public PalletTypesController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<PalletTypeDto>> GetById(int id)
        {
            var entity = await _dbContext.PalletTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(entity));
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<PalletTypeDto>>> Search()
        {
            var items = await _dbContext.PalletTypes
                .AsNoTracking()
                .OrderBy(x => x.SortNr)
                .ThenBy(x => x.Id)
                .ToListAsync();

            return Ok(items.Select(MapToDto).ToList());
        }

        [HttpPost]
        public async Task<ActionResult<PalletTypeDto>> Create([FromBody] CreatePalletTypeRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = new PalletType
            {
                Name = request.Name,
                IsEur = request.IsEur,
                IsPallet = request.IsPallet,
                IsActive = request.IsActive,
                SortNr = request.SortNr
            };

            _dbContext.PalletTypes.Add(entity);
            await _dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(entity));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<PalletTypeDto>> Update(int id, [FromBody] UpdatePalletTypeRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = await _dbContext.PalletTypes.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            entity.Name = request.Name;
            entity.IsEur = request.IsEur ?? false;
            entity.IsPallet = request.IsPallet ?? false;
            entity.IsActive = request.IsActive ?? false;
            entity.SortNr = request.SortNr;

            await _dbContext.SaveChangesAsync();

            return Ok(MapToDto(entity));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _dbContext.PalletTypes.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            _dbContext.PalletTypes.Remove(entity);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        private static PalletTypeDto MapToDto(PalletType entity) => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            IsEur = entity.IsEur,
            IsPallet = entity.IsPallet,
            IsActive = entity.IsActive,
            SortNr = entity.SortNr
        };
    }
}
