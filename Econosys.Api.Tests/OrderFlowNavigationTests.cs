using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Econosys.Api.Tests;

public class OrderFlowNavigationTests
{
    [Theory]
    [InlineData("product", 10)]
    [InlineData("inquiry", 30)]
    [InlineData("quotation", 40)]
    [InlineData("supplierorder", 50)]
    [InlineData("customerorder", 60)]
    public async Task DocumentTreesIncludeCalculationArchiveStatusAndKeepArchivedBranches(string entityType, int entityId)
    {
        await using var db = CreateContext();
        db.Products.Add(new Product { Id = 10 });
        db.Calculations.AddRange(
            new Calculation { Id = 20, ProductId = 10 },
            new Calculation { Id = 21, ProductId = 10, IsArchived = true });
        db.Inquiries.AddRange(
            new Inquiry { Id = 30, CalculationId = 20 },
            new Inquiry { Id = 31, CalculationId = 21 });
        db.Quotations.Add(new Quotation { Id = 40, CalculationId = 20, InquiryId = 30 });
        db.SupplierOrders.Add(new SupplierOrder { Id = 50, CalculationId = 20, QuotationId = 40, SupplierOrderNr = "ABC-50" });
        db.CustomerOrders.Add(new CustomerOrder { Id = 60, SupplierOrderId = 50, CustomerOrderNr = "ABC-50-2" });
        db.CustomerOrders.Add(new CustomerOrder { Id = 61, QuotationId = 40, CustomerOrderNr = "DIRECT-61" });
        await db.SaveChangesAsync();

        var response = await new OrderNavigationController(db).GetTree(entityType, entityId);
        var tree = Assert.IsType<OrderNavigationTreeDto>(Assert.IsType<OkObjectResult>(response.Result).Value);

        Assert.False(Assert.Single(tree.Calculations, calculation => calculation.Id == 20).IsArchived);
        var archived = Assert.Single(tree.Calculations, calculation => calculation.Id == 21);
        Assert.True(archived.IsArchived);
        Assert.Equal(31, Assert.Single(archived.Inquiries).Id);
        var calculation = Assert.Single(tree.Calculations, x => x.Id == 20);
        var quotation = Assert.Single(Assert.Single(calculation.Inquiries).Quotations);
        var supplierOrder = Assert.Single(quotation.SupplierOrders);
        Assert.Equal(50, supplierOrder.Id);
        Assert.Equal("ABC-50", supplierOrder.SupplierOrderNr);
        var customerOrder = Assert.Single(supplierOrder.CustomerOrders);
        Assert.Equal(60, customerOrder.Id);
        Assert.Equal("ABC-50-2", customerOrder.CustomerOrderNr);
        var directCustomerOrder = Assert.Single(quotation.CustomerOrders);
        Assert.Equal(61, directCustomerOrder.Id);
        Assert.Equal("DIRECT-61", directCustomerOrder.CustomerOrderNr);
    }

    [Fact]
    public async Task ReturnsOnlyTheSelectedCalculationAndItsRelatedDocumentsOnce()
    {
        await using var db = CreateContext();
        db.Products.Add(new Product { Id = 10, Name = "Låda" });
        db.Calculations.AddRange(
            new Calculation { Id = 20, ProductId = 10 },
            new Calculation { Id = 21, ProductId = 10 });
        db.Inquiries.AddRange(new Inquiry { Id = 30, CalculationId = 20 }, new Inquiry { Id = 31, CalculationId = 21 });
        db.Quotations.AddRange(
            new Quotation { Id = 40, CalculationId = 20, InquiryId = 30 },
            new Quotation { Id = 41, InquiryId = 30 },
            new Quotation { Id = 42, CalculationId = 21 });
        db.SupplierOrders.AddRange(
            new SupplierOrder { Id = 50, CalculationId = 20, InquiryId = 30, QuotationId = 40 },
            new SupplierOrder { Id = 51, InquiryId = 30 },
            new SupplierOrder { Id = 52, QuotationId = 41 },
            new SupplierOrder { Id = 53, CalculationId = 20 },
            new SupplierOrder { Id = 54, CalculationId = 21 });
        db.CustomerOrders.AddRange(
            new CustomerOrder { Id = 60, QuotationId = 40, SupplierOrderId = 50 },
            new CustomerOrder { Id = 61, SupplierOrderId = 51 },
            new CustomerOrder { Id = 62, QuotationId = 41 },
            new CustomerOrder { Id = 63, CalculationId = 20 },
            new CustomerOrder { Id = 64, SupplierOrderId = 54 });
        await db.SaveChangesAsync();

        var items = (await new OrderFlowNavigationService(db).GetCalculationFlowAsync(20, default))!;

        Assert.Equal(10, Assert.Single(items, x => x.Type == "product").Id);
        Assert.Equal(20, Assert.Single(items, x => x.Type == "calculation").Id);
        Assert.Equal(new[] { 30 }, Ids(items, "inquiry"));
        Assert.Equal(new[] { 41, 40 }, Ids(items, "quotation"));
        Assert.Equal(new[] { 53, 52, 51, 50 }, Ids(items, "supplierOrder"));
        Assert.Equal(new[] { 63, 62, 61, 60 }, Ids(items, "customerOrder"));
    }

