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
    [Fact]
    public async Task GetByIdReturnsCurrentProductInformationAndLookupNames()
    {
        await using var context = await CreateContext();
        var product = await context.Products.SingleAsync(item => item.Id == 1);
        var material = new Material { Id = 17, Name = "Material" };
        var construction = new Construction { Id = 1, Name = "Construction" };
        var varnish = new Varnish { Id = 23, Name = "Varnish" };
        context.AddRange(material, construction, varnish);
        product.Material = material;
        product.Construction = construction;
        product.Varnish = varnish;
        product.NrOfColors = "2";
        product.NoteInternal = "Internal";
        product.NoteExternal = "External";
        product.LengthMm = 100;
        product.WidthMm = 50;
        product.HeightMm = 20;
        var internalInfo = new ProductInternalInfo { IsOffset = true, IsFlexo = true };
        context.Add(internalInfo);
        product.InternalInfo = internalInfo;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var response = await CreateController(context).GetById(1);
        var result = Assert.IsType<ProductDto>(Assert.IsType<OkObjectResult>(response.Result).Value);

        Assert.Equal("Box 1", result.Name);
        Assert.Equal("Material", result.MaterialName);
        Assert.Equal("Construction", result.Construction?.Name);
        Assert.Equal("Varnish", result.VarnishName);
        Assert.Equal("2", result.NrOfColors);
        Assert.Equal("Internal", result.NoteInternal);
        Assert.Equal("External", result.NoteExternal);
        Assert.Equal((100, 50, 20), (result.LengthMm, result.WidthMm, result.HeightMm));
        Assert.True(result.IsOffset);
        Assert.True(result.IsFlexo);
        Assert.False(result.IsDigital);
    }

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
    public async Task MaterialNameCanBeFilteredAndSorted()
    {
        await using var context = await CreateContext();
        var paper = new Material { Id = 17, Name = "Paper" };
        var board = new Material { Id = 18, Name = "Board" };
        context.Materials.AddRange(paper, board);
        foreach (var product in context.Products)
        {
            product.Material = product.Id is 1 or 3 ? paper : board;
        }
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var filtered = GetPage(await controller.Search(new SearchProductsRequest
        {
            Filter = new FilterRequest { Conditions = [Condition("materialname", "oard", "contains")] },
            OrderBy = [new SortRequest { Field = "materialname" }, new SortRequest { Field = "id" }],
        }));
        Assert.Equal(new[] { 2, 4, 5 }, filtered.Items.Select(item => item.Id));
        Assert.All(filtered.Items, item => Assert.Equal("Board", item.MaterialName));

        var sorted = GetPage(await controller.Search(new SearchProductsRequest
        {
            OrderBy = [new SortRequest { Field = "materialname" }, new SortRequest { Field = "id" }],
        }));
        Assert.Equal(new[] { 2, 4, 5, 1, 3 }, sorted.Items.Select(item => item.Id));
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

    [Theory]
    [InlineData("asc", 1, 4, 5, 2, 3)]
    [InlineData("desc", 3, 2, 1, 4, 5)]
    public async Task PpwrColumnSortsByUpdateTimestampIncludingProductsWithoutUpdates(string direction, params int[] expectedIds)
    {
        await using var context = await CreateContext();
        var firstUpdate = new DateTime(2026, 11, 16, 10, 0, 0);
        context.Products.Single(product => product.Id == 2).InternalInfo = new ProductInternalInfo { PpwrUpdatedAt = firstUpdate };
        context.Products.Single(product => product.Id == 3).InternalInfo = new ProductInternalInfo { PpwrUpdatedAt = firstUpdate.AddDays(1) };
        context.Products.Single(product => product.Id == 4).InternalInfo = new ProductInternalInfo();
        await context.SaveChangesAsync();

        var page = GetPage(await CreateController(context).Search(new SearchProductsRequest
        {
            OrderBy =
            [
                new SortRequest { Field = "ppwrupdatedat", Direction = direction },
                new SortRequest { Field = "id", Direction = "asc" },
            ],
        }));

        Assert.Equal(expectedIds, page.Items.Select(item => item.Id));
        Assert.Equal(firstUpdate, page.Items.Single(item => item.Id == 2).PpwrUpdatedAt);
        Assert.Equal(firstUpdate.AddDays(1), page.Items.Single(item => item.Id == 3).PpwrUpdatedAt);
        Assert.All(page.Items.Where(item => item.Id is 1 or 4 or 5), item => Assert.Null(item.PpwrUpdatedAt));
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
