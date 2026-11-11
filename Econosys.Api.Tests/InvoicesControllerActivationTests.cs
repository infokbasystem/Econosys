using System.Reflection;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Econosys.Api.Tests;

public class InvoicesControllerActivationTests
{
    [Fact]
    public async Task MvcActivatesControllerWithConfigurationAndLoadsInvoiceSources()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ErrorHandling:SimulateInvoiceSaveFailure"] = "true",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<ILegacyUserResolutionService, LegacyUserResolutionService>();
        services.AddControllers();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Customers.Add(new Customer { Id = 1, Name = "Test customer" });
        dbContext.CustomerOrders.Add(new CustomerOrder { Id = 10, CustomerId = 1, CustomerName = "Test customer" });
        dbContext.DeliveryToCustomers.AddRange(
            new DeliveryToCustomer
            {
                Id = 30,
                CustomerOrderId = 10,
                DeliveryDate = new DateTime(2026, 1, 1),
                DeliveryStatus = (int)DeliveryStatus.Delivered,
                NrOfItems = 10,
            },
            new DeliveryToCustomer
            {
                Id = 31,
                CustomerOrderId = 10,
                DeliveryDate = new DateTime(2026, 1, 2),
                DeliveryStatus = 1,
                NrOfItems = 20,
            });
        await dbContext.SaveChangesAsync();

        var activator = scope.ServiceProvider.GetRequiredService<IControllerActivator>();
        var controllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider },
            ActionDescriptor = new ControllerActionDescriptor
            {
                ControllerTypeInfo = typeof(InvoicesController).GetTypeInfo(),
            },
        };
        var controller = Assert.IsType<InvoicesController>(activator.Create(controllerContext));

        var groupedResult = await controller.GetInvoiceSourceGrouped(onlyDelivered: true);
        var grouped = Assert.IsAssignableFrom<IReadOnlyList<InvoiceDeliveriesAndOrderCostsGroupedCustomerDto>>(
            Assert.IsType<OkObjectResult>(groupedResult.Result).Value);
        var customer = Assert.Single(grouped);
        Assert.Equal(1, customer.CustomerId);
        Assert.Equal(30, Assert.Single(customer.Deliveries).DeliveryId);

        var upcomingResult = await controller.GetInvoiceSourceGrouped(onlyDelivered: false);
        var upcoming = Assert.IsAssignableFrom<IReadOnlyList<InvoiceDeliveriesAndOrderCostsGroupedCustomerDto>>(
            Assert.IsType<OkObjectResult>(upcomingResult.Result).Value);
        Assert.Equal(2, Assert.Single(upcoming).Deliveries.Count);

        var listResult = await controller.GetInvoiceSourceList(onlyDelivered: true);
        var list = Assert.IsAssignableFrom<IReadOnlyList<InvoiceDeliveriesAndOrderCostsListRowDto>>(
            Assert.IsType<OkObjectResult>(listResult.Result).Value);
        Assert.Equal(30, Assert.Single(list).DeliveryId);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.Create(new CreateInvoiceRequest()));
        Assert.Equal("Simulated invoice save failure.", exception.Message);
        var updateException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.Update(1, new UpdateInvoiceRequest()));
        Assert.Equal("Simulated invoice save failure.", updateException.Message);
        activator.Release(controllerContext, controller);
    }
}
