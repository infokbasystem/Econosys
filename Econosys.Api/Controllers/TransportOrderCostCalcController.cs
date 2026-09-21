using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class TransportOrderCostCalcController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public TransportOrderCostCalcController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("by-transport-order/{transportOrderId:int}")]
        public async Task<ActionResult<List<TransportOrderCostCalcDto>>> GetByTransportOrderId(int transportOrderId)
        {
            var costCalcs = await _dbContext.TransportOrderCostCalcs
                .AsNoTracking()
                .Where(x => x.TransportOrderId == transportOrderId)
                .Include(x => x.TransportOrderCostCalcSupplierOrders)
                    .ThenInclude(x => x.TransportOrderCostCalcSupplierOrderDeliveries)
                .OrderBy(x => x.CreatedDateTime)
                .ThenBy(x => x.Id)
                .ToListAsync();

            return Ok(costCalcs.Select(MapToDto).ToList());
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<TransportOrderCostCalcDto>> GetById(int id)
        {
            var costCalc = await _dbContext.TransportOrderCostCalcs
                .AsNoTracking()
                .Include(x => x.TransportOrderCostCalcSupplierOrders)
                    .ThenInclude(x => x.TransportOrderCostCalcSupplierOrderDeliveries)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (costCalc is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(costCalc));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var costCalc = await _dbContext.TransportOrderCostCalcs
                .Include(x => x.TransportOrderCostCalcSupplierOrders)
                    .ThenInclude(x => x.TransportOrderCostCalcSupplierOrderDeliveries)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (costCalc is null)
            {
                return NotFound();
            }

            var latestVersionId = await _dbContext.TransportOrderCostCalcs
                .Where(x => x.TransportOrderId == costCalc.TransportOrderId)
                .OrderByDescending(x => x.CreatedDateTime)
                .ThenByDescending(x => x.Id)
                .Select(x => x.Id)
                .FirstAsync();

            if (costCalc.Id == latestVersionId)
            {
                return BadRequest("Den senaste kostnadskalkylen kan inte raderas.");
            }

            foreach (var supplierOrder in costCalc.TransportOrderCostCalcSupplierOrders)
            {
                _dbContext.TransportOrderCostCalcSupplierOrderDeliveries.RemoveRange(
                    supplierOrder.TransportOrderCostCalcSupplierOrderDeliveries);
            }

            _dbContext.TransportOrderCostCalcSupplierOrders.RemoveRange(costCalc.TransportOrderCostCalcSupplierOrders);
            _dbContext.TransportOrderCostCalcs.Remove(costCalc);
            await _dbContext.SaveChangesAsync();

            return NoContent();
        }

        // Recomputes the same row/header totals as the legacy TransportOrderCostCalcViewModel arithmetic.
        [HttpPut("{id:int}")]
        public async Task<ActionResult<TransportOrderCostCalcDto>> Update(int id, [FromBody] UpdateTransportOrderCostCalcRequest request)
        {
            var costCalc = await _dbContext.TransportOrderCostCalcs
                .Include(x => x.TransportOrderCostCalcSupplierOrders)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (costCalc is null)
            {
                return NotFound();
            }

            costCalc.IsManualCalc = request.IsManualCalc;
            costCalc.IsLTL = request.IsLTL;
            costCalc.Note = request.Note;
            costCalc.CalcCityFrom = request.CalcCityFrom;
            costCalc.CalcPostalNrTo = request.CalcPostalNrTo;
            costCalc.CostCalcForcePriceListId = request.CostCalcForcePriceListId;

            var rows = costCalc.TransportOrderCostCalcSupplierOrders;
            var totalArea = rows.Sum(x => x.TotalNrOfPalletPlaces.GetValueOrDefault() * x.PalletFactor.GetValueOrDefault());

            if (request.ToInternationalCost.HasValue)
            {
                foreach (var row in rows)
                {
                    row.ToInternationalCost = request.ToInternationalCost.Value
                        * row.TotalNrOfPalletPlaces.GetValueOrDefault()
                        * row.PalletFactor.GetValueOrDefault()
                        / (totalArea == 0 ? 1 : totalArea);
                }
            }

            if (request.ResultInternationalCost.HasValue)
            {
                foreach (var row in rows)
                {
                    row.ResultInternationalCost = request.ResultInternationalCost.Value
                        * row.TotalNrOfPalletPlaces.GetValueOrDefault()
                        * row.PalletFactor.GetValueOrDefault()
                        / (totalArea == 0 ? 1 : totalArea);
                }
            }

            foreach (var rowRequest in request.SupplierOrders)
            {
                var row = rows.FirstOrDefault(x => x.Id == rowRequest.Id);
                if (row is null)
                {
                    continue;
                }

                // Explicit per-row entry overrides the header-level apportionment above.
                if (rowRequest.ToInternationalCost.HasValue)
                {
                    row.ToInternationalCost = rowRequest.ToInternationalCost;
                }
                row.TransportOrderDomesticCost = rowRequest.TransportOrderDomesticCost;
                row.TransportOrderLoading = rowRequest.TransportOrderLoading;
                row.TransportOrderUnloading = rowRequest.TransportOrderUnloading;
                row.TransportOrderOther = rowRequest.TransportOrderOther;
                row.ResultDomesticCost = rowRequest.ResultDomesticCost;
                row.ResultOther = rowRequest.ResultOther;
                row.ResultNote = rowRequest.ResultNote;
            }

            foreach (var row in rows)
            {
                row.TransportOrderTotal = row.ToInternationalCost.GetValueOrDefault()
                    + row.TransportOrderDomesticCost.GetValueOrDefault()
                    + row.TransportOrderUnloading.GetValueOrDefault()
                    + row.TransportOrderLoading.GetValueOrDefault()
                    + row.TransportOrderOther.GetValueOrDefault();
                row.DiffTransportOrderCaclulation = row.CaclulationUsedTotal.GetValueOrDefault() - row.TransportOrderTotal.GetValueOrDefault();

                row.ResultTotal = row.ResultInternationalCost.GetValueOrDefault()
                    + row.ResultDomesticCost.GetValueOrDefault()
                    + row.ResultOther.GetValueOrDefault();
                row.DiffResultCaclulation = row.CaclulationUsedTotal.GetValueOrDefault() - row.ResultTotal.GetValueOrDefault();
                row.DiffResultTransportOrder = row.TransportOrderTotal.GetValueOrDefault() - row.ResultTotal.GetValueOrDefault();
            }

            costCalc.ResultInternationalCost = rows.Sum(x => x.ResultInternationalCost ?? 0);

            await _dbContext.SaveChangesAsync();

            var reloaded = await _dbContext.TransportOrderCostCalcs
                .AsNoTracking()
                .Include(x => x.TransportOrderCostCalcSupplierOrders)
                    .ThenInclude(x => x.TransportOrderCostCalcSupplierOrderDeliveries)
                .FirstAsync(x => x.Id == id);

            return Ok(MapToDto(reloaded));
        }

        private static TransportOrderCostCalcDto MapToDto(Models.TransportOrderCostCalc costCalc) => TransportOrderCostCalcMapper.MapToDto(costCalc);
    }
}
