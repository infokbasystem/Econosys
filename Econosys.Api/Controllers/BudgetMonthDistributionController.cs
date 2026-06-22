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
    public class BudgetMonthDistributionController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public BudgetMonthDistributionController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<ActionResult<BudgetMonthDistributionDto>> Get()
        {
            var entity = await _dbContext.BudgetMonthDistributions
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync();

            if (entity is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(entity));
        }

        [HttpPut]
        public async Task<ActionResult<BudgetMonthDistributionDto>> Upsert([FromBody] UpdateBudgetMonthDistributionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var total =
                (request.Month1 ?? 0m) +
                (request.Month2 ?? 0m) +
                (request.Month3 ?? 0m) +
                (request.Month4 ?? 0m) +
                (request.Month5 ?? 0m) +
                (request.Month6 ?? 0m) +
                (request.Month7 ?? 0m) +
                (request.Month8 ?? 0m) +
                (request.Month9 ?? 0m) +
                (request.Month10 ?? 0m) +
                (request.Month11 ?? 0m) +
                (request.Month12 ?? 0m);

            const decimal allowedDelta = 0.000001m;
            if (Math.Abs(total - 100m) > allowedDelta)
            {
                return BadRequest(new
                {
                    message = "Summan av manaderna maste vara 100."
                });
            }

            var entity = await _dbContext.BudgetMonthDistributions
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync();

            if (entity is null)
            {
                entity = new BudgetMonthDistribution();
                MapValues(request, entity);
                _dbContext.BudgetMonthDistributions.Add(entity);
            }
            else
            {
                MapValues(request, entity);
            }

            await _dbContext.SaveChangesAsync();

            return Ok(MapToDto(entity));
        }

        private static void MapValues(UpdateBudgetMonthDistributionRequest request, BudgetMonthDistribution entity)
        {
            entity.Month1 = request.Month1;
            entity.Month2 = request.Month2;
            entity.Month3 = request.Month3;
            entity.Month4 = request.Month4;
            entity.Month5 = request.Month5;
            entity.Month6 = request.Month6;
            entity.Month7 = request.Month7;
            entity.Month8 = request.Month8;
            entity.Month9 = request.Month9;
            entity.Month10 = request.Month10;
            entity.Month11 = request.Month11;
            entity.Month12 = request.Month12;
        }

        private static BudgetMonthDistributionDto MapToDto(BudgetMonthDistribution entity) => new()
        {
            Id = entity.Id,
            Month1 = entity.Month1,
            Month2 = entity.Month2,
            Month3 = entity.Month3,
            Month4 = entity.Month4,
            Month5 = entity.Month5,
            Month6 = entity.Month6,
            Month7 = entity.Month7,
            Month8 = entity.Month8,
            Month9 = entity.Month9,
            Month10 = entity.Month10,
            Month11 = entity.Month11,
            Month12 = entity.Month12,
        };
    }
}