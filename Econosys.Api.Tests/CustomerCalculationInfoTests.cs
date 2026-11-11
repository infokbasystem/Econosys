using System.ComponentModel.DataAnnotations;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Migrations;
using Econosys.Api.Models;
using Econosys.Api.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Econosys.Api.Tests;

public class CustomerCalculationInfoTests
{
    [Fact]
    public async Task InformationRoundTripsThroughCreateDetailsSearchAndUpdate()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);
        const string information = "Maximum pallet height 1200mm\nCall before delivery";

        var create = await controller.Create(new CreateCustomerRequest
        {
            Name = "Customer",
            ImportantCalculationInfo = information,
            CountryId = 752,
        });
        var created = Assert.IsType<CustomerDto>(Assert.IsType<CreatedAtActionResult>(create.Result).Value);
        Assert.Equal(information, created.ImportantCalculationInfo);
        var storedCustomer = await context.Customers.SingleAsync();
        Assert.Equal(42, storedCustomer.CompanyId);
        Assert.Null(storedCustomer.CountryId);
        context.ChangeTracker.Clear();

        var details = await controller.GetById(created.Id);
        var detailDto = Assert.IsType<CustomerDetailsDto>(Assert.IsType<OkObjectResult>(details.Result).Value);
        Assert.Equal(information, detailDto.Customer.ImportantCalculationInfo);

        var search = await controller.Search(new SearchCustomersRequest());
        var page = Assert.IsType<PagedResultDto<CustomerDto>>(Assert.IsType<OkObjectResult>(search.Result).Value);
        Assert.Equal(information, Assert.Single(page.Items).ImportantCalculationInfo);

        var update = await controller.Update(created.Id, new UpdateCustomerRequest
        {
            Name = "Customer",
            ImportantCalculationInfo = new string('a', 1000),
        });
        var updated = Assert.IsType<CustomerDto>(Assert.IsType<OkObjectResult>(update.Result).Value);
        Assert.Equal(1000, updated.ImportantCalculationInfo!.Length);
        context.ChangeTracker.Clear();
        Assert.Equal(updated.ImportantCalculationInfo,
            (await context.Customers.SingleAsync()).ImportantCalculationInfo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task InformationCanBeCleared(string? value)
    {
        await using var context = CreateContext();
        var customer = new Customer { Name = "Customer", ImportantCalculationInfo = "Old information" };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var result = await controller.Update(customer.Id, new UpdateCustomerRequest
        {
            Name = customer.Name,
            ImportantCalculationInfo = value,
        });

        var dto = Assert.IsType<CustomerDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(value, dto.ImportantCalculationInfo);
        context.ChangeTracker.Clear();
        Assert.Equal(value, (await context.Customers.SingleAsync()).ImportantCalculationInfo);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void RequestsEnforceMaximumLength(int length, bool expectedValid)
    {
        var value = new string('a', length);
        object[] requests =
        [
            new CreateCustomerRequest { ImportantCalculationInfo = value },
            new UpdateCustomerRequest { ImportantCalculationInfo = value },
        ];

        foreach (var request in requests)
        {
            var errors = new List<ValidationResult>();
            Assert.Equal(expectedValid,
                Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
            if (!expectedValid)
            {
                Assert.Contains(nameof(CreateCustomerRequest.ImportantCalculationInfo),
                    Assert.Single(errors).MemberNames);
            }
        }
    }

    [Fact]
    public void CustomerColumnIsOptionalAndLimitedTo1000Characters()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(Customer))!
            .FindProperty(nameof(Customer.ImportantCalculationInfo))!;

        Assert.True(property.IsNullable);
        Assert.Equal(1000, property.GetMaxLength());
    }

    [Fact]
    public void MigrationAddsOnlyOptionalCustomerInformationColumn()
    {
        var migration = new AddCustomerImportantCalculationInfo();
        var add = Assert.IsType<AddColumnOperation>(Assert.Single(migration.UpOperations));
        Assert.Equal("Customer", add.Table);
        Assert.Equal("ImportantCalculationInfo", add.Name);
        Assert.Equal("nvarchar(1000)", add.ColumnType);
        Assert.Equal(1000, add.MaxLength);
        Assert.True(add.IsNullable);

        var drop = Assert.IsType<DropColumnOperation>(Assert.Single(migration.DownOperations));
        Assert.Equal(add.Table, drop.Table);
        Assert.Equal(add.Name, drop.Name);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CustomersController CreateController(ApplicationDbContext context)
    {
        const string email = "customer-creator@example.com";
        context.LegacyUsers.Add(new LegacyUser { CompanyId = 42, Email = email });
        context.SaveChanges();

        var controller = new CustomersController(
            context,
            NullLogger<CustomersController>.Instance,
            new LegacyUserResolutionService(context));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Email, email)],
                    "test")),
            },
        };
        return controller;
    }
}
