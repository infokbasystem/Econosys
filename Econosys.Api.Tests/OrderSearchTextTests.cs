using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Econosys.Api.Tests;

public class OrderSearchTextTests
{
    [Theory]
    [InlineData("inquiry")]
    [InlineData("quotation")]
    [InlineData("customer")]
    [InlineData("supplier")]
    public async Task SeparateInputsMatchOnlyTheirDisplayedFields(string kind)
    {
        await using var db = await CreateContext();

        var products = await Search(db, kind, product: "  bOX%_  ");
        Assert.Equal(new[] { 1, 2, 3 }, products.Ids);
        var suppliers = await Search(db, kind, supplier: " mILL ");
        Assert.Equal(new[] { 1, 2, 4 }, suppliers.Ids);
        var customers = await Search(db, kind, customer: " sTORE ");
        Assert.Equal(new[] { 1, 2, 3 }, customers.Ids);
        var fallbackCustomer = await Search(db, kind, customer: "box%_");
        Assert.Equal(new[] { 4 }, fallbackCustomer.Ids);
    }

    [Theory]
    [InlineData("inquiry")]
    [InlineData("quotation")]
    [InlineData("customer")]
    [InlineData("supplier")]
    public async Task InputsCombineBeforeCountingAndPagination(string kind)
    {
        await using var db = await CreateContext();

        var page = await Search(db, kind, product: "box%_", supplier: "mill", customer: "store",
            pageNumber: 2, pageSize: 1);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { 2 }, page.Ids);
    }

    [Theory]
    [InlineData("inquiry")]
    [InlineData("quotation")]
    [InlineData("customer")]
    [InlineData("supplier")]
    public async Task BlankInputsIncludeMissingFieldsAndClearingRestoresResults(string kind)
    {
        await using var db = await CreateContext();

        Assert.Empty((await Search(db, kind, supplier: "missing")).Ids);
        var page = await Search(db, kind, product: " ", supplier: "", customer: "  ");
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, page.Ids);
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("supplier")]
    public async Task CustomerOrderNumberFilterMatchesTheCustomersReferenceOnly(string kind)
    {
        await using var db = await CreateContext();

        var matches = await Search(db, kind, customerReference: "  Po%_  ");
        Assert.Equal(new[] { 1, 2 }, matches.Ids);

        var cleared = await Search(db, kind, customerReference: "  ");
        Assert.Equal(5, cleared.TotalCount);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, cleared.Ids);
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("supplier")]
    public async Task CustomerOrderNumberFilterCombinesBeforeCountingAndPagination(string kind)
    {
        await using var db = await CreateContext();

        var page = await Search(db, kind, product: "box", supplier: "mill", customer: "store",
            customerReference: "po%_", pageNumber: 2, pageSize: 1);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { 2 }, page.Ids);
    }

    private static async Task<ApplicationDbContext> CreateContext()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var fields = new (string? Product, string? Supplier, string? Customer)[]
        {
            ("Box%_ packaging", "Paper Mill", "Store"),
            ("Box%_ packaging", "Paper Mill", "Store"),
            ("Box%_ packaging", "Other", "Store"),
            ("Store", "Paper Mill", "Box%_ packaging"),
            (null, null, null),
        };
        for (var index = 0; index < fields.Length; index++)
        {
            var id = index + 1;
            var (product, supplierName, customerName) = fields[index];
            var customer = customerName != null ? new Customer { Id = id, Name = customerName } : null;
            if (customer != null) db.Customers.Add(customer);
            var supplier = supplierName != null ? new Supplier { Id = id, Name = supplierName } : null;
            if (supplier != null) db.Suppliers.Add(supplier);
            var inquiry = new Inquiry
            {
                Id = id, Product = product, CustomerName = customerName,
                CustomerId = id < 4 ? customer?.Id : null,
            };
            db.Inquiries.Add(inquiry);
            if (supplier != null)
            {
                db.InquiryRecipients.Add(new InquiryRecipient { Id = id, InquiryId = id, SupplierId = supplier.Id });
                inquiry.SelectedInquiryRecipientId = id;
            }
            db.Quotations.Add(new Quotation
            {
                Id = id, InquiryId = id, Product = product, CustomerName = customerName,
                CustomerId = inquiry.CustomerId,
            });
            db.SupplierOrders.Add(new SupplierOrder
            {
                Id = id, Product = product, SupplierName = supplierName, CustomerId = customer?.Id,
                SupplierOrderNr = id == 4 ? "PO%_ wrong column" : $"SO-{id}",
                CustomerOrderNr = id switch { 1 => "PO%_ first", 2 => "po%_ second", 3 or 4 => "Other", _ => null },
            });
            db.CustomerOrders.Add(new CustomerOrder
            {
                Id = id, Product = product, SupplierOrderId = id, CustomerName = customerName,
                CustomerId = inquiry.CustomerId,
                CustomerOrderNr = id == 4 ? "PO%_ wrong column" : $"CO-{id}",
                YourOrderNr = id switch { 1 => "PO%_ first", 2 => "po%_ second", 3 or 4 => "Other", _ => null },
            });
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    private static async Task<(int TotalCount, int[] Ids)> Search(
        ApplicationDbContext db, string kind, string? product = null, string? supplier = null,
        string? customer = null, string? customerReference = null, int pageNumber = 1, int pageSize = 25)
    {
        var pagination = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
        var orderBy = new List<SortRequest> { new() { Field = "id", Direction = "asc" } };
        switch (kind)
        {
            case "inquiry":
                return GetPage(await new InquiriesController(db, NullLogger<InquiriesController>.Instance, null!)
                    .Search(new SearchInquiriesRequest
                    {
                        Product = product, SupplierName = supplier, CustomerName = customer,
                        Pagination = pagination, OrderBy = orderBy,
                    }), x => x.Id);
            case "quotation":
                return GetPage(await new QuotationsController(db, NullLogger<QuotationsController>.Instance, null!, null!)
                    .Search(new SearchQuotationsRequest
                    {
                        Product = product, SupplierName = supplier, CustomerName = customer,
                        Pagination = pagination, OrderBy = orderBy,
                    }), x => x.Id);
            case "customer":
                return GetPage(await new CustomerOrdersController(db, NullLogger<CustomerOrdersController>.Instance, null!)
                    .Search(new SearchCustomerOrdersRequest
                    {
                        Product = product, SupplierName = supplier, CustomerName = customer,
                        YourOrderNr = customerReference,
                        Pagination = pagination, OrderBy = orderBy,
                    }), x => x.Id);
            case "supplier":
                return GetPage(await new SupplierOrdersController(db, NullLogger<SupplierOrdersController>.Instance, null!, null!)
                    .Search(new SearchSupplierOrdersRequest
                    {
                        Product = product, SupplierName = supplier, CustomerName = customer,
                        CustomerOrderNr = customerReference,
                        Pagination = pagination, OrderBy = orderBy,
                    }), x => x.Id);
            default:
                throw new ArgumentException($"Unknown search kind '{kind}'.", nameof(kind));
        }
    }

    private static (int TotalCount, int[] Ids) GetPage<T>(
        ActionResult<PagedResultDto<T>> response, Func<T, int> getId)
    {
        var page = Assert.IsType<PagedResultDto<T>>(Assert.IsType<OkObjectResult>(response.Result).Value);
        return (page.TotalCount, page.Items.Select(getId).ToArray());
    }
}
