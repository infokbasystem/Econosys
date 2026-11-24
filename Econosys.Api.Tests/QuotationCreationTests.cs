using System.Security.Claims;
using System.Text.Json;
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

public class QuotationCreationTests
{
    [Fact]
    public async Task CreateCapturesProductChangesMadeAfterDraftWasPrepared()
    {
        await using var db = CreateContext();
        await Seed(db);
        var draft = await Draft(db);
        var product = await db.Products.SingleAsync();
        product.Name = "Updated after draft";
        await db.SaveChangesAsync();
        var request = JsonSerializer.Deserialize<CreateQuotationRequest>(JsonSerializer.Serialize(draft))!;
        request.CalculationRowIds = [40];

        var result = await Controller(db).Create(request);
        var saved = Assert.IsType<QuotationDto>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal("Updated after draft", saved.Product);
    }

    [Theory]
    [InlineData(false, 30, "Address 30")]
    [InlineData(true, 31, "Address 31 Extra")]
    public async Task DraftUsesSelectedInquiryAndLegacyCustomerDefaultsWithoutSaving(
        bool automaticFreight, int expectedAddressId, string expectedAddress)
    {
        await using var db = CreateContext();
        await Seed(db, automaticFreight);
        var draft = await Draft(db);

        Assert.Equal(0, draft.Id);
        Assert.Equal(7, draft.CompanyId);
        Assert.Equal(10, draft.CalculationId);
        Assert.Equal(50, draft.InquiryId);
        Assert.Equal(25, draft.CustomerId);
        Assert.Equal("Customer", draft.CustomerName);
        Assert.Equal("customer@example.com", draft.CustomerEmail);
        Assert.Equal("Invoice address", draft.Address);
        Assert.Equal("Second line", draft.Address2);
        Assert.Equal("12345", draft.PostalNr);
        Assert.Equal("Town", draft.PostalAddress);
        Assert.Equal("Sweden", draft.Country);
        Assert.Equal(expectedAddressId, draft.CustomerDeliveryAddressId);
        Assert.Equal(expectedAddress, draft.DeliveryAddress);
        Assert.Equal("Delivery", draft.DeliveryAddressName);
        Assert.Equal("54321", draft.DeliveryPostalNr);
        Assert.Equal("Delivery town", draft.DeliveryPostalAddress);
        Assert.Equal("Sweden", draft.DeliveryCountry);
        Assert.Equal(21, draft.SelectedSupplierId);
        Assert.Equal("Inquiry supplier", draft.SelectedSupplierName);
        Assert.True(draft.IsFsc);
        Assert.Equal("Seller", draft.SellerName);
        Assert.Equal("Customer reference", draft.YourReference);
        Assert.Equal("Current user", draft.OurReference);
        Assert.Equal("DAP", draft.TermsOfDelivery);
        Assert.Equal("30 days", draft.TermsOfPayment);
        Assert.Equal((short)30, draft.PaymentDays);
        Assert.Equal("Default message", draft.Message);
        Assert.Equal(SwedishTime.Now.Date, draft.Date);
        Assert.Equal("Box", draft.Product);
        Assert.Equal("External note", draft.ProductMessage);
        Assert.Equal("Translated material", draft.Material);
        Assert.Equal("Translated construction", draft.Construction);
        Assert.Equal("Custom format", draft.Format);
        Assert.Equal("4", draft.Color);
        Assert.Equal("Translated varnish, Extra varnish", draft.Varnish);
        Assert.Equal(1, draft.SalesCurrencyId);
        Assert.Equal(11.5, draft.SalesCurrencyRate);
        Assert.Equal(2, draft.PurchaseCurrencyId);
        Assert.Equal(10.5, draft.PurchaseCurrencyRate);
        Assert.Equal(3, draft.UnitId);
        Assert.True(draft.EurPallet);
        Assert.Equal(6, draft.PalletFormatId);
        Assert.Equal(95d, draft.EurPalletValue);
        var row = Assert.Single(draft.QuotationRows);
        Assert.Equal(-40, row.Id);
        Assert.Equal(40, row.CalculationRowId);
        Assert.Equal(1000, row.Edition);
        Assert.Equal(12.35, row.Price);
        Assert.Equal("EUR", row.SalesCurrencyName);
        Assert.Equal("1000 pcs", row.UnitName);
        Assert.Equal((short)1000, row.UnitMultiplicator);
        Assert.Empty(await db.Quotations.ToListAsync());
        Assert.Empty(await db.QuotationRows.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DraftSavesAndReloadsWithAllHeaderFieldsAndOnlyRetainedRows(bool removeRows)
    {
        await using var db = CreateContext();
        await Seed(db);
        db.CalculationRows.Add(new CalculationRow
        {
            Id = 43, CalculationId = 10, CompanyId = 7, Edition = 3000,
            SalesPrice = 20, PercentAddition = 25,
        });
        await db.SaveChangesAsync();
        var draft = await Draft(db);
        Assert.Equal(2, draft.QuotationRows.Count);
        var request = JsonSerializer.Deserialize<CreateQuotationRequest>(JsonSerializer.Serialize(draft))!;
        request.CalculationRowIds = removeRows ? [] : [40];
        request.Message = "Edited message";
        request.DeliveryAddress = "Edited address";
        request.YourReference = "Edited reference";
        var controller = Controller(db);
        var saved = Assert.IsType<QuotationDto>(Assert.IsType<CreatedAtActionResult>(
            (await controller.Create(request)).Result).Value);
        Assert.True(saved.Id > 0);
        Assert.Equal(7, saved.CompanyId);
        Assert.Equal(99, saved.CreatedBy);
        Assert.Equal(99, saved.EditedBy);
        db.ChangeTracker.Clear();

        var reloaded = Assert.IsType<QuotationDto>(Assert.IsType<OkObjectResult>(
            (await controller.GetById(saved.Id)).Result).Value);
        Assert.Equal(10, reloaded.CalculationId);
        Assert.Equal(50, reloaded.InquiryId);
        Assert.Equal(21, reloaded.SelectedSupplierId);
        Assert.Equal("Customer", reloaded.CustomerName);
        Assert.Equal("Invoice address", reloaded.Address);
        Assert.Equal("Second line", reloaded.Address2);
        Assert.Equal("12345", reloaded.PostalNr);
        Assert.Equal("Town", reloaded.PostalAddress);
        Assert.Equal("Sweden", reloaded.Country);
        Assert.Equal("Edited address", reloaded.DeliveryAddress);
        Assert.Equal("Delivery", reloaded.DeliveryAddressName);
        Assert.Equal("Edited message", reloaded.Message);
        Assert.Equal("Edited reference", reloaded.YourReference);
        Assert.Equal("Current user", reloaded.OurReference);
        Assert.Equal("DAP", reloaded.TermsOfDelivery);
        Assert.Equal("30 days", reloaded.TermsOfPayment);
        Assert.Equal((short)30, reloaded.PaymentDays);
        Assert.Equal(draft.Date, reloaded.Date);
        Assert.Equal(draft.Product, reloaded.Product);
        Assert.Equal(draft.ProductMessage, reloaded.ProductMessage);
        Assert.Equal(draft.Material, reloaded.Material);
        Assert.Equal(draft.Construction, reloaded.Construction);
        Assert.Equal(draft.Format, reloaded.Format);
        Assert.Equal(draft.Color, reloaded.Color);
        Assert.Equal(draft.Varnish, reloaded.Varnish);
        Assert.True(reloaded.IsFsc);
        Assert.Equal(draft.SalesCurrencyId, reloaded.SalesCurrencyId);
        Assert.Equal(draft.SalesCurrencyRate, reloaded.SalesCurrencyRate);
        Assert.Equal(draft.PurchaseCurrencyId, reloaded.PurchaseCurrencyId);
        Assert.Equal(draft.PurchaseCurrencyRate, reloaded.PurchaseCurrencyRate);
        Assert.Equal(draft.UnitId, reloaded.UnitId);
        Assert.Equal(draft.PalletFormatId, reloaded.PalletFormatId);
        Assert.Equal(draft.EurPalletValue, reloaded.EurPalletValue);
        Assert.True(reloaded.EurPallet);
        Assert.Equal(removeRows ? 0 : 1, reloaded.QuotationRows.Count);
        if (!removeRows)
        {
            var row = Assert.Single(reloaded.QuotationRows);
            Assert.True(row.Id > 0);
            Assert.Equal(40, row.CalculationRowId);
            Assert.Equal(12.35, row.Price);
            Assert.Equal(7, (await db.QuotationRows.SingleAsync()).CompanyId);
        }
    }

    [Fact]
    public async Task InquiryChoicesAreRestrictedToCalculation()
    {
        await using var db = CreateContext();
        await Seed(db);
        var controller = Controller(db);
        var choices = Assert.IsType<List<FilterOptionDto<int>>>(Assert.IsType<OkObjectResult>(
            (await controller.GetCalculationInquiries(10, default)).Result).Value);
        Assert.Equal(50, Assert.Single(choices).Id);
        Assert.Contains("Inquiry supplier", choices[0].Name);
        Assert.IsType<NotFoundResult>((await controller.GetCalculationInquiries(999, default)).Result);
        db.Calculations.Add(new Calculation { Id = 12 });
        await db.SaveChangesAsync();
        Assert.Empty(Assert.IsType<List<FilterOptionDto<int>>>(Assert.IsType<OkObjectResult>(
            (await controller.GetCalculationInquiries(12, default)).Result).Value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    [InlineData(999)]
    public async Task DraftAndSaveRequireInquiryFromSameCalculation(int inquiryId)
    {
        await using var db = CreateContext();
        await Seed(db);
        var controller = Controller(db);
        var draftError = Assert.IsType<ObjectResult>((await controller.FromCalculation(10, inquiryId, default)).Result);
        Assert.Equal(400, draftError.StatusCode);
        Assert.IsType<BadRequestObjectResult>((await controller.Create(new CreateQuotationRequest
        {
            CompanyId = 7, CalculationId = 10, CustomerId = 25,
            InquiryId = inquiryId, CalculationRowIds = [40],
        })).Result);
        Assert.Empty(await db.Quotations.ToListAsync());
    }

    [Fact]
    public async Task DraftAndSaveRejectCustomerMismatch()
    {
        await using var db = CreateContext();
        await Seed(db);
        (await db.Inquiries.SingleAsync(x => x.Id == 50)).CustomerId = 999;
        await db.SaveChangesAsync();
        var controller = Controller(db);
        Assert.Equal(400, Assert.IsType<ObjectResult>(
            (await controller.FromCalculation(10, 50, default)).Result).StatusCode);
        Assert.IsType<BadRequestObjectResult>((await controller.Create(new CreateQuotationRequest
        {
            CompanyId = 7, CalculationId = 10, InquiryId = 50, CustomerId = 25,
        })).Result);
    }

    [Theory]
    [InlineData(41)]
    [InlineData(42)]
    [InlineData(999)]
    public async Task SaveRejectsArchivedForeignOrMissingCalculationRows(int rowId)
    {
        await using var db = CreateContext();
        await Seed(db);
        var result = await Controller(db).Create(new CreateQuotationRequest
        {
            CompanyId = 7, CalculationId = 10, InquiryId = 50, CustomerId = 25,
            CalculationRowIds = [rowId],
        });
        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(await db.Quotations.ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0d)]
    public async Task MissingMarkupBlocksDraftAndSaveLikeLegacy(double? markup)
    {
        await using var db = CreateContext();
        await Seed(db);
        (await db.CalculationRows.SingleAsync(x => x.Id == 40)).PercentAddition = markup;
        await db.SaveChangesAsync();
        var controller = Controller(db);
        var result = Assert.IsType<ObjectResult>((await controller.FromCalculation(10, 50, default)).Result);
        Assert.Equal("Det finns kalkylrader utan påslag.", Assert.IsType<ProblemDetails>(result.Value).Detail);
        Assert.IsType<BadRequestObjectResult>((await controller.Create(new CreateQuotationRequest
        {
            CompanyId = 7, CalculationId = 10, InquiryId = 50, CustomerId = 25,
            CalculationRowIds = [40],
        })).Result);
    }

    [Theory]
    [InlineData((short)3, (short)1, 12.346)]
    [InlineData(null, (short)1, 12.3)]
    [InlineData(null, null, 12.35)]
    public void RowPriceUsesCalculationThenUnitPrecision(short? calculationDecimals, short? unitDecimals, double price)
    {
        var row = QuotationDraftMapper.MapRow(new Calculation
        {
            NrOfCalcDecimalsSales = calculationDecimals,
            Unit = new Unit { NrOfCalcDecimals = unitDecimals },
        }, new CalculationRow { Id = 40, SalesPrice = 12.3456 });
        Assert.Equal(price, row.Price);
    }

    [Fact]
    public async Task SavedQuotationCanUpdateDeliveryAndPalletValuesThenDelete()
    {
        await using var db = CreateContext();
        await Seed(db);
        var draft = await Draft(db);
        var request = JsonSerializer.Deserialize<CreateQuotationRequest>(JsonSerializer.Serialize(draft))!;
        request.CalculationRowIds = [40];
        var controller = Controller(db);
        var saved = Assert.IsType<QuotationDto>(Assert.IsType<CreatedAtActionResult>(
            (await controller.Create(request)).Result).Value);
        var update = JsonSerializer.Deserialize<UpdateQuotationRequest>(JsonSerializer.Serialize(saved))!;
        update.QuotationRowIds = saved.QuotationRows.Select(row => row.Id).ToList();
        update.CustomerDeliveryAddressId = 31;
        update.DeliveryAddress = "Updated address";
        update.EurPalletValue = 123;
        update.Material = "Edited material text";
        var updated = Assert.IsType<QuotationDto>(Assert.IsType<OkObjectResult>(
            (await controller.Update(saved.Id, update)).Result).Value);
        Assert.Equal("Edited material text", updated.Material);
        Assert.Equal(31, updated.CustomerDeliveryAddressId);
        Assert.Equal("Updated address", updated.DeliveryAddress);
        Assert.Equal(123d, updated.EurPalletValue);
        Assert.Equal(50, updated.InquiryId);
        Assert.Single(updated.QuotationRows);
        db.ChangeTracker.Clear();
        Assert.IsType<NoContentResult>(await controller.Delete(saved.Id));
        Assert.Empty(await db.Quotations.ToListAsync());
        Assert.Empty(await db.QuotationRows.ToListAsync());
        Assert.Equal(2, await db.Inquiries.CountAsync());
    }

    [Fact]
    public async Task StandaloneQuotationCanStillBeSavedWithoutCalculationOrInquiry()
    {
        await using var db = CreateContext();
        var saved = Assert.IsType<QuotationDto>(Assert.IsType<CreatedAtActionResult>(
            (await Controller(db).Create(new CreateQuotationRequest
            {
                CompanyId = 7, Product = "Standalone", Message = "Message",
            })).Result).Value);
        Assert.Equal("Standalone", saved.Product);
        Assert.Equal("Message", saved.Message);
        Assert.Null(saved.CalculationId);
        Assert.Null(saved.InquiryId);
        Assert.Empty(saved.QuotationRows);
    }

    [Fact]
    public async Task OrderCostCurrencyIsSavedReloadedAndPreservedWhenOmitted()
    {
        await using var db = CreateContext();
        var controller = Controller(db);
        var saved = Assert.IsType<QuotationDto>(Assert.IsType<CreatedAtActionResult>(
            (await controller.Create(new CreateQuotationRequest
            {
                CompanyId = 7,
                OrderCosts = [new UpsertQuotationOrderCostRequest { CostId = 3, InPriceCurrencyId = 7 }],
            })).Result).Value);
        var costId = Assert.Single(saved.OrderCosts).Id;
        Assert.Equal(7, saved.OrderCosts[0].InPriceCurrencyId);
        db.ChangeTracker.Clear();
        var reloaded = Assert.IsType<QuotationDto>(Assert.IsType<OkObjectResult>(
            (await controller.GetById(saved.Id)).Result).Value);
        Assert.Equal(7, Assert.Single(reloaded.OrderCosts).InPriceCurrencyId);

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var update = JsonSerializer.Deserialize<UpdateQuotationRequest>(
            $$"""{"id":{{saved.Id}},"orderCosts":[{"id":{{costId}},"costId":3,"inPriceCurrencyId":2}]}""", options)!;
        var updated = Assert.IsType<QuotationDto>(Assert.IsType<OkObjectResult>(
            (await controller.Update(saved.Id, update)).Result).Value);
        Assert.Equal(2, Assert.Single(updated.OrderCosts).InPriceCurrencyId);
        db.ChangeTracker.Clear();
        Assert.Equal(2, (await db.OrderCosts.SingleAsync()).InPriceCurrencyId);

        var omitted = JsonSerializer.Deserialize<UpdateQuotationRequest>(
            $$"""{"id":{{saved.Id}},"orderCosts":[{"id":{{costId}},"costId":3}]}""", options)!;
        var preserved = Assert.IsType<QuotationDto>(Assert.IsType<OkObjectResult>(
            (await controller.Update(saved.Id, omitted)).Result).Value);
        Assert.Equal(2, Assert.Single(preserved.OrderCosts).InPriceCurrencyId);
        var clear = JsonSerializer.Deserialize<UpdateQuotationRequest>(
            $$"""{"id":{{saved.Id}},"orderCosts":[{"id":{{costId}},"costId":3,"inPriceCurrencyId":null}]}""", options)!;
        var cleared = Assert.IsType<QuotationDto>(Assert.IsType<OkObjectResult>(
            (await controller.Update(saved.Id, clear)).Result).Value);
        Assert.Null(Assert.Single(cleared.OrderCosts).InPriceCurrencyId);
        db.ChangeTracker.Clear();
        Assert.Null((await db.OrderCosts.SingleAsync()).InPriceCurrencyId);
    }

    [Fact]
    public async Task HideExtraCostsSettingCanBeSavedReloadedAndChanged()
    {
        await using var db = CreateContext();
        var controller = Controller(db);
        var created = Assert.IsType<QuotationDto>(Assert.IsType<CreatedAtActionResult>(
            (await controller.Create(new CreateQuotationRequest { CompanyId = 7, HideExtraCostsOnPrint = true })).Result).Value);
        Assert.True(created.HideExtraCostsOnPrint);
        db.ChangeTracker.Clear();
        var reloaded = Assert.IsType<QuotationDto>(Assert.IsType<OkObjectResult>(
            (await controller.GetById(created.Id)).Result).Value);
        Assert.True(reloaded.HideExtraCostsOnPrint);

        var preserved = Assert.IsType<QuotationDto>(Assert.IsType<OkObjectResult>(
            (await controller.Update(created.Id, new UpdateQuotationRequest { Id = created.Id })).Result).Value);
        Assert.True(preserved.HideExtraCostsOnPrint);
        var updated = Assert.IsType<QuotationDto>(Assert.IsType<OkObjectResult>(
            (await controller.Update(created.Id, new UpdateQuotationRequest
            {
                Id = created.Id, HideExtraCostsOnPrint = false
            })).Result).Value);
        Assert.False(updated.HideExtraCostsOnPrint);
        db.ChangeTracker.Clear();
        Assert.False((await db.Quotations.SingleAsync()).HideExtraCostsOnPrint);
    }

    [Fact]
    public async Task RowInfoPrintSelectionSurvivesCreateUpdateAndReload()
    {
        await using var db = CreateContext();
        await Seed(db);
        (await db.CalculationRows.SingleAsync(row => row.Id == 40)).Info = "Row information";
        db.CalculationRows.Add(new CalculationRow
        {
            Id = 43, CalculationId = 10, CompanyId = 7, Edition = 3000,
            SalesPrice = 20, PercentAddition = 25, Info = "Other information",
        });
        await db.SaveChangesAsync();
        var controller = Controller(db);
        var draft = await Draft(db);
        Assert.All(draft.QuotationRows, row => Assert.True(row.PrintInfo));
        Assert.Equal("Row information", draft.QuotationRows.Single(row => row.CalculationRowId == 40).Info);

        var request = new CreateQuotationRequest
        {
            CompanyId = 7, CalculationId = 10, InquiryId = 50, CustomerId = 25,
            CalculationRowIds = [40, 43], HiddenCalculationRowInfoIds = [40],
        };
        var created = Assert.IsType<QuotationDto>(Assert.IsType<CreatedAtActionResult>(
            (await controller.Create(request)).Result).Value);
        var hiddenRow = created.QuotationRows.Single(row => row.CalculationRowId == 40);
        var visibleRow = created.QuotationRows.Single(row => row.CalculationRowId == 43);
        Assert.Equal("Row information", hiddenRow.Info);
        Assert.False(hiddenRow.PrintInfo);
        Assert.True(visibleRow.PrintInfo);

        db.ChangeTracker.Clear();
        var loaded = Assert.IsType<QuotationDto>(Assert.IsType<OkObjectResult>(
            (await controller.GetById(created.Id)).Result).Value);
        Assert.False(loaded.QuotationRows.Single(row => row.Id == hiddenRow.Id).PrintInfo);
        var update = new UpdateQuotationRequest
        {
            Id = created.Id, QuotationRowIds = [hiddenRow.Id, visibleRow.Id],
        };
        var preserved = Assert.IsType<QuotationDto>(Assert.IsType<OkObjectResult>(
            (await controller.Update(created.Id, update)).Result).Value);
        Assert.False(preserved.QuotationRows.Single(row => row.Id == hiddenRow.Id).PrintInfo);
        update.HiddenQuotationRowInfoIds = [visibleRow.Id];
        var changed = Assert.IsType<QuotationDto>(Assert.IsType<OkObjectResult>(
            (await controller.Update(created.Id, update)).Result).Value);
        Assert.True(changed.QuotationRows.Single(row => row.Id == hiddenRow.Id).PrintInfo);
        Assert.False(changed.QuotationRows.Single(row => row.Id == visibleRow.Id).PrintInfo);
        db.ChangeTracker.Clear();
        loaded = Assert.IsType<QuotationDto>(Assert.IsType<OkObjectResult>(
            (await controller.GetById(created.Id)).Result).Value);
        Assert.True(loaded.QuotationRows.Single(row => row.Id == hiddenRow.Id).PrintInfo);
        Assert.False(loaded.QuotationRows.Single(row => row.Id == visibleRow.Id).PrintInfo);
    }

    [Fact]
    public async Task RejectsPrintOptionsForUnrelatedQuotationRows()
    {
        await using var db = CreateContext();
        await Seed(db);
        var controller = Controller(db);
        var request = new CreateQuotationRequest
        {
            CompanyId = 7, CalculationId = 10, InquiryId = 50, CustomerId = 25,
            CalculationRowIds = [40], HiddenCalculationRowInfoIds = [42],
        };
        Assert.IsType<BadRequestObjectResult>((await controller.Create(request)).Result);
        request.HiddenCalculationRowInfoIds = null;
        var created = Assert.IsType<QuotationDto>(Assert.IsType<CreatedAtActionResult>(
            (await controller.Create(request)).Result).Value);
        var row = Assert.Single(created.QuotationRows);
        Assert.True(row.PrintInfo);
        Assert.IsType<BadRequestObjectResult>((await controller.Update(created.Id, new UpdateQuotationRequest
        {
            Id = created.Id, QuotationRowIds = [row.Id], HiddenQuotationRowInfoIds = [row.Id + 100],
        })).Result);
        Assert.True((await db.QuotationRows.SingleAsync()).PrintInfo);
    }

    private static async Task<QuotationDto> Draft(ApplicationDbContext db) =>
        Assert.IsType<QuotationDto>(Assert.IsType<OkObjectResult>(
            (await Controller(db).FromCalculation(10, 50, default)).Result).Value);

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static QuotationsController Controller(ApplicationDbContext db) =>
        new(db, NullLogger<QuotationsController>.Instance, new UserResolution(), new PalletFormatOptionsService(db));

    private sealed class UserResolution : ILegacyUserResolutionService
    {
        public Task<LegacyUser?> ResolveCurrentUserAsync(ClaimsPrincipal principal) =>
            Task.FromResult<LegacyUser?>(new LegacyUser { Id = 99, CompanyId = 7, Name = "Current user" });
    }

    private static async Task Seed(ApplicationDbContext db, bool automaticFreight = false)
    {
        var customer = new Customer
        {
            Id = 25, CompanyId = 7, Name = "Customer", Email = "customer@example.com",
            Address = "Invoice address", Address2 = "Second line", PostalNr = "12345",
            PostalAddress = "Town", Country = "Sweden", Reference = "Customer reference",
            TermsOfDelivery = "DAP", TermsOfPayment = "30 days", PaymentDays = 30,
            Language = new Language { Id = 5, Name = "English", LanguageCode = "en" },
            ResponsibleUser = new LegacyUser { Id = 98, Name = "Seller" },
        };
        db.Calculations.Add(new Calculation
        {
            Id = 10, CompanyId = 7, Customer = customer,
            Supplier = new Supplier { Id = 20, Name = "Calculation supplier" },
            Product = new Product
            {
                Id = 15, Name = "Box", NoteExternal = "External note",
                Format = "Custom format", LengthMm = 100,
                Material = new Material { TranslationCode = 100, Name = "Material" },
                Construction = new Construction { TranslationCode = 101, Name = "Construction" },
                NrOfColors = "4", Varnish = new Varnish { Id = 6, Name = "Varnish", TranslationCode = 102 },
                VarnishOther = "Extra varnish",
            },
            SalesCurrency = new Currency { Id = 1, Name = "EUR" }, SalesCurrencyRate = 11.5,
            PurchaseCurrency = new Currency { Id = 2, Name = "SEK" }, PurchaseCurrencyRate = 10.5,
            Unit = new Unit { Id = 3, Name = "1000 pcs", Multiplicator = 1000, NrOfCalcDecimals = 1 },
            NrOfCalcDecimalsSales = 2, DoDebitPalletPrice = true, PalletFormatId = 6,
            AutoFreightPrice = automaticFreight, CustomerDeliveryAddressId = 31,
            CalculationRows =
            [
                new CalculationRow { Id = 40, CompanyId = 7, Edition = 1000, SalesPrice = 12.3456, PercentAddition = 25 },
                new CalculationRow { Id = 41, CompanyId = 7, Edition = 2000, Archived = true },
            ],
        });
        db.Calculations.Add(new Calculation
        {
            Id = 11, CompanyId = 7,
            CalculationRows = [new CalculationRow { Id = 42, CompanyId = 7, PercentAddition = 25 }],
        });
        db.Inquiries.AddRange(new Inquiry
        {
            Id = 50, CompanyId = 7, CalculationId = 10, Customer = customer, CustomerName = "Customer",
            IsFSC = true, SelectedInquiryRecipient = new InquiryRecipient
            {
                Id = 60, CompanyId = 7, Supplier = new Supplier { Id = 21, Name = "Inquiry supplier" },
            },
        }, new Inquiry { Id = 51, CompanyId = 7, CalculationId = 11 });
        foreach (var id in new[] { 30, 31 })
        {
            db.CustomerDeliveryAddresses.Add(new CustomerDeliveryAddress
            {
                Id = id, CustomerId = 25, IsDefault = id == 30,
                Name = "Delivery", Address = $"Address {id}", AddressExtra = "Extra",
                PostalNr = "54321", PostalAddress = "Delivery town", Country = "Sweden",
            });
        }
        db.Settings.Add(new Setting { CompanyId = 7, DefaultCustomerMessage = "Default message" });
        db.PalletFormatPrices.Add(new PalletFormatPrice { CustomerId = 25, PalletFormatId = 6, PalletPrice = 95 });
        db.TranslationItems.AddRange(
            new TranslationItem { TranslationCode = 100, LangCode = "en", Translation = "Translated material" },
            new TranslationItem { TranslationCode = 101, LangCode = "en", Translation = "Translated construction" },
            new TranslationItem { TranslationCode = 102, LangCode = "en", Translation = "Translated varnish" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }
}
