using System.Security.Claims;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Econosys.Api.Tests;

public class InvoiceDraftTests
{
    [Fact]
    public async Task CreditRelationsResolveBothDirectionsAndExcludeUnrelatedInvoices()
    {
        using var context = CreateContext();
        context.Invoices.AddRange(
            new Invoice { Id = 1, InvoiceNumber = 100, InvoiceTypeCode = InvoiceTypeCode.Normal },
            new Invoice { Id = 2, InvoiceNumber = 102, InvoiceTypeCode = InvoiceTypeCode.Credit, CreditingInvoiceNumber = 100 },
            new Invoice { Id = 3, InvoiceNumber = 101, InvoiceTypeCode = InvoiceTypeCode.Credit, CreditingInvoiceNumber = 100 },
            new Invoice { Id = 4, InvoiceNumber = 103, InvoiceTypeCode = InvoiceTypeCode.Normal, CreditingInvoiceNumber = 100 },
            new Invoice { Id = 5, InvoiceTypeCode = InvoiceTypeCode.Credit, CreditingInvoiceNumber = 100 },
            new Invoice { Id = 6, InvoiceNumber = 104, InvoiceTypeCode = InvoiceTypeCode.Credit, CreditingInvoiceNumber = 999 },
            new Invoice { Id = 7 });
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var original = Assert.IsType<InvoiceCreditRelationsResponse>(
            Assert.IsType<OkObjectResult>((await controller.GetCreditRelations(1, CancellationToken.None)).Result).Value);
        Assert.Null(original.CreditedInvoice);
        Assert.Equal([new InvoiceReferenceResponse(3, 101), new InvoiceReferenceResponse(2, 102)], original.CreditInvoices);

        var credit = Assert.IsType<InvoiceCreditRelationsResponse>(
            Assert.IsType<OkObjectResult>((await controller.GetCreditRelations(2, CancellationToken.None)).Result).Value);
        Assert.Equal(new InvoiceReferenceResponse(1, 100), credit.CreditedInvoice);
        Assert.Empty(credit.CreditInvoices);

        var missingOriginal = Assert.IsType<InvoiceCreditRelationsResponse>(
            Assert.IsType<OkObjectResult>((await controller.GetCreditRelations(6, CancellationToken.None)).Result).Value);
        Assert.Null(missingOriginal.CreditedInvoice);
        var noNumber = Assert.IsType<InvoiceCreditRelationsResponse>(
            Assert.IsType<OkObjectResult>((await controller.GetCreditRelations(7, CancellationToken.None)).Result).Value);
        Assert.Empty(noNumber.CreditInvoices);
        Assert.IsType<NotFoundResult>((await controller.GetCreditRelations(999, CancellationToken.None)).Result);
    }

