using System.Security.Claims;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Econosys.Api.Tests;

public class InvoiceDraftTests
{
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
            NullLogger<InvoicesController>.Instance, new UserResolution(7));
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
        Assert.Equal(7, row.CompanyId);
        Assert.Equal(7, accountRow.CompanyId);
        Assert.Equal("3001", accountRow.AccountNr);
        Assert.Equal(100d, accountRow.Sum);
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
            Id = 1, EU = eu, Export = export,
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
        Assert.Equal(3.13d, Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 13).Sum);
        Assert.Equal(0.37d, Assert.Single(draft.InvoiceRows, x => x.PriceTypeId == 12).Sum);
        Assert.Equal(draft.InvoiceRows.Select(x => x.AccountNr), draft.InvoiceAccountRows.Select(x => x.AccountNr));

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
        context.DeliveryFromStocks.Add(new DeliveryFromStock { Id = 30, CustomerOrderId = 10, NrOfItems = 1 });
        context.OrderCosts.Add(new OrderCost
        {
            Id = 20, CustomerOrderId = 10, NrOf = 1, OutPrice = 2.5m,
            Cost = new Cost { Id = 40, AccountDomestic = 3500, AccountEU = 3510, AccountExport = 3520 },
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
        new(context, NullLogger<InvoicesController>.Instance, new UserResolution());

    private sealed class UserResolution(int companyId = 1) : ILegacyUserResolutionService
    {
        public Task<LegacyUser?> ResolveCurrentUserAsync(ClaimsPrincipal user) =>
            Task.FromResult<LegacyUser?>(new LegacyUser { Id = 42, CompanyId = companyId });
    }
}
