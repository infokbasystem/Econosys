using System.Data.Common;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using Econosys.Api.Common;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services;

public sealed record SmtpDeliverySettings(string Host, int Port, bool EnableSsl, string? Username, string? Password);

public interface IEmailTransport
{
    Task SendAsync(MailMessage message, SmtpDeliverySettings settings, CancellationToken cancellationToken);
}

public sealed class SmtpEmailTransport : IEmailTransport
{
    public async Task SendAsync(MailMessage message, SmtpDeliverySettings settings, CancellationToken cancellationToken)
    {
        using var smtp = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(settings.Username, settings.Password),
            Timeout = 60000,
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        await smtp.SendMailAsync(message, timeout.Token);
    }
}

public sealed class EmailValidationException(string message) : Exception(message);
public sealed class EmailConfigurationException(string message) : Exception(message);
public sealed class EmailDeliveryCancelledException(Exception innerException)
    : Exception("SMTP delivery was cancelled or timed out.", innerException);
public sealed class EmailSmtpFailureException(SmtpException innerException, string? serverDetail)
    : Exception("SMTP delivery could not be confirmed.", innerException)
{
    public SmtpException SmtpException { get; } = innerException;
    public string? ServerDetail { get; } = serverDetail;
}
public sealed class EmailLogPersistenceException(Exception innerException)
    : Exception("The email was sent, but its log could not be saved.", innerException);

