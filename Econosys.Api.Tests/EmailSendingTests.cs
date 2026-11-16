using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Econosys.Api.Tests;

public class EmailSendingTests
{
    private const string SelectedRecipient = "[{\"email\":\"to@example.com\",\"doSend\":true}]";

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static LegacyUser User(int companyId = 1) => new()
    {
        Id = 42,
        CompanyId = companyId,
        Email = "sender@example.com",
        Name = "Sender Name",
    };

    private static async Task AddSettings(ApplicationDbContext db, int companyId = 1)
    {
        db.MailSettings.AddRange(
            new MailSetting { CompanyId = companyId, Setting = " SMTPServer ", Value = "smtp.example.com" },
            new MailSetting { CompanyId = companyId, Setting = "smtpserverport", Value = "587" },
            new MailSetting { CompanyId = companyId, Setting = "SMTPUseSSL", Value = "True" },
            new MailSetting { CompanyId = companyId, Setting = "SendUserName", Value = "account" },
            new MailSetting { CompanyId = companyId, Setting = "SendPassword", Value = "secret-value" });
        await db.SaveChangesAsync();
    }

    private static SendEmailRequest Request(string? receivers = SelectedRecipient, string? cc = null) => new()
    {
        Subject = "Subject",
        Body = "<strong>Content</strong>",
        Receivers = receivers,
        CcRecipients = cc,
    };

    [Fact]
    public async Task SendsOnlySelectedRecipientsAndUsesCompanySettingsAndAttachments()
    {
        await using var db = CreateDb();
        await AddSettings(db, 2);
        db.MailSettings.Add(new MailSetting { CompanyId = 1, Setting = "SMTPServer", Value = "wrong.example.com" });
        await db.SaveChangesAsync();
        var transport = new FakeTransport();
        var bytes = Encoding.UTF8.GetBytes("pdf contents");
        var request = Request(
            "[{\"email\":\"to@example.com\",\"doSend\":true},{\"email\":\"TO@example.com\",\"doSend\":true},{\"email\":\"hidden@example.com\",\"doSend\":false}]",
            "[{\"email\":\"to@example.com\",\"doSend\":true},{\"email\":\"copy@example.com\",\"doSend\":true},{\"email\":\"ignored@example.com\",\"doSend\":false}]") with
        {
            Files = [new FormFile(new MemoryStream(bytes), 0, bytes.Length, "files", "report.pdf")
                { Headers = new HeaderDictionary(), ContentType = "application/pdf" }],
        };

        await new EmailService(db, transport).SendAsync(request, User(2), CancellationToken.None);

        Assert.Equal(1, transport.Calls);
        Assert.Equal("sender@example.com", transport.From);
        Assert.Equal("Sender Name", transport.SenderName);
        Assert.Equal(new[] { "to@example.com" }, transport.To);
        Assert.Equal(new[] { "copy@example.com" }, transport.Cc);
        Assert.Equal("Subject", transport.Subject);
        Assert.Contains("<strong>Content</strong>", transport.Body);
        Assert.True(transport.IsBodyHtml);
        Assert.Equal("report.pdf", transport.AttachmentName);
        Assert.Equal("application/pdf", transport.AttachmentContentType);
        Assert.Equal(bytes, transport.AttachmentBytes);
        Assert.Equal(new SmtpDeliverySettings("smtp.example.com", 587, true, "account", "secret-value"), transport.Settings);
    }

