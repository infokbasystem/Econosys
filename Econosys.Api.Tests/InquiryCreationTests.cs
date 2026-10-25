using System.Security.Claims;
using Econosys.Api.Common;
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

public class InquiryCreationTests
{
    [Theory]
    [InlineData(false, 30)]
    [InlineData(true, 31)]
    public async Task DraftUsesLegacyDefaultsWithoutSaving(bool automaticFreight, int expectedAddressId)
    {
        await using var db = CreateContext();
        await Seed(db, automaticFreight);

        var draft = Assert.IsType<InquiryDto>(Assert.IsType<OkObjectResult>(
            (await Controller(db).FromCalculation(10, default)).Result).Value);

        Assert.Equal(0, draft.Id);
        Assert.Equal(7, draft.CompanyId);
        Assert.Equal(10, draft.CalculationId);
        Assert.Equal(20, draft.SelectedSupplierId);
        Assert.Equal("Supplier", draft.SelectedSupplierName);
        Assert.Equal("supplier@example.com", draft.SelectedSupplierEmail);
        Assert.Equal("Reference", draft.SelectedRecipientYourRef);
        Assert.Equal("DAP", draft.SelectedRecipientTermsOfDelivery);
        Assert.Equal("30 days", draft.SelectedRecipientTermsOfPayment);
        Assert.Equal(5, draft.SelectedRecipientLanguageId);
        Assert.Equal("English", draft.SelectedRecipientLanguageName);
        Assert.True(draft.IsFSC);
        Assert.Equal(25, draft.CustomerId);
        Assert.Equal("Customer", draft.CustomerName);
        Assert.Equal("customer@example.com", draft.CustomerEmail);
        Assert.Equal(expectedAddressId, draft.CustomerDeliveryAddressId);
        Assert.Equal($"Address {expectedAddressId} Extra", draft.DeliveryAddress);
        Assert.Equal("Delivery", draft.DeliveryAddressName);
        Assert.Equal("12345", draft.DeliveryPostalNr);
        Assert.Equal("Town", draft.DeliveryPostalAddress);
        Assert.Equal("Sweden", draft.DeliveryCountry);
        Assert.Equal("Current user", draft.OurRef);
        Assert.Equal(SwedishTime.Now.Date.AddDays(5), draft.AnswerDueDate);
        Assert.Equal("Box", draft.Product);
        Assert.Equal("Translated material", draft.Material);
        Assert.Equal("Translated construction", draft.Construction);
        Assert.Equal("Custom format", draft.Format);
        Assert.Equal("4 + Translated varnish, Extra varnish", draft.Color);
        Assert.Equal("EUR", draft.PurchaseCurrencyName);
        Assert.Equal("SEK", draft.SalesCurrencyName);
        var row = Assert.Single(draft.InquiryRows);
        Assert.Equal(-40, row.Id);
        Assert.Equal(40, row.CalculationRowId);
        Assert.Equal(1000, row.Edition);
        Assert.Equal(12d, row.PurchasePrice);
        Assert.Equal(3d, row.Freight);
        Assert.Empty(await db.Inquiries.ToListAsync());
        Assert.Empty(await db.InquiryRecipients.ToListAsync());
        Assert.Empty(await db.InquiryRows.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveAndReloadPreservesRecipientEditsAndOnlyRetainedRows(bool removeAllRows)
    {
        await using var db = CreateContext();
        await Seed(db);
        db.CalculationRows.Add(new CalculationRow
        {
            Id = 43, CalculationId = 10, CompanyId = 7, Edition = 3000,
        });
        await db.SaveChangesAsync();
        var controller = Controller(db);
        var result = await controller.Create(new CreateInquiryRequest
        {
            CompanyId = 7, CalculationId = 10, CustomerId = 25, CustomerName = "Customer",
            SelectedSupplierId = 20, SelectedRecipientYourRef = "Edited reference",
            SelectedRecipientTermsOfDelivery = "EXW", SelectedRecipientTermsOfPayment = "60 days",
            SelectedRecipientLanguageId = 5,
            CalculationRowIds = removeAllRows ? [] : [40],
            CustomerDeliveryAddressId = 30, DeliveryAddress = "Edited address",
        });
        var saved = Assert.IsType<InquiryDto>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.True(saved.Id > 0);
        Assert.True(saved.SelectedInquiryRecipientId > 0);
        Assert.Equal(removeAllRows ? 0 : 1, saved.InquiryRows.Count);
        db.ChangeTracker.Clear();

        var reloaded = Assert.IsType<InquiryDto>(Assert.IsType<OkObjectResult>(
            (await controller.GetById(saved.Id)).Result).Value);
        Assert.Equal(20, reloaded.SelectedSupplierId);
        Assert.Equal("Edited reference", reloaded.SelectedRecipientYourRef);
        Assert.Equal("EXW", reloaded.SelectedRecipientTermsOfDelivery);
        Assert.Equal("60 days", reloaded.SelectedRecipientTermsOfPayment);
        Assert.Equal(5, reloaded.SelectedRecipientLanguageId);
        Assert.Equal("Edited address", reloaded.DeliveryAddress);
        Assert.Equal(removeAllRows ? 0 : 1, reloaded.InquiryRows.Count);
        if (!removeAllRows)
        {
            var row = Assert.Single(reloaded.InquiryRows);
            Assert.True(row.Id > 0);
            Assert.Equal(40, row.CalculationRowId);
            Assert.Equal(1000, row.Edition);
            Assert.Equal(12d, row.PurchasePrice);
            Assert.Equal(3d, row.Freight);
        }
        var recipient = await db.InquiryRecipients.SingleAsync();
        Assert.Equal(saved.Id, recipient.InquiryId);
        Assert.Equal("Loading instructions", recipient.Note);
    }

    [Fact]
    public async Task SavedInquiryCanBeUpdatedAndDeletedWithItsRowsAndRecipient()
    {
        await using var db = CreateContext();
        await Seed(db);
        var controller = Controller(db);
        var created = Assert.IsType<InquiryDto>(Assert.IsType<CreatedAtActionResult>(
            (await controller.Create(new CreateInquiryRequest
            {
                CompanyId = 7, CalculationId = 10, CustomerId = 25,
                SelectedSupplierId = 20, SelectedRecipientLanguageId = 5,
                CalculationRowIds = [40],
            })).Result).Value);
        var updated = Assert.IsType<InquiryDto>(Assert.IsType<OkObjectResult>(
            (await controller.Update(created.Id, new UpdateInquiryRequest
            {
                CompanyId = 7, CalculationId = 10, CustomerId = 25,
                SelectedInquiryRecipientId = created.SelectedInquiryRecipientId,
                SelectedRecipientYourRef = "Updated again", SelectedRecipientLanguageId = 5,
                InquiryRowIds = created.InquiryRows.Select(row => row.Id).ToList(),
            })).Result).Value);
        Assert.Single(updated.InquiryRows);
        Assert.Equal("Updated again", updated.SelectedRecipientYourRef);

        db.ChangeTracker.Clear();
        Assert.IsType<NoContentResult>(await controller.Delete(created.Id));
        Assert.Empty(await db.Inquiries.ToListAsync());
        Assert.Empty(await db.InquiryRows.ToListAsync());
        Assert.Empty(await db.InquiryRecipients.ToListAsync());
        Assert.Equal(2, await db.CalculationRows.CountAsync());
        Assert.Single(await db.Suppliers.ToListAsync());
        Assert.Single(await db.Customers.ToListAsync());
    }

    [Fact]
    public async Task CreateRejectsSupplierFromAnotherCalculation()
    {
        await using var db = CreateContext();
        await Seed(db);
        var result = await Controller(db).Create(new CreateInquiryRequest
        {
            CompanyId = 7, CalculationId = 10, SelectedSupplierId = 999,
        });
        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(await db.Inquiries.ToListAsync());
    }

    [Fact]
    public async Task StandaloneInquiryStillSavesWithoutCalculationRowsOrSupplier()
    {
        await using var db = CreateContext();
        var result = await Controller(db).Create(new CreateInquiryRequest { CompanyId = 7, Product = "Box" });
        var saved = Assert.IsType<InquiryDto>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal("Box", saved.Product);
        Assert.Null(saved.CalculationId);
        Assert.Null(saved.SelectedInquiryRecipientId);
        Assert.Empty(saved.InquiryRows);
    }

    [Theory]
    [InlineData(41)]
    [InlineData(42)]
    [InlineData(999)]
    public async Task CreateRejectsArchivedForeignOrMissingRows(int rowId)
    {
        await using var db = CreateContext();
        await Seed(db);
        db.CalculationRows.Add(new CalculationRow { Id = 42, CalculationId = 11 });
        await db.SaveChangesAsync();

        var result = await Controller(db).Create(new CreateInquiryRequest
        {
            CompanyId = 7, CalculationId = 10, CalculationRowIds = [rowId],
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(await db.Inquiries.ToListAsync());
    }

    [Fact]
    public async Task MissingCalculationAndMissingPartiesReturnErrors()
    {
        await using var db = CreateContext();
        var controller = Controller(db);
        Assert.IsType<NotFoundResult>((await controller.FromCalculation(999, default)).Result);
        db.Calculations.Add(new Calculation { Id = 10 });
        await db.SaveChangesAsync();
        var error = Assert.IsType<ObjectResult>((await controller.FromCalculation(10, default)).Result);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public void DraftFallsBackToDimensionsAndOriginalNamesAndOmitsLegacyNoVarnish()
    {
        var draft = InquiryDraftMapper.Map(new Calculation
        {
            Product = new Product
            {
                LengthMm = 100, WidthMm = 0, HeightMm = 20,
                Material = new Material { Name = "Material" },
                Construction = new Construction { Name = "Construction" },
                NrOfColors = "2", VarnishId = 7, Varnish = new Varnish { Name = "None" },
            },
            CalculationRows = [new CalculationRow { Id = 1 }, new CalculationRow { Id = 2 }],
        }, null, new LegacyUser(), DateTime.Today, null, new Dictionary<int, string?>());

        Assert.Equal("100 x 20", draft.Format);
        Assert.Equal("Material", draft.Material);
        Assert.Equal("Construction", draft.Construction);
        Assert.Equal("2", draft.Color);
        Assert.Null(draft.AnswerDueDate);
        Assert.Null(draft.CustomerDeliveryAddressId);
        Assert.Equal(2, draft.InquiryRows.Select(row => row.Id).Distinct().Count());
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static InquiriesController Controller(ApplicationDbContext db) =>
        new(db, NullLogger<InquiriesController>.Instance, new UserResolution());

    private sealed class UserResolution : ILegacyUserResolutionService
    {
        public Task<LegacyUser?> ResolveCurrentUserAsync(ClaimsPrincipal principal) =>
            Task.FromResult<LegacyUser?>(new LegacyUser { Id = 99, CompanyId = 7, Name = "Current user" });
    }

    private static async Task Seed(ApplicationDbContext db, bool automaticFreight = false)
    {
        db.Calculations.Add(new Calculation
        {
            Id = 10, CompanyId = 7,
            Product = new Product
            {
                Id = 15, Name = "Box", Format = "Custom format", LengthMm = 100,
                Material = new Material { TranslationCode = 100, Name = "Material" },
                Construction = new Construction { TranslationCode = 101, Name = "Construction" },
                NrOfColors = "4", Varnish = new Varnish { Id = 6, TranslationCode = 102, Name = "Varnish" },
                VarnishOther = "Extra varnish",
            },
            Supplier = new Supplier
            {
                Id = 20, Name = "Supplier", Email = "supplier@example.com", Reference = "Reference",
                TermsOfDelivery = "DAP", TermsOfPayment = "30 days", FscDefault = true,
                Language = new Language { Id = 5, Name = "English", LanguageCode = "en" },
            },
            Customer = new Customer { Id = 25, Name = "Customer", Email = "customer@example.com" },
            PurchaseCurrency = new Currency { Id = 1, Name = "EUR" },
            SalesCurrency = new Currency { Id = 2, Name = "SEK" },
            AutoFreightPrice = automaticFreight, CustomerDeliveryAddressId = 31,
            LoadingInstruction = "Loading instructions",
            CalculationRows =
            [
                new CalculationRow { Id = 40, CompanyId = 7, Edition = 1000, PurchasePrice = 12, Freight = 3 },
                new CalculationRow { Id = 41, CompanyId = 7, Edition = 2000, Archived = true },
            ],
        });
        foreach (var id in new[] { 30, 31 })
        {
            db.CustomerDeliveryAddresses.Add(new CustomerDeliveryAddress
            {
                Id = id, CustomerId = 25, IsDefault = id == 30, Name = "Delivery",
                Address = $"Address {id}", AddressExtra = "Extra",
                PostalNr = "12345", PostalAddress = "Town", Country = "Sweden",
            });
        }
        db.Settings.Add(new Setting { CompanyId = 7, NrOfInquiryAnswerDays = 5 });
        db.TranslationItems.AddRange(
            new TranslationItem { TranslationCode = 100, LangCode = "en", Translation = "Translated material" },
            new TranslationItem { TranslationCode = 101, LangCode = "en", Translation = "Translated construction" },
            new TranslationItem { TranslationCode = 102, LangCode = "en", Translation = "Translated varnish" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }
}
