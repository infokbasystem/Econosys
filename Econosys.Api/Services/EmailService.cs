using System.Data.Common;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
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
public sealed class EmailLogPersistenceException(Exception innerException)
    : Exception("The email was sent, but its log could not be saved.", innerException);

public sealed class EmailService(ApplicationDbContext dbContext, IEmailTransport transport)
{
    private static readonly string[] SettingNames =
        ["SMTPServer", "SMTPServerPort", "SMTPUseSSL", "SendUserName", "SendPassword"];

    public async Task SendAsync(SendEmailRequest request, LegacyUser user, CancellationToken cancellationToken)
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
            Body = $"<div style=\"font-family: Verdana, Geneva, sans-serif; font-size: 11pt;\">{request.Body ?? string.Empty}</div>",
            IsBodyHtml = true,
        };
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

        await transport.SendAsync(message, new SmtpDeliverySettings(host.Trim(), port, enableSsl, username?.Trim(), password), cancellationToken);

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
            }
            catch (Exception exception) when (exception is DbUpdateException or DbException or TimeoutException)
            {
                throw new EmailLogPersistenceException(exception);
            }
        }
    }

    private async Task<(string Item, int Id)?> ResolveLogItemAsync(
        SendEmailRequest request, CancellationToken cancellationToken)
    {
        if (request.CallOffId.HasValue && request.TransportOrderId.HasValue)
        {
            throw new EmailValidationException("Välj endast ett avrop eller en transportorder för mail-loggen.");
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