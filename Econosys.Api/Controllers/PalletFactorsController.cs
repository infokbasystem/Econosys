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
    public class PalletFactorsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public PalletFactorsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpPost("search")]
        public async Task<ActionResult<PagedResultDto<PalletFactorDto>>> Search([FromBody] SearchPalletFactorsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var lengthFrom = request.LengthFrom!.Value;
            var lengthTo = request.LengthTo!.Value;
            var widthFrom = request.WidthFrom!.Value;
            var widthTo = request.WidthTo!.Value;

            if (lengthFrom > lengthTo || widthFrom > widthTo)
            {
                return BadRequest(new { message = "From-values must be less than or equal to to-values." });
            }

            var query = _dbContext.PalletFactors
                .AsNoTracking()
                .Where(x => x.LengthTo >= lengthFrom && x.LengthFrom <= lengthTo)
                .Where(x => x.WidthTo >= widthFrom && x.WidthFrom <= widthTo);

            var totalCount = await query.CountAsync();
            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)request.PageSize);

            var items = await query
                .OrderBy(x => x.LengthFrom)
                .ThenBy(x => x.WidthFrom)
                .ThenBy(x => x.Id)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(x => new PalletFactorDto
                {
                    Id = x.Id,
                    LengthFrom = x.LengthFrom,
                    LengthTo = x.LengthTo,
                    WidthFrom = x.WidthFrom,
                    WidthTo = x.WidthTo,
                    PalletFactors = x.PalletFactors
                })
                .ToListAsync();

            return Ok(new PagedResultDto<PalletFactorDto>
            {
                Items = items,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<PalletFactorDto>> Update(int id, [FromBody] UpdatePalletFactorRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = await _dbContext.PalletFactors.FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null)
            {
                return NotFound();
            }

            entity.PalletFactors = request.PalletFactors;
            await _dbContext.SaveChangesAsync();

            return Ok(new PalletFactorDto
            {
                Id = entity.Id,
                LengthFrom = entity.LengthFrom,
                LengthTo = entity.LengthTo,
                WidthFrom = entity.WidthFrom,
                WidthTo = entity.WidthTo,
                PalletFactors = entity.PalletFactors
            });
        }
    }
}