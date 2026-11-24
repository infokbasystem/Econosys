using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Econosys.Api.Tests;

public class InvoiceStockDeliveryAdjustmentTests
{
    [Theory]
    [InlineData(30, 70)]
    [InlineData(100, 0)]
    public async Task PreviewDoesNotChangeStockAndApplyUsesReviewedQuantity(double credited, double expected)
    {
        await using var db = CreateDb();
        await Seed(db, credited);
        var service = new InvoiceStockDeliveryAdjustmentService(db);

        var preview = await service.PreviewAsync(2, CancellationToken.None);
        Assert.Equal(new InvoiceStockDeliveryAdjustmentResponse(10, 100, credited, expected), Assert.Single(preview!));
        Assert.Equal(100, (await db.DeliveryFromStocks.FindAsync(10))!.NrOfItems);
        Assert.True(await service.ApplyAsync(2, preview!, CancellationToken.None));
        Assert.Equal(expected, (await db.DeliveryFromStocks.FindAsync(10))!.NrOfItems);
        await Assert.ThrowsAsync<InvoiceStockDeliveryAdjustmentException>(
            () => service.ApplyAsync(2, preview!, CancellationToken.None));
        Assert.Equal(expected, (await db.DeliveryFromStocks.FindAsync(10))!.NrOfItems);
    }

    [Fact]
    public async Task ChangedDeliveriesOrInvoiceRowsAreRejectedWithoutPartialUpdates()
    {
        await using var db = CreateDb();
        await Seed(db, 30);
        db.DeliveryFromStocks.Add(new DeliveryFromStock { Id = 11, NrOfItems = 40 });
        db.InvoiceRows.AddRange(
            new InvoiceRow { Id = 3, InvoiceId = 1, PriceTypeId = 1, DeliveryFromStockId = 11, NrOf = 40 },
            new InvoiceRow { Id = 4, InvoiceId = 2, PriceTypeId = 1, DeliveryFromStockId = 11, NrOf = 10 });
        await db.SaveChangesAsync();
        var service = new InvoiceStockDeliveryAdjustmentService(db);
        var preview = (await service.PreviewAsync(2, CancellationToken.None))!;

        (await db.DeliveryFromStocks.FindAsync(11))!.NrOfItems = 35;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvoiceStockDeliveryAdjustmentException>(
            () => service.ApplyAsync(2, preview, CancellationToken.None));
        Assert.Equal(100, (await db.DeliveryFromStocks.FindAsync(10))!.NrOfItems);
        Assert.Equal(35, (await db.DeliveryFromStocks.FindAsync(11))!.NrOfItems);

        (await db.DeliveryFromStocks.FindAsync(11))!.NrOfItems = 40;
        (await db.InvoiceRows.FindAsync(2))!.NrOf = 25;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvoiceStockDeliveryAdjustmentException>(
            () => service.ApplyAsync(2, preview, CancellationToken.None));
        Assert.Equal(100, (await db.DeliveryFromStocks.FindAsync(10))!.NrOfItems);
        Assert.Equal(40, (await db.DeliveryFromStocks.FindAsync(11))!.NrOfItems);
    }

    [Fact]
    public async Task RejectsAmbiguousOriginalsAndInvalidCreditQuantities()
    {
        await using var db = CreateDb();
        await Seed(db, 101);
        var service = new InvoiceStockDeliveryAdjustmentService(db);
        Assert.Null(await service.PreviewAsync(999, CancellationToken.None));
        await Assert.ThrowsAsync<InvoiceStockDeliveryAdjustmentException>(() => service.PreviewAsync(1, CancellationToken.None));
        await Assert.ThrowsAsync<InvoiceStockDeliveryAdjustmentException>(() => service.PreviewAsync(2, CancellationToken.None));
        (await db.InvoiceRows.FindAsync(2))!.NrOf = 30;
        db.Invoices.Add(new Invoice { Id = 3, InvoiceNumber = 100, CustomerId = 1 });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvoiceStockDeliveryAdjustmentException>(() => service.PreviewAsync(2, CancellationToken.None));
        Assert.Equal(100, (await db.DeliveryFromStocks.FindAsync(10))!.NrOfItems);
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task Seed(ApplicationDbContext db, double credited)
    {
        db.DeliveryFromStocks.Add(new DeliveryFromStock { Id = 10, NrOfItems = 100 });
        db.Invoices.AddRange(
            new Invoice { Id = 1, InvoiceNumber = 100, InvoiceTypeCode = InvoiceTypeCode.Normal, CustomerId = 1 },
            new Invoice { Id = 2, InvoiceNumber = 101, InvoiceTypeCode = InvoiceTypeCode.Credit, CreditingInvoiceNumber = 100, CustomerId = 1 });
        db.InvoiceRows.AddRange(
            new InvoiceRow { Id = 1, InvoiceId = 1, PriceTypeId = 1, DeliveryFromStockId = 10, NrOf = 100 },
            new InvoiceRow { Id = 2, InvoiceId = 2, PriceTypeId = 1, DeliveryFromStockId = 10, NrOf = credited },
            new InvoiceRow { Id = 5, InvoiceId = 2, PriceTypeId = 107, DeliveryFromStockId = 10, NrOf = 2 });
        await db.SaveChangesAsync();
    }
}
