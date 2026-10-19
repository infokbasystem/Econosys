using System.Security.Claims;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Econosys.Api.Tests;

public class OrderCreationTests
{
    [Fact]
    public void OrdersMapTheExistingRequiredCompanyColumn()
    {
        using var context = CreateContext();
        foreach (var entityType in new[] { typeof(CustomerOrder), typeof(SupplierOrder) })
        {
            var entity = context.Model.FindEntityType(entityType)!;
            var property = entity.FindProperty("CompanyId");

            Assert.NotNull(property);
            Assert.False(property.IsNullable);
            Assert.Equal("CompanyId", property.GetColumnName());
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public async Task CustomerOrderCreateAssignsTheAuthenticatedUsersCompanyBeforeSaving(int companyId)
    {
        var interceptor = new StopBeforeSaveInterceptor();
        using var context = CreateContext(interceptor);
        var controller = new CustomerOrdersController(
            context,
            NullLogger<CustomerOrdersController>.Instance,
            new UserResolution(new LegacyUser { Id = 42, CompanyId = companyId }));

        await Assert.ThrowsAsync<SaveInterceptedException>(
            () => controller.Create(new CreateCustomerOrderRequest()));

        var order = Assert.Single(context.ChangeTracker.Entries<CustomerOrder>()).Entity;
        Assert.Equal(companyId, order.CompanyId);
        Assert.Equal(42, order.CreatedBy);
        Assert.Equal(42, order.EditedBy);
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(7, 123)]
    public async Task SupplierOrderCreateAssignsTheAuthenticatedUsersCompanyBeforeSaving(
        int companyId, int? basedOnSupplierOrderId)
    {
        var interceptor = new StopBeforeSaveInterceptor();
        using var context = CreateContext(interceptor);
        var controller = new SupplierOrdersController(
            context,
            NullLogger<SupplierOrdersController>.Instance,
            new UserResolution(new LegacyUser { Id = 42, CompanyId = companyId }),
            new PalletFormatOptionsService(context));

        await Assert.ThrowsAsync<SaveInterceptedException>(
            () => controller.Create(new CreateSupplierOrderRequest
            {
                BasedOnSupplierOrderId = basedOnSupplierOrderId,
            }));

        var order = Assert.Single(context.ChangeTracker.Entries<SupplierOrder>()).Entity;
        Assert.Equal(companyId, order.CompanyId);
        Assert.Equal(basedOnSupplierOrderId, order.BasedOnSupplierOrderId);
        Assert.Equal(42, order.CreatedBy);
        Assert.Equal(42, order.EditedBy);
    }

    private static ApplicationDbContext CreateContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelMappingOnly;Integrated Security=true")
            .AddInterceptors(interceptors)
            .Options);

    private sealed class UserResolution(LegacyUser user) : ILegacyUserResolutionService
    {
        public Task<LegacyUser?> ResolveCurrentUserAsync(ClaimsPrincipal principal) =>
            Task.FromResult<LegacyUser?>(user);
    }

    private sealed class SaveInterceptedException : Exception { }

    private sealed class StopBeforeSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new SaveInterceptedException();
    }
}
