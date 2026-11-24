using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Econosys.Api.Tests;

public class InvoiceInventoryAccountingTests
{
    [Fact]
    public async Task CreditReversesInventoryPostingsWithoutChangingTheDeliveredQuantity()
    {
        using var context = CreateContext();
        AddDelivery(context, 1);
        AddAccounts(context);
        await context.SaveChangesAsync();
        var result = await InvoiceInventoryAccounting.BuildAsync(context, [Product(1, 2)], isCredit: true);
        AssertPosting(result, "1460", "Lager ut", -2.25);
        AssertPosting(result, "4990", "Lagerförändring", 2.25);
    }

    [Fact]
    public async Task RoundsEachLineToEvenBeforeAggregatingFallbackAndOverrideAccounts()
    {
        using var context = CreateContext();
        var first = AddDelivery(context, 1);
        first.CustomerOrder!.SupplierOrder!.Unit!.Multiplicator = 2;
        context.DeliveryFromStocks.AddRange(
            new DeliveryFromStock { Id = 2, CustomerOrder = first.CustomerOrder, Inventory = new Inventory { Id = 2, AccountNr = 0 } },
            new DeliveryFromStock { Id = 3, CustomerOrder = first.CustomerOrder, Inventory = new Inventory { Id = 3, AccountNr = 1461 } });
        AddAccounts(context);
        await context.SaveChangesAsync();

        var result = await InvoiceInventoryAccounting.BuildAsync(context, [Product(1, 2), Product(2, 2), Product(3, 4)]);

        Assert.Equal(3, result.Count);
        AssertPosting(result, "1460", "Lager ut", 2.24);
        AssertPosting(result, "1461", "Lager ut", 2.25);
        AssertPosting(result, "4990", "Lagerförändring", -4.49);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task UsesOneForAnUnsetOrZeroMultiplierAndDoesNotRequireDefaultAccountForOverride(int? multiplicator)
    {
        using var context = CreateContext();
        var delivery = AddDelivery(context, 1, new Inventory { Id = 1, AccountNr = 1461 });
        delivery.CustomerOrder!.SupplierOrder!.Unit!.Multiplicator = (short?)multiplicator;
        context.Accounts.Add(new Account { Id = 19, AccountNr = 4990 });
        await context.SaveChangesAsync();

        var result = await InvoiceInventoryAccounting.BuildAsync(context, [Product(1, 2)]);

        Assert.Equal(2, result.Count);
        AssertPosting(result, "1461", "Lager ut", 2.25);
        AssertPosting(result, "4990", "Lagerförändring", -2.25);
    }

    [Fact]
    public async Task SkipsExemptStockWithoutPricingAndIgnoresPalletDirectAndCostRows()
    {
        using var context = CreateContext();
        context.DeliveryFromStocks.Add(new DeliveryFromStock
        {
            Id = 1,
            Inventory = new Inventory { Id = 1, NoInventoryValue = true },
        });
        await context.SaveChangesAsync();

        Assert.Empty(await InvoiceInventoryAccounting.BuildAsync(context, []));
        var result = await InvoiceInventoryAccounting.BuildAsync(context,
        [
            Product(1, null),
            new() { PriceTypeId = 12, DeliveryFromStockId = 2, NrOf = 5 },
            new() { PriceTypeId = 13, DeliveryFromStockId = 2, NrOf = 5 },
            new() { PriceTypeId = 1, DeliveryToCustomerId = 2, NrOf = 5 },
            new() { PriceTypeId = 2, OrderCostId = 2, NrOf = 5 },
            Product(0, 1),
        ]);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData("purchase price", false)]
    [InlineData("purchase price", true)]
    [InlineData("stock valuation rate", false)]
    [InlineData("stock valuation rate", true)]
    [InlineData("quantity", false)]
    [InlineData("quantity", true)]
    public async Task RejectsMissingNumericInputsButAcceptsExplicitZero(string field, bool explicitZero)
    {
        using var context = CreateContext();
        var supplierOrder = AddDelivery(context, 1).CustomerOrder!.SupplierOrder!;
        var row = Product(1, 2);
        double? value = explicitZero ? 0 : null;
        if (field == "purchase price")
            supplierOrder.PurchasePrice = value;
        else if (field == "stock valuation rate")
            supplierOrder.PurchaseCurrency!.RateStockValue = value;
        else
            row.NrOf = value;
        AddAccounts(context);
        await context.SaveChangesAsync();

        if (!explicitZero)
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => InvoiceInventoryAccounting.BuildAsync(context, [row]));
            Assert.Contains(field, exception.Message);
            return;
        }

        var result = await InvoiceInventoryAccounting.BuildAsync(context, [row]);
        Assert.Equal(2, result.Count);
        AssertPosting(result, "1460", "Lager ut", 0);
        AssertPosting(result, "4990", "Lagerförändring", 0);
    }

    [Fact]
    public async Task NetsInventoryAndCounterpartWhenTheirAccountNumbersMatch()
    {
        using var context = CreateContext();
        AddDelivery(context, 1, new Inventory { Id = 1, AccountNr = 4990 });
        AddAccounts(context);
        await context.SaveChangesAsync();

        var result = await InvoiceInventoryAccounting.BuildAsync(context, [Product(1, 2)]);

        Assert.Single(result);
        AssertPosting(result, "4990", "Lager ut", 0);
    }

    [Theory]
    [InlineData("purchase currency")]
    [InlineData("unit")]
    [InlineData("account ID 18")]
    [InlineData("account ID 19")]
    public async Task ThrowsExplicitlyWhenRequiredConfigurationIsMissing(string missing)
    {
        using var context = CreateContext();
        var supplierOrder = AddDelivery(context, 1).CustomerOrder!.SupplierOrder!;
        if (missing == "purchase currency")
            supplierOrder.PurchaseCurrency = null;
        if (missing == "unit")
            supplierOrder.Unit = null;
        if (missing != "account ID 19")
            context.Accounts.Add(new Account { Id = 19, AccountNr = 4990 });
        context.Accounts.Add(new Account { Id = 18, AccountNr = missing == "account ID 18" ? 0 : 1460 });
        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => InvoiceInventoryAccounting.BuildAsync(context, [Product(1, 2)]));

        Assert.Contains(missing, exception.Message);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static DeliveryFromStock AddDelivery(ApplicationDbContext context, int id, Inventory? inventory = null)
    {
        var delivery = new DeliveryFromStock
        {
            Id = id,
            Inventory = inventory,
            NrOfItems = 999,
            CustomerOrder = new CustomerOrder
            {
                SupplierOrder = new SupplierOrder
                {
                    PurchasePrice = 1,
                    PurchaseCurrencyRate = 7,
                    PurchaseCurrency = new Currency { RateStockValue = 1.125, RateToSek = 5 },
                    Unit = new Unit { Multiplicator = 1 },
                },
            },
        };
        context.DeliveryFromStocks.Add(delivery);
        return delivery;
    }

    private static void AddAccounts(ApplicationDbContext context) => context.Accounts.AddRange(
        new Account { Id = 18, AccountNr = 1460 },
        new Account { Id = 19, AccountNr = 4990 });

    private static InvoiceRowMutationRequest Product(int deliveryId, double? quantity) => new()
    {
        PriceTypeId = 1,
        DeliveryFromStockId = deliveryId,
        NrOf = quantity,
    };

    private static void AssertPosting(List<InvoiceAccountRowMutationRequest> rows, string account, string text, double sum)
    {
        var row = Assert.Single(rows, row => row.AccountNr == account);
        Assert.Equal(text, row.Text);
        Assert.Equal(sum, row.Sum);
    }
}
