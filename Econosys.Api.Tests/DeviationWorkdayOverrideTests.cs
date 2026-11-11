using System.Security.Claims;
using Econosys.Api.Common;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Econosys.Api.Tests;

public class DeviationWorkdayOverrideTests
{
    [Fact]
    public async Task DeviationUpdateSavesAndClearsWorkdayOverrideWithOtherFields()
    {
        using var context = CreateContext();
        var deviation = new Deviation { Status = "CLOSED" };
        context.Deviations.Add(deviation);
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var updateResult = await controller.Update(deviation.Id, new UpdateDeviationRequest
        {
            Status = "CLOSED",
            Description = "Updated with workday adjustment",
            OverrideHandlingWorkdays = 12,
            HandlingWorkdaysOverrideReason = "Customer paused over summer",
            Costs = new List<CreateDeviationCostRequest>()
        });

        var updated = Assert.IsType<DeviationDto>(Assert.IsType<OkObjectResult>(updateResult.Result).Value);
        Assert.Equal(12, deviation.OverrideHandlingWorkdays);
        Assert.Equal("Customer paused over summer", deviation.HandlingWorkdaysOverrideReason);
        Assert.Equal("Updated with workday adjustment", deviation.Description);
        Assert.Equal(12, updated.OverrideHandlingWorkdays);
        Assert.Equal("Customer paused over summer", updated.HandlingWorkdaysOverrideReason);
        Assert.Equal(42, deviation.EditedByUserId);
        Assert.NotNull(deviation.EditedTimeStamp);

        var clearResult = await controller.Update(deviation.Id, new UpdateDeviationRequest
        {
            Status = "CLOSED",
            Costs = new List<CreateDeviationCostRequest>()
        });

        Assert.IsType<OkObjectResult>(clearResult.Result);
        Assert.Null(deviation.OverrideHandlingWorkdays);
        Assert.Null(deviation.HandlingWorkdaysOverrideReason);
    }

    [Theory]
    [InlineData(-1, "A reason")]
    [InlineData(2, " ")]
    public async Task DeviationUpdateRejectsInvalidWorkdayOverride(int days, string reason)
    {
        using var context = CreateContext();
        var deviation = new Deviation { Status = "CLOSED" };
        context.Deviations.Add(deviation);
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var result = await controller.Update(deviation.Id, new UpdateDeviationRequest
        {
            Status = "CLOSED",
            OverrideHandlingWorkdays = days,
            HandlingWorkdaysOverrideReason = reason,
            Costs = new List<CreateDeviationCostRequest>()
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SupplierReportUsesOverrideOrElapsedWeekdays()
    {
        using var context = CreateContext();
        context.Suppliers.Add(new Supplier { Id = 5, Name = "Supplier" });
        context.Deviations.AddRange(
            new Deviation
            {
                IsInternal = false,
                SupplierId = 5,
                DeviationOpened = new DateTime(2026, 10, 19),
                DeviationClosedToSupplier = new DateTime(2026, 10, 26),
                OverrideHandlingWorkdays = 12
            },
            new Deviation
            {
                IsInternal = false,
                SupplierId = 5,
                DeviationOpened = new DateTime(2026, 10, 23),
                DeviationClosedToSupplier = new DateTime(2026, 10, 26)
            });
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var action = await controller.GetSupplierReport(null);
        var report = Assert.IsType<SupplierDeviationReportDto>(Assert.IsType<OkObjectResult>(action.Result).Value);
        var supplier = Assert.Single(report.Suppliers);
        var deviations = supplier.Deviations.ToDictionary(x => x.DeviationId);

        Assert.Equal(12, deviations.Values.Single(x => x.DaysToCloseToSupplier == 12).DaysToCloseToSupplier);
        Assert.Equal(1, deviations.Values.Single(x => x.DaysToCloseToSupplier == 1).DaysToCloseToSupplier);
        Assert.Equal(6.5m, supplier.AverageDaysToCloseToSupplier);
    }

    [Fact]
    public async Task OverviewUsesOverrideForCompletedMetricsAndWeekdaysForOpenAge()
    {
        using var context = CreateContext();
        var openDate = SwedishTime.Today.AddDays(-7);
        context.Deviations.AddRange(
            new Deviation
            {
                Status = "CLOSED",
                DeviationOpened = new DateTime(2026, 10, 19),
                DeviationClosed = new DateTime(2026, 10, 26),
                OverrideHandlingWorkdays = 12
            },
            new Deviation
            {
                Status = "CLOSED",
                DeviationOpened = new DateTime(2026, 10, 23),
                DeviationClosed = new DateTime(2026, 10, 26)
            },
            new Deviation
            {
                Status = "OPEN",
                DeviationOpened = openDate
            });
        await context.SaveChangesAsync();
        var openDeviation = await context.Deviations.SingleAsync(x => x.Status == "OPEN");
        var controller = CreateController(context);

        var action = await controller.GetOverview(2026);
        var overview = Assert.IsType<DeviationOverviewDto>(Assert.IsType<OkObjectResult>(action.Result).Value);
        var october = overview.MonthlyStats.Single(x => x.MonthNumber == 10);

        Assert.Equal(6.5m, october.AverageHandlingDays);
        Assert.Equal(50m, october.HandledWithin10DaysPercent);
        Assert.Equal(DeviationWorkdayCalculator.CountElapsedWeekdays(openDate, SwedishTime.Today),
            Assert.Single(overview.OpenItems).OpenDays);
        Assert.Equal(openDeviation.Id, Assert.Single(overview.OpenItems).Id);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static DeviationsController CreateController(ApplicationDbContext context) =>
        new(context, new UserResolution(new LegacyUser { Id = 42, CompanyId = 1 }));

    private sealed class UserResolution(LegacyUser user) : ILegacyUserResolutionService
    {
        public Task<LegacyUser?> ResolveCurrentUserAsync(ClaimsPrincipal principal) =>
            Task.FromResult<LegacyUser?>(user);
    }
}
