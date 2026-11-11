using Econosys.Api.Models;
using Econosys.Api.Services;
using System.Text;
using System.Xml.Linq;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Econosys.Api.Tests;

public class CustomerOrderPdfTests
{
    public CustomerOrderPdfTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = false;
    }

    [Fact]
    public void PricingUsesSavedCustomerValuesAndSalesPrecision()
    {
        var order = Fixture();
        order.SelectedCalculationRow = new CalculationRow
        {
            Edition = 999999, SalesPrice = 888888, Calculation = new Calculation { NrOfCalcDecimalsSales = 3 }
        };
        var content = CustomerOrderPdfDocument.BuildContent(order, new DocumentPdfTranslations("EN"));
        Assert.Equal("SEK/1000pcs", content.PriceHeading);
        Assert.Equal("6,400", content.Quantity);
        Assert.Equal("7,230.000", content.Price);
        order.SalesPrice = null;
        Assert.Equal("", CustomerOrderPdfDocument.BuildContent(order, new DocumentPdfTranslations("EN")).Price);
    }

    [Fact]
    public void SpecialUnitCustomersUsePerPiecePrice()
    {
        var order = Fixture();
        order.Customer = new Customer { SpecialUnitHandling = true };
        var content = CustomerOrderPdfDocument.BuildContent(order, new DocumentPdfTranslations("EN"));
        Assert.Equal("SEK/pcs", content.PriceHeading);
        Assert.Equal("7.230", content.Price);
    }

    [Fact]
    public void CostsUseCustomerPricesAndSuppressDmtAmounts()
    {
        var order = Fixture();
        var type = new Cost { Name = "Tooling", TranslationCodeCustomerOrderKnown = 501, TranslationCodeCustomerOrderUnknown = 502 };
        order.OrderCosts = new List<OrderCost>
        {
            new() { Id = 1, Cost = type, DoDebit = true, DoPrintOnCustomerOrder = true, OutPrice = 4503, InPrice = 999999 },
            new() { Id = 2, Cost = type, DoDebit = true, DoPrintOnCustomerOrder = true, OutPrice = 0 },
            new() { Id = 3, Cost = type, DoDebit = true, DoPrintOnCustomerOrder = true },
            new() { Id = 4, Cost = new Cost { Name = "DMT", DmtPercent = 5 }, DoDebit = true, DoPrintOnCustomerOrder = true, OutPrice = 666666 },
            new() { Id = 5, Cost = type, DoDebit = false, DoPrintOnCustomerOrder = true, OutPrice = 100 },
            new() { Id = 6, Cost = type, DoDebit = true, DoPrintOnCustomerOrder = false, OutPrice = 100 }
        };
        var texts = new DocumentPdfTranslations("EN", new[]
        {
            new TranslationItem { TranslationCode = 501, LangCode = "EN", Translation = "Known tooling" },
            new TranslationItem { TranslationCode = 502, LangCode = "EN", Translation = "Tooling will be added" }
        });
        var costs = CustomerOrderPdfDocument.BuildContent(order, texts).Costs;
        Assert.Equal(4, costs.Count);
        Assert.Equal("4,503.00 SEK", costs[0].Amount);
        Assert.Equal("Known tooling", costs[0].Description);
        Assert.Equal("0.00 SEK", costs[1].Amount);
        Assert.Equal("", costs[2].Amount);
        Assert.Equal("Tooling will be added", costs[2].Description);
        Assert.Equal("", costs[3].Amount);
    }

    [Fact]
    public void PalletsAndWeekDeliveryRespectSavedValues()
    {
        var order = Fixture();
        order.EurPallet = true;
        order.EurPalletValue = 50;
        var texts = new DocumentPdfTranslations("EN");
        Assert.Equal("", CustomerOrderPdfDocument.BuildContent(order, texts).Costs[0].Amount);
        order.NrOfEurPallet = 2;
        Assert.Equal("100.00 SEK", CustomerOrderPdfDocument.BuildContent(order, texts).Costs[0].Amount);
        order.DeliveryDate = new DateTime(2027, 1, 1);
        order.DeliveryDateWeekMode = true;
        order.IsCallOff = true;
        Assert.Equal("2026 week 53 for call-off", CustomerOrderPdfDocument.BuildContent(order, texts).Delivery);
    }

    [Theory]
    [InlineData("SW", "Ordererk\u00e4nnande")]
    [InlineData("EN", "Order acknowledgement")]
    public void CustomerOrderMatchesReferenceAndRendersOnePage(string language, string title)
    {
        var document = CustomerOrderPdfDocument.CreateDocument(Fixture(), Company(), new DocumentPdfTranslations(language));
        Assert.Single(document.GenerateImages());
        var bytes = document.GeneratePdf();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        var text = RenderedText(document);
        Assert.Contains(title, text);
        Assert.Contains("43625", text);
        Assert.Contains("158964", text);
        Assert.Contains("1200x800x1300mm 680st/pall", text);
        Assert.Contains("External customer message", text);
        Assert.Contains("info@econopack.se", text);
        Assert.DoesNotContain("SECRET", text);
        var directory = Environment.GetEnvironmentVariable("ECONOSYS_CUSTOMER_PDF_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, $"customerorder-{language}.pdf"), bytes);
            File.WriteAllBytes(Path.Combine(directory, $"customerorder-{language}.png"), document.GenerateImages().Single());
        }
    }

    [Fact]
    public void LongProductNotesAndCostListsPaginate()
    {
        var order = Fixture();
        order.ProductMessage = string.Join("\n", Enumerable.Repeat("A long external product note that must wrap naturally.", 50));
        order.OrderCosts = Enumerable.Range(1, 80).Select(id => new OrderCost
        {
            Id = id, DoDebit = true, DoPrintOnCustomerOrder = true, OutPrice = 25,
            Cost = new Cost { Name = "Additional tooling cost" }
        }).ToList();
        var pages = CustomerOrderPdfDocument.CreateDocument(order, Company(), new DocumentPdfTranslations("EN")).GenerateImages().ToList();
        Assert.InRange(pages.Count, 2, 15);
    }

    [Fact]
    public async Task PdfEndpointLoadsSavedCustomerDataWithoutPersistingChanges()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var order = Fixture();
        order.Customer = new Customer { Name = "SECRET LIVE CUSTOMER", Language = new Language { LanguageCode = "EN" } };
        order.SelectedCalculationRow = new CalculationRow { Calculation = new Calculation { NrOfCalcDecimalsSales = 3 } };
        order.PalletFormatId = 10;
        db.CustomerOrders.Add(order);
        db.PalletFormats.Add(new PalletFormat { Id = 10, Name = "EUR-pall", TranslationCode = 502 });
        db.CompanyInfos.Add(Company());
        db.TranslationItems.Add(new TranslationItem { TranslationCode = 502, LangCode = "EN", Translation = "EUR pallet" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var controller = new PdfController(db);
        var file = Assert.IsType<FileContentResult>(await controller.GetCustomerOrderPdfById(order.Id));
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("customerorder_43625.pdf", file.FileDownloadName);
        Assert.True(file.EnableRangeProcessing);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(file.FileContents, 0, 5));
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.IsType<NotFoundObjectResult>(await controller.GetCustomerOrderPdfById(-1));
        var saved = await db.CustomerOrders.AsNoTracking().SingleAsync();
        Assert.False(saved.OrderConfirmationPrinted);
        Assert.Equal(7230d, saved.SalesPrice);
        Assert.Equal(1, await db.TranslationItems.CountAsync());
    }

    [Fact]
    public void SnapshotsAndTranslatedStandardTextsArePrintedWithoutSupplierInformation()
    {
        var order = Fixture();
        order.Customer = new Customer { Name = "SECRET LIVE NAME", Address = "SECRET LIVE ADDRESS" };
        order.SupplierOrder = new SupplierOrder { SupplierName = "SECRET SUPPLIER", PurchasePrice = 888888 };
        order.OrderCosts.First().Cost!.DoPrintScrapToolsTextOnCustomerOrder = true;
        var texts = new DocumentPdfTranslations("EN", new[]
        {
            new TranslationItem { TranslationCode = DocumentPdfTranslations.ToolingDisclaimer, LangCode = "EN", Translation = "Fixed tooling standard text" },
            new TranslationItem { TranslationCode = DocumentPdfTranslations.PaymentDisclaimer, LangCode = "EN", Translation = "Fixed payment standard text" }
        });
        var text = RenderedText(CustomerOrderPdfDocument.CreateDocument(order, Company(), texts));
        Assert.Contains("AB Tingstad Papper", text);
        Assert.Contains("Box 13013", text);
        Assert.Contains("Fixed tooling standard text", text);
        Assert.Contains("Fixed payment standard text", text);
        Assert.Contains("18 months", text);
        Assert.DoesNotContain("SECRET", text);
        Assert.DoesNotContain("888888", text);
    }

    [Fact]
    public void MissingValuesRenderWithoutInventingAZeroPrice()
    {
        var order = new CustomerOrder { Id = 1 };
        var content = CustomerOrderPdfDocument.BuildContent(order, new DocumentPdfTranslations(null));
        Assert.Equal("", content.Quantity);
        Assert.Equal("", content.Price);
        Assert.Single(CustomerOrderPdfDocument.CreateDocument(order, null, new DocumentPdfTranslations(null)).GenerateImages());
    }

    private static string RenderedText(IDocument document) =>
        string.Join(" ", document.GenerateSvg().SelectMany(svg => XDocument.Parse(svg).Descendants())
            .Where(element => element.Name.LocalName == "text").Select(element => element.Value));

    private static CompanyInfo Company() => new()
    {
        CompanyId = 1, CompanyName = "Econopack", Address = "Jolengatan 19", ZipCode = "431 49", PostalAddress = "M\u00f6lndal",
        Telephone2 = "+46 (0)31 363 35 02", Email = "info@econopack.se", Web = "www.econopack.se", Bank = "SECRET BANK"
    };

    private static CustomerOrder Fixture() => new()
    {
        Id = 1, CustomerOrderNr = "43625", CompanyId = 1, Edition = 6400, SalesPrice = 7230,
        SalesCurrency = new Currency { Name = "SEK" }, Unit = new Econosys.Api.Models.Unit { Name = "1000pcs", NrOfCalcDecimals = 0 },
        CustomerName = "AB Tingstad Papper", YourOrderNr = "158964", Product = "1968993 - Vanster", TimeOfDelivery = "2026-05-02",
        Date = new DateTime(2026, 5, 22), YourReference = "Johan Larsson", OurReference = "Petra Toll",
        Address = "Box 13013", PostalNr = "40251", PostalAddress = "G\u00f6teborg", Country = "Sverige",
        DeliveryAddressName = "AB Tingstad Papper", DeliveryAddress = "Box 13013", DeliveryPostalNr = "40251",
        DeliveryPostalAddress = "G\u00f6teborg", DeliveryCountry = "Sverige", Format = "123x45x67mm",
        Construction = "O201, Slitsl\u00e5da", Material = "Vit/Brun, B-flute", Color = "2",
        ProductMessage = "1200x800x1300mm 680st/pall", Message = "External customer message",
        LogisticsInfoInternal = "SECRET LOGISTICS NOTE", InvoicingInfo = "SECRET INVOICING NOTE",
        TermsOfDelivery = "DAP", TermsOfPayment = "30 dagar fr\u00e5n fakturadatum",
        OrderCosts = new List<OrderCost>
        {
            new() { Id = 1, DoDebit = true, DoPrintOnCustomerOrder = true, OutPrice = 4503, InPrice = 999999,
                Note = "SECRET COST NOTE", Cost = new Cost { Name = "stansverktyg" } }
        }
    };
}