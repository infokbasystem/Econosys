using System.Text.Json;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Econosys.Api.Tests;

public class ProductSearchTests
{
    [Theory]
    [InlineData("constructionid", 1, 1, 2, 4)]
    [InlineData("customerid", 10, 1, 3, 4)]
    [InlineData("supplierid", 20, 1, 2, 4)]
    public async Task FiltersMatchAcrossAllProducts(string field, int value, params int[] expectedIds)
    {
        await using var context = await CreateContext();
        var result = await CreateController(context).Search(new SearchProductsRequest
        {
            Filter = new FilterRequest { Conditions = [Condition(field, value)] },
            OrderBy = [new SortRequest { Field = "id" }],
        });

        var page = GetPage(result);
        Assert.Equal(expectedIds, page.Items.Select(item => item.Id));
        Assert.Equal(expectedIds.Length, page.TotalCount);
    }

    [Fact]
    public async Task FiltersCombineWithNameAndActiveBeforePaginationWithoutDuplicates()
    {
        await using var context = await CreateContext();
        var controller = CreateController(context);
        var request = new SearchProductsRequest
        {
            Filter = new FilterRequest
            {
                Conditions =
                [
                    Condition("constructionid", 1),
                    Condition("customerid", 10),
                    Condition("supplierid", 20),
                    Condition("name", "Box", "contains"),
                    Condition("active", true),
                ],
            },
            Pagination = new PaginationRequest { PageNumber = 1, PageSize = 1 },
            OrderBy = [new SortRequest { Field = "id" }],
        };

        var page = GetPage(await controller.Search(request));
        Assert.Equal(1, Assert.Single(page.Items).Id);
        Assert.Equal(1, page.TotalCount);

        request.Filter.Conditions.RemoveAt(4);
        page = GetPage(await controller.Search(request));
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.True(page.HasNextPage);

        request.Pagination.PageNumber = 2;
        page = GetPage(await controller.Search(request));
        Assert.Equal(4, Assert.Single(page.Items).Id);

        request.Filter.Conditions.Clear();
        request.Pagination.PageNumber = 1;
        page = GetPage(await controller.Search(request));
        Assert.Equal(5, page.TotalCount);
    }

    [Theory]
    [InlineData("customerid", "contains", 10)]
    [InlineData("supplierid", "in", 20)]
    [InlineData("customerid", "eq", null)]
    [InlineData("supplierid", "eq", "invalid")]
    public async Task InvalidRelationshipFiltersReturnBadRequest(string field, string op, object? value)
    {
        await using var context = await CreateContext();
        var result = await CreateController(context).Search(new SearchProductsRequest
        {
            Filter = new FilterRequest { Conditions = [Condition(field, value, op)] },
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private static FilterConditionDto Condition(string field, object? value, string op = "eq") => new()
    {
        Field = field,
        Operator = op,
        Value = JsonSerializer.SerializeToElement(value),
    };

    private static ProductsController CreateController(ApplicationDbContext context) =>
        new(context, NullLogger<ProductsController>.Instance, null!);

    private static PagedResultDto<ProductDto> GetPage(ActionResult<PagedResultDto<ProductDto>> response) =>
        Assert.IsType<PagedResultDto<ProductDto>>(Assert.IsType<OkObjectResult>(response.Result).Value);

    private static async Task<ApplicationDbContext> CreateContext()
    {
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        context.Products.AddRange(
            Product(1, 1, 10, 20),
            Product(2, 1, 11, 20),
            Product(3, 2, 10, 21),
            Product(4, 1, 10, 20, active: false),
            new Product { Id = 5, Name = "Unrelated", Active = true });
        await context.SaveChangesAsync();
        return context;
    }

    private static Product Product(int id, int constructionId, int customerId, int supplierId, bool active = true) => new()
    {
        Id = id,
        Name = $"Box {id}",
        ConstructionId = constructionId,
        Active = active,
        Calculations =
        [
            new Calculation { CustomerId = customerId, SupplierId = supplierId },
            new Calculation { CustomerId = customerId, SupplierId = supplierId },
        ],
    };
}
