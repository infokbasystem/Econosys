using System.Globalization;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Econosys.Api.Tests;

public class TransportOrderOverviewSortingTests
{
    [Theory]
    [InlineData("2026-11-04", true, "2026-11-02", "v. 45")]
    [InlineData("2026-11-02", true, "2026-11-02", "v. 45")]
    [InlineData("2026-11-08", true, "2026-11-02", "v. 45")]
    [InlineData("2026-01-01", true, "2025-12-29", "v. 1")]
    [InlineData("2027-01-01", true, "2026-12-28", "v. 53")]
    [InlineData("2026-11-04", false, "2026-11-04", "2026-11-04")]
    [InlineData(null, true, null, "Pending")]
    [InlineData(null, false, null, "Pending")]
    public async Task TopTableIncludesChronologicalSortDatesWithoutChangingDisplay(
        string? dateText, bool weekMode, string? expectedSortDate, string expectedDisplay)
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var date = dateText is null ? (DateTime?)null : DateTime.Parse(dateText, CultureInfo.InvariantCulture);
        var supplierOrder = new SupplierOrder
        {
            Id = 1,
            CompanyId = 1,
            EconopackTransportResponsible = true,
            Edition = 100,
            ConfirmedDeliveryDate = date,
            ConfirmedDeliveryDateWeekMode = weekMode,
        };
        context.SupplierOrders.Add(supplierOrder);
        context.CustomerOrders.Add(new CustomerOrder
        {
            Id = 1,
            CompanyId = 1,
            SupplierOrderId = supplierOrder.Id,
            DeliveryDate = date,
            DeliveryDateWeekMode = weekMode,
            TimeOfDelivery = "Pending",
        });
        await context.SaveChangesAsync();

        var controller = new LogisticsController(context, new LegacyUserResolutionService(context), null!);
        var result = await controller.GetTopTable();
        var rows = Assert.IsType<List<TransportOrderOverviewTopTableRowDto>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var row = Assert.Single(rows);

        Assert.Equal(expectedSortDate, row.DeliveryTimeSortDate);
        Assert.Equal(expectedSortDate, row.ConfirmedDeliveryTimeSortDate);
        Assert.Equal(expectedDisplay, row.DeliveryTime);
        Assert.Equal(dateText is null ? string.Empty : expectedDisplay, row.ConfirmedDeliveryTime);
    }
}
