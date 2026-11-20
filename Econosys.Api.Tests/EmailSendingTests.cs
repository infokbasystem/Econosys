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

    [Theory]
    [InlineData("SUPPLIERORDER")]
    [InlineData("INQUIRY")]
    public async Task SupplierRecipientDefaultsUseDocumentFlagsAndSenderCopy(string itemType)
    {
        await using var db = CreateDb();
        db.SupplierOrders.Add(new SupplierOrder { Id = 73, SupplierId = 9 });
        db.Inquiries.Add(new Inquiry { Id = 73, SelectedInquiryRecipientId = 8 });
        db.InquiryRecipients.Add(new InquiryRecipient { Id = 8, InquiryId = 73, SupplierId = 9 });
        db.SupplierContactPersons.AddRange(
            new SupplierContactPerson { Id = 1, SupplierId = 9, Email = "unselected@example.com", Title = "Sales" },
            new SupplierContactPerson { Id = 2, SupplierId = 9, Email = "order@example.com", MailSupplierOrder = true, Title = "Purchasing" },
            new SupplierContactPerson { Id = 3, SupplierId = 9, Email = "inquiry@example.com", MailInquiry = true },
            new SupplierContactPerson { Id = 4, SupplierId = 10, Email = "wrong-supplier@example.com", MailInquiry = true, MailSupplierOrder = true },
            new SupplierContactPerson { Id = 5, SupplierId = 9, Email = "", MailSupplierOrder = true });
        await db.SaveChangesAsync();

        var result = await new EmailRecipientService(db).GetAsync(itemType, 73, null, [], User(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(4, result.Recipients.Count);
        Assert.Equal(itemType == "INQUIRY" ? "inquiry@example.com" : "order@example.com", result.Recipients[0].Email);
        Assert.True(result.Recipients[0].DoSend);
        Assert.All(result.Recipients.Skip(1).SkipLast(1), x => Assert.False(x.DoSend));
        Assert.Equal("Purchasing", result.Recipients.Single(x => x.Email == "order@example.com").Title);
        Assert.Equal(new EmailRecipientResponse("", true), result.Recipients[^1]);
        Assert.Equal(new[] { new EmailRecipientResponse("sender@example.com", true), new EmailRecipientResponse("", true) }, result.CcRecipients);
    }

    [Theory]
    [InlineData("QUOTATION", "quotation@example.com")]
    [InlineData("CUSTOMERORDER", "order@example.com")]
    [InlineData("INVOICE", "invoice@example.com")]
    [InlineData("CALLOFFCONFIRMATION", "confirmation@example.com")]
    public async Task CustomerRecipientDefaultsUseCustomerRatherThanSupplierAndCorrectCopies(string type, string selected)
    {
        await using var db = CreateDb();
        db.Quotations.Add(new Quotation { Id = 73, CustomerId = 9 });
        db.CustomerOrders.Add(new CustomerOrder { Id = 73, CustomerId = 9 });
        db.Invoices.Add(new Invoice { Id = 73, CustomerId = 9 });
        db.CallOffs.Add(new CallOff { Id = 73, ShipperId = 8 });
        db.Shippers.Add(new Shipper { Id = 8, Mail = "shipper@example.com" });
        db.DeliveryFromStocks.Add(new DeliveryFromStock { Id = 7, CustomerOrderId = 73 });
        db.CallOffDeliveries.Add(new CallOffDelivery { Id = 6, CallOffId = 73, DeliveryFromStockId = 7 });
        db.CustomerContactPersons.AddRange(
            new CustomerContactPerson { Id = 1, CustomerId = 9, Email = "quotation@example.com", MailQuotation = true },
            new CustomerContactPerson { Id = 2, CustomerId = 9, Email = "order@example.com", MailCustomerOrder = true },
            new CustomerContactPerson { Id = 3, CustomerId = 9, Email = "invoice@example.com", MailInvoice = true },
            new CustomerContactPerson { Id = 4, CustomerId = 9, Email = "confirmation@example.com", MailCallOffConfirmation = true },
            new CustomerContactPerson { Id = 5, CustomerId = 10, Email = "wrong-customer@example.com", MailQuotation = true });
        await db.SaveChangesAsync();

        var result = await new EmailRecipientService(db).GetAsync(type, 73, null, [], User(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(selected, result.Recipients[0].Email);
        Assert.True(result.Recipients[0].DoSend);
        Assert.Equal(5, result.Recipients.Count);
        Assert.All(result.Recipients.Skip(1).SkipLast(1), x => Assert.False(x.DoSend));
        var copies = type == "INVOICE"
            ? new[] { "customerinvoice@econopack.se", "skickadepdffakturor@econopack.se", "" }
            : type == "CALLOFFCONFIRMATION" ? new[] { "transport@econopack.se", "" } : new[] { "sender@example.com", "" };
        Assert.Equal(copies, result.CcRecipients.Select(x => x.Email));
        Assert.All(result.CcRecipients, x => Assert.True(x.DoSend));
    }

    [Fact]
    public async Task LogisticsRecipientDefaultsFollowSelectedPdfDestination()
    {
        await using var db = CreateDb();
        db.Shippers.Add(new Shipper { Id = 8, Mail = "shipper@example.com" });
        db.CallOffs.Add(new CallOff { Id = 73, ShipperId = 8 });
        db.TransportOrders.Add(new TransportOrder { Id = 73, ShipperId = 8 });
        db.SupplierFactories.Add(new SupplierFactory { Id = 7, SupplierId = 9 });
        db.SupplierOrders.Add(new SupplierOrder { Id = 6, SupplierId = 9, SupplierFactoryId = 7 });
        db.Inventories.Add(new Inventory { Id = 5, Email = "inventory@example.com" });
        db.DeliveryToStocks.Add(new DeliveryToStock { Id = 4, InventoryId = 5 });
        db.TransportOrderDeliveries.Add(new TransportOrderDelivery { Id = 3, TransportOrderId = 73, SupplierOrderId = 6, DeliveryToStockId = 4 });
        db.SupplierContactPersons.AddRange(
            new SupplierContactPerson { Id = 1, SupplierId = 9, Email = "transport@example.com", DoMailTransportOrder = true },
            new SupplierContactPerson { Id = 2, SupplierId = 9, Email = "order@example.com", MailSupplierOrder = true });
        await db.SaveChangesAsync();
        var service = new EmailRecipientService(db);

        foreach (var type in new[] { "CALLOFF", "TRANSPORTORDER" })
        {
            var result = await service.GetAsync(type, 73, "TRANSPORTER", [], User(), CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal(new EmailRecipientResponse("shipper@example.com", true), result.Recipients[0]);
            Assert.Equal("transport@econopack.se", result.CcRecipients[0].Email);
        }
        var supplier = await service.GetAsync("TRANSPORTORDER", 73, "SUPPLIER", [7], User(), CancellationToken.None);
        Assert.NotNull(supplier);
        Assert.Equal(new EmailRecipientResponse("transport@example.com", true), supplier.Recipients[0]);
        Assert.False(supplier.Recipients[1].DoSend);
        var inventory = await service.GetAsync("TRANSPORTORDER", 73, "RECEIVER", [5], User(), CancellationToken.None);
        Assert.NotNull(inventory);
        Assert.Equal(new EmailRecipientResponse("inventory@example.com", true), inventory.Recipients[0]);
        await Assert.ThrowsAsync<EmailValidationException>(() => service.GetAsync("TRANSPORTORDER", 73, "SUPPLIER", [999], User(), CancellationToken.None));
        await Assert.ThrowsAsync<EmailValidationException>(() => service.GetAsync("TRANSPORTORDER", 73, "RECEIVER", [999], User(), CancellationToken.None));
    }

    [Fact]
    public async Task RecipientDefaultsDoNotSelectGeneralSupplierEmailOrReuseAnotherDocument()
    {
        await using var db = CreateDb();
        db.Suppliers.Add(new Supplier { Id = 9, Email = "general@example.com" });
        db.SupplierOrders.Add(new SupplierOrder { Id = 73, SupplierId = 9 });
        await db.SaveChangesAsync();
        var service = new EmailRecipientService(db);

        var result = await service.GetAsync("SUPPLIERORDER", 73, null, [], User(), CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(new EmailRecipientResponse("", true), Assert.Single(result.Recipients));
        Assert.Null(await service.GetAsync("SUPPLIERORDER", 999, null, [], User(), CancellationToken.None));
        await Assert.ThrowsAsync<EmailValidationException>(() => service.GetAsync("unknown", 73, null, [], User(), CancellationToken.None));
    }

    private static void AddMailTexts(ApplicationDbContext db, string item, int subjectCode, int bodyCode,
        params (int Code, string Lang, string? Text, string? Html)[] translations)
    {
        db.Languages.AddRange(new Language { Id = 1, LanguageCode = "SW" }, new Language { Id = 2, LanguageCode = "EN" });
        db.MailTexts.AddRange(
            new MailText { Id = 1, Item = item, Type = "Subject", TranslationCode = subjectCode },
            new MailText { Id = 2, Item = item, Type = "Body", TranslationCode = bodyCode });
        db.TranslationItems.AddRange(translations.Select((x, i) => new TranslationItem
            { Id = i + 1, TranslationCode = x.Code, LangCode = x.Lang, Translation = x.Text, TranslationHtml = x.Html }));
    }

    [Theory]
    [InlineData(2, "Order confirmation: 39697 PO-1 Box", "<p>Ack 39697 PO-1 Box Vecka 47</p><b>Sender Name Sales manager</b> www.econopack.se")]
    [InlineData(1, "Ordererkännande: 39697 / PO-1 / Box", "<p>Ordererkännande 39697 Vecka 47</p><b>Sender Name Försäljningschef</b> www.econopack.se")]
    public async Task EmailTextUsesLegacyTranslationForCustomerLanguageAndFillsPlaceholders(int languageId, string subject, string body)
    {
        await using var db = CreateDb();
        db.Customers.Add(new Customer { Id = 9, LanguageId = languageId });
        db.CustomerOrders.Add(new CustomerOrder { Id = 73, CustomerId = 9, CustomerOrderNr = "39697", YourOrderNr = "PO-1",
            Product = "Box", DeliveryDate = new DateTime(2026, 11, 18), DeliveryDateWeekMode = true });
        db.CompanyInfos.Add(new CompanyInfo { CompanyId = 1, Web = "www.econopack.se" });
        AddMailTexts(db, "CustomerOrder", -74, -75,
            (-74, "SW", "Ordererkännande: %NR% / %ERTORDERNR% / %PRODUKTNAMN%", null),
            (-74, "EN", "Order confirmation: %NR% %ERTORDERNR% %PRODUKTNAMN%", null),
            (-75, "SW", null, "<p>Ordererkännande %NR% %LEVERANS%</p>%SIGNATURE%"),
            (-75, "EN", null, "<p>Ack %NR% %ERTORDERNR% %PRODUKTNAMN% %LEVERANS%</p>%SIGNATURE%"),
            (EmailTextBuilder.SignatureTranslationCode, "SW", null, "<b>%EMPLOYEENAME% %EMPLOYEETITLE%</b> %COMPANYWEB%"),
            (EmailTextBuilder.SignatureTranslationCode, "EN", null, "<b>%EMPLOYEENAME% %EMPLOYEETITLE%</b> %COMPANYWEB%"));
        await db.SaveChangesAsync();
        var user = User();
        user.Title = "Försäljningschef";
        user.TitleEnglish = "Sales manager";

        var result = await new EmailRecipientService(db).GetAsync("CUSTOMERORDER", 73, null, [], user, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(subject, result.Subject);
        Assert.Equal(body, result.Body);
    }

    [Fact]
    public async Task EmailTextUsesSupplierOrderTemplateNumberAndEmbedsLogo()
    {
        await using var db = CreateDb();
        db.Suppliers.Add(new Supplier { Id = 9, SupplierOrderTemplateNr = 1 });
        db.SupplierOrders.Add(new SupplierOrder { Id = 73, SupplierId = 9, CustomerOrderNr = "39697", Product = "Box" });
        AddMailTexts(db, "SupplierOrder", -72, -73,
            (-72, "SW", "Beställning %NR% / %PRODUKTNAMN%", null),
            (-73, "SW", null, "<p>Beställning %NR%</p>%LOGO%"));
        await db.SaveChangesAsync();

        var result = await new EmailRecipientService(db).GetAsync("SUPPLIERORDER", 73, null, [], User(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Beställning 73 / 39697 / Box", result.Subject);
        Assert.StartsWith("<p>Beställning 73 / 39697</p><img src=\"data:image/png;base64,", result.Body);
    }

    [Fact]
    public async Task SendEmbedsDataUriImagesAsInlineResources()
    {
        await using var db = CreateDb();
        await AddSettings(db);
        var transport = new FakeTransport();
        var image = new byte[] { 1, 2, 3 };
        var request = Request() with { Body = $"<p>Hi</p><img src=\"data:image/png;base64,{Convert.ToBase64String(image)}\" alt=\"logo\">" };

        await new EmailService(db, transport, NullLogger<EmailService>.Instance).SendAsync(request, User(), CancellationToken.None);

        Assert.Contains("<img src=\"cid:image1@econosys\" alt=\"logo\">", transport.HtmlView);
        Assert.DoesNotContain("data:image", transport.HtmlView);
        Assert.Equal(image, transport.InlineImageBytes);
    }

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

        await new EmailService(db, transport, NullLogger<EmailService>.Instance).SendAsync(request, User(2), CancellationToken.None);

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
            new LogEntry { Id = 5, Item = "CALLOFF", ItemId = 74, Action = "SENDMAIL", DateTime = new DateTime(2026, 7, 12, 8, 32, 0) },
            new LogEntry { Id = 6, Item = "CUSTOMERORDER", ItemId = 73, Action = "SENDMAIL", DateTime = new DateTime(2026, 7, 12, 8, 34, 0) });
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
        Assert.Single(await service.GetAsync(MailLogService.CustomerOrderItem, 73, CancellationToken.None));
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

        await new EmailService(db, transport, NullLogger<EmailService>.Instance).SendAsync(request, User(), CancellationToken.None);

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
        var service = new EmailService(db, transport, NullLogger<EmailService>.Instance);

        await Assert.ThrowsAsync<EmailValidationException>(() => service.SendAsync(
            Request() with { CallOffId = 73 }, User(), CancellationToken.None));
        await Assert.ThrowsAsync<EmailValidationException>(() => service.SendAsync(
            Request() with { CallOffId = 73, TransportOrderId = 73 }, User(), CancellationToken.None));
        Assert.Equal(0, transport.Calls);

        db.CallOffs.Add(new CallOff { Id = 73 });
        await db.SaveChangesAsync();
        transport.Failure = new SmtpException("Delivery failed");
        await Assert.ThrowsAsync<EmailSmtpFailureException>(() => service.SendAsync(
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

        var error = await Assert.ThrowsAsync<EmailValidationException>(() => new EmailService(db, transport, NullLogger<EmailService>.Instance)
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

        await new EmailService(db, transport, NullLogger<EmailService>.Instance).SendAsync(
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

    [Fact]
    public async Task SendLogsSmtpFailureWithoutSensitiveDetailsAndRethrows()
    {
        await using var db = CreateDb();
        await AddSettings(db);
        var logger = new RecordingEmailLogger();
        var failure = new SmtpException(SmtpStatusCode.GeneralFailure, "secret-value to@example.com");
        var transport = new FakeTransport { Failure = failure };

        var error = await Assert.ThrowsAsync<EmailSmtpFailureException>(() =>
            new EmailService(db, transport, logger).SendAsync(Request(), User(), CancellationToken.None));

        Assert.Same(failure, error.InnerException);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("delivery could not be confirmed", entry.Message);
        Assert.DoesNotContain("secret-value", entry.Message);
        Assert.DoesNotContain("to@example.com", entry.Message);
        Assert.DoesNotContain("Subject", entry.Message);
        Assert.Empty(await db.LogEntries.ToListAsync());
    }

    [Fact]
    public async Task SendLogsAcceptanceAndPreservesSentStatusWhenHistoryFails()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new FailingLogDb(options);
        await AddSettings(db);
        db.CallOffs.Add(new CallOff { Id = 73 });
        await db.SaveChangesAsync();
        db.FailLogSave = true;
        var logger = new RecordingEmailLogger();
        var transport = new FakeTransport();

        await Assert.ThrowsAsync<EmailLogPersistenceException>(() =>
            new EmailService(db, transport, logger).SendAsync(
                Request() with { CallOffId = 73 }, User(), CancellationToken.None));

        Assert.Equal(1, transport.Calls);
        Assert.Contains(logger.Entries, x => x.Level == LogLevel.Information && x.Message.Contains("SMTP accepted"));
        Assert.Contains(logger.Entries, x => x.Level == LogLevel.Error && x.Message.Contains("history could not be saved"));
    }

    [Fact]
    public async Task SendLogsCancellationDuringSmtpAsUnconfirmed()
    {
        await using var db = CreateDb();
        await AddSettings(db);
        using var cancellation = new CancellationTokenSource();
        var logger = new RecordingEmailLogger();
        var transport = new FakeTransport { Failure = new OperationCanceledException() };

        await Assert.ThrowsAsync<EmailDeliveryCancelledException>(() =>
            new EmailService(db, transport, logger).SendAsync(Request(), User(), cancellation.Token));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("delivery could not be confirmed", entry.Message);
        Assert.Contains("Client cancellation: False", entry.Message);
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

        await Assert.ThrowsAsync<EmailValidationException>(() => new EmailService(db, transport, NullLogger<EmailService>.Instance)
            .SendAsync(Request(recipients), User(), CancellationToken.None));
        Assert.Equal(0, transport.Calls);
    }

    [Fact]
    public async Task CopyRecipientAloneCannotSend()
    {
        await using var db = CreateDb();
        var transport = new FakeTransport();
        await Assert.ThrowsAsync<EmailValidationException>(() => new EmailService(db, transport, NullLogger<EmailService>.Instance)
            .SendAsync(Request("[]", SelectedRecipient), User(), CancellationToken.None));
        Assert.Equal(0, transport.Calls);
    }

    [Fact]
    public async Task InvalidSelectedCopyRecipientNeverSends()
    {
        await using var db = CreateDb();
        var transport = new FakeTransport();
        await Assert.ThrowsAsync<EmailValidationException>(() => new EmailService(db, transport, NullLogger<EmailService>.Instance)
            .SendAsync(Request(SelectedRecipient, "[{\"email\":\"invalid\",\"doSend\":true}]"), User(), CancellationToken.None));
        Assert.Equal(0, transport.Calls);
    }

    [Fact]
    public async Task MissingInvalidOrDuplicateSettingsAndSenderNeverSend()
    {
        await using var db = CreateDb();
        var transport = new FakeTransport();
        var service = new EmailService(db, transport, NullLogger<EmailService>.Instance);
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
    public async Task ControllerReturnsSafeSmtpStatusInErrorResponse()
    {
        await using var db = CreateDb();
        await AddSettings(db);
        var transport = new FakeTransport
        {
            Failure = new SmtpException(SmtpStatusCode.MailboxUnavailable, "secret-value to@example.com"),
        };

        var result = Assert.IsType<ObjectResult>(await Controller(db, transport, User()).Send(Request(), CancellationToken.None));
        var response = System.Text.Json.JsonSerializer.Serialize(result.Value);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Contains("\"errorCode\":\"SmtpFailure\"", response);
        Assert.Contains("\"smtpStatus\":\"MailboxUnavailable\"", response);
        Assert.Contains("\"smtpStatusCode\":550", response);
        Assert.Contains("MailboxUnavailable (550)", response);
        Assert.DoesNotContain("secret-value", response);
        Assert.DoesNotContain("to@example.com", response);
    }

    [Theory]
    [InlineData("554 5.7.1 Rejected due to policy secret-value", "5.7.1", "säkerhetsskäl")]
    [InlineData("554 5.2.2 Mailbox full secret-value", "5.2.2", "brevlåda är full")]
    [InlineData("554 Rejected secret-value", null, "ingen identifierbar utökad felkod")]
    public async Task ControllerReturnsSafeSmtpExplanation(string serverMessage, string? enhancedCode, string expectedDetail)
    {
        await using var db = CreateDb();
        await AddSettings(db);
        var transport = new FakeTransport
        {
            Failure = new SmtpException(SmtpStatusCode.TransactionFailed, serverMessage),
        };

        var result = Assert.IsType<ObjectResult>(await Controller(db, transport, User()).Send(Request(), CancellationToken.None));
        using var response = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(result.Value));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal(enhancedCode, response.RootElement.GetProperty("enhancedSmtpStatus").GetString());
        Assert.Contains(expectedDetail, response.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("secret-value", response.RootElement.GetRawText());
    }

    [Theory]
    [InlineData("noreply@example.com not allowed to send as sender@example.com", true)]
    [InlineData("noreply@example.com not allowed to send as someone@example.com", false)]
    [InlineData("secret-value@example.com not allowed to send as sender@example.com", false)]
    public async Task ControllerReportsSendAsDenialWithoutExposingSecrets(string serverMessage, bool showsAddresses)
    {
        await using var db = CreateDb();
        await AddSettings(db);
        var transport = new FakeTransport
        {
            Failure = new SmtpException(SmtpStatusCode.TransactionFailed, $"554 5.7.1 Denied; {serverMessage}; secret-value"),
        };

        var result = Assert.IsType<ObjectResult>(await Controller(db, transport, User()).Send(Request(), CancellationToken.None));
        using var response = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(result.Value));
        var detail = response.RootElement.GetProperty("detail").GetString()!;

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Contains("skicka som", detail);
        Assert.Equal(showsAddresses, detail.Contains("noreply@example.com"));
        if (showsAddresses) Assert.Contains("sender@example.com", detail);
        Assert.DoesNotContain("secret-value", response.RootElement.GetRawText());
        Assert.DoesNotContain("someone@example.com", response.RootElement.GetRawText());
    }

    [Fact]
    public async Task ControllerReturnsNetworkErrorCodeWithoutRawException()
    {
        await using var db = CreateDb();
        await AddSettings(db);
        var transport = new FakeTransport
        {
            Failure = new SmtpException("secret-value", new SocketException((int)SocketError.ConnectionRefused)),
        };

        var result = Assert.IsType<ObjectResult>(await Controller(db, transport, User()).Send(Request(), CancellationToken.None));
        var response = System.Text.Json.JsonSerializer.Serialize(result.Value);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Contains("\"networkError\":\"ConnectionRefused\"", response);
        Assert.Contains("ConnectionRefused", response);
        Assert.DoesNotContain("secret-value", response);
    }

    [Fact]
    public async Task ControllerDistinguishesSmtpTimeoutFromClientCancellation()
    {
        await using var db = CreateDb();
        await AddSettings(db);
        var transport = new FakeTransport { Failure = new OperationCanceledException("secret-value") };
        var controller = Controller(db, transport, User());

        var result = Assert.IsType<ObjectResult>(await controller.Send(Request(), CancellationToken.None));
        var response = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Contains("\"errorCode\":\"SmtpCancelled\"", response);
        Assert.Contains("Kontrollera innan", response);
        Assert.DoesNotContain("secret-value", response);

        using var cancellation = new CancellationTokenSource();
        transport.BeforeFailure = cancellation.Cancel;
        await Assert.ThrowsAsync<OperationCanceledException>(() => controller.Send(Request(), cancellation.Token));
    }

    [Fact]
    public async Task MultipartEndpointBindsModalRequestAndFiles()
    {
        await using var db = CreateDb();
        await AddSettings(db);
        db.CallOffs.Add(new CallOff { Id = 3158 });
        db.LegacyUsers.Add(User());
        db.LogEntries.Add(new LogEntry
        {
            Item = "CUSTOMERORDER",
            ItemId = 73,
            Action = "SENDMAIL",
            DateTime = new DateTime(2026, 7, 12, 8, 34, 0),
            Info1 = "customer@example.com",
            Info2 = "Order confirmation",
        });
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

            using var customerOrderHistoryResponse = await client.GetAsync("/api/email/log/customerorder/73");
            Assert.Equal(HttpStatusCode.OK, customerOrderHistoryResponse.StatusCode);
            Assert.Contains("\"subject\":\"Order confirmation\"", await customerOrderHistoryResponse.Content.ReadAsStringAsync());

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

    [Fact]
    public async Task SmtpServerRejectionReturnsEnhancedCodeAndExplanation()
    {
        await using var db = CreateDb();
        await AddSettings(db);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        (await db.MailSettings.SingleAsync(x => x.Setting == " SMTPServer ")).Value = "127.0.0.1";
        (await db.MailSettings.SingleAsync(x => x.Setting == "smtpserverport")).Value = port.ToString();
        (await db.MailSettings.SingleAsync(x => x.Setting == "SMTPUseSSL")).Value = "false";
        await db.SaveChangesAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var receive = ReceiveSmtpMessage(listener, timeout.Token,
            "554 5.7.1 Denied; noreply@example.com not allowed to send as sender@example.com;");

        var result = Assert.IsType<ObjectResult>(await Controller(db, new SmtpEmailTransport(), User()).Send(Request(), timeout.Token));
        await receive;
        using var response = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(result.Value));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal(554, response.RootElement.GetProperty("smtpStatusCode").GetInt32());
        Assert.Equal("5.7.1", response.RootElement.GetProperty("enhancedSmtpStatus").GetString());
        Assert.Contains("säkerhetsskäl", response.RootElement.GetProperty("detail").GetString());
        Assert.Contains("noreply@example.com", response.RootElement.GetProperty("detail").GetString());
        Assert.Contains("sender@example.com", response.RootElement.GetProperty("detail").GetString());
    }

    private static async Task<string> ReceiveSmtpMessage(TcpListener listener, CancellationToken cancellationToken, string? dataResponse = null)
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
                await writer.WriteLineAsync(dataResponse ?? "250 accepted");
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

    private static EmailController Controller(ApplicationDbContext db, IEmailTransport transport, LegacyUser? user) =>
        new(new FakeUserResolution(user), new EmailService(db, transport, NullLogger<EmailService>.Instance), NullLogger<EmailController>.Instance)
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

    private sealed class RecordingEmailLogger : ILogger<EmailService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
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
        public string? HtmlView { get; private set; }
        public byte[]? InlineImageBytes { get; private set; }
        public Exception? Failure { get; set; }
        public Action? BeforeFailure { get; set; }
        public Action? OnSent { get; set; }

        public async Task SendAsync(MailMessage message, SmtpDeliverySettings settings, CancellationToken cancellationToken)
        {
            Calls++;
            BeforeFailure?.Invoke();
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
            if (message.AlternateViews.Count > 0)
            {
                var view = message.AlternateViews[0];
                using (var reader = new StreamReader(view.ContentStream, leaveOpen: true)) HtmlView = await reader.ReadToEndAsync(cancellationToken);
                using var image = new MemoryStream();
                await view.LinkedResources[0].ContentStream.CopyToAsync(image, cancellationToken);
                InlineImageBytes = image.ToArray();
            }
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