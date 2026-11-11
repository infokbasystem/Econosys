using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Econosys.Api.Tests;

public class SupplierOrderNumberDisplayTests
{
    [Fact]
    public async Task TransportCostTablesReceiveSupplierOrderNumberWithoutChangingIds()
    {
        using var context = CreateContext();
        context.SupplierOrders.Add(new SupplierOrder { Id = 50, SupplierOrderNr = "ABC-50" });
        context.TransportOrderCostCalcs.Add(new TransportOrderCostCalc
        {
            Id = 10,
            TransportOrderId = 20,
            TransportOrderCostCalcSupplierOrders = new List<TransportOrderCostCalcSupplierOrder>
            {
                new() { Id = 30, SupplierOrderId = 50 },
            },
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var controller = new TransportOrderCostCalcController(context);

        var result = await controller.GetById(10);
        var dto = Assert.IsType<TransportOrderCostCalcDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var row = Assert.Single(dto.SupplierOrders);
        Assert.Equal(50, row.SupplierOrderId);
        Assert.Equal("ABC-50", row.SupplierOrderNr);

        var listResult = await controller.GetByTransportOrderId(20);
        var list = Assert.IsType<List<TransportOrderCostCalcDto>>(
            Assert.IsType<OkObjectResult>(listResult.Result).Value);
        Assert.Equal("ABC-50", Assert.Single(Assert.Single(list).SupplierOrders).SupplierOrderNr);
    }

    [Fact]
    public async Task MonthlyCostsReportUsesTheFullSupplierOrderNumber()
    {
        using var context = CreateContext();
        context.SupplierOrders.Add(new SupplierOrder
        {
            Id = 50, SupplierOrderNr = "ABC-50", Created = new DateTime(2026, 11, 1),
        });
        context.OrderCosts.Add(new OrderCost { Id = 10, SupplierOrderId = 50, InPrice = 125 });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new ReportingController(context).GetMonthlyOrderCostsReport(
            new MonthlyOrderCostsReportRequestDto { Year = 2026, Month = 11 });

        var dto = Assert.IsType<MonthlyOrderCostsReportResponseDto>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("ABC-50", Assert.Single(dto.Rows).OrderNumber);
    }

    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
