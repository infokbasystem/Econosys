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

public class SupplierOrderPdfTests
{
    public SupplierOrderPdfTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = false;
    }

    [Fact]
    public void PricingUsesSavedOrderAndPurchasePrecision()
    {
        var order = Fixture();
        order.SelectedCalculationRow = new CalculationRow
        {
            Edition = 999999, PurchasePrice = 888888, Calculation = new Calculation { NrOfCalcDecimalsPurchase = 3 }
        };
        var content = SupplierOrderPdfDocument.BuildContent(order, new DocumentPdfTranslations("EN"));
        Assert.Equal("SEK/1000pcs", content.PriceHeading);
        Assert.Equal("6,400", content.Quantity);
        Assert.Equal("7,230.000", content.Price);
        order.PurchasePrice = null;
        Assert.Equal("", SupplierOrderPdfDocument.BuildContent(order, new DocumentPdfTranslations("EN")).Price);
    }

    [Fact]
    public void GoodsMarkingUsesTheLegacySwitchesRatherThanPrintingTheirNames()
    {
        var order = Fixture();
        order.GoodsMarking = "Custom marking";
        Assert.Equal("Custom marking", SupplierOrderPdfDocument.BuildContent(order, new DocumentPdfTranslations("SW")).GoodsMarking);
        order.HideProductNameOnPrint = true;
        order.HideCustomerInfoOnPrint = true;
        order.HideCustomerNameOnPrint = true;
        Assert.Equal("1968993 - Vanster\n158964\nAB Tingstad Papper",
            SupplierOrderPdfDocument.BuildContent(order, new DocumentPdfTranslations("SW")).GoodsMarking);
    }

    [Fact]
    public void CostsUseSupplierPricesAndTheirOwnCurrenciesWithoutCustomerDebitFiltering()
    {
        var order = Fixture();
        var type = new Cost { Name = "Tooling", TranslationCodeSupplierOrderKnown = 501, TranslationCodeSupplierOrderUnknown = 502 };
        order.OrderCosts = new List<OrderCost>
        {
            new() { Id = 1, Cost = type, DoDebit = false, DoPrintOnSupplierOrder = true, InPrice = 4503, OutPrice = 999999 },
            new() { Id = 2, Cost = type, DoPrintOnSupplierOrder = true, InPrice = 0, InPriceCurrency = new Currency { Name = "EUR" } },
            new() { Id = 3, Cost = type, DoPrintOnSupplierOrder = true },
            new() { Id = 4, Cost = type, DoPrintOnSupplierOrder = false, InPrice = 666666 }
        };
        var texts = new DocumentPdfTranslations("EN", new[]
        {
            new TranslationItem { TranslationCode = 501, LangCode = "EN", Translation = "Known tooling" },
            new TranslationItem { TranslationCode = 502, LangCode = "EN", Translation = "Tooling will be added" }
        });
        var costs = SupplierOrderPdfDocument.BuildContent(order, texts).Costs;
        Assert.Equal(3, costs.Count);
        Assert.Equal("4,503.00 SEK", costs[0].Amount);
        Assert.Equal("Known tooling", costs[0].Description);
        Assert.Equal("0.00 EUR", costs[1].Amount);
        Assert.Equal("", costs[2].Amount);
        Assert.Equal("Tooling will be added", costs[2].Description);
    }

    [Fact]
    public void DeliveryAndConfirmationUseIsoYearForWeekDates()
    {
        var order = Fixture();
        order.DeliveryDate = new DateTime(2027, 1, 1);
        order.DeliveryDateWeekMode = true;
        var texts = new DocumentPdfTranslations("EN");
        Assert.Equal("2026 week 53", SupplierOrderPdfDocument.BuildContent(order, texts).DesiredDelivery);
        Assert.Equal("", SupplierOrderPdfDocument.BuildContent(order, texts).Confirmation);
        order.Confirmed = true;
        order.ConfirmedDeliveryDate = new DateTime(2027, 1, 8);
        order.ConfirmedDeliveryDateWeekMode = true;
        Assert.Equal("Confirmed\n2027 week 01", SupplierOrderPdfDocument.BuildContent(order, texts).Confirmation);
    }

    [Theory]
    [InlineData("SW", "Best\u00e4llning")]
    [InlineData("EN", "Purchase order")]
    public void SupplierOrderMatchesReferenceAndRendersOnePage(string language, string title)
    {
        var document = SupplierOrderPdfDocument.CreateDocument(Fixture(), Company(), new DocumentPdfTranslations(language),
            new PalletFormat { Name = "EUR-pall" }, "34567");
        Assert.Single(document.GenerateImages());
        var bytes = document.GeneratePdf();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        var text = RenderedText(document);
        Assert.Contains(title, text);
        Assert.Contains("43625", text);
        Assert.Contains("34567", text);
        Assert.Contains("28352", text);
        Assert.Contains("1200x800x1300mm 680st/pall", text);
        Assert.Contains("EUR-pall", text);
        Assert.Contains("info@econopack.se", text);
        Assert.DoesNotContain("SECRET", text);
        var directory = Environment.GetEnvironmentVariable("ECONOSYS_SUPPLIER_PDF_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, $"supplierorder-{language}.pdf"), bytes);
            File.WriteAllBytes(Path.Combine(directory, $"supplierorder-{language}.png"), document.GenerateImages().Single());
        }
    }

    [Fact]
    public void LongTextAndCostListsPaginate()
    {
        var order = Fixture();
        order.Message = string.Join("\n", Enumerable.Repeat("A long product note that must wrap without clipping.", 50));
        order.OrderCosts = Enumerable.Range(1, 80).Select(id => new OrderCost
        {
            Id = id, DoPrintOnSupplierOrder = true, InPrice = 25,
            Cost = new Cost { Name = "Additional tooling cost" }
        }).ToList();
        var pages = SupplierOrderPdfDocument.CreateDocument(order, Company(), new DocumentPdfTranslations("EN")).GenerateImages().ToList();
        Assert.InRange(pages.Count, 2, 15);
    }

    [Fact]
    public async Task ExistingEndpointLoadsSupplierDataAndDoesNotPersistChanges()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var order = Fixture();
        order.Supplier = new Supplier { Name = "Supplier", Language = new Language { LanguageCode = "EN" } };
        order.SelectedCalculationRow = new CalculationRow { Calculation = new Calculation { NrOfCalcDecimalsPurchase = 3 } };
        order.PalletFormatId = 10;
        db.SupplierOrders.Add(order);
        db.SupplierOrders.Add(new SupplierOrder { Id = 99, SupplierOrderNr = "PREV-34567" });
        db.PalletFormats.Add(new PalletFormat { Id = 10, Name = "EUR-pall", TranslationCode = 502 });
        db.CompanyInfos.Add(Company());
        db.TranslationItems.Add(new TranslationItem { TranslationCode = 502, LangCode = "EN", Translation = "EUR pallet" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var controller = new PdfController(db);
        var file = Assert.IsType<FileContentResult>(await controller.GetSupplierOrderPdfById(order.Id));
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("supplierorder_43625.pdf", file.FileDownloadName);
        Assert.True(file.EnableRangeProcessing);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(file.FileContents, 0, 5));
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.IsType<NotFoundObjectResult>(await controller.GetSupplierOrderPdfById(-1));
        Assert.False((await db.SupplierOrders.SingleAsync(item => item.Id == order.Id)).Confirmed);
        Assert.Equal(7230d, (await db.SupplierOrders.SingleAsync(item => item.Id == order.Id)).PurchasePrice);
        Assert.Equal(1, await db.TranslationItems.CountAsync());
    }

    [Fact]
    public void PalletFormatTranslationAndSupplierTemplateRulesArePreserved()
    {
        var order = Fixture();
        order.HideCustomerInfoOnPrint = true;
        order.Supplier = new Supplier { SupplierOrderTemplateNr = 1 };
        var texts = new DocumentPdfTranslations("EN", new[]
        {
            new TranslationItem { TranslationCode = 501, LangCode = "EN", Translation = "Translated pallet" }
        });
        var content = SupplierOrderPdfDocument.BuildContent(order, texts, new PalletFormat { TranslationCode = 501, Name = "Pallet" });
        Assert.Equal("Translated pallet", content.PalletInfo);
        Assert.DoesNotContain("158964", content.GoodsMarking);
        Assert.Contains("43625 / 158964", RenderedText(SupplierOrderPdfDocument.CreateDocument(order, Company(), texts)));
    }

    [Fact]
    public void NamesMovedToGoodsMarkingAreNotDuplicatedInNormalSections()
    {
        var order = Fixture();
        order.Customer!.Address = "PRIVATE CUSTOMER STREET";
        order.HideProductNameOnPrint = true;
        order.HideCustomerNameOnPrint = true;
        order.HideCustomerInfoOnPrint = true;
        var text = RenderedText(SupplierOrderPdfDocument.CreateDocument(order, Company(), new DocumentPdfTranslations("EN")));
        Assert.DoesNotContain("PRIVATE CUSTOMER STREET", text);
        Assert.DoesNotContain("Box 13013", text);
        Assert.Equal(1, text.Split(order.Customer.Name!, StringSplitOptions.None).Length - 1);
        Assert.Equal(1, text.Split(order.Product!, StringSplitOptions.None).Length - 1);
        Assert.Contains("158964", text);
        Assert.DoesNotContain("SECRET", text);
    }

    [Fact]
    public void MissingRelationshipsRenderAndQuantityCostsKeepTheirUnitPrice()
    {
        Assert.Single(SupplierOrderPdfDocument.CreateDocument(new SupplierOrder { Id = 1 }, null,
            new DocumentPdfTranslations(null)).GenerateImages());
        var order = Fixture();
        order.OrderCosts = new List<OrderCost>
        {
            new() { Id = 1, DoPrintOnSupplierOrder = true, InPrice = 25, NrOf = 3,
                Cost = new Cost { Name = "Plates", IsNrOf = true }, InPriceCurrency = new Currency { Name = "EUR" } }
        };
        Assert.Equal("3 pcs \u00d7 25.00 EUR", SupplierOrderPdfDocument.BuildContent(order, new DocumentPdfTranslations("EN")).Costs[0].Amount);
    }

    private static string RenderedText(IDocument document) =>
        string.Join(" ", document.GenerateSvg().SelectMany(svg => XDocument.Parse(svg).Descendants())
            .Where(element => element.Name.LocalName == "text").Select(element => element.Value));

    private static CompanyInfo Company() => new()
    {
        CompanyId = 1, CompanyName = "Econopack", Address = "Jolengatan 19", ZipCode = "431 49", PostalAddress = "M\u00f6lndal",
        Telephone2 = "+46 (0)31 363 35 02", Email = "info@econopack.se", Web = "www.econopack.se", Bank = "SECRET BANK"
    };

    private static SupplierOrder Fixture() => new()
    {
        Id = 1, SupplierOrderNr = "43625", CompanyId = 1, Edition = 6400, PurchasePrice = 7230,
        PurchaseCurrency = new Currency { Name = "SEK" }, Unit = new Econosys.Api.Models.Unit { Name = "1000pcs", NrOfCalcDecimals = 0 },
        Customer = new Customer { Name = "AB Tingstad Papper" }, CustomerOrderNr = "158964",
        Product = "1968993 - Vanster", TimeOfDelivery = "2026-05-02", InquiryId = 28352, BasedOnSupplierOrderId = 99,
        Date = new DateTime(2026, 5, 22), YourReference = "Johan Larsson", OurReference = "Petra Toll",
        DeliveryAddressName = "AB Tingstad Papper", DeliveryAddress = "Box 13013", DeliveryPostalNr = "40251",
        DeliveryPostalAddress = "G\u00f6teborg", DeliveryCountry = "Sverige", Format = "123x45x67mm",
        Construction = "O201, Slitsl\u00e5da", Material = "Vit/Brun, B-flute", Color = "2",
        LoadingInstruction = "1200x800x1300mm 680st/pall", EurPallet = true,
        GoodsMarking = "1968993 - Vanster\n158964\nAB Tingstad Papper", TermsOfDelivery = "DAP", TermsOfPayment = "45 dagar netto",
        OrderCosts = new List<OrderCost>
        {
            new() { Id = 1, DoPrintOnSupplierOrder = true, InPrice = 4503, OutPrice = 999999, Note = "SECRET COST NOTE",
                Cost = new Cost { Name = "stansverktyg" } }
        }
    };
}