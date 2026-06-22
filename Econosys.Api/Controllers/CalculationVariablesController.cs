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
    public class CalculationVariablesController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public CalculationVariablesController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<ActionResult<CalculationVariableDto>> Get()
        {
            var entity = await _dbContext.CalculationVariables
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
        public async Task<ActionResult<CalculationVariableDto>> Upsert([FromBody] UpdateCalculationVariableRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var entity = await _dbContext.CalculationVariables
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync();

            if (entity is null)
            {
                entity = new CalculationVariable();
                MapValues(request, entity);
                _dbContext.CalculationVariables.Add(entity);
            }
            else
            {
                MapValues(request, entity);
            }

            await _dbContext.SaveChangesAsync();

            return Ok(MapToDto(entity));
        }

        private static void MapValues(UpdateCalculationVariableRequest request, CalculationVariable entity)
        {
            entity.FreightPerPallet = request.FreightPerPallet;
            entity.FreightPerVehicle = request.FreightPerVehicle;
            entity.LoadingPerPallet = request.LoadingPerPallet;
            entity.StoragePerM2 = request.StoragePerM2;
            entity.Interest = request.Interest;
            entity.LoadingPerDelivery = request.LoadingPerDelivery;
            entity.FreightFromStockPerPallet = request.FreightFromStockPerPallet;
            entity.TruckLoadingLengthMm = request.TruckLoadingLengthMm;
            entity.TruckLoadingWidthMm = request.TruckLoadingWidthMm;
        }

        private static CalculationVariableDto MapToDto(CalculationVariable entity) => new()
        {
            Id = entity.Id,
            FreightPerPallet = entity.FreightPerPallet,
            FreightPerVehicle = entity.FreightPerVehicle,
            LoadingPerPallet = entity.LoadingPerPallet,
            StoragePerM2 = entity.StoragePerM2,
            Interest = entity.Interest,
            LoadingPerDelivery = entity.LoadingPerDelivery,
            FreightFromStockPerPallet = entity.FreightFromStockPerPallet,
            TruckLoadingLengthMm = entity.TruckLoadingLengthMm,
            TruckLoadingWidthMm = entity.TruckLoadingWidthMm,
        };
    }
}
