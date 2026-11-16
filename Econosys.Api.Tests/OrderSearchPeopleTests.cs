using System.Security.Claims;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Econosys.Api.Tests;

public class OrderSearchPeopleTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnfilteredSearchIncludesUnassignedOrdersAndInactivePeople(bool supplierOrders)
    {
        using var db = await CreateContext();

        var page = await Search(db, supplierOrders);

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(("Salesperson", "Creator"), (page.Items[0].Seller, page.Items[0].Creator));
        Assert.Equal(("Former salesperson", "Former creator"), (page.Items[1].Seller, page.Items[1].Creator));
        Assert.Null(page.Items[2].Seller);
        Assert.Null(page.Items[2].Creator);
        Assert.Null(page.Items[3].Seller);
        Assert.Null(page.Items[3].Creator);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PersonFiltersCombineAndApplyBeforeCountingAndPagination(bool supplierOrders)
    {
        using var db = await CreateContext();

        var combined = await Search(db, supplierOrders, sellerIds: [11], creatorIds: [44]);
        Assert.Equal(1, combined.TotalCount);
        Assert.Equal(5, Assert.Single(combined.Items).Id);

        var page = await Search(db, supplierOrders, sellerIds: [11, 22], creatorIds: [33, 44],
            pageNumber: 2, pageSize: 1);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, Assert.Single(page.Items).Id);

        var sellersOnly = await Search(db, supplierOrders, sellerIds: [22]);
        Assert.Equal(2, Assert.Single(sellersOnly.Items).Id);
        var creatorsOnly = await Search(db, supplierOrders, creatorIds: [33]);
        Assert.Equal(1, Assert.Single(creatorsOnly.Items).Id);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task EmptyPersonSelectionMatchesNoOrders(bool supplierOrders, bool emptySellers)
    {
        using var db = await CreateContext();

        var page = await Search(db, supplierOrders,
            sellerIds: emptySellers ? [] : null,
            creatorIds: emptySellers ? null : []);

        Assert.Equal(0, page.TotalCount);
        Assert.Empty(page.Items);
    }

    private static async Task<ApplicationDbContext> CreateContext()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.LegacyUsers.AddRange(
            new LegacyUser { Id = 11, Name = "Salesperson", Active = true },
            new LegacyUser { Id = 22, Name = "Former salesperson" },
            new LegacyUser { Id = 33, Name = "Creator", Active = true },
            new LegacyUser { Id = 44, Name = "Former creator" });
        db.Customers.AddRange(
            new Customer { Id = 101, ResponsibleUserId = 11 },
            new Customer { Id = 102, ResponsibleUserId = 22 },
            new Customer { Id = 103 });
        var people = new (int? CustomerId, int? CreatedBy)[]
        {
            (101, 33), (102, 44), (null, null), (103, 999), (101, 44),
        };
        for (var index = 0; index < people.Length; index++)
        {
            var (customerId, createdBy) = people[index];
            db.SupplierOrders.Add(new SupplierOrder { Id = index + 1, CustomerId = customerId, CreatedBy = createdBy });
            db.CustomerOrders.Add(new CustomerOrder
            {
                Id = index + 1, CustomerId = customerId, CreatedBy = createdBy,
                ResponsibleUserId = 22,
            });
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    private static async Task<(int TotalCount, (int Id, string? Seller, string? Creator)[] Items)> Search(
        ApplicationDbContext db, bool supplierOrders, int[]? sellerIds = null, int[]? creatorIds = null,
        int pageNumber = 1, int pageSize = 25)
    {
        var pagination = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
        if (supplierOrders)
        {
            var controller = new SupplierOrdersController(db, NullLogger<SupplierOrdersController>.Instance,
                new UserResolution(), new PalletFormatOptionsService(db));
            var result = await controller.Search(new SearchSupplierOrdersRequest
            {
                CustomerResponsibleUserIds = sellerIds, CreatedByIds = creatorIds, Pagination = pagination,
            });
            var page = Assert.IsType<PagedResultDto<SupplierOrderDto>>(
                Assert.IsType<OkObjectResult>(result.Result).Value);
            return (page.TotalCount, page.Items.Select(x => (x.Id, x.SellerName, x.CreatedByUserName)).ToArray());
        }

        var customerController = new CustomerOrdersController(db, NullLogger<CustomerOrdersController>.Instance,
            new UserResolution());
        var customerResult = await customerController.Search(new SearchCustomerOrdersRequest
        {
            CustomerResponsibleUserIds = sellerIds, CreatedByIds = creatorIds, Pagination = pagination,
        });
        var customerPage = Assert.IsType<PagedResultDto<CustomerOrderDto>>(
            Assert.IsType<OkObjectResult>(customerResult.Result).Value);
        return (customerPage.TotalCount, customerPage.Items.Select(x => (x.Id, x.SellerName, x.CreatedByUserName)).ToArray());
    }

    private sealed class UserResolution : ILegacyUserResolutionService
    {
        public Task<LegacyUser?> ResolveCurrentUserAsync(ClaimsPrincipal principal) =>
            Task.FromResult<LegacyUser?>(null);
    }
}
