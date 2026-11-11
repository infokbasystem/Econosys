using System.Text;
using System.Xml.Linq;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xunit;

namespace Econosys.Api.Tests;

public class TransportOrderPdfTests
{
    public TransportOrderPdfTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = false;
    }

    [Fact]
    public void RecipientsFilterDeliveriesAndSupplierFactoryHeaderLikeLegacy()
    {
        var deliveries = new[] { Delivery(1, 10, 100), Delivery(2, 20, 200) };
        var factories = new Dictionary<int, SupplierFactory>
        {
            [10] = new() { Id = 10, Name = "Factory A", Address = "Street 1", City = "Malmo" },
            [20] = new() { Id = 20, Name = "Factory B", Address = "Street 2", City = "Lund" },
        };

        var transporter = TransportOrderPdfDocument.BuildContent("TRANSPORTER", null, deliveries, factories, []);
        var supplier = TransportOrderPdfDocument.BuildContent("SUPPLIER", 10, deliveries, factories, []);
        var receiver = TransportOrderPdfDocument.BuildContent("RECEIVER", 200, deliveries, factories, []);

        Assert.Equal(2, transporter.Deliveries.Count);
        Assert.Contains("Factory A, Street 1, Malmo", transporter.SupplierFactoryText);
        Assert.Contains("Factory B, Street 2, Lund", transporter.SupplierFactoryText);
        Assert.Equal("Factory A, Street 1, Malmo", supplier.SupplierFactoryText);
        Assert.Equal("SO-1", Assert.Single(supplier.Deliveries).SupplierOrderNumber);
        Assert.Equal(2, Assert.Single(receiver.Deliveries).Id);
        Assert.Contains("Factory A, Street 1, Malmo", receiver.SupplierFactoryText);
        Assert.Contains("Factory B, Street 2, Lund", receiver.SupplierFactoryText);
        Assert.Equal("120x80x130", transporter.Deliveries[0].PalletSizeInfo);
        Assert.Equal(string.Empty, Assert.Single(receiver.Deliveries).PalletSizeInfo);
        Assert.Equal("4/2 pallets, 2p i height", transporter.Deliveries[0].PalletInfo);
    }

    [Fact]
    public void DocumentRendersPalletSchemeAndSharedCompanyFooter()
    {
        var order = new TransportOrder
        {
            Id = 1,
            TransportOrderNr = 260418,
            DateCreated = new DateTime(2026, 11, 5),
            DateLoading = new DateTime(2026, 11, 16),
            DateDelivery = new DateTime(2026, 11, 19),
            SenderReference = "Johan Sarman",
            Shipper = new Shipper { Name = "Carrier AB" },
        };
        var firstDelivery = Delivery(1, 10, 100);
        firstDelivery.TotalWeight = 852;
        var deliveries = new[] { firstDelivery };
        var factories = new Dictionary<int, SupplierFactory>
        {
            [10] = new() { Id = 10, Name = "Factory A", Address = "Street 1", City = "Malmo" },
        };
        var pallets = new[]
        {
            new TransportOrderDeliveryPallet
            {
                TransportOrderDeliveryId = 1,
                PosCmX = 0,
                PosCmY = 0,
                Width = 80,
                Height = 120,
            },
        };
        var document = TransportOrderPdfDocument.CreateDocument(order, Company(), new DocumentPdfTranslations("EN"),
            "TRANSPORTER", null, deliveries, factories, pallets);

        var pdf = document.GeneratePdf();
        var pages = document.GenerateImages().ToList();
        var text = string.Join(" ", document.GenerateSvg().SelectMany(svg => XDocument.Parse(svg).Descendants())
            .Where(element => element.Name.LocalName == "text").Select(element => element.Value));

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        Assert.Single(pages);
        Assert.Contains("260418", text);
        Assert.Contains("Carrier AB", text);
        Assert.Contains("SO-1", text);
        Assert.Contains("852 kg", text);
        Assert.Contains("info@econopack.se", text);

        var scheme = TransportOrderPdfDocument.RenderPalletScheme(pallets,
            new Dictionary<int, string> { [1] = "SO-1" });
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, scheme[..8]);

        var artifactsDirectory = Environment.GetEnvironmentVariable("ECONOSYS_TRANSPORT_PDF_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(artifactsDirectory))
        {
            Directory.CreateDirectory(artifactsDirectory);
            File.WriteAllBytes(Path.Combine(artifactsDirectory, "transport-order.png"), pages.Single());
            File.WriteAllBytes(Path.Combine(artifactsDirectory, "transport-order.pdf"), pdf);
        }
    }

    [Fact]
    public async Task EndpointValidatesRecipientsAndReturnsTransporterPdf()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.TransportOrders.Add(new TransportOrder
        {
            Id = 1,
            TransportOrderNr = 260418,
            Shipper = new Shipper { Id = 2, Name = "Carrier AB" },
        });
        db.CompanyInfos.Add(Company());
        await db.SaveChangesAsync();

        var controller = new PdfController(db);
        Assert.IsType<BadRequestObjectResult>(await controller.GetTransportOrderPdfById(1, "SUPPLIER", null));
        Assert.IsType<NotFoundObjectResult>(await controller.GetTransportOrderPdfById(99, "TRANSPORTER", null));

        var file = Assert.IsType<FileContentResult>(await controller.GetTransportOrderPdfById(1, "TRANSPORTER", null));
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("Transportorder_260418_2.pdf", file.FileDownloadName);
        Assert.True(file.EnableRangeProcessing);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(file.FileContents, 0, 5));
    }

    [Fact]
    public async Task RecipientOptionsAreDistinctAndLimitedToTheTransportOrder()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.TransportOrders.Add(new TransportOrder { Id = 1, TransportOrderNr = 100 });
        db.SupplierFactories.Add(new SupplierFactory { Id = 10, Name = "Factory" });
        db.Inventories.Add(new Inventory { Id = 20, Name = "Warehouse" });
        db.SupplierFactories.Add(new SupplierFactory { Id = 11, Name = "Other factory" });
        db.Inventories.Add(new Inventory { Id = 21, Name = "Other warehouse" });
        db.TransportOrderDeliveries.AddRange(
            new TransportOrderDelivery
            {
                Id = 1,
                TransportOrderId = 1,
                SupplierOrder = new SupplierOrder { Id = 101, CompanyId = 1, SupplierName = "Supplier", SupplierFactoryId = 10 },
                DeliveryToStock = new DeliveryToStock { Id = 201, CompanyId = 1, InventoryId = 20 },
            },
            new TransportOrderDelivery
            {
                Id = 2,
                TransportOrderId = 1,
                SupplierOrder = new SupplierOrder { Id = 102, CompanyId = 1, SupplierName = "Supplier", SupplierFactoryId = 10 },
                DeliveryToStock = new DeliveryToStock { Id = 202, CompanyId = 1, InventoryId = 20 },
            });
        db.TransportOrderDeliveries.Add(new TransportOrderDelivery
        {
            Id = 3,
            TransportOrderId = 2,
            SupplierOrder = new SupplierOrder { Id = 103, CompanyId = 1, SupplierName = "Other supplier", SupplierFactoryId = 11 },
            DeliveryToStock = new DeliveryToStock { Id = 203, CompanyId = 1, InventoryId = 21 },
        });
        await db.SaveChangesAsync();

        var controller = new TransportOrderController(db, null!, null!, null!);
        var actionResult = await controller.GetPdfRecipientOptions(1, CancellationToken.None);
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        var options = Assert.IsType<TransportOrderPdfRecipientOptionsDto>(result.Value);

        var factory = Assert.Single(options.SupplierFactories);
        Assert.Equal(10, factory.Id);
        Assert.Equal("Supplier - Factory", factory.Name);
        var inventory = Assert.Single(options.Inventories);
        Assert.Equal(20, inventory.Id);
        Assert.Equal("Warehouse", inventory.Name);
    }

    private static TransportOrderDelivery Delivery(int id, int factoryId, int inventoryId) => new()
    {
        Id = id,
        SortOrder = id,
        SupplierOrder = new SupplierOrder
        {
            SupplierFactoryId = factoryId,
            SupplierOrderNr = $"SO-{id}",
            SupplierName = $"Supplier {id}",
            Product = $"Product {id}",
        },
        DeliveryToStock = new DeliveryToStock
        {
            InventoryId = inventoryId,
            Inventory = new Inventory { Id = inventoryId, Name = $"Warehouse {id}" },
            NrOfPallets = 4,
            PalletIsStackable = true,
            PalletLength = 120,
            PalletWidth = 80,
            PalletHeight = 130,
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