using System.Text;
using System.Xml.Linq;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using QuestPDF.Infrastructure;
using QuestPDF.Fluent;
using Xunit;

namespace Econosys.Api.Tests;

public class QuotationPdfTests
{
    public QuotationPdfTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = false;
    }

    [Theory]
    [InlineData(null, "Kund")]
    [InlineData(" en ", "Customer")]
    [InlineData("SW", "Kund")]
    [InlineData("unknown-language", "Kund")]
    public void LanguageDefaultsAreSafe(string? language, string expected)
    {
        Assert.Equal(expected, new DocumentPdfTranslations(language).Text(-90));
    }

    [Fact]
    public void TranslationsAreNormalizedAndDeterministic()
    {
        var translations = new DocumentPdfTranslations(" en ", new[]
        {
            new TranslationItem { Id = 2, TranslationCode = -90, LangCode = "EN", Translation = "Later" },
            new TranslationItem { Id = 1, TranslationCode = -90, LangCode = " en ", Translation = "Recipient" },
            new TranslationItem { Id = 3, TranslationCode = 200, LangCode = "SV", Translation = "Fallback" },
            new TranslationItem { Id = 4, TranslationCode = -93, LangCode = "EN", Translation = "QuotationValidityText" }
        });
        Assert.Equal("Recipient", translations.Text(-90));
        Assert.Equal("Fallback", translations.Text(200, "Master"));
        Assert.Equal("Master", translations.Text(201, "Master"));
        Assert.Equal("", translations.Text(-93));
    }

    [Fact]
    public void InvoiceStillRendersWithSharedLayout()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var bytes = InvoicePdfDocument.Generate(new Invoice
        {
            Id = 1,
            InvoiceNumber = 123,
            CustomerName = "Customer",
            InvoiceDate = new DateTime(2026, 11, 9)
        }, new CompanyInfo { CompanyName = "Econopack" });

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        Assert.True(bytes.Length > 1000);
    }

    [Theory]
    [InlineData("SV")]
    [InlineData("EN")]
    public void QuotationRendersOnePage(string language)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var quotation = Fixture();
        var document = QuotationPdfDocument.CreateDocument(quotation, new CompanyInfo
        {
            CompanyName = "Econopack", Address = "Jolengatan 19", ZipCode = "431 49", PostalAddress = "Molndal",
            Email = "info@econopack.se", Web = "www.econopack.se"
        }, new DocumentPdfTranslations(language));

        Assert.Single(document.GenerateImages());
        var bytes = document.GeneratePdf();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        var artifactDirectory = Environment.GetEnvironmentVariable("ECONOSYS_PDF_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(artifactDirectory))
        {
            Directory.CreateDirectory(artifactDirectory);
            File.WriteAllBytes(Path.Combine(artifactDirectory, $"quotation-{language}.pdf"), bytes);
            File.WriteAllBytes(Path.Combine(artifactDirectory, $"quotation-{language}.png"), document.GenerateImages().Single());
            File.WriteAllText(Path.Combine(artifactDirectory, $"quotation-{language}.svg"), document.GenerateSvg().Single());
        }
    }

    [Fact]
    public void SavedPricesAndCalculationInfoArePrintedWithoutRecalculation()
    {
        var quotation = Fixture();
        var content = QuotationPdfDocument.BuildContent(quotation, new DocumentPdfTranslations("EN"));
        Assert.Equal("SEK/1000pcs", content.PriceHeading);
        Assert.Equal("7,230", content.Prices[0].Price);
        Assert.Equal("2+1 colours", content.Prices[0].Note);
        Assert.Equal("6,400", content.Prices[0].Quantity);
    }

    [Fact]
    public void LongContentPaginates()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var quotation = Fixture();
        quotation.ProductMessage = string.Join("\n", Enumerable.Repeat("A long product note that must wrap without clipping.", 65));
        quotation.QuotationRows = Enumerable.Range(1, 100).Select(id => new QuotationRow
        {
            Id = id, Edition = id * 1000, Price = 123.45,
            CalculationRow = new CalculationRow { Info = "2+1 colours, detailed specification" }
        }).ToList();
        var pages = QuotationPdfDocument.CreateDocument(quotation, null, new DocumentPdfTranslations("SV")).GenerateImages().ToList();
        Assert.InRange(pages.Count, 2, 15);
    }

    [Fact]
    public void CostsRespectPrintFlagsAndKnownPricesIncludingZero()
    {
        var quotation = Fixture();
        var costType = new Cost { Name = "Tooling", TranslationCodeQuotationKnown = 501, TranslationCodeQuotationUnknown = 502 };
        quotation.OrderCosts = new List<OrderCost>
        {
            new() { Id = 1, DoDebit = true, DoPrintOnQuotation = true, Cost = costType, OutPrice = 4503, CalculationRowId = 50 },
            new() { Id = 2, DoDebit = true, DoPrintOnQuotation = true, Cost = costType, OutPrice = 0 },
            new() { Id = 3, DoDebit = true, DoPrintOnQuotation = true, Cost = costType },
            new() { Id = 4, DoDebit = false, DoPrintOnQuotation = true, Cost = costType, OutPrice = 100 },
            new() { Id = 5, DoDebit = true, DoPrintOnQuotation = false, Cost = costType, OutPrice = 100 },
            new() { Id = 6, DoDebit = true, DoPrintOnQuotation = true, SupplierOrderId = 20, Cost = costType, OutPrice = 100 }
        };
        var texts = new DocumentPdfTranslations("EN", new[]
        {
            new TranslationItem { TranslationCode = 501, LangCode = "EN", Translation = "Known tooling" },
            new TranslationItem { TranslationCode = 502, LangCode = "EN", Translation = "Tooling will be added" }
        });
        var costs = QuotationPdfDocument.BuildContent(quotation, texts).Costs;
        Assert.Equal(3, costs.Count);
        Assert.Contains(costs, cost => cost.Description == "Known tooling" && cost.Amount == "0.00 SEK");
        Assert.Contains(costs, cost => cost.Description == "Tooling will be added" && cost.Amount == "");
        Assert.Contains(costs, cost => cost.Amount == "4,503.00 SEK" && cost.EditionInfo.Contains("6,400"));
    }

    [Fact]
    public void PalletUnknownPriceAndQuantityCostsAreNotInvented()
    {
        var quotation = Fixture();
        quotation.EurPallet = true;
        quotation.EurPalletValue = 50;
        quotation.OrderCosts.Add(new OrderCost
        {
            Id = 1, DoDebit = true, DoPrintOnQuotation = true, OutPrice = 20, NrOf = 3,
            Cost = new Cost { Name = "Plates", IsNrOf = true }
        });
        var costs = QuotationPdfDocument.BuildContent(quotation, new DocumentPdfTranslations("EN")).Costs;
        Assert.Equal("", costs[0].Amount);
        Assert.Contains("will be added", costs[0].Description);
        Assert.Contains("60.00 SEK", costs[1].Amount);
        quotation.NrOfEurPallet = 2;
        Assert.Equal("100.00 SEK", QuotationPdfDocument.BuildContent(quotation, new DocumentPdfTranslations("EN")).Costs[0].Amount);
    }

    [Fact]
    public async Task PdfEndpointAndTranslationSettingsAreReadOnly()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        await using var db = CreateContext();
        var quotation = Fixture();
        quotation.Customer = new Customer { Id = 20, Name = "Live name", Language = new Language { Id = 2, LanguageCode = "EN" } };
        db.Quotations.Add(quotation);
        db.TranslationItems.Add(new TranslationItem
        {
            TranslationCode = DocumentPdfTranslations.ToolingDisclaimer, LangCode = "EN", Translation = "Custom tooling text"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var pdfController = new PdfController(db);
        var result = Assert.IsType<FileContentResult>(await pdfController.GetQuotationPdfById(quotation.Id));
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("quotation_25691.pdf", result.FileDownloadName);
        Assert.True(result.EnableRangeProcessing);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.IsType<NotFoundObjectResult>(await pdfController.GetQuotationPdfById(-1));

        var settingsController = new TranslationItemsController(db, NullLogger<TranslationItemsController>.Instance);
        var settings = Assert.IsType<TranslationItemsController.DocumentTextSettingsResponse>(
            Assert.IsType<OkObjectResult>((await settingsController.GetDocumentTexts(default)).Result).Value);
        Assert.Contains("EN", settings.Languages);
        Assert.Contains(settings.Definitions, definition => definition.Code == DocumentPdfTranslations.ToolingDisclaimer);
        Assert.Equal("Custom tooling text", Assert.Single(settings.Items).Translation);
        Assert.Equal(1, await db.TranslationItems.CountAsync());
        Assert.False((await db.Quotations.SingleAsync()).Printed);
    }

    [Fact]
    public async Task SavedStandardTextChangesAreLoadedOnNextRead()
    {
        await using var db = CreateContext();
        var controller = new TranslationItemsController(db, NullLogger<TranslationItemsController>.Instance);
        var request = new BatchUpsertTranslationItemsRequest
        {
            Items = new List<BatchUpsertTranslationItemRequestItem>
            {
                new() { TranslationCode = DocumentPdfTranslations.ToolingDisclaimer, LangCode = "SW", Translation = "Saved Swedish text" },
                new() { TranslationCode = DocumentPdfTranslations.PaymentDisclaimer, LangCode = "EN", Translation = "Saved English text" }
            }
        };
        Assert.IsType<OkObjectResult>((await controller.BatchUpsert(request)).Result);
        var texts = new DocumentPdfTranslations("SW", await db.TranslationItems.AsNoTracking().ToListAsync());
        Assert.Equal("Saved Swedish text", texts.Text(DocumentPdfTranslations.ToolingDisclaimer));
        request.Items[0].Translation = "Updated Swedish text";
        await controller.BatchUpsert(request);
        texts = new DocumentPdfTranslations("SW", await db.TranslationItems.AsNoTracking().ToListAsync());
        Assert.Equal("Updated Swedish text", texts.Text(DocumentPdfTranslations.ToolingDisclaimer));
        Assert.Equal(2, await db.TranslationItems.CountAsync());
    }

    [Fact]
    public void MissingPricesStayBlankAndPrecisionUsesQuotationCalculation()
    {
        var quotation = Fixture();
        quotation.Calculation = new Calculation { NrOfCalcDecimalsSales = 3 };
        quotation.QuotationRows.First().Price = 12.3456;
        quotation.QuotationRows.Last().Price = null;
        var content = QuotationPdfDocument.BuildContent(quotation, new DocumentPdfTranslations("EN"));
        Assert.Equal("12.346", content.Prices[0].Price);
        Assert.Equal("", content.Prices[1].Price);
        Assert.Equal("", content.Prices[1].Note);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RenderedTextUsesSnapshotsAndProtectsInternalInformation(bool hideDelivery)
    {
        var quotation = Fixture();
        quotation.HideDeliveryAddressOnPrint = hideDelivery;
        quotation.DeliveryAddressName = "UNIQUE DELIVERY ADDRESS";
        quotation.Customer = new Customer { Name = "LIVE CUSTOMER NOT SNAPSHOT" };
        quotation.IsFsc = true;
        quotation.NoteInternal = "SECRET QUOTATION NOTE";
        quotation.Message = "External customer message";
        quotation.OrderCosts.Add(new OrderCost
        {
            Id = 1, DoDebit = true, DoPrintOnQuotation = true, OutPrice = 4503,
            SupplierName = "SECRET SUPPLIER", Note = "SECRET COST NOTE",
            Cost = new Cost { Name = "Tooling" }
        });
        var texts = new DocumentPdfTranslations("EN", new[]
        {
            new TranslationItem { TranslationCode = DocumentPdfTranslations.ToolingDisclaimer,
                LangCode = "EN", Translation = "CUSTOM TOOLING DISCLAIMER" }
        });
        var svg = QuotationPdfDocument.CreateDocument(quotation, null, texts).GenerateSvg().Single();
        var renderedText = string.Join(" ", XDocument.Parse(svg).Descendants()
            .Where(element => element.Name.LocalName == "text").Select(element => element.Value));
        Assert.Contains("AB Tingstad Papper", renderedText);
        Assert.Contains("Quotation", renderedText);
        Assert.Contains("CUSTOM TOOLING DISCLAIMER", renderedText);
        Assert.Contains("External customer message", renderedText);
        Assert.Contains("2+1 colours", renderedText);
        Assert.Contains("FSC certified", renderedText);
        Assert.Equal(!hideDelivery, renderedText.Contains("UNIQUE DELIVERY ADDRESS"));
        Assert.DoesNotContain("SECRET", renderedText);
        Assert.DoesNotContain("LIVE CUSTOMER", renderedText);
    }

    [Fact]
    public void NonInvoiceFooterMatchesTwoContactLinesAndExcludesBankDetails()
    {
        var company = new CompanyInfo
        {
            CompanyName = "Econopack", Address = "Jolengatan 19", ZipCode = "431 49",
            PostalAddress = "M\u00f6lndal", Telephone1 = "0313633502", Telephone2 = "+46 (0)31 363 35 02",
            Email = "info@econopack.se", Web = "www.econopack.se",
            Bank = "INVOICE BANK", BG = "INVOICE GIRO", VATNr = "INVOICE VAT", IBAN = "INVOICE IBAN"
        };
        var lines = DocumentPdfLayout.ContactLines(company);
        Assert.Equal("Jolengatan 19, 431 49 M\u00f6lndal \u2022 Sverige \u2022 T +46 (0)31 363 35 02", lines.Address);
        Assert.Equal("\u2022 info@econopack.se \u2022 www.econopack.se", lines.Contact);
        var document = QuotationPdfDocument.CreateDocument(Fixture(), company, new DocumentPdfTranslations("SW"));
        Assert.Single(document.GenerateImages());
        var rendered = string.Join(" ", XDocument.Parse(document.GenerateSvg().Single()).Descendants()
            .Where(element => element.Name.LocalName == "text").Select(element => element.Value));
        Assert.Contains(lines.Address, rendered);
        Assert.Contains(lines.Contact, rendered);
        Assert.DoesNotContain("INVOICE", rendered);
        Assert.DoesNotContain("F-skatt", rendered);
        Assert.Contains("Sweden", DocumentPdfLayout.ContactLines(company, new DocumentPdfTranslations("EN").Text).Address);
        var reportFooter = DocumentPdfLayout.ContactReportFooter(company);
        var reportText = Assert.IsType<Telerik.Reporting.TextBox>(Assert.Single(reportFooter.Items));
        Assert.Equal(lines.Address + "\n" + lines.Contact, reportText.Value);
        Assert.True(reportText.CanGrow);
    }

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private static Quotation Fixture() => new()
    {
        Id = 25691, Date = new DateTime(2026, 5, 22), CustomerName = "AB Tingstad Papper",
        Address = "Box 13013", PostalNr = "40251", PostalAddress = "Goteborg", Country = "Sverige",
        DeliveryAddressName = "AB Tingstad Papper", DeliveryAddress = "Box 13013",
        DeliveryPostalNr = "40251", DeliveryPostalAddress = "Goteborg", DeliveryCountry = "Sverige",
        OurReference = "Petra Toll", YourReference = "Johan Larsson", Product = "1968993 - Vanster",
        Format = "123x45x67mm", Construction = "O201, Slitslada", Material = "Vit/Brun, B-flute",
        ProductMessage = "1200x800x1300mm 680st/pall\nA longer note that wraps naturally within the product column.",
        TimeOfDelivery = "2026-05-02", TermsOfDelivery = "DAP", TermsOfPayment = "30 dagar fran fakturadatum",
        SalesCurrency = new Currency { Name = "SEK" }, Unit = new Models.Unit { Name = "1000pcs", NrOfCalcDecimals = 0 },
        QuotationRows = new List<QuotationRow>
        {
            new() { Id = 1, Edition = 6400, Price = 7230, CalculationRowId = 50,
                CalculationRow = new CalculationRow { Info = "2+1 colours", SalesPrice = 99999 } },
            new() { Id = 2, Edition = 12800, Price = 4203 }
        }
    };
}