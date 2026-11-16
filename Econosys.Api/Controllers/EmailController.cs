using Econosys.Api.DTOs;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Econosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/email")]
public sealed class EmailController(
    ILegacyUserResolutionService userResolution,
    EmailService emailService,
    ILogger<EmailController> logger) : ControllerBase
{
    /// <summary>Gets sent-email history for a call-off or transport order.</summary>
    [HttpGet("log/{itemType}/{itemId:int:min(1)}")]
    [ProducesResponseType(typeof(IReadOnlyList<MailLogEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<MailLogEntryResponse>>> GetLog(
        string itemType,
        int itemId,
        [FromServices] IMailLogService mailLog,
        CancellationToken cancellationToken)
    {
        var item = itemType.ToUpperInvariant() switch
        {
            "CALLOFF" => MailLogService.CallOffItem,
            "TRANSPORTORDER" => MailLogService.TransportOrderItem,
            _ => null,
        };
        if (item is null)
        {
            return BadRequest(new ProblemDetails { Detail = "Ogiltig typ för mail-loggen." });
        }

        return Ok(await mailLog.GetAsync(item, itemId, cancellationToken));
    }

    [HttpPost("send")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(SendEmailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Send([FromForm] SendEmailRequest request, CancellationToken cancellationToken)
    {
        var user = await userResolution.ResolveCurrentUserAsync(User);
        if (user is null)
        {
            return Unauthorized(new { message = "Kunde inte hitta den inloggade användaren." });
        }

        try
        {
            await emailService.SendAsync(request, user, cancellationToken);
            return Ok(new SendEmailResponse("E-postmeddelandet skickades.", request.InquiryId, request.QuotationId));
        }
        catch (EmailValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (EmailConfigurationException exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message });
        }
        catch (EmailLogPersistenceException exception)
        {
            logger.LogError(exception, "Email was sent but its log could not be saved for user {UserId}.", user.Id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "E-postmeddelandet skickades, men kunde inte sparas i mail-loggen. Skicka inte om meddelandet.",
                emailSent = true,
            });
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Email delivery failed for user {UserId}.", user.Id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Kunde inte bekräfta att e-postmeddelandet skickades. Kontrollera innan du försöker igen." });
        }
    }
}