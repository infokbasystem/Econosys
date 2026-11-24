using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Econosys.Api.Tests;

public class InvoiceCustomerOrderCompletionTests
{
    [Fact]
    public async Task ListsHeaderAndDeliveryOrdersWithLegacyQuantitiesWithoutDuplicates()
    {
        await using var db = CreateDb();
        db.Inventories.Add(new Inventory { Id = 20, Name = "Lager", IsInventory = true });
        db.SupplierOrders.Add(new SupplierOrder { Id = 10, InventoryId = 20, ProducedEdition = 999 });
        db.CustomerOrders.AddRange(
            new CustomerOrder { Id = 1, CustomerOrderNr = "Header", InvoicingInfo = "Header billing info", Edition = 30, Completed = true, CompletedDate = new DateTime(2026, 11, 1) },
            new CustomerOrder { Id = 2, CustomerOrderNr = "Stock", InvoicingInfo = "Stock billing info", SupplierOrderId = 10, Edition = 200 },
            new CustomerOrder { Id = 3, CustomerOrderNr = "Direct", Edition = 100 },
            new CustomerOrder { Id = 4, CustomerOrderNr = "Unrelated" });
        db.Invoices.Add(new Invoice { Id = 42, CustomerOrderId = 1 });
        db.DeliveryToStocks.Add(new DeliveryToStock { Id = 100, SupplierOrderId = 10, DeliveryStatus = 2, NrOfItems = 120 });
        db.DeliveryFromStocks.AddRange(
            new DeliveryFromStock { Id = 101, CustomerOrderId = 2, DeliveryStatus = 2, NrOfItems = 20 },
            new DeliveryFromStock { Id = 102, CustomerOrderId = 2, DeliveryStatus = 1, NrOfItems = 50 });
        db.DeliveryToCustomers.Add(new DeliveryToCustomer { Id = 103, CustomerOrderId = 3, DeliveryStatus = 2, NrOfItems = 35 });
        db.InvoiceRows.AddRange(
            new InvoiceRow { Id = 1, InvoiceId = 42, DeliveryFromStockId = 101 },
            new InvoiceRow { Id = 2, InvoiceId = 42, DeliveryFromStockId = 101 },
            new InvoiceRow { Id = 3, InvoiceId = 42, DeliveryToCustomerId = 103 });
        await db.SaveChangesAsync();

        var rows = await new InvoiceCustomerOrderCompletionService(db).GetAsync(42, CancellationToken.None);

        Assert.NotNull(rows);
        Assert.Equal(3, rows.Count);
        var header = Assert.Single(rows, row => row.Id == 1);
        Assert.Equal("Header billing info", header.InvoicingInfo);
        Assert.True(header.Completed);
        Assert.Equal(new DateOnly(2026, 11, 1), header.CompletedDate);
        var stock = Assert.Single(rows, row => row.Id == 2);
        Assert.Equal("Stock billing info", stock.InvoicingInfo);
        Assert.Equal("Lager", stock.InventoryName);
        Assert.Equal(120, stock.Edition);
        Assert.Equal(20, stock.Delivered);
        Assert.Equal(20, stock.Invoiced);
        Assert.Equal(100, stock.Remaining);
        var direct = Assert.Single(rows, row => row.Id == 3);
        Assert.Equal(100, direct.Edition);
        Assert.Equal(35, direct.Delivered);
        Assert.Equal(65, direct.Remaining);
    }

    [Fact]
    public async Task UpdatesOnlyLinkedOrdersAndRejectsInvalidBatchesWithoutPartialChanges()
    {
        await using var db = CreateDb();
        db.CustomerOrders.AddRange(new CustomerOrder { Id = 1 }, new CustomerOrder { Id = 2, Completed = true });
        db.Invoices.Add(new Invoice { Id = 42, CustomerOrderId = 1 });
        await db.SaveChangesAsync();
        var service = new InvoiceCustomerOrderCompletionService(db);

        Assert.Null(await service.GetAsync(999, CancellationToken.None));
        Assert.Null(await service.UpdateAsync(999, [], CancellationToken.None));
        Assert.False(await service.UpdateAsync(42,
            [new(1, true, new DateOnly(2026, 11, 23)), new(2, false, null)], CancellationToken.None));
        Assert.False((await db.CustomerOrders.FindAsync(1))!.Completed);
        Assert.False(await service.UpdateAsync(42, [new(1, true, null), new(1, false, null)], CancellationToken.None));
        Assert.True(await service.UpdateAsync(42, [new(1, true, new DateOnly(2026, 11, 23))], CancellationToken.None));
        var order = await db.CustomerOrders.FindAsync(1);
        Assert.True(order!.Completed);
        Assert.Equal(new DateTime(2026, 11, 23), order.CompletedDate);
        Assert.True((await db.CustomerOrders.FindAsync(2))!.Completed);
        Assert.True(await service.UpdateAsync(42, [new(1, false, null)], CancellationToken.None));
        Assert.False(order.Completed);
        Assert.Null(order.CompletedDate);
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
