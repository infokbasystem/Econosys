using System.Text;
using System.Xml.Linq;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xunit;

namespace Econosys.Api.Tests;

public class InquiryPdfTests
{
    public InquiryPdfTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = false;
    }

    [Theory]
    [InlineData("SW", "F\u00f6rfr\u00e5gan")]
    [InlineData("EN", "Inquiry")]
    public void InquiryMatchesReferenceAndRendersOnePage(string language, string title)
    {
        var inquiry = Fixture();
        var document = InquiryPdfDocument.CreateDocument(inquiry, Company(), new DocumentPdfTranslations(language));
        Assert.Single(document.GenerateImages());
        var bytes = document.GeneratePdf();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        var text = RenderedText(document);
        Assert.Contains(title, text);
        Assert.Contains("2+1 colours", text);
        Assert.Contains("1200x800x1300mm", text);
        Assert.Contains("External product note", text);
        Assert.Contains("info@econopack.se", text);
        Assert.Contains("?", text);
        Assert.DoesNotContain("98765", text);
        Assert.DoesNotContain("45678", text);
        Assert.DoesNotContain("SECRET", text);
        var directory = Environment.GetEnvironmentVariable("ECONOSYS_INQUIRY_PDF_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, $"inquiry-{language}.pdf"), bytes);
            File.WriteAllBytes(Path.Combine(directory, $"inquiry-{language}.png"), document.GenerateImages().Single());
        }
    }

    [Fact]
    public void RetainedRowsUseSavedQuantityAndExactCalculationRowInfo()
    {
        var inquiry = Fixture();
        var content = InquiryPdfDocument.BuildContent(inquiry, new DocumentPdfTranslations("EN"));
        Assert.Equal("EUR/1000pcs", content.PriceHeading);
        Assert.Equal(2, content.Lines.Count);
        Assert.Equal("6,400", content.Lines[0].Quantity);
        Assert.Equal("2+1 colours", content.Lines[0].Info);
        Assert.Equal("2+2 colours", content.Lines[1].Info);
    }

    [Fact]
    public void RecipientLanguageOverridesSupplierAndCustomer()
    {
        var inquiry = Fixture();
        inquiry.Customer!.Language = new Language { LanguageCode = "DE" };
        inquiry.SelectedInquiryRecipient!.Supplier = new Supplier { Language = new Language { LanguageCode = "SW" } };
        Assert.Equal("EN", InquiryPdfDocument.RecipientLanguageCode(inquiry));
        inquiry.SelectedInquiryRecipient.Language = null;
        Assert.Equal("SW", InquiryPdfDocument.RecipientLanguageCode(inquiry));
        inquiry.SelectedInquiryRecipient.Language = new Language { LanguageCode = " " };
        Assert.Equal("SW", InquiryPdfDocument.RecipientLanguageCode(inquiry));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HideCustomerSettingProtectsBothCustomerAddressBlocks(bool hideCustomer)
    {
        var inquiry = Fixture();
        inquiry.HideCustomerInfoOnPrint = hideCustomer;
        inquiry.CustomerName = "PRIVATE CUSTOMER";
        inquiry.DeliveryAddressName = "PRIVATE DELIVERY";
        var text = RenderedText(InquiryPdfDocument.CreateDocument(inquiry, Company(), new DocumentPdfTranslations("EN")));
        Assert.Equal(!hideCustomer, text.Contains("PRIVATE CUSTOMER"));
        Assert.Equal(!hideCustomer, text.Contains("PRIVATE DELIVERY"));
        Assert.Equal(!hideCustomer, text.Contains("Customer street"));
        Assert.Contains("Supplier reference", text);
        Assert.Contains("Recipient delivery terms", text);
        Assert.Contains("Recipient payment terms", text);
        Assert.DoesNotContain("SECRET", text);
    }

    [Fact]
    public void EmptyInquiryAndMissingLinksStillRenderSafely()
    {
        var inquiry = new Inquiry { Id = 1, InquiryRows = new List<InquiryRow> { new() { Id = 1, Edition = 1000 } } };
        var content = InquiryPdfDocument.BuildContent(inquiry, new DocumentPdfTranslations(null));
        Assert.Equal("", content.Lines[0].Info);
        Assert.Single(InquiryPdfDocument.CreateDocument(inquiry, null, new DocumentPdfTranslations(null)).GenerateImages());
    }

    [Fact]
    public void LongProductNotesAndManyRowsPaginate()
    {
        var inquiry = Fixture();
        inquiry.Calculation!.Product!.NoteExternal = string.Join("\n", Enumerable.Repeat("A long external product note that must wrap naturally.", 60));
        inquiry.InquiryRows = Enumerable.Range(1, 100).Select(id => new InquiryRow
        {
            Id = id, Edition = id * 1000,
            CalculationRow = new CalculationRow { Info = "A long row specification, 2+1 colours" }
        }).ToList();
        var pages = InquiryPdfDocument.CreateDocument(inquiry, Company(), new DocumentPdfTranslations("EN")).GenerateImages().ToList();
        Assert.InRange(pages.Count, 2, 15);
    }

    [Fact]
    public async Task ExistingEndpointGeneratesBrandedPdfWithoutTrackingOrSaving()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var inquiry = Fixture();
        inquiry.SelectedInquiryRecipient!.Language = new Language { Id = 2, LanguageCode = "EN" };
        inquiry.SelectedInquiryRecipient.Supplier = new Supplier
        {
            Name = "Supplier", Language = new Language { Id = 1, LanguageCode = "SW" }
        };
        db.Inquiries.Add(inquiry);
        db.CompanyInfos.Add(Company());
        db.TranslationItems.Add(new TranslationItem { TranslationCode = -42, LangCode = "EN", Translation = "Supplier inquiry" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var controller = new PdfController(db);
        var file = Assert.IsType<FileContentResult>(await controller.GetInquiryPdfById(inquiry.Id));
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("inquiry_25691.pdf", file.FileDownloadName);
        Assert.True(file.EnableRangeProcessing);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(file.FileContents, 0, 5));
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.IsType<NotFoundObjectResult>(await controller.GetInquiryPdfById(-1));
        Assert.Equal(2, await db.InquiryRows.CountAsync());
        Assert.Equal(new DateTime(2026, 5, 22), (await db.Inquiries.SingleAsync()).CreatedAt);
        Assert.Equal(1, await db.TranslationItems.CountAsync());
    }

    [Fact]
    public void PackingTextAlreadyInProductNoteIsNotDuplicated()
    {
        var inquiry = Fixture();
        inquiry.Calculation!.Product!.NoteExternal = "1200x800x1300mm 680st/pall";
        Assert.Equal("1200x800x1300mm 680st/pall",
            InquiryPdfDocument.BuildContent(inquiry, new DocumentPdfTranslations("SW")).ProductNote);
    }

    private static string RenderedText(IDocument document) =>
        string.Join(" ", document.GenerateSvg().SelectMany(svg => XDocument.Parse(svg).Descendants())
            .Where(element => element.Name.LocalName == "text").Select(element => element.Value));

    private static CompanyInfo Company() => new()
    {
        CompanyId = 1, CompanyName = "Econopack", Address = "Jolengatan 19", ZipCode = "431 49", PostalAddress = "M\u00f6lndal",
        Telephone2 = "+46 (0)31 363 35 02", Email = "info@econopack.se", Web = "www.econopack.se", Bank = "SECRET BANK"
    };

    private static Inquiry Fixture() => new()
    {
        Id = 25691, CompanyId = 1, CreatedAt = new DateTime(2026, 5, 22), AnswerDueDate = new DateTime(2026, 5, 29),
        CustomerName = "AB Tingstad Papper", Customer = new Customer
        {
            Name = "SECRET LIVE NAME", Address = "Customer street", PostalNr = "40251", PostalAddress = "G\u00f6teborg", Country = "Sverige"
        },
        DeliveryAddressName = "AB Tingstad Papper", DeliveryAddress = "Box 13013", DeliveryPostalNr = "40251",
        DeliveryPostalAddress = "G\u00f6teborg", DeliveryCountry = "Sverige", OurRef = "Petra Toll",
        Product = "1968993 - V\u00e4nster", Format = "123x45x67mm", Construction = "O201, Slitsl\u00e5da",
        Material = "Vit/Brun, B-flute", Color = "2", TimeOfDelivery = "2026-05-02",
        SelectedInquiryRecipient = new InquiryRecipient
        {
            Id = 10, YourRef = "Supplier reference", TermsOfDelivery = "Recipient delivery terms",
            TermsOfPayment = "Recipient payment terms", Note = "External recipient note",
            Language = new Language { LanguageCode = "EN" }
        },
        Calculation = new Calculation
        {
            PurchaseCurrency = new Currency { Name = "EUR" }, SalesCurrency = new Currency { Name = "SECRET SALES CURRENCY" },
            Unit = new Models.Unit { Name = "1000pcs", Multiplicator = 1000 },
            Product = new Product { NoteExternal = "External product note that wraps naturally within the product column." },
            PalletLength = 1200, PalletWidth = 800, PalletHeight = 1300, EditionPerPallet = 680
        },
        InquiryRows = new List<InquiryRow>
        {
            new() { Id = 2, CalculationRowId = 12, Edition = 6400, CalculationRow = new CalculationRow { Id = 12, Edition = 9900, Info = "2+2 colours", PurchasePrice = 98765 } },
            new() { Id = 1, CalculationRowId = 11, Edition = 6400, CalculationRow = new CalculationRow { Id = 11, Edition = 9900, Info = "2+1 colours", SalesPrice = 45678 } }
        }
    };
}