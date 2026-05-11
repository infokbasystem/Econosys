using Econosys.Api.Data;
using Econosys.Api.DTOs;
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
                })
                .ToListAsync();

            return Ok(items);
        }

        private static CostDto MapToDto(Models.Cost cost)
        {
            return new CostDto
            {
                Id = cost.Id,
                Name = cost.Name,
                IsActive = cost.IsActive,
            };
        }
    }
}