    [Fact]
    public async Task MailLogReturnsMatchingSentEntriesInChronologicalOrder()
    {
        await using var db = CreateDb();
        db.LegacyUsers.Add(User());
        db.LogEntries.AddRange(
            new LogEntry { Id = 1, Item = "CALLOFF", ItemId = 73, Action = "SENDMAIL", DateTime = new DateTime(2026, 7, 12, 8, 33, 0), EmployeeId = 42, Info1 = "to@example.com", Info2 = "First subject" },
            new LogEntry { Id = 2, Item = "CALLOFF", ItemId = 73, Action = "SENDMAIL", DateTime = new DateTime(2026, 7, 12, 8, 31, 0), EmployeeId = 999, Info1 = "other@example.com", Info2 = "Earlier subject" },
            new LogEntry { Id = 3, Item = "CALLOFF", ItemId = 73, Action = "SAVE", DateTime = new DateTime(2026, 7, 12, 8, 30, 0) },
            new LogEntry { Id = 4, Item = "TRANSPORTORDER", ItemId = 73, Action = "SENDMAIL", DateTime = new DateTime(2026, 7, 12, 8, 32, 0) },
            new LogEntry { Id = 5, Item = "CALLOFF", ItemId = 74, Action = "SENDMAIL", DateTime = new DateTime(2026, 7, 12, 8, 32, 0) });
        await db.SaveChangesAsync();

        var service = new MailLogService(db);
        var entries = await service.GetAsync("CALLOFF", 73, CancellationToken.None);

        Assert.Equal(new[] { 2, 1 }, entries.Select(entry => entry.Id));
        Assert.Equal(TimeSpan.FromHours(2), entries[0].DateTime.Offset);
        Assert.Equal("other@example.com", entries[0].Recipients);
        Assert.Equal("Earlier subject", entries[0].Subject);
        Assert.Equal("", entries[0].EmployeeName);
        Assert.Equal("Sender Name", entries[1].EmployeeName);
        Assert.Single(await service.GetAsync("TRANSPORTORDER", 73, CancellationToken.None));
        Assert.Empty(await service.GetAsync("TRANSPORTORDER", 74, CancellationToken.None));
    }

    [Theory]
    [InlineData("CALLOFF")]
    [InlineData("TRANSPORTORDER")]
    public async Task SuccessfulLogisticsEmailIsRecordedInLegacyLog(string item)
    {
        await using var db = CreateDb();
        await AddSettings(db);
        db.LegacyUsers.Add(User());
        db.CallOffs.Add(new CallOff { Id = 73 });
        db.TransportOrders.Add(new TransportOrder { Id = 73 });
        await db.SaveChangesAsync();
        var transport = new FakeTransport();
        var request = Request(
            "[{\"email\":\"to@example.com\",\"doSend\":true},{\"email\":\"other@example.com\",\"doSend\":true},{\"email\":\"ignored@example.com\",\"doSend\":false}]",
            "[{\"email\":\"copy@example.com\",\"doSend\":true}]") with
        {
            CallOffId = item == "CALLOFF" ? 73 : null,
            TransportOrderId = item == "TRANSPORTORDER" ? 73 : null,
        };

        await new EmailService(db, transport).SendAsync(request, User(), CancellationToken.None);

        var entry = Assert.Single(await new MailLogService(db).GetAsync(item, 73, CancellationToken.None));
        Assert.Equal("to@example.com; other@example.com", entry.Recipients);
        Assert.Equal("Subject", entry.Subject);
        Assert.Equal("Sender Name", entry.EmployeeName);
        var stored = await db.LogEntries.SingleAsync();
        Assert.Equal("SENDMAIL", stored.Action);
        Assert.Equal("Econosys", stored.Source);
        Assert.Equal(42, stored.EmployeeId);
    }

    [Fact]
    public async Task InvalidOrFailedLogisticsEmailDoesNotCreateLog()
    {
        await using var db = CreateDb();
        await AddSettings(db);
        var transport = new FakeTransport();
        var service = new EmailService(db, transport);

        await Assert.ThrowsAsync<EmailValidationException>(() => service.SendAsync(
            Request() with { CallOffId = 73 }, User(), CancellationToken.None));
        await Assert.ThrowsAsync<EmailValidationException>(() => service.SendAsync(
            Request() with { CallOffId = 73, TransportOrderId = 73 }, User(), CancellationToken.None));
        Assert.Equal(0, transport.Calls);

        db.CallOffs.Add(new CallOff { Id = 73 });
        await db.SaveChangesAsync();
        transport.Failure = new SmtpException("Delivery failed");
        await Assert.ThrowsAsync<SmtpException>(() => service.SendAsync(
            Request() with { CallOffId = 73 }, User(), CancellationToken.None));
        Assert.Empty(await db.LogEntries.ToListAsync());
    }

