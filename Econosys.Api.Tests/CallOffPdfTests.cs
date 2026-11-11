using System.Text;
using System.Xml.Linq;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xunit;

namespace Econosys.Api.Tests;

public class CallOffPdfTests
{
    public CallOffPdfTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = false;
    }

    [Fact]
    public void ContentUsesSavedPalletPlacesOrStackabilityAndBuildsConfirmationPrices()
    {
        var callOff = Fixture();
        var delivery = Delivery();
        var texts = new DocumentPdfTranslations("EN");
        var palletFormats = new Dictionary<int, string> { [8] = "EUR-pall" };

        var content = CallOffPdfDocument.BuildContent(callOff, new[] { delivery }, palletFormats, texts);

        var row = Assert.Single(content.Deliveries);
        Assert.Equal(2m, row.NumberOfPalletPlaces);
        Assert.Equal("Fragile\nEUR-pall\n1200 x 800 x 1400", row.Note);
        Assert.Equal("20.000 SEK/pcs", row.UnitPrice);
        Assert.Equal("40,000.00 SEK", row.Price);
        Assert.Equal(40000m, content.TotalPrice.GetValueOrDefault());
        Assert.Equal("Company AB\nDock Name\nDock Street\n123 45 City", content.DeliveryAddress);
        Assert.Equal("JA: 125.50 kr", content.DebitFreight);
        Assert.Equal(4m, content.TotalNumberOfPallets);
        Assert.Equal(2m, content.TotalNumberOfPalletPlaces);

        delivery.NrOfPalletPlaces = 3.5m;
        Assert.Equal(3.5m, Assert.Single(CallOffPdfDocument.BuildContent(callOff, new[] { delivery }, palletFormats, texts).Deliveries).NumberOfPalletPlaces);

        delivery.NrOfPalletPlaces = null;
        delivery.DeliveryFromStock!.CustomerOrder!.Customer!.SpecialUnitHandling = false;
        delivery.DeliveryFromStock.CustomerOrder.Unit!.Multiplicator = 1000;
        var standardUnitContent = CallOffPdfDocument.BuildContent(callOff, new[] { delivery }, palletFormats, texts);
        Assert.Equal(40000m, Assert.Single(standardUnitContent.Deliveries).LineTotal);

        delivery.DeliveryFromStock.CustomerOrder.SalesPrice = null;
        var noPriceContent = CallOffPdfDocument.BuildContent(callOff, new[] { delivery }, palletFormats, texts);
        Assert.Null(noPriceContent.TotalPrice);
    }

    [Fact]
    public void FreightOrderHasTwoSectionsAndConfirmationHasPricesAndSharedFooter()
    {
        var callOff = Fixture();
        var deliveries = new[] { Delivery() };
        var texts = new DocumentPdfTranslations("EN");
        var palletFormats = new Dictionary<int, string> { [8] = "EUR-pall" };
        var freightOrder = CallOffPdfDocument.CreateFreightOrderDocument(callOff, Company(), texts, deliveries, palletFormats);
        var confirmation = CallOffPdfDocument.CreateConfirmationDocument(callOff, Company(), texts, deliveries, palletFormats);
        var freightText = RenderedText(freightOrder);
        var confirmationText = RenderedText(confirmation);
        var freightPages = freightOrder.GenerateImages().ToList();
        var confirmationPages = confirmation.GenerateImages().ToList();

        Assert.Equal(2, freightPages.Count);
        Assert.Single(confirmationPages);
        Assert.Contains("Fraktbeställning / avrop från lager", freightText);
        Assert.Contains("Följesedel", freightText);
        Assert.DoesNotContain("Pris/enh", freightText);
        Assert.Contains("Avropsbekräftelse", confirmationText);
        Assert.Contains("Pris/enh", confirmationText);
        Assert.Contains("Pris", confirmationText);
        Assert.Contains("20.000", confirmationText);
        Assert.Contains("40,000.00", confirmationText);
        Assert.Contains("SEK", confirmationText);
        Assert.Contains("info@econopack.se", freightText);
        Assert.Contains("info@econopack.se", confirmationText);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(freightOrder.GeneratePdf(), 0, 5));
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(confirmation.GeneratePdf(), 0, 5));

        var artifactsDirectory = Environment.GetEnvironmentVariable("ECONOSYS_CALLOFF_PDF_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(artifactsDirectory))
        {
            Directory.CreateDirectory(artifactsDirectory);
            for (var index = 0; index < freightPages.Count; index++)
            {
                File.WriteAllBytes(Path.Combine(artifactsDirectory, $"calloff-{index + 1}.png"), freightPages[index]);
            }
            File.WriteAllBytes(Path.Combine(artifactsDirectory, "calloff-confirmation.png"), confirmationPages.Single());
        }
    }

    [Fact]
    public async Task EndpointsReturnBothPdfVariantsAndNotFoundForMissingCallOff()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CallOffs.Add(Fixture());
        db.CompanyInfos.Add(Company());
        await db.SaveChangesAsync();
        var controller = new PdfController(db);

        Assert.IsType<NotFoundObjectResult>(await controller.GetCallOffPdfById(99));
        Assert.IsType<NotFoundObjectResult>(await controller.GetCallOffConfirmationPdfById(99));

        var freightOrder = Assert.IsType<FileContentResult>(await controller.GetCallOffPdfById(1));
        var confirmation = Assert.IsType<FileContentResult>(await controller.GetCallOffConfirmationPdfById(1));
        Assert.Equal("application/pdf", freightOrder.ContentType);
        Assert.Equal("calloff_1.pdf", freightOrder.FileDownloadName);
        Assert.True(freightOrder.EnableRangeProcessing);
        Assert.Equal("application/pdf", confirmation.ContentType);
        Assert.Equal("calloffconfirmation_1.pdf", confirmation.FileDownloadName);
        Assert.True(confirmation.EnableRangeProcessing);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(freightOrder.FileContents, 0, 5));
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(confirmation.FileContents, 0, 5));
    }

    private static string RenderedText(IDocument document) =>
        string.Join(" ", document.GenerateSvg().SelectMany(svg => XDocument.Parse(svg).Descendants())
            .Where(element => element.Name.LocalName == "text").Select(element => element.Value));

    private static CallOff Fixture() => new()
    {
        Id = 1,
        Reference = "Calloff ref",
        Note = "102250",
        CreatedDateTime = new DateTime(2026, 11, 5),
        DeliveryDate = new DateTime(2026, 11, 13),
        DoDebitFreight = true,
        FreightCostToDebit = 125.5m,
        Shipper = new Shipper { Name = "Carrier AB" },
        CustomerDeliveryAddress = new CustomerDeliveryAddress
        {
            Customer = new Customer { Name = "Company AB" },
            Name = "Dock Name",
            Address = "Dock Street",
            PostalNr = "123 45",
            PostalAddress = "City",
        },
    };

    private static CallOffDelivery Delivery(decimal? palletPlaces = null) => new()
    {
        Id = 11,
        SortOrder = 1,
        Note = "Fragile",
        DeliveryAddressFreeText = "Dock 2",
        NrOfPalletPlaces = palletPlaces,
        DeliveryFromStock = new DeliveryFromStock
        {
            NrOfItems = 2000,
            NrOfPallets = 4,
            PalletIsStackable = true,
            PalletFormatId = 8,
            PalletLength = 1200,
            PalletWidth = 800,
            PalletHeight = 1400,
            Inventory = new Inventory { Name = "Warehouse A" },
            CustomerOrder = new CustomerOrder
            {
                CustomerOrderNr = "CO-100",
                Product = "Product A",
                CustomerName = "Company AB",
                PalletFormatId = 8,
                SalesPrice = 20000,
                SalesCurrency = new Currency { Name = "SEK" },
                Unit = new Econosys.Api.Models.Unit { Name = "1000pcs", NrOfCalcDecimals = 2 },
                Customer = new Customer { SpecialUnitHandling = true },
            },
        },
    };

    private static CompanyInfo Company() => new()
    {
        CompanyId = 1,
        CompanyName = "Econopack",
        Address = "Jolengatan 19",
        ZipCode = "431 49",
        PostalAddress = "Molndal",
        Telephone2 = "+46 (0)31 363 35 02",
        Email = "info@econopack.se",
        Web = "www.econopack.se",
    };
}
