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
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public async Task DraftCopiesPalletDebitFromCalculationAndPersistsIt(bool debitPallet, int palletType)
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        var customer = new Customer { Id = 1, Name = "Customer" };
        var supplier = new Supplier { Id = 2, Name = "Supplier" };
        db.Calculations.Add(new Calculation
        {
            Id = 10, Customer = customer, Supplier = supplier, PalletType = palletType,
            DoDebitPalletPrice = debitPallet, PalletFormatId = 6,
            CalculationRows = [new CalculationRow { Id = 20 }],
        });
        db.Inquiries.Add(new Inquiry
        {
            Id = 30, Customer = customer,
            SelectedInquiryRecipient = new InquiryRecipient { Id = 40, Supplier = supplier },
        });
        db.Quotations.Add(new Quotation { Id = 50, CalculationId = 10, InquiryId = 30 });
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

        var request = JsonSerializer.Deserialize<CreateSupplierOrderRequest>(JsonSerializer.Serialize(draft))!;
        var orders = new SupplierOrdersController(db, NullLogger<SupplierOrdersController>.Instance, user, palletOptions);
        var saved = Assert.IsType<SupplierOrderDto>(Assert.IsType<CreatedAtActionResult>((await orders.Create(request)).Result).Value);
        db.ChangeTracker.Clear();
        var reloaded = Assert.IsType<SupplierOrderDto>(Assert.IsType<OkObjectResult>((await orders.GetById(saved.Id)).Result).Value);
        Assert.Equal(debitPallet, reloaded.EurPallet);
        Assert.Equal(palletType == 2, reloaded.OneWayPallet);
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