    [Fact]
    public async Task RecipientListTooLongForLogIsRejectedBeforeSending()
    {
        await using var db = CreateDb();
        db.CallOffs.Add(new CallOff { Id = 73 });
        await db.SaveChangesAsync();
        var recipients = System.Text.Json.JsonSerializer.Serialize(Enumerable.Range(0, 60)
            .Select(index => new { email = $"recipient{index:D3}@example.com", doSend = true }));
        var transport = new FakeTransport();

        var error = await Assert.ThrowsAsync<EmailValidationException>(() => new EmailService(db, transport)
            .SendAsync(Request(recipients) with { CallOffId = 73 }, User(), CancellationToken.None));

        Assert.Contains("mail-loggen", error.Message);
        Assert.Equal(0, transport.Calls);
        Assert.Empty(await db.LogEntries.ToListAsync());
    }

    [Fact]
    public async Task LogIsSavedWhenRequestIsCancelledAfterEmailDelivery()
    {
        await using var db = CreateDb();
        await AddSettings(db);
        db.CallOffs.Add(new CallOff { Id = 73 });
        await db.SaveChangesAsync();
        using var cancellation = new CancellationTokenSource();
        var transport = new FakeTransport { OnSent = cancellation.Cancel };

        await new EmailService(db, transport).SendAsync(
            Request() with { CallOffId = 73 }, User(), cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.Single(await db.LogEntries.ToListAsync());
    }

    [Fact]
    public async Task LogWriteFailureReportsThatEmailWasAlreadySent()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new FailingLogDb(options);
        await AddSettings(db);
        db.CallOffs.Add(new CallOff { Id = 73 });
        await db.SaveChangesAsync();
        db.FailLogSave = true;
        var transport = new FakeTransport();

        var result = Assert.IsType<ObjectResult>(await Controller(db, transport, User()).Send(
            Request() with { CallOffId = 73 }, CancellationToken.None));

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        Assert.Equal(1, transport.Calls);
        var response = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"emailSent\":true", response);
        Assert.Contains("mail-loggen", response);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("[{\"email\":\"bad address\",\"doSend\":true}]")]
    [InlineData("[{\"email\":\"to@example.com\",\"doSend\":false}]")]
    [InlineData("[{\"email\":\"to@example.com\"}]")]
    public async Task InvalidOrUnselectedRecipientsNeverSend(string recipients)
    {
        await using var db = CreateDb();
        await AddSettings(db);
        var transport = new FakeTransport();

        await Assert.ThrowsAsync<EmailValidationException>(() => new EmailService(db, transport)
            .SendAsync(Request(recipients), User(), CancellationToken.None));
        Assert.Equal(0, transport.Calls);
    }

    [Fact]
    public async Task CopyRecipientAloneCannotSend()
    {
        await using var db = CreateDb();
        var transport = new FakeTransport();
        await Assert.ThrowsAsync<EmailValidationException>(() => new EmailService(db, transport)
            .SendAsync(Request("[]", SelectedRecipient), User(), CancellationToken.None));
        Assert.Equal(0, transport.Calls);
    }

    [Fact]
    public async Task InvalidSelectedCopyRecipientNeverSends()
    {
        await using var db = CreateDb();
        var transport = new FakeTransport();
        await Assert.ThrowsAsync<EmailValidationException>(() => new EmailService(db, transport)
            .SendAsync(Request(SelectedRecipient, "[{\"email\":\"invalid\",\"doSend\":true}]"), User(), CancellationToken.None));
        Assert.Equal(0, transport.Calls);
    }

    [Fact]
    public async Task MissingInvalidOrDuplicateSettingsAndSenderNeverSend()
    {
        await using var db = CreateDb();
        var transport = new FakeTransport();
        var service = new EmailService(db, transport);
        await Assert.ThrowsAsync<EmailConfigurationException>(() => service.SendAsync(Request(), User(), CancellationToken.None));

        await AddSettings(db);
        var port = await db.MailSettings.SingleAsync(setting => setting.Setting == "smtpserverport");
        port.Value = "invalid";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<EmailConfigurationException>(() => service.SendAsync(Request(), User(), CancellationToken.None));

        port.Value = "587";
        var ssl = await db.MailSettings.SingleAsync(setting => setting.Setting == "SMTPUseSSL");
        ssl.Value = "invalid";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<EmailConfigurationException>(() => service.SendAsync(Request(), User(), CancellationToken.None));

        ssl.Value = "true";
        db.MailSettings.Add(new MailSetting { CompanyId = 1, Setting = "smtpserver", Value = "duplicate" });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<EmailConfigurationException>(() => service.SendAsync(Request(), User(), CancellationToken.None));
        await Assert.ThrowsAsync<EmailConfigurationException>(() => service.SendAsync(Request(), new LegacyUser { CompanyId = 1 }, CancellationToken.None));
        Assert.Equal(0, transport.Calls);
    }

    [Fact]
    public async Task ControllerReturnsSuccessOnlyAfterTransportAndHidesFailures()
    {
        await using var db = CreateDb();
        await AddSettings(db);
        var transport = new FakeTransport();
        var controller = Controller(db, transport, User());
        var result = await controller.Send(Request() with { InquiryId = 7 }, CancellationToken.None);
        var response = Assert.IsType<SendEmailResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(7, response.InquiryId);
        Assert.Equal(1, transport.Calls);

        transport.Failure = new SmtpException("secret-value");
        var failed = Assert.IsType<ObjectResult>(await controller.Send(Request(), CancellationToken.None));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, failed.StatusCode);
        Assert.DoesNotContain("secret-value", System.Text.Json.JsonSerializer.Serialize(failed.Value));
        Assert.IsType<UnauthorizedObjectResult>(await Controller(db, transport, null).Send(Request(), CancellationToken.None));
    }

    [Fact]
    public async Task MultipartEndpointBindsModalRequestAndFiles()
    {
        await using var db = CreateDb();
        await AddSettings(db);
        db.CallOffs.Add(new CallOff { Id = 3158 });
        db.LegacyUsers.Add(User());
        await db.SaveChangesAsync();
        var transport = new FakeTransport();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(EmailController).Assembly);
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(db);
        builder.Services.AddSingleton<ILegacyUserResolutionService>(new FakeUserResolution(User()));
        builder.Services.AddSingleton<IEmailTransport>(transport);
        builder.Services.AddScoped<EmailService>();
        builder.Services.AddScoped<IMailLogService, MailLogService>();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            using var form = new MultipartFormDataContent
            {
                { new StringContent("Subject"), "subject" },
                { new StringContent("<b>Body</b>"), "body" },
                { new StringContent(SelectedRecipient), "receivers" },
                { new StringContent("[]"), "ccRecipients" },
                { new StringContent("7"), "inquiryId" },
            };
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes("multipart-pdf"));
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            form.Add(file, "files", "document.pdf");

            using var response = await client.PostAsync("/api/email/send", form);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("\"inquiryId\":7", await response.Content.ReadAsStringAsync());
            Assert.Equal(1, transport.Calls);
            Assert.Equal("document.pdf", transport.AttachmentName);
            Assert.Equal("multipart-pdf", Encoding.UTF8.GetString(transport.AttachmentBytes!));

            using var loggedForm = new MultipartFormDataContent
            {
                { new StringContent("Freight order"), "subject" },
                { new StringContent(SelectedRecipient), "receivers" },
                { new StringContent("3158"), "callOffId" },
            };
            using var loggedResponse = await client.PostAsync("/api/email/send", loggedForm);
            Assert.Equal(HttpStatusCode.OK, loggedResponse.StatusCode);

            using var historyResponse = await client.GetAsync("/api/email/log/calloff/3158");
            Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
            var history = await historyResponse.Content.ReadAsStringAsync();
            Assert.Contains("\"recipients\":\"to@example.com\"", history);
            Assert.Contains("\"subject\":\"Freight order\"", history);
            Assert.Contains("\"employeeName\":\"Sender Name\"", history);

            using var invalidResponse = await client.GetAsync("/api/email/log/unknown/3158");
            Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task SmtpTransportDeliversToLoopbackServer()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var receive = ReceiveSmtpMessage(listener, timeout.Token);
        using var message = new MailMessage("sender@example.com", "recipient@example.com", "Local SMTP test", "Message content");

        await new SmtpEmailTransport().SendAsync(message,
            new SmtpDeliverySettings("127.0.0.1", port, false, null, null), timeout.Token);
        var received = await receive;

        Assert.Contains("Subject: Local SMTP test", received);
        Assert.Contains("Message content", received);
    }

    private static async Task<string> ReceiveSmtpMessage(TcpListener listener, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        await using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };
        await writer.WriteLineAsync("220 localhost ready");
        var data = new StringBuilder();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) || line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("250 localhost");
            }
            else if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("354 send data");
                while (await reader.ReadLineAsync(cancellationToken) is { } content && content != ".")
                {
                    data.AppendLine(content);
                }
                await writer.WriteLineAsync("250 accepted");
            }
            else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("221 bye");
                break;
            }
            else
            {
                await writer.WriteLineAsync("250 ok");
            }
        }
        return data.ToString();
    }

    private static EmailController Controller(ApplicationDbContext db, FakeTransport transport, LegacyUser? user) =>
        new(new FakeUserResolution(user), new EmailService(db, transport), NullLogger<EmailController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    private sealed class FailingLogDb(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public bool FailLogSave { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (FailLogSave) throw new DbUpdateException("Simulated log failure.");
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class FakeUserResolution(LegacyUser? user) : ILegacyUserResolutionService
    {
        public Task<LegacyUser?> ResolveCurrentUserAsync(ClaimsPrincipal principal) => Task.FromResult(user);
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test")], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class FakeTransport : IEmailTransport
    {
        public int Calls { get; private set; }
        public string? From { get; private set; }
        public string? SenderName { get; private set; }
        public string[] To { get; private set; } = [];
        public string[] Cc { get; private set; } = [];
        public string? Subject { get; private set; }
        public string? Body { get; private set; }
        public bool IsBodyHtml { get; private set; }
        public string? AttachmentName { get; private set; }
        public string? AttachmentContentType { get; private set; }
        public byte[]? AttachmentBytes { get; private set; }
        public SmtpDeliverySettings? Settings { get; private set; }
        public Exception? Failure { get; set; }
        public Action? OnSent { get; set; }

        public async Task SendAsync(MailMessage message, SmtpDeliverySettings settings, CancellationToken cancellationToken)
        {
            Calls++;
            if (Failure is not null)
            {
                throw Failure;
            }
            From = message.From?.Address;
            SenderName = message.From?.DisplayName;
            To = message.To.Select(address => address.Address).ToArray();
            Cc = message.CC.Select(address => address.Address).ToArray();
            Subject = message.Subject;
            Body = message.Body;
            IsBodyHtml = message.IsBodyHtml;
            Settings = settings;
            if (message.Attachments.Count > 0)
            {
                var attachment = message.Attachments[0];
                AttachmentName = attachment.Name;
                AttachmentContentType = attachment.ContentType.MediaType;
                using var buffer = new MemoryStream();
                await attachment.ContentStream.CopyToAsync(buffer, cancellationToken);
                AttachmentBytes = buffer.ToArray();
            }
            OnSent?.Invoke();
        }
    }
}