using Econosys.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Econosys.Api.Controllers;

/// <summary>Compares and refreshes the product information saved on documents.</summary>
[ApiController]
[Authorize]
[Route("api/document-products")]
public sealed class DocumentProductsController(
    IDocumentProductSnapshotService snapshots,
    ILegacyUserResolutionService users) : ControllerBase
{
    /// <summary>Returns current translated product information for a calculation.</summary>
    [HttpGet("calculations/{calculationId:int}")]
    public async Task<IActionResult> FromCalculation(
        int calculationId, [FromQuery] int? customerId, [FromQuery] int? supplierId,
        [FromQuery] int? languageId, CancellationToken cancellationToken)
    {
        var user = await users.ResolveCurrentUserAsync(User);
        if (user is null) return Unauthorized();
        try
        {
            return Ok(await snapshots.FromCalculationAsync(calculationId, user.CompanyId, customerId, supplierId, languageId, cancellationToken));
        }
        catch (DocumentProductSourceException error)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: error.Message);
        }
    }

    /// <summary>Returns saved and current product information for a document.</summary>
    [HttpGet("{type}/{id:int}")]
    public async Task<IActionResult> Get(string type, int id, CancellationToken cancellationToken)
    {
        var user = await users.ResolveCurrentUserAsync(User);
        if (user is null) return Unauthorized();
        try
        {
            var result = await snapshots.GetAsync(type, id, user.CompanyId, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (DocumentProductSourceException error)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: error.Message);
        }
    }

    /// <summary>Replaces a document's saved product information with the current translated values.</summary>
    [HttpPut("{type}/{id:int}")]
    public async Task<IActionResult> Refresh(string type, int id, [FromBody] RefreshDocumentProductRequest request, CancellationToken cancellationToken)
    {
        var user = await users.ResolveCurrentUserAsync(User);
        if (user is null) return Unauthorized();
        try
        {
            var result = await snapshots.RefreshAsync(type, id, user.CompanyId, user.Id, request.Edited, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (DocumentProductSourceException error)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: error.Message);
        }
        catch (DocumentProductConflictException)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: "Dokumentet har ändrats. Läs in det igen innan produktinformationen uppdateras.");
        }
    }
}

/// <summary>Version of the document last observed before refreshing its product information.</summary>
public sealed record RefreshDocumentProductRequest(DateTime? Edited);
