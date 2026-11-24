using System.Security.Claims;
using System.Text.Json;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Econosys.Api.Tests;

public class SupplierOrderWorkflowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DraftCopiesQuotationCostVersionAndSelectedCalculationCosts(bool hasPreviousOrder)
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        var customer = new Customer { Id = 1, Name = "Customer", InvoiceCostsSeparately = true };
        var supplier = new Supplier { Id = 2, Name = "Supplier" };
        var manualCost = new Cost { Id = 100, Name = "Manual" };
        var calculationCost = new Cost { Id = 101, Name = "Calculation", IsCalculation = true, DoPrintSupplierOrderDefault = true };
        db.Calculations.Add(new Calculation
        {
            Id = 10, CompanyId = 1, Customer = customer, Supplier = supplier,
            Product = new Product { Id = 3, Name = "Product" },
            CalculationRows = [new CalculationRow { Id = 20 }, new CalculationRow { Id = 21 }],
        });
        db.Inquiries.Add(new Inquiry
        {
            Id = 30, Customer = customer,
            SelectedInquiryRecipient = new InquiryRecipient { Id = 40, Supplier = supplier },
        });
        db.Quotations.Add(new Quotation { Id = 50, CalculationId = 10, InquiryId = 30 });
        db.OrderCosts.AddRange(
            new OrderCost { Id = 1, QuotationId = 50, Cost = manualCost, DoDebit = true, OutPrice = 10, Note = "Quote" },
            new OrderCost { Id = 2, QuotationId = 50, Cost = calculationCost, CalculationRowId = 21, OutPrice = 99 });
        db.CalculationRowCosts.AddRange(
            new CalculationRowCost { Id = 1, CalculationRowId = 20, Cost = calculationCost, DoDebit = true, InPrice = 4, OutPrice = 8 },
            new CalculationRowCost { Id = 2, CalculationRowId = 21, Cost = calculationCost, OutPrice = 999 });
        if (hasPreviousOrder)
        {
            db.SupplierOrders.AddRange(
                new SupplierOrder { Id = 60, CompanyId = 1, QuotationId = 50 },
                new SupplierOrder { Id = 61, CompanyId = 1, QuotationId = 50 });
            db.OrderCosts.AddRange(
                new OrderCost { Id = 3, QuotationId = 50, SupplierOrderId = 60, Cost = manualCost, OutPrice = 20 },
                new OrderCost
                {
                    Id = 4, QuotationId = 50, SupplierOrderId = 61, Cost = manualCost,
                    DoDebit = true, InPrice = 7, InPriceCurrencyId = 8, InPriceAttested = 7,
                    OutPrice = 30, NrOf = 2, CalculationRowId = 21, SupplierId = 2, SupplierName = "Supplier",
                    Note = "Latest", DoPrintOnQuotation = true, DoPrintOnCustomerOrder = true,
                    DoPrintOnSupplierOrder = true, IsCostInvoicedSeparately = true,
                });
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var user = new UserResolution();
        var palletOptions = new PalletFormatOptionsService(db);
        var workflow = new SupplierOrderWorkflowController(db, user, palletOptions);
        var result = await workflow.CreateDraft(10, new CreateSupplierOrderDraftRequest
        {
            CalculationRowId = 20, InquiryId = 30, QuotationId = 50,
        });
        var draft = Assert.IsType<SupplierOrderWorkflowDraftDto>(Assert.IsType<OkObjectResult>(result).Value).SupplierOrder;
        Assert.Equal(2, draft.OrderCosts.Count);
        var copied = Assert.Single(draft.OrderCosts, cost => cost.CostId == 100);
        var fromCalculation = Assert.Single(draft.OrderCosts, cost => cost.CostId == 101);
        Assert.Equal(hasPreviousOrder ? 30 : 10, copied.OutPrice);
        Assert.Equal(hasPreviousOrder ? "Latest" : "Quote", copied.Note);
        Assert.Equal(0, copied.Id);
        Assert.Null(copied.SupplierOrderId);
        Assert.Null(copied.InPriceAttested);
        Assert.False(copied.IsCostInvoicedSeparately);
        Assert.True(copied.DoInvoiceSeparately);
        Assert.Equal(20, fromCalculation.CalculationRowId);
        Assert.Equal(8, fromCalculation.OutPrice);
        Assert.True(fromCalculation.DoPrintOnSupplierOrder);
        if (hasPreviousOrder)
        {
            Assert.Equal(8, copied.InPriceCurrencyId);
            Assert.Equal(2, copied.SupplierId);
            Assert.Equal(21, copied.CalculationRowId);
            Assert.True(copied.DoPrintOnCustomerOrder);
        }

        var request = JsonSerializer.Deserialize<CreateSupplierOrderRequest>(JsonSerializer.Serialize(draft))!;
        var orders = new SupplierOrdersController(db, NullLogger<SupplierOrdersController>.Instance, user, palletOptions);
        var saved = Assert.IsType<SupplierOrderDto>(Assert.IsType<CreatedAtActionResult>((await orders.Create(request)).Result).Value);
        db.ChangeTracker.Clear();
        var persisted = await db.OrderCosts.Where(cost => cost.SupplierOrderId == saved.Id).ToListAsync();
        Assert.Equal(2, persisted.Count);
        Assert.All(persisted, cost => Assert.Equal(50, cost.QuotationId));
        Assert.Equal(20, Assert.Single(persisted, cost => cost.CostId == 101).CalculationRowId);
        var savedManualCost = Assert.Single(persisted, cost => cost.CostId == 100);
        Assert.Equal(hasPreviousOrder ? 30 : 10, savedManualCost.OutPrice);
        Assert.Equal(hasPreviousOrder ? 21 : null, savedManualCost.CalculationRowId);
        Assert.Equal(hasPreviousOrder ? 2 : null, savedManualCost.SupplierId);
        Assert.Equal(hasPreviousOrder ? 4 : 2, await db.OrderCosts.CountAsync(cost => cost.SupplierOrderId != saved.Id));
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public async Task DraftCopiesPalletDebitFromCalculationAndPersistsIt(bool debitPallet, int palletType)
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        var customer = new Customer { Id = 1, Name = "Customer" };
        var supplier = new Supplier
        {
            Id = 2, Name = "Supplier", Language = new Language { Id = 2, LanguageCode = "en" }
        };
        db.Calculations.Add(new Calculation
        {
            Id = 10, CompanyId = 1, Customer = customer, Supplier = supplier, PalletType = palletType,
            Product = new Product
            {
                Id = 3, Name = "Current product", NrOfColors = "4", NoteExternal = "Current note",
                Material = new Material { Id = 4, Name = "Material", TranslationCode = 100 },
                Varnish = new Varnish { Id = 5, Name = "Varnish", TranslationCode = 101 },
                InternalInfo = new ProductInternalInfo { IsDigital = true },
            },
            DoDebitPalletPrice = debitPallet, PalletFormatId = 6,
            CalculationRows = [new CalculationRow { Id = 20 }],
        });
        db.Inquiries.Add(new Inquiry
        {
            Id = 30, Customer = customer, Product = "Old product", Material = "Old material",
            SelectedInquiryRecipient = new InquiryRecipient { Id = 40, Supplier = supplier },
        });
        db.Quotations.Add(new Quotation { Id = 50, CalculationId = 10, InquiryId = 30 });
        db.TranslationItems.AddRange(
            new TranslationItem { LangCode = "en", TranslationCode = 100, Translation = "Translated material" },
            new TranslationItem { LangCode = "en", TranslationCode = 101, Translation = "Translated varnish" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var user = new UserResolution();
        var palletOptions = new PalletFormatOptionsService(db);
        var workflow = new SupplierOrderWorkflowController(db, user, palletOptions);
        var result = await workflow.CreateDraft(10, new CreateSupplierOrderDraftRequest
        {
            CalculationRowId = 20, InquiryId = 30, QuotationId = 50,
        });
        var draft = Assert.IsType<SupplierOrderWorkflowDraftDto>(Assert.IsType<OkObjectResult>(result).Value).SupplierOrder;
        Assert.Equal(debitPallet, draft.EurPallet);
        Assert.Equal(palletType == 2, draft.OneWayPallet);
        Assert.Equal(6, draft.PalletFormatId);
        Assert.Equal("Current product", draft.Product);
        Assert.Equal("4", draft.Color);
        Assert.Equal("Translated material", draft.Material);
        Assert.Equal("Translated varnish", draft.Varnish);
        Assert.Equal("Digital", draft.PrintMethod);
        Assert.Equal("Current note", draft.ProductNoteExternal);

        var currentProduct = await db.Products.SingleAsync();
        currentProduct.Name = "Updated after draft";
        await db.SaveChangesAsync();
        var request = JsonSerializer.Deserialize<CreateSupplierOrderRequest>(JsonSerializer.Serialize(draft))!;
        var orders = new SupplierOrdersController(db, NullLogger<SupplierOrdersController>.Instance, user, palletOptions);
        var saved = Assert.IsType<SupplierOrderDto>(Assert.IsType<CreatedAtActionResult>((await orders.Create(request)).Result).Value);
        db.ChangeTracker.Clear();
        var reloaded = Assert.IsType<SupplierOrderDto>(Assert.IsType<OkObjectResult>((await orders.GetById(saved.Id)).Result).Value);
        Assert.Equal(debitPallet, reloaded.EurPallet);
        Assert.Equal(palletType == 2, reloaded.OneWayPallet);
        Assert.Equal("Updated after draft", reloaded.Product);
        Assert.Equal("4", reloaded.Color);
        Assert.Equal("Translated varnish", reloaded.Varnish);
        Assert.Equal("Current note", reloaded.ProductNoteExternal);
    }

    [Fact]
    public async Task AutomaticQuotationCopiesProductMaterialToEditableMaterialField()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Settings.Add(new Setting { CompanyId = 1 });
        db.Calculations.Add(new Calculation
        {
            Id = 10,
            Customer = new Customer { Id = 1, Name = "Customer" },
            Supplier = new Supplier { Id = 2, Name = "Supplier" },
            Product = new Product { Id = 3, Name = "Box", Material = new Material { Id = 4, Name = "Kraft 300" } },
        });
        db.Inquiries.Add(new Inquiry { Id = 30, CalculationId = 10 });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var workflow = new SupplierOrderWorkflowController(db, new UserResolution(), new PalletFormatOptionsService(db));
        var created = Assert.IsType<SupplierOrderWorkflowDocumentDto>(Assert.IsType<OkObjectResult>(
            (await workflow.CreateQuotationFromCalculation(10, 30)).Result).Value);

        Assert.Equal("Kraft 300", (await db.Quotations.SingleAsync(x => x.Id == created.Id)).Material);
    }

    private sealed class UserResolution : ILegacyUserResolutionService
    {
        public Task<LegacyUser?> ResolveCurrentUserAsync(ClaimsPrincipal principal) =>
            Task.FromResult<LegacyUser?>(new LegacyUser { Id = 1, CompanyId = 1, Name = "User" });
    }
}