    [Fact]
    public async Task UsesLegacyNamesVisibleOrderNumbersAndDateSources()
    {
        await using var db = CreateContext();
        var supplier = new Supplier { Id = 1, Name = "Bart sp. z.o.o." };
        var customer = new Customer { Id = 1, Name = "Salico AB" };
        db.Calculations.Add(new Calculation
        {
            Id = 20, Supplier = supplier, Customer = customer,
            CreatedAt = new DateTime(2026, 9, 1), EditedAt = new DateTime(2026, 10, 19),
        });
        db.Inquiries.Add(new Inquiry
        {
            Id = 30, CalculationId = 20, Customer = customer,
            SelectedInquiryRecipient = new InquiryRecipient { Supplier = supplier },
            CreatedAt = new DateTime(2026, 9, 16), EditedAt = new DateTime(2026, 10, 1),
        });
        db.Quotations.Add(new Quotation
        {
            Id = 40, CalculationId = 20, Customer = customer, CreatedAt = new DateTime(2026, 9, 17),
        });
        db.SupplierOrders.Add(new SupplierOrder
        {
            Id = 50, CalculationId = 20, SupplierOrderNr = "44723-A", Supplier = supplier, Customer = customer,
            Created = new DateTime(2026, 10, 12),
        });
        db.CustomerOrders.Add(new CustomerOrder
        {
            Id = 60, SupplierOrderId = 50, CustomerOrderNr = "44517", Customer = customer,
            Created = new DateTime(2026, 10, 19),
        });
        await db.SaveChangesAsync();

        var items = (await new OrderFlowNavigationService(db).GetCalculationFlowAsync(20, default))!;

        var calculation = Assert.Single(items, x => x.Type == "calculation");
        Assert.Equal("261019", calculation.DisplayDate);
        Assert.Equal("Bart sp. z.o.o.", calculation.SupplierName);
        Assert.Equal("Salico AB", calculation.CustomerName);
        var inquiry = Assert.Single(items, x => x.Type == "inquiry");
        Assert.Equal("260916", inquiry.DisplayDate);
        Assert.Equal("Bart sp. z.o.o.", inquiry.SupplierName);
        Assert.Equal("260917", Assert.Single(items, x => x.Type == "quotation").DisplayDate);
        var order = Assert.Single(items, x => x.Type == "supplierOrder");
        Assert.Equal("44723-A", order.Number);
        Assert.Equal("261012", order.DisplayDate);
        Assert.Equal("44517", Assert.Single(items, x => x.Type == "customerOrder").Number);
    }

    [Fact]
    public async Task ReturnsAllDocumentsForShowMoreAndHandlesMissingOptionalRelations()
    {
        await using var db = CreateContext();
        db.Calculations.Add(new Calculation { Id = 20 });
        db.Inquiries.AddRange(Enumerable.Range(1, 5).Select(id => new Inquiry { Id = id, CalculationId = 20 }));
        db.SupplierOrders.AddRange(Enumerable.Range(1, 10).Select(id => new SupplierOrder { Id = id, CalculationId = 20 }));
        await db.SaveChangesAsync();

        var items = (await new OrderFlowNavigationService(db).GetCalculationFlowAsync(20, default))!;

        Assert.DoesNotContain(items, x => x.Type == "product");
        Assert.Equal(5, items.Count(x => x.Type == "inquiry"));
        Assert.Equal(10, items.Count(x => x.Type == "supplierOrder"));
        Assert.All(items, x => Assert.Null(x.DisplayDate));
        Assert.Null(Assert.Single(items, x => x.Type == "calculation").SupplierName);
    }

