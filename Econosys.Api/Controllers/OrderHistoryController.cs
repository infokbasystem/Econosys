using Econosys.Api.DTOs;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Econosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/orderhistory")]
public sealed class OrderHistoryController(IOrderHistoryService history) : ControllerBase
{
    /// <summary>Gets production, deliveries, invoices and stocktakes for a supplier order.</summary>
    [HttpGet("supplierorders/{id:int:min(1)}")]
    [ProducesResponseType<OrderHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderHistoryResponse>> GetSupplierOrder(int id, CancellationToken cancellationToken)
    {
        var result = await history.GetAsync(id, false, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Gets a customer order's deliveries and its supplier order's inbound movements and stocktakes.</summary>
    [HttpGet("customerorders/{id:int:min(1)}")]
    [ProducesResponseType<OrderHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderHistoryResponse>> GetCustomerOrder(int id, CancellationToken cancellationToken)
    {
        var result = await history.GetAsync(id, true, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
