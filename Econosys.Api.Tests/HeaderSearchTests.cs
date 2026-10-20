using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Econosys.Api.Tests;

public class HeaderSearchTests
{
    [Fact]
    public async Task TransportSearchMatchesNumbersAndTextAndLimitsResults()
    {
        await using var context = CreateContext();
        context.TransportOrders.AddRange(Enumerable.Range(1, 6).Select(index => new TransportOrder
        {
            TransportOrderNr = 43410 + index,
            SenderReference = "Forest shipment",
            DateCreated = new DateTime(2026, 10, index),
        }));
        context.TransportOrders.Add(new TransportOrder
        {
            TransportOrderNr = 43410,
            SenderReference = "Other shipment",
            DateCreated = new DateTime(2026, 9, 1),
        });
        await context.SaveChangesAsync();

        var controller = new TransportOrderController(context, null!, null!);
        var numericResults = GetItems(await controller.Search(new HeaderSearchRequest { SearchTerm = "43410" }));
        var textResults = GetItems(await controller.Search(new HeaderSearchRequest { SearchTerm = "Forest" }));

        var numericResult = Assert.Single(numericResults);
        Assert.Equal("43410", numericResult.Number);
        Assert.Equal(5, textResults.Count);
        Assert.All(textResults, item => Assert.Equal("Forest shipment", item.Details));
    }

    [Fact]
    public async Task CallOffTextSearchFollowsCustomerOrderAndStockDeliverySupplierOrders()
    {
        await using var context = CreateContext();
        var customerOrderCallOff = new CallOff();
        customerOrderCallOff.CallOffDeliveries.Add(new CallOffDelivery
        {
            DeliveryFromStock = new DeliveryFromStock
            {
                CustomerOrder = new CustomerOrder
                {
                    SupplierOrder = new SupplierOrder { SupplierOrderNr = "SO-ALPHA" },
                },
            },
        });

        var stockDeliveryCallOff = new CallOff();
        stockDeliveryCallOff.CallOffDeliveries.Add(new CallOffDelivery
        {
            DeliveryFromStock = new DeliveryFromStock
            {
                DeliveryToStock = new DeliveryToStock
                {
                    SupplierOrder = new SupplierOrder { SupplierOrderNr = "SO-ALPHA" },
                },
            },
        });

        context.CallOffs.AddRange(customerOrderCallOff, stockDeliveryCallOff);
        await context.SaveChangesAsync();

        var controller = new CallOffController(context, null!);
        var results = GetItems(await controller.Search(new HeaderSearchRequest { SearchTerm = "SO-ALPHA" }));

        Assert.Equal(2, results.Count);
        Assert.All(results, item => Assert.Equal("SO-ALPHA", item.Details));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static List<HeaderSearchResultDto> GetItems(ActionResult<List<HeaderSearchResultDto>> response)
    {
        var result = Assert.IsType<OkObjectResult>(response.Result);
        return Assert.IsType<List<HeaderSearchResultDto>>(result.Value);
    }
}