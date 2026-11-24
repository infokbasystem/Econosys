using Econosys.Api.DTOs;
using Econosys.Api.Services;
using System.Net.Sockets;
using System.Text.RegularExpressions;
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
    private static readonly Regex EnhancedSmtpStatusRegex = new(
        @"(?<![\d.])[45]\.[0-7]\.\d{1,3}(?![\d.])",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Gets legacy contact selections and copy-to defaults for a document.</summary>
    [HttpGet("recipients/{itemType}/{itemId:int:min(1)}")]
    [ProducesResponseType(typeof(EmailRecipientsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmailRecipientsResponse>> GetRecipients(
        string itemType, int itemId, [FromQuery] string? receiverType,
        [FromQuery] int[] receiverIds, [FromServices] IEmailRecipientService recipients,
        CancellationToken cancellationToken)
    {
        var user = await userResolution.ResolveCurrentUserAsync(User);
        if (user is null)
            return Unauthorized(new ProblemDetails { Detail = "Kunde inte hitta den inloggade användaren." });

        try
        {
            var result = await recipients.GetAsync(itemType, itemId, receiverType, receiverIds, user, cancellationToken);
            return result is null
                ? NotFound(new ProblemDetails { Detail = "Dokumentet finns inte." })
                : Ok(result);
        }
        catch (EmailValidationException exception)
        {
            return BadRequest(new ProblemDetails { Detail = exception.Message });
        }
    }

    /// <summary>Gets sent-email history for an invoice, customer order, call-off, or transport order.</summary>
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
            "CUSTOMERORDER" => MailLogService.CustomerOrderItem,
            "INVOICE" => MailLogService.InvoiceItem,
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
        catch (EmailSmtpFailureException failure) when (!cancellationToken.IsCancellationRequested)
        {
            var exception = failure.SmtpException;
            var socketException = exception.InnerException;
            while (socketException is not null && socketException is not SocketException)
                socketException = socketException.InnerException;
            var networkError = (socketException as SocketException)?.SocketErrorCode.ToString();
            var status = exception.StatusCode.ToString();
            var networkDetails = networkError is null ? "" : $" Nätverksfel: {networkError}.";
            var enhancedStatusMatch = EnhancedSmtpStatusRegex.Match(exception.Message);
            var enhancedStatus = enhancedStatusMatch.Success ? enhancedStatusMatch.Value : null;
            var detail = enhancedStatus is null
                ? "SMTP-servern angav ingen identifierbar utökad felkod. Kontakta e-postadministratören för serverns detaljer."
                : DescribeEnhancedSmtpStatus(enhancedStatus);
            if (failure.ServerDetail is not null)
                detail = $"{detail} {failure.ServerDetail}";
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = $"Kunde inte bekräfta att e-postmeddelandet skickades. SMTP-status: {status} ({(int)exception.StatusCode}).{networkDetails} Kontrollera innan du försöker igen.",
                errorCode = "SmtpFailure",
                smtpStatus = status,
                smtpStatusCode = (int)exception.StatusCode,
                networkError,
                enhancedSmtpStatus = enhancedStatus,
                detail,
            });
        }
        catch (EmailDeliveryCancelledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "SMTP-anslutningen avbröts eller tog för lång tid. Kunde inte bekräfta att e-postmeddelandet skickades. Kontrollera innan du försöker igen.",
                errorCode = "SmtpCancelled",
            });
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Email delivery failed for user {UserId}.", user.Id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Kunde inte bekräfta att e-postmeddelandet skickades. Kontrollera innan du försöker igen." });
        }
    }

    private static string DescribeEnhancedSmtpStatus(string status)
    {
        var category = status switch
        {
            "5.1.1" => "Mottagarens brevlåda finns inte.",
            "4.2.2" or "5.2.2" => "Mottagarens brevlåda är full.",
            "4.3.4" or "5.3.4" => "Meddelandet är större än servern tillåter.",
            "4.7.1" or "5.7.1" => "Leveransen är inte tillåten eller har avvisats av säkerhetsskäl.",
            _ => DescribeEnhancedSmtpCategory(status[2]),
        };
        return $"Utökad SMTP-felkod: {status}. {category}";
    }

    private static string DescribeEnhancedSmtpCategory(char category) =>
        category switch
        {
            '0' => "E-postsystemet angav ett ospecificerat fel.",
            '1' => "Felet gäller en e-postadress.",
            '2' => "Felet gäller en brevlåda.",
            '3' => "Felet gäller e-postsystemet.",
            '4' => "Felet gäller nätverk eller routning.",
            '5' => "Felet gäller SMTP-protokollet.",
            '6' => "Felet gäller meddelandets innehåll eller format.",
            '7' => "Felet gäller säkerhet eller serverns policy.",
            _ => "E-postsystemet rapporterade ett fel.",
        };
}