public sealed class EmailService(
    ApplicationDbContext dbContext,
    IEmailTransport transport,
    ILogger<EmailService> logger)
{
    private static readonly string[] SettingNames =
        ["SMTPServer", "SMTPServerPort", "SMTPUseSSL", "SendUserName", "SendPassword"];
    private static readonly Regex DataImageRegex = new(
        """src\s*=\s*(?<quote>["'])data:(?<type>image/[a-z0-9.+-]+);base64,(?<data>[a-z0-9+/=\s]+)\k<quote>""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SendAsDeniedRegex = new(
        @"(?<account>[^\s<>;]+@[^\s<>;]+)\s+not allowed to send as\s+(?<sender>[^\s<>;]+@[^\s<>;]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task SendAsync(SendEmailRequest request, LegacyUser user, CancellationToken cancellationToken)
    {
        try
        {
            await SendCoreAsync(request, user, cancellationToken);
        }
        catch (EmailValidationException)
        {
            logger.LogWarning("Email validation failed for user {UserId}.", user.Id);
            throw;
        }
        catch (EmailConfigurationException)
        {
            logger.LogWarning("Email configuration is invalid for user {UserId}, company {CompanyId}.",
                user.Id, user.CompanyId);
            throw;
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException or TimeoutException)
        {
            logger.LogError("Email operation failed for user {UserId}; delivery could not be confirmed: {ErrorType}.",
                user.Id, exception.GetType().Name);
            throw;
        }
    }

    private async Task SendCoreAsync(SendEmailRequest request, LegacyUser user, CancellationToken cancellationToken)
    {
        var recipients = ParseRecipients(request.Receivers);
        var ccRecipients = ParseRecipients(request.CcRecipients);
        if (recipients.Count == 0)
        {
            throw new EmailValidationException("Välj minst en mottagare.");
        }

        var logItem = await ResolveLogItemAsync(request, cancellationToken);
        var logRecipients = string.Join("; ", recipients);
        if (logItem.HasValue && logRecipients.Length > 1000)
        {
            throw new EmailValidationException("Mottagarlistan är för lång för mail-loggen.");
        }

        if (!TryParseAddress(user.Email, out var sender))
        {
            throw new EmailConfigurationException("E-postadress saknas eller är ogiltig för användaren.");
        }

        if (request.Subject?.IndexOfAny(['\r', '\n']) >= 0)
        {
            throw new EmailValidationException("Ämnet får inte innehålla radbrytningar.");
        }

        var rows = await dbContext.MailSettings.AsNoTracking()
            .Where(setting => setting.CompanyId == user.CompanyId)
            .ToListAsync(cancellationToken);
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var name = row.Setting?.Trim();
            if (name is null || !SettingNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }
            if (!settings.TryAdd(name, row.Value))
            {
                throw new EmailConfigurationException("E-postinställningarna innehåller dubbletter.");
            }
        }

        settings.TryGetValue("SMTPServer", out var host);
        settings.TryGetValue("SMTPServerPort", out var rawPort);
        settings.TryGetValue("SMTPUseSSL", out var rawSsl);
        settings.TryGetValue("SendUserName", out var username);
        settings.TryGetValue("SendPassword", out var password);
        if (string.IsNullOrWhiteSpace(host) || !int.TryParse(rawPort, out var port) || port is < 1 or > 65535
            || !bool.TryParse(rawSsl, out var enableSsl))
        {
            throw new EmailConfigurationException("SMTP-server, port eller SSL-inställning saknas eller är ogiltig.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(sender!, user.Name ?? string.Empty),
            Subject = request.Subject ?? string.Empty,
            IsBodyHtml = true,
        };
        SetHtmlBody(message, $"<div style=\"font-family: DM Sans,Verdana, Geneva, sans-serif; font-size: 11pt;\">{request.Body ?? string.Empty}</div>");
        foreach (var recipient in recipients)
        {
            message.To.Add(recipient);
        }
        foreach (var recipient in ccRecipients.Except(recipients, StringComparer.OrdinalIgnoreCase))
        {
            message.CC.Add(recipient);
        }
        foreach (var file in request.Files)
        {
            var fileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
            var stream = file.OpenReadStream();
            try
            {
                message.Attachments.Add(new Attachment(stream,
                    string.IsNullOrWhiteSpace(fileName) ? "attachment" : fileName,
                    string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType));
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        try
        {
            await transport.SendAsync(message, new SmtpDeliverySettings(host.Trim(), port, enableSsl, username?.Trim(), password), cancellationToken);
        }
        catch (SmtpException exception)
        {
            logger.LogError("SMTP delivery could not be confirmed for user {UserId}, status {SmtpStatus}.",
                user.Id, exception.StatusCode);
            throw new EmailSmtpFailureException(exception, GetSendAsDenialDetail(exception, sender!, password));
        }
        catch (OperationCanceledException exception)
        {
            logger.LogWarning("SMTP delivery was cancelled or timed out for user {UserId}; delivery could not be confirmed. Client cancellation: {ClientCancelled}.",
                user.Id, cancellationToken.IsCancellationRequested);
            if (cancellationToken.IsCancellationRequested) throw;
            throw new EmailDeliveryCancelledException(exception);
        }
        logger.LogInformation("SMTP accepted an email for user {UserId}.", user.Id);

        if (logItem is { } item)
        {
            dbContext.LogEntries.Add(new LogEntry
            {
                Action = MailLogService.SendMailAction,
                Item = item.Item,
                ItemId = item.Id,
                DateTime = SwedishTime.Now,
                EmployeeId = user.Id,
                Info1 = logRecipients,
                Info2 = message.Subject,
                Source = "Econosys",
            });
            try
            {
                // Once SMTP has accepted the email, a disconnected client must not cancel its log.
                await dbContext.SaveChangesAsync(CancellationToken.None);
                logger.LogInformation("Email history saved for {ItemType} {ItemId}, user {UserId}.",
                    item.Item, item.Id, user.Id);
            }
            catch (Exception exception) when (exception is DbUpdateException or DbException or TimeoutException)
            {
                logger.LogError("SMTP accepted an email but history could not be saved for {ItemType} {ItemId}, user {UserId}: {ErrorType}.",
                    item.Item, item.Id, user.Id, exception.GetType().Name);
                throw new EmailLogPersistenceException(exception);
            }
        }
    }

    private static string? GetSendAsDenialDetail(SmtpException exception, string sender, string? password)
    {
        var match = SendAsDeniedRegex.Match(exception.Message);
        if (!match.Success
            || !TryParseAddress(match.Groups["account"].Value, out var account)
            || !TryParseAddress(match.Groups["sender"].Value, out var deniedSender))
            return null;

        const string genericDetail = "SMTP-servern anger att kontot inte har behörighet att skicka som den angivna avsändaren.";
        if (!string.Equals(deniedSender, sender, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrEmpty(password)
                && (account!.Contains(password, StringComparison.OrdinalIgnoreCase)
                    || deniedSender!.Contains(password, StringComparison.OrdinalIgnoreCase))))
            return genericDetail;

        return $"SMTP-servern anger att {account} inte har behörighet att skicka som {deniedSender}.";
    }

    // Like legacy, images in the body are embedded as inline attachments; most mail clients block data: URIs.
    private static void SetHtmlBody(MailMessage message, string html)
    {
        var images = new List<LinkedResource>();
        html = DataImageRegex.Replace(html, match =>
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(match.Groups["data"].Value);
            }
            catch (FormatException)
            {
                return match.Value;
            }
            var contentId = $"image{images.Count + 1}@econosys";
            images.Add(new LinkedResource(new MemoryStream(bytes), match.Groups["type"].Value) { ContentId = contentId });
            return $"src={match.Groups["quote"].Value}cid:{contentId}{match.Groups["quote"].Value}";
        });
        if (images.Count == 0)
        {
            message.Body = html;
            return;
        }
        var view = AlternateView.CreateAlternateViewFromString(html, System.Text.Encoding.UTF8, "text/html");
        images.ForEach(view.LinkedResources.Add);
        message.AlternateViews.Add(view);
    }

    private async Task<(string Item, int Id)?> ResolveLogItemAsync(
        SendEmailRequest request, CancellationToken cancellationToken)
    {
        if (new[] { request.CallOffId, request.TransportOrderId, request.InvoiceId }.Count(id => id.HasValue) > 1)
        {
            throw new EmailValidationException("Välj endast ett dokument för mail-loggen.");
        }

        if (request.InvoiceId is int invoiceId)
        {
            if (invoiceId <= 0 || !await dbContext.Invoices.AnyAsync(x => x.Id == invoiceId, cancellationToken))
            {
                throw new EmailValidationException("Fakturan för mail-loggen finns inte.");
            }
            return (MailLogService.InvoiceItem, invoiceId);
        }

        if (request.CallOffId is int callOffId)
        {
            if (callOffId <= 0 || !await dbContext.CallOffs.AnyAsync(x => x.Id == callOffId, cancellationToken))
            {
                throw new EmailValidationException("Avropet för mail-loggen finns inte.");
            }
            return (MailLogService.CallOffItem, callOffId);
        }

        if (request.TransportOrderId is int transportOrderId)
        {
            if (transportOrderId <= 0 || !await dbContext.TransportOrders.AnyAsync(x => x.Id == transportOrderId, cancellationToken))
            {
                throw new EmailValidationException("Transportordern för mail-loggen finns inte.");
            }
            return (MailLogService.TransportOrderItem, transportOrderId);
        }

        return null;
    }

    private static HashSet<string> ParseRecipients(string? json)
    {
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return recipients;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new EmailValidationException("Ogiltig mottagarlista.");
            }
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("doSend", out var selected)
                    || selected.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    throw new EmailValidationException("Ogiltig mottagarlista.");
                }
                if (!selected.GetBoolean())
                {
                    continue;
                }
                if (!item.TryGetProperty("email", out var email) || email.ValueKind != JsonValueKind.String
                    || !TryParseAddress(email.GetString(), out var address))
                {
                    throw new EmailValidationException("En vald e-postadress är ogiltig.");
                }
                recipients.Add(address!);
            }
        }
        catch (JsonException)
        {
            throw new EmailValidationException("Ogiltig mottagarlista.");
        }
        return recipients;
    }

    private static bool TryParseAddress(string? value, out string? address)
    {
        address = null;
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || !MailAddress.TryCreate(trimmed, out var parsed)
            || !string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        address = parsed.Address;
        return true;
    }
}