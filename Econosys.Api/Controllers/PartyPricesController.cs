using System.ComponentModel.DataAnnotations;
using Econosys.Api.DTOs;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Econosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/party-prices/{partyType}/{id:int}")]
public class PartyPricesController(IPartyPricesService prices) : ControllerBase
{
    /// <summary>Get a customer's selling prices or a supplier's purchase prices and available items.</summary>
    [HttpGet]
    [ProducesResponseType<PartyPricesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartyPricesResponse>> Get(
        [RegularExpression("^(customer|supplier)$")] string partyType,
        int id, CancellationToken cancellationToken)
    {
        var response = await prices.GetAsync(id, partyType == "supplier", cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>Replace a customer's or supplier's pallet and extra-cost prices.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [RegularExpression("^(customer|supplier)$")] string partyType,
        int id, UpdatePartyPricesRequest request, CancellationToken cancellationToken)
    {
        var result = await prices.SaveAsync(id, partyType == "supplier", request, cancellationToken);
        if (!result.Found)
            return NotFound();
        if (result.Error is not null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: result.Error);

        return NoContent();
    }
}
