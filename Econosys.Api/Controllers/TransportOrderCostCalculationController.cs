using Econosys.Api.DTOs;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Econosys.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class TransportOrderCostCalculationController : ControllerBase
    {
        private readonly ITransportOrderCostCalculationService _calculationService;

        public TransportOrderCostCalculationController(ITransportOrderCostCalculationService calculationService)
        {
            _calculationService = calculationService;
        }

        public class RecalculateRequest
        {
            public int DeliveryStatus { get; set; }
        }

        [HttpPost("{transportOrderId:int}/recalculate")]
        public async Task<ActionResult<TransportOrderCostCalcDto>> Recalculate(int transportOrderId, [FromBody] RecalculateRequest request)
        {
            var result = await _calculationService.RecalculateAsync(transportOrderId, request.DeliveryStatus);
            if (!result.Success || result.CostCalc is null)
            {
                return BadRequest(result.ErrorMessage ?? "Kunde inte beräkna kalkyl.");
            }

            return Ok(TransportOrderCostCalcMapper.MapToDto(result.CostCalc));
        }
    }
}