    [Fact]
    public void AccountingRowsMapRequiredLegacyCompanyColumn()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(InvoiceAccountRow))!;
        var property = entity.FindProperty("CompanyId");

        Assert.NotNull(property);
        Assert.False(property.IsNullable);
        Assert.Equal(typeof(int), property.ClrType);
        Assert.Equal("CompanyId", property.GetColumnName());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavingInvoiceAssignsUserCompanyToAllRows(bool update)
    {
        using var context = CreateContext();
        var controller = new InvoicesController(context,
            NullLogger<InvoicesController>.Instance, new UserResolution(7), new ConfigurationBuilder().Build());
        var existing = new Invoice { Id = 50 };
        if (update)
        {
            existing.InvoiceAccountRows.Add(new InvoiceAccountRow { AccountNr = "3000" });
            context.Invoices.Add(existing);
            await context.SaveChangesAsync();
        }

        InvoiceMutationRequest request = update
            ? new UpdateInvoiceRequest()
            : new CreateInvoiceRequest();
        request.TimeOfDelivery = "2026-11-23";
        request.TermsOfDelivery = "DAP";
        request.InvoiceRows = [new() { AccountNr = "3001", Sum = 100 }];
        request.InvoiceAccountRows = [new() { AccountNr = "3001", Sum = 100 }];
        if (update)
        {
            Assert.IsType<OkObjectResult>((await controller.Update(existing.Id, (UpdateInvoiceRequest)request)).Result);
        }
        else
        {
            Assert.IsType<CreatedAtActionResult>((await controller.Create((CreateInvoiceRequest)request)).Result);
        }

        context.ChangeTracker.Clear();
        var row = Assert.Single(await context.InvoiceRows.ToListAsync());
        var accountRow = Assert.Single(await context.InvoiceAccountRows.ToListAsync());
        var savedInvoice = Assert.Single(await context.Invoices.ToListAsync());
        Assert.Equal(7, row.CompanyId);
        Assert.Equal(7, accountRow.CompanyId);
        Assert.Equal("3001", accountRow.AccountNr);
        Assert.Equal(100d, accountRow.Sum);
        Assert.Equal("2026-11-23", savedInvoice.TimeOfDelivery);
        Assert.Equal("DAP", savedInvoice.TermsOfDelivery);
    }

    [Theory]
    [InlineData(0, true, "2026-11-30", " DAP ", "dap", "DAP")]
    [InlineData(1, true, null, "DAP", "DAP", "DAP")]
    [InlineData(0, false, null, "DAP", "DAP", "DAP")]
    [InlineData(0, true, "2026-11-30", "DAP", "EXW", null)]
    [InlineData(0, true, "2026-11-30", null, "DAP", null)]
    public async Task DraftDefaultsDeliveryDateAndTermsOnlyWhenCustomerOrdersAgree(
        int secondOrderDayOffset, bool secondOrderHasDate, string? expectedTimeOfDelivery,
        string? firstTerms, string? secondTerms, string? expectedTerms)
    {
        using var context = CreateContext();
        SeedSelection(context, new Customer { Id = 1 });
        var firstOrderDate = new DateTime(2026, 11, 30, 8, 0, 0);
        context.CustomerOrders.Local.Single().DeliveryDate = firstOrderDate;
        context.CustomerOrders.Local.Single().TermsOfDelivery = firstTerms;
        context.CustomerOrders.Add(new CustomerOrder
        {
            Id = 11,
            CustomerId = 1,
            SalesPrice = 10,
            DeliveryDate = secondOrderHasDate
                ? firstOrderDate.Date.AddDays(secondOrderDayOffset).AddHours(17)
                : null,
            TermsOfDelivery = secondTerms,
        });
        context.DeliveryToCustomers.Add(new DeliveryToCustomer { Id = 31, CustomerOrderId = 11, NrOfItems = 1 });
        await context.SaveChangesAsync();

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(new CreateInvoiceDraftFromSelectionRequest
        {
            CustomerId = 1,
            Deliveries =
            [
                new() { Id = 30, Type = "DeliveryToCustomer" },
                new() { Id = 31, Type = "DeliveryToCustomer" },
            ],
        }));

        Assert.Equal(expectedTimeOfDelivery, draft.TimeOfDelivery);
        Assert.Equal(expectedTerms, draft.TermsOfDelivery);
    }

    [Theory]
    [InlineData(false, false, false, "3000", "3500")]
    [InlineData(true, false, false, "3100", "3510")]
    [InlineData(false, true, false, "3200", "3520")]
    [InlineData(true, true, false, "3100", "3510")]
    [InlineData(false, false, true, "3999", "3999")]
    [InlineData(true, false, true, "3999", "3999")]
    [InlineData(false, true, true, "3999", "3999")]
    public async Task DraftAssignsLegacyAccountsAndPreservesThemWhenSaved(
        bool eu, bool export, bool overrides, string deliveryAccount, string costAccount)
    {
        using var context = CreateContext();
        SeedSelection(context, new Customer
        {
            Id = 1, EU = eu, Export = export, VAT = true,
            AccountNrAccountsReceivable = overrides ? 1599 : null,
            AccountNrEarnings = overrides ? 3999 : null,
        });
        context.Accounts.Add(new Account { Id = 10, InvoicePayMethodId = "1,3", AccountNr = 1510 });
        context.PriceTypes.AddRange(
            new PriceType
            {
                Id = 1,
                Account = new Account { Id = 11, AccountNr = 3000, AccountNrEu = 3100, AccountNrExport = 3200 },
            },
            new PriceType { Id = 12, Account = new Account { Id = 12, AccountNr = 3740, AccountNrEu = 3741 } },
            new PriceType { Id = 13, Account = new Account { Id = 13, AccountNr = 2611, AccountNrExport = 2612 } });
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var draft = GetDraft(await controller.CreateDraftFromSelection(Selection()));

        Assert.Equal(overrides ? "1599" : "1510", draft.Account);
        Assert.Equal(4, draft.InvoiceRows.Count);
        Assert.All(draft.InvoiceRows.Where(x => x.PriceTypeId == 1),
            row => Assert.Equal(deliveryAccount, row.AccountNr));
        Assert.Equal(costAccount, Assert.Single(draft.InvoiceRows, x => x.OrderCostId == 20).AccountNr);
        Assert.Equal("2611", Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 13).AccountNr);
        Assert.Equal("3740", Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 12).AccountNr);
        Assert.Equal(10d, Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 1).Sum);
        Assert.Equal(2.5d, Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 2).Sum);
        Assert.Equal(3.12d, Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 13).Sum);
        Assert.Equal(0.38d, Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 12).Sum);
        Assert.Empty(draft.InvoiceAccountRows);

        var saved = await controller.Create(new CreateInvoiceRequest
        {
            CustomerId = draft.CustomerId,
            Account = draft.Account,
            InvoiceRows = draft.InvoiceRows.Select(row => new InvoiceRowMutationRequest
            {
                AccountNr = row.AccountNr, PriceTypeId = row.PriceTypeId,
                DeliveryToCustomerId = row.DeliveryToCustomerId,
                DeliveryFromStockId = row.DeliveryFromStockId, OrderCostId = row.OrderCostId,
                SortOrder = row.SortOrder, Sum = row.Sum,
            }).ToList(),
            InvoiceAccountRows = draft.InvoiceAccountRows.Select(row => new InvoiceAccountRowMutationRequest
            {
                AccountNr = row.AccountNr, Sum = row.Sum,
            }).ToList(),
        });
        var invoice = Assert.IsType<InvoiceDto>(Assert.IsType<CreatedAtActionResult>(saved.Result).Value);
        context.ChangeTracker.Clear();
        var persisted = await context.Invoices.Include(x => x.InvoiceRows)
            .Include(x => x.InvoiceAccountRows).SingleAsync(x => x.Id == invoice.Id);
        Assert.Equal(draft.Account, persisted.Account);
        Assert.Equal(draft.InvoiceRows.Select(x => x.AccountNr),
            persisted.InvoiceRows.OrderBy(x => x.SortOrder).Select(x => x.AccountNr));
        Assert.Equal(draft.InvoiceAccountRows.Select(x => x.AccountNr).OrderBy(x => x),
            persisted.InvoiceAccountRows.Select(x => x.AccountNr).OrderBy(x => x));
    }

    [Theory]
    [InlineData("DeliveryToCustomer", false)]
    [InlineData("DeliveryFromStock", false)]
    [InlineData("DeliveryFromStock", true)]
    public async Task DraftSupportsEitherDeliveryTypeAndCostsOnly(string deliveryType, bool costsOnly)
    {
        using var context = CreateContext();
        SeedSelection(context, new Customer { Id = 1, AccountNrEarnings = 3999 });
        await context.SaveChangesAsync();
        var selection = Selection();
        selection.Deliveries = costsOnly ? [] : [new() { Id = 30, Type = deliveryType }];

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(selection));

        Assert.Equal(costsOnly ? 3 : 4, draft.InvoiceRows.Count);
        Assert.All(draft.InvoiceRows.Where(x => x.Calculate), row => Assert.Equal("3999", row.AccountNr));
        if (!costsOnly)
        {
            var delivery = Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 1);
            Assert.Equal(deliveryType == "DeliveryFromStock" ? 30 : null, delivery.DeliveryFromStockId);
            Assert.Equal(deliveryType == "DeliveryToCustomer" ? 30 : null, delivery.DeliveryToCustomerId);
        }
    }

    [Fact]
    public async Task MissingMarketAccountsDoNotFallBackToDomesticOrAnotherCost()
    {
        using var context = CreateContext();
        SeedSelection(context, new Customer { Id = 1, EU = true });
        context.PriceTypes.Add(new PriceType { Id = 1, Account = new Account { Id = 11, AccountNr = 3000 } });
        context.Costs.Local.Single().AccountEU = null;
        await context.SaveChangesAsync();

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(Selection()));

        Assert.Null(draft.Account);
        Assert.All(draft.InvoiceRows, row => Assert.Null(row.AccountNr));
        Assert.All(draft.InvoiceAccountRows, row => Assert.Null(row.AccountNr));
    }

    [Theory]
    [InlineData("DeliveryToCustomer", false)]
    [InlineData("DeliveryFromStock", false)]
    [InlineData("DeliveryToCustomer", true)]
    public async Task SingleOrderDraftCopiesOrderReferenceAndOrderNumbers(string deliveryType, bool costsOnly)
    {
        using var context = CreateContext();
        SeedSelection(context, new Customer { Id = 1, Reference = "Customer contact" });
        var order = context.CustomerOrders.Local.Single();
        order.OurReference = "Order salesperson";
        order.YourReference = "Order contact";
        order.YourOrderNr = "PO-123";
        order.CustomerOrderNr = "CO-501-2";
        await context.SaveChangesAsync();
        var selection = Selection();
        selection.Deliveries = costsOnly ? [] : [new() { Id = 30, Type = deliveryType }];

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(selection));

        Assert.Equal("Order salesperson", draft.OurReference);
        Assert.Equal("PO-123", draft.YourOrderNr);
        Assert.Equal("CO-501-2", draft.OurOrderNrFreeInvoice);
        Assert.Equal("Customer contact", draft.YourReference);
        Assert.Empty(await context.Invoices.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DraftWithoutExactlyOneOrderKeepsExistingHeaderDefaults(bool multipleOrders)
    {
        using var context = CreateContext();
        SeedSelection(context, new Customer { Id = 1, Reference = "Customer contact" });
        var order = context.CustomerOrders.Local.Single();
        order.OurReference = "First salesperson";
        order.YourOrderNr = "PO-123";
        order.CustomerOrderNr = "CO-501";
        var selection = Selection();
        if (multipleOrders)
        {
            context.CustomerOrders.Add(new CustomerOrder
            {
                Id = 11, CustomerId = 1, OurReference = "Second salesperson",
                YourOrderNr = "PO-456", CustomerOrderNr = "CO-502",
            });
            context.OrderCosts.Local.Single().CustomerOrderId = 11;
        }
        else
        {
            selection.Deliveries = [];
            selection.OrderCostIds = [];
        }
        await context.SaveChangesAsync();

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(selection));

        Assert.Null(draft.OurReference);
        Assert.Null(draft.YourOrderNr);
        Assert.Null(draft.OurOrderNrFreeInvoice);
        Assert.Equal("Customer contact", draft.YourReference);
    }

    [Theory]
    [InlineData("DeliveryToCustomer")]
    [InlineData("DeliveryFromStock")]
    public async Task ProductRowsUseLegacyMultilineText(string deliveryType)
    {
        using var context = CreateContext();
        SeedProductTextSource(context);
        await context.SaveChangesAsync();

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(ProductSelection(deliveryType)));

        var row = Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 1);
        Assert.Equal("Printed box\nErt ordernr: PO-123\nVårt ordernr: CO-501-2\nVikt: 1 kg", NormalizeLines(row.Text));
        Assert.Null(row.Weight);
        Assert.Null(row.Text2);
        Assert.Equal("Setup, order CO-501-2", Assert.Single(draft.InvoiceRows, x => x.OrderCostId == 20).Text);
    }

    [Theory]
    [InlineData(0, null, "PO-123")]
    [InlineData(1, "TRP-456", "TRP-456")]
    [InlineData(1, "  ", "PO-123")]
    [InlineData(2, "TRP-456", "PO-123")]
    public async Task DirectProductTextUsesOnlyAnUnambiguousNonblankTransportOrderNumber(
        int matchingRecords, string? transportNumber, string expectedNumber)
    {
        using var context = CreateContext();
        SeedProductTextSource(context);
        for (var i = 0; i < matchingRecords; i++)
        {
            context.TransportOrderDeliveries.Add(new TransportOrderDelivery
            {
                Id = 100 + i, DeliveryToCustomerId = 30, CustomerYourOrderNr = transportNumber,
            });
        }
        context.TransportOrderDeliveries.Add(new TransportOrderDelivery
        {
            Id = 200, DeliveryToStockId = 30, CustomerYourOrderNr = "Unrelated delivery",
        });
        await context.SaveChangesAsync();

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(ProductSelection("DeliveryToCustomer")));

        Assert.Equal($"Printed box\nErt ordernr: {expectedNumber}\nVårt ordernr: CO-501-2\nVikt: 1 kg",
            NormalizeLines(Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 1).Text));
    }

    [Theory]
    [InlineData(0, null, "PO-123")]
    [InlineData(1, "CALL-456", "CALL-456")]
    [InlineData(1, null, "")]
    [InlineData(2, "CALL-456", "PO-123")]
    public async Task StockProductTextUsesNoteFromExactlyOneDistinctCallOff(
        int callOffCount, string? note, string expectedNumber)
    {
        using var context = CreateContext();
        SeedProductTextSource(context);
        for (var i = 0; i < callOffCount; i++)
        {
            context.CallOffs.Add(new CallOff
            {
                Id = 100 + i, Note = note,
                CallOffDeliveries =
                [
                    new() { Id = 200 + i * 2, DeliveryFromStockId = 30 },
                    new() { Id = 201 + i * 2, DeliveryFromStockId = 30 },
                ],
            });
        }
        await context.SaveChangesAsync();

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(ProductSelection("DeliveryFromStock")));

        Assert.Equal($"Printed box\nErt ordernr: {expectedNumber}\nVårt ordernr: CO-501-2\nVikt: 1 kg",
            NormalizeLines(Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 1).Text));
    }

    [Theory]
    [InlineData("DeliveryToCustomer", false)]
    [InlineData("DeliveryToCustomer", true)]
    [InlineData("DeliveryFromStock", false)]
    [InlineData("DeliveryFromStock", true)]
    public async Task ProductTextUsesStoredCallOffTextAndCustomerLanguage(string deliveryType, bool translated)
    {
        using var context = CreateContext();
        SeedProductTextSource(context);
        context.Customers.Local.Single().LanguageId = 1;
        context.Languages.Add(new Language { Id = 1, LanguageCode = "EN" });
        context.TranslationItems.Add(new TranslationItem { Id = 1, TranslationCode = -59, LangCode = "DE", Translation = "Wrong language" });
        if (translated)
        {
            context.TranslationItems.Add(new TranslationItem { Id = 2, TranslationCode = -59, LangCode = "EN", Translation = "Call-off" });
        }
        context.DeliveryToCustomers.Local.Single().CallOff = "AV-42";
        context.DeliveryFromStocks.Local.Single().CallOff = "AV-42";
        context.CallOffs.Add(new CallOff
        {
            Id = 999, Note = "PO-123",
            CallOffDeliveries = [new() { Id = 100, DeliveryFromStockId = 30 }],
        });
        await context.SaveChangesAsync();

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(ProductSelection(deliveryType)));

        Assert.Equal($"Printed box\nErt ordernr: PO-123\nVårt ordernr: CO-501-2\n{(translated ? "Call-off" : "Avrop")} AV-42\nVikt: 1 kg",
            NormalizeLines(Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 1).Text));
    }

    [Theory]
    [InlineData("DeliveryToCustomer", false, true)]
    [InlineData("DeliveryToCustomer", true, true)]
    [InlineData("DeliveryToCustomer", true, false)]
    [InlineData("DeliveryFromStock", false, true)]
    [InlineData("DeliveryFromStock", true, true)]
    [InlineData("DeliveryFromStock", true, false)]
    public async Task SlattTextSuppressesOrderNumberOnlyWhenItsSameTypeParentIsSelected(
        string deliveryType, bool includeParent, bool sameTypeParent)
    {
        using var context = CreateContext();
        SeedProductTextSource(context);
        context.DeliveryToCustomers.Add(new DeliveryToCustomer
        {
            Id = 31, CustomerOrderId = 10, ParentDeliveryId = 30, IsSlattPallet = true, NrOfItems = 2500,
        });
        context.DeliveryFromStocks.Add(new DeliveryFromStock
        {
            Id = 31, CustomerOrderId = 10, ParentDeliveryId = 30, IsSlattPallet = true, NrOfItems = 2500, InventoryId = 1000,
        });
        await context.SaveChangesAsync();
        var selection = ProductSelection(deliveryType);
        selection.Deliveries = [new() { Id = 31, Type = deliveryType }];
        if (includeParent)
        {
            var otherType = deliveryType == "DeliveryToCustomer" ? "DeliveryFromStock" : "DeliveryToCustomer";
            selection.Deliveries.Add(new() { Id = 30, Type = sameTypeParent ? deliveryType : otherType });
        }

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(selection));

        var child = Assert.Single(draft.InvoiceRows, x => deliveryType == "DeliveryToCustomer"
            ? x.DeliveryToCustomerId == 31 : x.DeliveryFromStockId == 31);
        var expectedNumber = includeParent && sameTypeParent ? "" : "PO-123";
        Assert.Equal($"Printed box\nErt ordernr: {expectedNumber}\nVårt ordernr: CO-501-2\nVikt: 1\u00a0250 kg", NormalizeLines(child.Text));
        Assert.Null(child.Weight);
    }

    [Theory]
    [InlineData(500, 2501d, "1\u00a0251")]
    [InlineData(1, 499d, "0")]
    [InlineData(null, 1000d, "0")]
    [InlineData(1000, null, "0")]
    public async Task ProductTextFormatsPerDeliveryWeightWithLegacyRoundingAndMissingValues(
        int? weightPer1000, double? quantity, string expectedWeight)
    {
        using var context = CreateContext();
        SeedProductTextSource(context);
        context.Products.Local.Single().NetWeightPer1000 = weightPer1000;
        context.DeliveryToCustomers.Local.Single().NrOfItems = quantity;
        await context.SaveChangesAsync();

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(ProductSelection("DeliveryToCustomer")));

        Assert.Equal($"Printed box\nErt ordernr: PO-123\nVårt ordernr: CO-501-2\nVikt: {expectedWeight} kg",
            NormalizeLines(Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 1).Text));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingProductRelationshipsKeepAvailableTextWithoutFailing(bool missingQuotation)
    {
        using var context = CreateContext();
        SeedProductTextSource(context);
        if (missingQuotation)
        {
            context.CustomerOrders.Local.Single().QuotationId = null;
        }
        else
        {
            context.Calculations.Local.Single().ProductId = null;
            context.Calculations.Local.Single().Product = null;
        }
        await context.SaveChangesAsync();

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(ProductSelection("DeliveryToCustomer")));

        Assert.Equal("Printed box\nErt ordernr: PO-123\nVårt ordernr: CO-501-2\nVikt: 0 kg",
            NormalizeLines(Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 1).Text));
    }

    [Fact]
    public async Task DeliveryWithoutCustomerOrderKeepsLegacyLabelsAndAvailableCallOff()
    {
        using var context = CreateContext();
        context.Customers.Add(new Customer { Id = 1 });
        context.SupplierOrders.Add(new SupplierOrder { Id = 50, CustomerId = 1 });
        context.DeliveryToCustomers.Add(new DeliveryToCustomer
        {
            Id = 30, SupplierOrderId = 50, CallOff = "AV-42", NrOfItems = 1000,
        });
        await context.SaveChangesAsync();
        var selection = ProductSelection("DeliveryToCustomer");
        selection.OrderCostIds = [];

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(selection));

        Assert.Equal("Ert ordernr: \nVårt ordernr: \nAvrop AV-42\nVikt: 0 kg",
            NormalizeLines(Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 1).Text));
    }

    [Fact]
    public async Task GeneratedProductTextAndSubsequentManualEditsSurviveSaving()
    {
        using var context = CreateContext();
        SeedProductTextSource(context);
        await context.SaveChangesAsync();
        var controller = CreateController(context);
        var draft = GetDraft(await controller.CreateDraftFromSelection(ProductSelection("DeliveryToCustomer")));
        var product = Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 1);

        var created = Assert.IsType<InvoiceDto>(Assert.IsType<CreatedAtActionResult>((await controller.Create(new CreateInvoiceRequest
        {
            CustomerId = 1,
            InvoiceRows = [new() { PriceTypeId = 1, DeliveryToCustomerId = 30, Text = product.Text }],
        })).Result).Value);
        context.ChangeTracker.Clear();
        var reloaded = GetDraft(await controller.GetById(created.Id));
        Assert.Equal(product.Text, Assert.Single(reloaded.InvoiceRows).Text);

        var editedText = product.Text + "\nManual note";
        Assert.IsType<OkObjectResult>((await controller.Update(created.Id, new UpdateInvoiceRequest
        {
            CustomerId = 1,
            InvoiceRows = [new() { PriceTypeId = 1, DeliveryToCustomerId = 30, Text = editedText }],
        })).Result);
        context.ChangeTracker.Clear();
        Assert.Equal(editedText, Assert.Single(GetDraft(await controller.GetById(created.Id)).InvoiceRows).Text);
    }

    [Theory]
    [InlineData(false, "Setup translated, order CO-501-2")]
    [InlineData(true, "001 Setup\nPrinted box, order CO-501-2\nErt onr: PO-123")]
    public async Task CostTextUsesLegacyMixedTranslationOrOriginalCostOnlyLayout(bool costsOnly, string expected)
    {
        using var context = CreateContext();
        SeedProductTextSource(context);
        context.Costs.Local.Single().Name = "001 Setup";
        context.Costs.Local.Single().TranslationCodeInvoiceRow = 44;
        context.Customers.Local.Single().Language = new Language { Id = 5, LanguageCode = "EN" };
        context.Languages.Add(context.Customers.Local.Single().Language!);
        context.TranslationItems.Add(new TranslationItem { TranslationCode = 44, LangCode = "EN", Translation = "Setup translated" });
        await context.SaveChangesAsync();
        var selection = Selection();
        if (costsOnly) selection.Deliveries = [];

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(selection));

        var row = Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 2);
        Assert.Equal(expected, NormalizeLines(row.Text));
        Assert.Equal("40", row.PriceKey);
        Assert.True(row.CompareWithOrder);
        Assert.Null(row.UnitId);
    }

    [Theory]
    [InlineData(false, -1d, 2.55555d)]
    [InlineData(true, 1.5d, 3.833325d)]
    public async Task CostsUseTheirOwnQuantitySemanticsWithoutProductUnitDivision(bool quantityCost, double expectedQuantity, double expectedSum)
    {
        using var context = CreateContext();
        SeedSelection(context, new Customer { Id = 1 });
        context.CustomerOrders.Local.Single().Unit = new Unit { Id = 2, Multiplicator = 1000 };
        context.Units.Add(context.CustomerOrders.Local.Single().Unit!);
        context.Costs.Local.Single().IsNrOf = quantityCost;
        context.OrderCosts.Local.Single().NrOf = 1.5m;
        context.OrderCosts.Local.Single().OutPrice = 2.55555m;
        await context.SaveChangesAsync();

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(Selection()));

        var row = Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 2);
        Assert.Equal(expectedQuantity, row.NrOf);
        Assert.Equal(expectedSum, row.Sum!.Value, 8);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AutomaticCostsIncludeRecurringDmtAndRecalculatePercentageWithoutMutatingSource(bool percentage)
    {
        using var context = CreateContext();
        SeedSelection(context, new Customer { Id = 1 });
        var order = context.CustomerOrders.Local.Single();
        order.Unit = new Unit { Id = 2, Multiplicator = 1000 };
        order.SelectedCalculationRow = new CalculationRow { Id = 50, Freight = 40, Edition = 2000 };
        context.Units.Add(order.Unit);
        context.CalculationRows.Add(order.SelectedCalculationRow);
        var cost = context.Costs.Local.Single();
        cost.IsNrOf = false;
        cost.DmtPercent = percentage ? 10 : null;
        cost.DmtFixed = percentage ? null : 15;
        context.InvoiceRows.Add(new InvoiceRow { Id = 100, InvoiceId = 999, OrderCostId = 20, PriceTypeId = 2 });
        context.OrderCosts.AddRange(
            new OrderCost { Id = 21, CustomerOrderId = 10, CostId = 40, OutPrice = 5, DoDebit = false },
            new OrderCost { Id = 22, CustomerOrderId = 10, CostId = 40, OutPrice = 5, DoDebit = true, DoInvoiceSeparately = true },
            new OrderCost { Id = 23, CustomerOrderId = 10, CostId = 40, OutPrice = 5, DoDebit = true, DoInvoiceSeparatelyImmediately = true });
        await context.SaveChangesAsync();
        var selection = Selection();
        selection.OrderCostIds = [];

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(selection));

        var row = Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 2);
        Assert.Equal(20, row.OrderCostId);
        Assert.Equal(percentage ? 8d : 2.5d, row.Sum);
        context.ChangeTracker.Clear();
        Assert.Equal(2.5m, (await context.OrderCosts.FindAsync(20))!.OutPrice);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingSalesPriceOrDmtCalculationReturnsVisibleValidation(bool missingDmtSource)
    {
        using var context = CreateContext();
        SeedSelection(context, new Customer { Id = 1 });
        context.OrderCosts.Local.Single().OutPrice = null;
        context.OrderCosts.Local.Single().InPrice = 99;
        if (missingDmtSource) context.Costs.Local.Single().DmtPercent = 10;
        await context.SaveChangesAsync();

        var result = await CreateController(context).CreateDraftFromSelection(Selection());

        var error = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains(missingDmtSource ? "DMT-kostnad" : "försäljningspris",
            System.Text.Json.JsonSerializer.SerializeToElement(error.Value).GetProperty("message").GetString());
        Assert.Empty(await context.Invoices.ToListAsync());
    }

    [Fact]
    public async Task OrdinaryInvoicedCostsAreNotAutomaticallyRepeatedAndExplicitSelectionIsRejected()
    {
        using var context = CreateContext();
        SeedSelection(context, new Customer { Id = 1 });
        context.InvoiceRows.Add(new InvoiceRow { Id = 100, InvoiceId = 999, OrderCostId = 20, PriceTypeId = 2 });
        await context.SaveChangesAsync();
        var selection = Selection();
        selection.OrderCostIds = [];
        var controller = CreateController(context);

        Assert.DoesNotContain(GetDraft(await controller.CreateDraftFromSelection(selection)).InvoiceRows, x => x.PriceTypeId == 2);
        selection.OrderCostIds = [20];
        Assert.IsType<BadRequestObjectResult>((await controller.CreateDraftFromSelection(selection)).Result);
    }

    [Theory]
    [InlineData(false, null, 2.5d, 0d, 0.5d)]
    [InlineData(false, 25, -2.5d, 0d, -0.5d)]
    [InlineData(true, 12, 12.5d, 1.5d, 0d)]
    [InlineData(true, 0, 12.5d, 3.12d, 0.38d)]
    [InlineData(true, null, -12.5d, -3.12d, -0.38d)]
    [InlineData(true, 25, 4.06d, 1.02d, -0.08d)]
    [InlineData(true, 25, -4.06d, -1.02d, 0.08d)]
    public async Task VatAndRoundingRespectCustomerAndSignedExemptTotals(bool vat, int? rate, double amount, double expectedVat, double expectedRounding)
    {
        using var context = CreateContext();
        SeedSelection(context, new Customer { Id = 1, VAT = vat, VATRate = (short?)rate });
        context.OrderCosts.Local.Single().OutPrice = (decimal)amount;
        await context.SaveChangesAsync();
        var selection = Selection();
        selection.Deliveries = [];

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(selection));

        Assert.Equal(expectedVat, Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 13).Sum);
        var rounding = Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 12);
        Assert.Equal("Öresutjämning", rounding.Text);
        Assert.Equal(expectedRounding, rounding.Sum);
        Assert.Equal(vat ? (rate.GetValueOrDefault() == 0 ? 0.25 : rate!.Value / 100d) : 0, draft.VatRate);
    }

    [Theory]
    [InlineData(false, false, false, "3700")]
    [InlineData(true, false, false, "3710")]
    [InlineData(false, true, false, "3720")]
    [InlineData(true, false, true, "3999")]
    public async Task AutomaticPalletsUseTranslationQuantityAndMarketAccount(bool eu, bool export, bool earningsOverride, string expectedAccount)
    {
        using var context = CreateContext();
        SeedSelection(context, new Customer
        {
            Id = 1, EU = eu, Export = export, AccountNrEarnings = earningsOverride ? 3999 : null,
            Language = new Language { Id = 5, LanguageCode = "EN" },
        });
        var order = context.CustomerOrders.Local.Single();
        order.EurPallet = true;
        order.EurPalletValue = 15;
        order.PalletFormatId = 6;
        order.SupplierOrder = new SupplierOrder { Id = 50, Supplier = new Supplier { Id = 60, CostCenter = "CC-60" } };
        context.SupplierOrders.Add(order.SupplierOrder);
        context.DeliveryToCustomers.Local.Single().NrOfPallets = 3;
        context.PalletFormats.Add(new PalletFormat { Id = 6, TranslationCode = 77 });
        context.TranslationItems.Add(new TranslationItem { TranslationCode = 77, LangCode = "EN", Translation = "EUR pallet" });
        context.PriceTypes.Add(new PriceType { Id = 107, Account = new Account { Id = 107, AccountNr = 3700, AccountNrEu = 3710, AccountNrExport = 3720 } });
        await context.SaveChangesAsync();

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(Selection()));

        var pallet = Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 107);
        Assert.Equal("EUR pallet", pallet.Text);
        Assert.Equal(3d, pallet.NrOf);
        Assert.Equal(45d, pallet.Sum);
        Assert.Equal("EURPallet", pallet.PriceKey);
        Assert.Equal(expectedAccount, pallet.AccountNr);
        Assert.Equal("CC-60", pallet.CostCenter);
        Assert.False(context.DeliveryToCustomers.Local.Single().IsPalletInvoicedSeparately);
    }

    [Theory]
    [InlineData("palletsNow", "DeliveryToCustomer", 30d)]
    [InlineData("palletsFuture", "DeliveryFromStock", 45d)]
    public async Task SeparatePalletModesSelectOnlyTheirPeriodAndMarkOnlyOnSave(string mode, string expectedType, double expectedAmount)
    {
        using var context = CreateContext();
        SeedProductTextSource(context);
        context.Customers.Local.Single().InvoicePalletsSeparately = true;
        context.CustomerOrders.Local.Single().EurPalletValue = 15;
        var direct = context.DeliveryToCustomers.Local.Single();
        direct.DeliveryStatus = (int)DeliveryStatus.Delivered;
        direct.DeliveryDate = Econosys.Api.Common.SwedishTime.Now.Date;
        direct.NrOfPallets = 2;
        var stock = context.DeliveryFromStocks.Local.Single();
        stock.DeliveryStatus = (int)DeliveryStatus.Delivered;
        stock.DeliveryDate = Econosys.Api.Common.SwedishTime.Now.Date.AddDays(1);
        stock.NrOfPallets = 3;
        await context.SaveChangesAsync();
        var controller = CreateController(context);
        var selection = new CreateInvoiceDraftFromSelectionRequest { CustomerId = 1, InvoiceMode = mode };

        var draft = GetDraft(await controller.CreateDraftFromSelection(selection));

        Assert.Equal(2, draft.UnitId);
        Assert.DoesNotContain(draft.InvoiceRows, x => x.PriceTypeId is 1 or 2 or 108);
        Assert.Empty(draft.InvoiceAccountRows);
        var pallet = Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 107);
        Assert.Equal(expectedType == "DeliveryToCustomer" ? 30 : null, pallet.DeliveryToCustomerId);
        Assert.Equal(expectedType == "DeliveryFromStock" ? 30 : null, pallet.DeliveryFromStockId);
        Assert.Equal("EUR-pall PO-123/CO-501-2", pallet.Text);
        Assert.Equal(expectedAmount, pallet.Sum);
        Assert.False(direct.IsPalletInvoicedSeparately);
        Assert.False(stock.IsPalletInvoicedSeparately);
        var request = new CreateInvoiceRequest
        {
            CustomerId = 1,
            InvoiceRows = [new() { PriceTypeId = 107, DeliveryToCustomerId = pallet.DeliveryToCustomerId, DeliveryFromStockId = pallet.DeliveryFromStockId, Sum = pallet.Sum }],
        };
        var created = Assert.IsType<InvoiceDto>(Assert.IsType<CreatedAtActionResult>((await controller.Create(request)).Result).Value);
        Assert.Equal(expectedType == "DeliveryToCustomer", direct.IsPalletInvoicedSeparately);
        Assert.Equal(expectedType == "DeliveryFromStock", stock.IsPalletInvoicedSeparately);
        Assert.IsType<ConflictObjectResult>((await controller.Create(request)).Result);
        Assert.IsType<OkObjectResult>((await controller.Update(created.Id, new UpdateInvoiceRequest { CustomerId = 1, InvoiceRows = [] })).Result);
        Assert.False(direct.IsPalletInvoicedSeparately);
        Assert.False(stock.IsPalletInvoicedSeparately);
        selection.Deliveries = [];
        GetDraft(await controller.CreateDraftFromSelection(selection));
        var replacement = Assert.IsType<InvoiceDto>(Assert.IsType<CreatedAtActionResult>((await controller.Create(request)).Result).Value);
        Assert.IsType<NoContentResult>(await controller.Delete(replacement.Id));
        Assert.False(direct.IsPalletInvoicedSeparately);
        Assert.False(stock.IsPalletInvoicedSeparately);
    }

    [Fact]
    public async Task PalletCustomersRemainSelectableAfterTheirProductsHaveBeenInvoiced()
    {
        using var context = CreateContext();
        SeedSelection(context, new Customer { Id = 1, Name = "Pallet customer", InvoicePalletsSeparately = true });
        var delivery = context.DeliveryToCustomers.Local.Single();
        delivery.DeliveryDate = Econosys.Api.Common.SwedishTime.Now.Date;
        delivery.DeliveryStatus = (int)DeliveryStatus.Delivered;
        delivery.NrOfPallets = 2;
        context.InvoiceRows.Add(new InvoiceRow { Id = 100, InvoiceId = 999, DeliveryToCustomerId = 30, PriceTypeId = 1 });
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var grouped = Assert.IsAssignableFrom<IReadOnlyList<InvoiceDeliveriesAndOrderCostsGroupedCustomerDto>>(
            Assert.IsType<OkObjectResult>((await controller.GetInvoiceSourceGrouped()).Result).Value);
        var customer = Assert.Single(grouped);
        Assert.Equal("Pallet customer", customer.CustomerName);
        Assert.Empty(customer.Deliveries);
        Assert.Empty(customer.OrderCosts);
        var list = Assert.IsAssignableFrom<IReadOnlyList<InvoiceDeliveriesAndOrderCostsListRowDto>>(
            Assert.IsType<OkObjectResult>((await controller.GetInvoiceSourceList()).Result).Value);
        Assert.Equal("PalletsNow", Assert.Single(list).Type);
        Assert.Equal(new InvoicePalletSourceDto("PalletsNow", 2), Assert.Single(customer.Pallets));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PalletSourceBucketsRemainSelectableAlongsideProductsAndExcludeAlreadyBilledPallets(bool productsBilled)
    {
        using var context = CreateContext();
        SeedSelection(context, new Customer { Id = 1, Name = "Pallet customer", InvoicePalletsSeparately = true });
        context.CustomerOrders.Local.Single().CustomerName = "Pallet customer";
        var today = Econosys.Api.Common.SwedishTime.Now.Date;
        var direct = context.DeliveryToCustomers.Local.Single();
        direct.DeliveryDate = today.AddHours(20);
        direct.DeliveryStatus = (int)DeliveryStatus.Delivered;
        direct.NrOfPallets = 2;
        var stock = context.DeliveryFromStocks.Local.Single();
        stock.DeliveryDate = today.AddDays(1);
        stock.DeliveryStatus = (int)DeliveryStatus.Delivered;
        stock.NrOfPallets = 3;
        context.DeliveryToCustomers.AddRange(
            new DeliveryToCustomer { Id = 31, CustomerOrderId = 10, DeliveryStatus = 2, DeliveryDate = today.AddDays(-1), NrOfPallets = 4 },
            new DeliveryToCustomer { Id = 32, CustomerOrderId = 10, DeliveryStatus = 2, DeliveryDate = today, NrOfPallets = 100 },
            new DeliveryToCustomer { Id = 33, CustomerOrderId = 10, DeliveryStatus = 1, DeliveryDate = today, NrOfPallets = 100 });
        context.InvoiceRows.AddRange(
            new InvoiceRow { Id = 100, InvoiceId = 999, DeliveryFromStockId = 30, PriceTypeId = 1 },
            new InvoiceRow { Id = 101, InvoiceId = 999, DeliveryToCustomerId = 32, PriceTypeId = 107 });
        if (productsBilled)
            context.InvoiceRows.Add(new InvoiceRow { Id = 102, InvoiceId = 999, DeliveryToCustomerId = 30, PriceTypeId = 1 });
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var grouped = Assert.IsAssignableFrom<IReadOnlyList<InvoiceDeliveriesAndOrderCostsGroupedCustomerDto>>(
            Assert.IsType<OkObjectResult>((await controller.GetInvoiceSourceGrouped()).Result).Value);
        var customer = Assert.Single(grouped);
        Assert.Equal(2, customer.Pallets.Count);
        Assert.Contains(new InvoicePalletSourceDto("PalletsNow", 6), customer.Pallets);
        Assert.Contains(new InvoicePalletSourceDto("PalletsInFuture", 3), customer.Pallets);
        Assert.Equal(!productsBilled, customer.Deliveries.Any(x => x.DeliveryId == 30));
        var list = Assert.IsAssignableFrom<IReadOnlyList<InvoiceDeliveriesAndOrderCostsListRowDto>>(
            Assert.IsType<OkObjectResult>((await controller.GetInvoiceSourceList()).Result).Value);
        Assert.Equal(6m, Assert.Single(list, x => x.Type == "PalletsNow").Quantity);
        Assert.Equal(3m, Assert.Single(list, x => x.Type == "PalletsInFuture").Quantity);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task CombinedInvoicesOmitSeparateOrAlreadyBilledPallets(bool separate, bool invoiced)
    {
        using var context = CreateContext();
        SeedSelection(context, new Customer { Id = 1, InvoicePalletsSeparately = separate });
        context.CustomerOrders.Local.Single().EurPallet = true;
        context.CustomerOrders.Local.Single().EurPalletValue = 15;
        context.DeliveryToCustomers.Local.Single().NrOfPallets = 3;
        context.DeliveryToCustomers.Local.Single().IsPalletInvoicedSeparately = invoiced;
        if (!separate && !invoiced)
        {
            context.InvoiceRows.Add(new InvoiceRow { Id = 100, InvoiceId = 999, DeliveryToCustomerId = 30, PriceTypeId = 107 });
        }
        await context.SaveChangesAsync();

        var draft = GetDraft(await CreateController(context).CreateDraftFromSelection(Selection()));

        Assert.DoesNotContain(draft.InvoiceRows, x => x.PriceTypeId == 107);
    }

    [Fact]
    public async Task FreightIsAddedOncePerCallOffAndCannotBeSavedTwice()
    {
        using var context = CreateContext();
        SeedSelection(context, new Customer { Id = 1 });
        context.DeliveryFromStocks.Add(new DeliveryFromStock { Id = 31, CustomerOrderId = 10, NrOfItems = 2, InventoryId = 1000 });
        context.CallOffs.AddRange(
            new CallOff
            {
                Id = 80, DoDebitFreight = true, FreightCostToDebit = 50,
                CallOffDeliveries = [new() { Id = 90, DeliveryFromStockId = 30 }, new() { Id = 91, DeliveryFromStockId = 31 }],
            },
            new CallOff { Id = 81, DoDebitFreight = false, CallOffDeliveries = [new() { Id = 92, DeliveryFromStockId = 30 }] });
        context.PriceTypes.Add(new PriceType { Id = 108, Account = new Account { Id = 108, AccountNr = 3800 } });
        await context.SaveChangesAsync();
        var selection = new CreateInvoiceDraftFromSelectionRequest
        {
            CustomerId = 1, Deliveries = [new() { Type = "DeliveryFromStock", Id = 30 }, new() { Type = "DeliveryFromStock", Id = 31 }],
        };
        var controller = CreateController(context);

        var draft = GetDraft(await controller.CreateDraftFromSelection(selection));

        var freight = Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 108);
        Assert.Equal("Frakt", freight.Text);
        Assert.Equal(50d, freight.Sum);
        Assert.Equal("3800", freight.AccountNr);
        Assert.Equal("Freight:80", freight.PriceKey);
        var request = new CreateInvoiceRequest { CustomerId = 1, InvoiceRows = [new() { PriceTypeId = 108, PriceKey = freight.PriceKey, Sum = freight.Sum }] };
        var saved = Assert.IsType<InvoiceDto>(Assert.IsType<CreatedAtActionResult>((await controller.Create(request)).Result).Value);
        Assert.IsType<ConflictObjectResult>((await controller.Create(request)).Result);
        Assert.DoesNotContain(GetDraft(await controller.CreateDraftFromSelection(selection)).InvoiceRows, x => x.PriceTypeId == 108);
        Assert.IsType<NoContentResult>(await controller.Delete(saved.Id));
        Assert.Single(GetDraft(await controller.CreateDraftFromSelection(selection)).InvoiceRows, x => x.PriceTypeId == 108);
    }

    [Fact]
    public async Task InventoryAccountingRecalculatesOnPreviewSaveEditAndRemoval()
    {
        using var context = CreateContext();
        SeedSelection(context, new Customer { Id = 1 });
        var inventory = context.Inventories.Local.Single();
        inventory.NoInventoryValue = false;
        inventory.AccountNr = 1400;
        var supplierOrder = new SupplierOrder
        {
            Id = 50, PurchasePrice = 20,
            Unit = new Unit { Id = 60, Multiplicator = 1000 },
            PurchaseCurrency = new Currency { Id = 70, RateStockValue = 10 },
        };
        context.SupplierOrders.Add(supplierOrder);
        context.CustomerOrders.Local.Single().SupplierOrder = supplierOrder;
        context.Accounts.Add(new Account { Id = 19, AccountNr = 4900 });
        await context.SaveChangesAsync();
        var controller = CreateController(context);
        var rows = new List<InvoiceRowMutationRequest> { new() { PriceTypeId = 1, DeliveryFromStockId = 30, NrOf = 2000 } };
        var preview = Assert.IsType<List<InvoiceAccountRowMutationRequest>>(Assert.IsType<OkObjectResult>(
            (await controller.PreviewAccounting(new(1, rows))).Result).Value);
        Assert.Equal(400d, Assert.Single(preview, x => x.AccountNr == "1400").Sum);
        Assert.Equal(-400d, Assert.Single(preview, x => x.AccountNr == "4900").Sum);
        Assert.IsType<BadRequestObjectResult>((await controller.PreviewAccounting(new(2, rows))).Result);
        var saved = Assert.IsType<InvoiceDto>(Assert.IsType<CreatedAtActionResult>((await controller.Create(new CreateInvoiceRequest
        {
            CustomerId = 1, InvoiceRows = rows, InvoiceAccountRows = [new() { Sum = 999 }],
        })).Result).Value);
        Assert.Equal(400d, Assert.Single(saved.InvoiceAccountRows, x => x.AccountNr == "1400").Sum);
        rows[0].NrOf = 3000;
        var edited = GetDraft(await controller.Update(saved.Id, new UpdateInvoiceRequest { CustomerId = 1, InvoiceRows = rows }));
        Assert.Equal(600d, Assert.Single(edited.InvoiceAccountRows, x => x.AccountNr == "1400").Sum);
        var cleared = GetDraft(await controller.Update(saved.Id, new UpdateInvoiceRequest { CustomerId = 1, InvoiceRows = [] }));
        Assert.Empty(cleared.InvoiceAccountRows);
    }

    private static string? NormalizeLines(string? text) => text?.Replace("\r\n", "\n");

    private static void SeedProductTextSource(ApplicationDbContext context)
    {
        SeedSelection(context, new Customer { Id = 1 });
        var order = context.CustomerOrders.Local.Single();
        order.Product = "Printed box";
        order.YourOrderNr = "PO-123";
        order.CustomerOrderNr = "CO-501-2";
        order.QuotationId = 50;
        context.Quotations.Add(new Quotation
        {
            Id = 50,
            Calculation = new Calculation { Id = 60, Product = new Product { Id = 70, NetWeightPer1000 = 500 } },
        });
        context.DeliveryToCustomers.Local.Single().NrOfItems = 2;
        context.DeliveryFromStocks.Local.Single().NrOfItems = 2;
        context.Costs.Local.Single().Name = "Setup";
    }

    private static CreateInvoiceDraftFromSelectionRequest ProductSelection(string deliveryType) => new()
    {
        CustomerId = 1,
        Deliveries = [new() { Id = 30, Type = deliveryType }],
        OrderCostIds = [20],
    };

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void SeedSelection(ApplicationDbContext context, Customer customer)
    {
        context.Customers.Add(customer);
        context.CustomerOrders.Add(new CustomerOrder
        {
            Id = 10, CustomerId = 1, SalesPrice = 10,
        });
        context.DeliveryToCustomers.Add(new DeliveryToCustomer { Id = 30, CustomerOrderId = 10, NrOfItems = 1 });
        context.DeliveryFromStocks.Add(new DeliveryFromStock
        {
            Id = 30, CustomerOrderId = 10, NrOfItems = 1,
            Inventory = new Inventory { Id = 1000, NoInventoryValue = true },
        });
        context.OrderCosts.Add(new OrderCost
        {
            Id = 20, CustomerOrderId = 10, NrOf = 1, OutPrice = 2.5m, DoDebit = true,
            Cost = new Cost { Id = 40, Name = "Setup", IsNrOf = true, AccountDomestic = 3500, AccountEU = 3510, AccountExport = 3520 },
        });
    }

    private static CreateInvoiceDraftFromSelectionRequest Selection() => new()
    {
        CustomerId = 1,
        Deliveries = [new() { Id = 30, Type = "DeliveryToCustomer" }],
        OrderCostIds = [20],
    };

    private static InvoiceDto GetDraft(ActionResult<InvoiceDto> result) =>
        Assert.IsType<InvoiceDto>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static InvoicesController CreateController(ApplicationDbContext context) =>
        new(context, NullLogger<InvoicesController>.Instance, new UserResolution(), new ConfigurationBuilder().Build());

    private sealed class UserResolution(int companyId = 1) : ILegacyUserResolutionService
    {
        public Task<LegacyUser?> ResolveCurrentUserAsync(ClaimsPrincipal user) =>
            Task.FromResult<LegacyUser?>(new LegacyUser { Id = 42, CompanyId = companyId });
    }
}