    [Fact]
    public async Task MissingCalculationReturnsNotFound()
    {
        await using var db = CreateContext();
        var service = new OrderFlowNavigationService(db);
        var controller = new OrderNavigationController(db);

        var response = await controller.GetCalculationFlow(999, service, default);

        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Theory]
    [InlineData("product", 10)]
    [InlineData("inquiry", 30)]
    [InlineData("quotation", 40)]
    [InlineData("supplierOrder", 50)]
    [InlineData("customerOrder", 60)]
    public async Task GroupedDocumentFlowIncludesProductBranchesAndInheritedLinks(string entityType, int entityId)
    {
        await using var db = CreateContext();
        db.Products.AddRange(new Product { Id = 10, Name = "Product" }, new Product { Id = 11 });
        db.Calculations.AddRange(
            new Calculation { Id = 20, ProductId = 10 },
            new Calculation { Id = 21, ProductId = 10, IsArchived = true },
            new Calculation { Id = 22, ProductId = 11 });
        db.Inquiries.AddRange(
            new Inquiry { Id = 30, CalculationId = 20 },
            new Inquiry { Id = 31, CalculationId = 21 },
            new Inquiry { Id = 32, CalculationId = 22 });
        db.Quotations.AddRange(
            new Quotation { Id = 40, InquiryId = 30 },
            new Quotation { Id = 41, InquiryId = 31 });
        db.SupplierOrders.AddRange(
            new SupplierOrder { Id = 50, QuotationId = 40, SupplierOrderNr = "ORDER-50" },
            new SupplierOrder { Id = 51, QuotationId = 41 });
        db.CustomerOrders.AddRange(
            new CustomerOrder { Id = 60, SupplierOrderId = 50, CustomerOrderNr = "ORDER-50-1" },
            new CustomerOrder { Id = 61, SupplierOrderId = 51 },
            new CustomerOrder { Id = 62, CalculationId = 20 });
        await db.SaveChangesAsync();

        var response = await new OrderNavigationController(db).GetFlow(
            entityType, entityId, new OrderFlowNavigationService(db), default);
        var items = Assert.IsAssignableFrom<IReadOnlyList<OrderFlowNavigationItemDto>>(
            Assert.IsType<OkObjectResult>(response.Result).Value);

        Assert.Equal("Product", Assert.Single(items, x => x.Type == "product").Name);
        Assert.Equal(new[] { 20, 21 }, Ids(items, "calculation").Order().ToArray());
        Assert.Equal(new[] { 31, 30 }, Ids(items, "inquiry"));
        Assert.Equal(new[] { 41, 40 }, Ids(items, "quotation"));
        Assert.Equal(new[] { 51, 50 }, Ids(items, "supplierOrder"));
        Assert.Equal(new[] { 62, 61, 60 }, Ids(items, "customerOrder"));
        Assert.Equal("ORDER-50", Assert.Single(items, x => x.Type == "supplierOrder" && x.Id == 50).Number);
        Assert.Equal("ORDER-50-1", Assert.Single(items, x => x.Type == "customerOrder" && x.Id == 60).Number);
        Assert.Equal(new[] { 21, 31, 41, 51, 61 }, items.Where(x => x.IsArchived).Select(x => x.Id).ToArray());
        Assert.Equal(items.Count, items.Select(x => (x.Type, x.Id)).Distinct().Count());
    }

    [Theory]
    [InlineData("inquiry", 30)]
    [InlineData("quotation", 40)]
    [InlineData("supplierorder", 50)]
    [InlineData("customerorder", 60)]
    public async Task FlowIncludesStandaloneDocumentChainWithoutCalculation(string entityType, int entityId)
    {
        await using var db = CreateContext();
        db.Inquiries.Add(new Inquiry { Id = 30, CustomerName = "Legacy customer" });
        db.Quotations.Add(new Quotation { Id = 40, InquiryId = 30, CustomerName = "Legacy customer" });
        db.SupplierOrders.Add(new SupplierOrder
        {
            Id = 50, QuotationId = 40, SupplierName = "Legacy supplier", Customer2 = "Legacy customer",
        });
        db.CustomerOrders.Add(new CustomerOrder { Id = 60, SupplierOrderId = 50, CustomerName = "Legacy customer" });
        await db.SaveChangesAsync();

        var items = (await new OrderFlowNavigationService(db).GetFlowAsync(entityType, entityId, default))!;

        Assert.Equal(new[] { "inquiry", "quotation", "supplierOrder", "customerOrder" }, items.Select(x => x.Type).ToArray());
        Assert.All(items, x => Assert.Equal("Legacy customer", x.CustomerName));
        Assert.Equal("Legacy supplier", Assert.Single(items, x => x.Type == "supplierOrder").SupplierName);
        Assert.All(items, x => Assert.False(x.IsArchived));
    }

    [Theory]
    [InlineData("product")]
    [InlineData("calculation")]
    [InlineData("inquiry")]
    [InlineData("quotation")]
    [InlineData("supplierorder")]
    [InlineData("customerorder")]
    public async Task GroupedFlowReturnsNotFoundForMissingEntity(string entityType)
    {
        await using var db = CreateContext();

        var response = await new OrderNavigationController(db).GetFlow(
            entityType, 999, new OrderFlowNavigationService(db), default);

        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Fact]
    public async Task GroupedFlowRejectsUnsupportedEntityType()
    {
        await using var db = CreateContext();

        var response = await new OrderNavigationController(db).GetFlow(
            "unknown", 1, new OrderFlowNavigationService(db), default);

        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Fact]
    public async Task ProductWithoutCalculationsStillAppearsInFlow()
    {
        await using var db = CreateContext();
        db.Products.Add(new Product { Id = 10, Name = "Standalone product" });
        await db.SaveChangesAsync();

        var items = await new OrderFlowNavigationService(db).GetFlowAsync("product", 10, default);

        Assert.Equal("Standalone product", Assert.Single(items!).Name);
    }

    private static int[] Ids(IReadOnlyList<OrderFlowNavigationItemDto> items, string type) =>
        items.Where(x => x.Type == type).Select(x => x.Id).ToArray();

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
