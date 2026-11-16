using System.Text.Json;
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
    public async Task CalculationSearchMatchesOnlyTheExactId()
    {
        await using var context = CreateContext();
        context.Products.Add(new Product { Id = 10, Name = "Product 12" });
        context.Calculations.AddRange(
            new Calculation { Id = 123, ProductId = 10 },
            new Calculation { Id = 1234, ProductId = 10 });
        await context.SaveChangesAsync();

        var controller = new CalculationsController(context, null!, null!);
        async Task<PagedResultDto<CalculationSearchResultDto>> SearchById(int id)
        {
            var response = await controller.Search(new SearchCalculationsRequest
            {
                Filter = new FilterRequest
                {
                    Conditions = [new FilterConditionDto
                    {
                        Field = "id", Operator = "eq", Value = JsonSerializer.SerializeToElement(id),
                    }],
                },
                Pagination = new PaginationRequest { PageNumber = 1, PageSize = 5 },
            }, default);
            return Assert.IsType<PagedResultDto<CalculationSearchResultDto>>(
                Assert.IsType<OkObjectResult>(response.Result).Value);
        }

        var result = await SearchById(123);
        Assert.Equal(1, result.TotalCount);
        var calculation = Assert.Single(result.Items);
        Assert.Equal(123, calculation.Id);
        Assert.Equal("Product 12", calculation.ProductName);
        Assert.Empty((await SearchById(12)).Items);
    }

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

        var controller = new TransportOrderController(context, null!, null!, null!);
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

        var controller = new CallOffController(context, null!, null!);
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