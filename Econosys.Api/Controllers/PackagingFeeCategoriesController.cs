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
    public class PackagingFeeCategoriesController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public PackagingFeeCategoriesController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<PackagingFeeCategoryDto>> GetById(int id)
        {
            var entity = await _dbContext.PackagingFeeCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(entity));
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<PackagingFeeCategoryDto>>> Search()
        {
            var items = await _dbContext.PackagingFeeCategories
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .ToListAsync();

            return Ok(items.Select(MapToDto).ToList());
        }

        [HttpPost]
        public async Task<ActionResult<PackagingFeeCategoryDto>> Create([FromBody] CreatePackagingFeeCategoryRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = new PackagingFeeCategory
            {
                CategoryCode = request.CategoryCode,
                Description = request.Description
            };

            _dbContext.PackagingFeeCategories.Add(entity);
            await _dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(entity));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<PackagingFeeCategoryDto>> Update(int id, [FromBody] UpdatePackagingFeeCategoryRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = await _dbContext.PackagingFeeCategories.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            if (request.CategoryCode is not null) entity.CategoryCode = request.CategoryCode;
            if (request.Description is not null) entity.Description = request.Description;

            await _dbContext.SaveChangesAsync();

            return Ok(MapToDto(entity));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _dbContext.PackagingFeeCategories.FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                return NotFound();
            }

            _dbContext.PackagingFeeCategories.Remove(entity);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        private static PackagingFeeCategoryDto MapToDto(PackagingFeeCategory entity) => new()
        {
            Id = entity.Id,
            CategoryCode = entity.CategoryCode,
            Description = entity.Description
        };
    }
}
