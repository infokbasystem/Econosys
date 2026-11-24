using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Econosys.Api.Tests;

public class FinanceCreditTests
{
    [Fact]
    public async Task UnbookedTotalsSubtractNegativeAndPositiveCreditRepresentations()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Invoices.AddRange(
            new Invoice { Id = 1, InvoiceDate = new DateTime(2026, 11, 1), InvoiceTypeCode = InvoiceTypeCode.Normal,
                InvoiceRows = [new InvoiceRow { Id = 1, Sum = 100 }] },
            new Invoice { Id = 2, InvoiceDate = new DateTime(2026, 11, 1), InvoiceTypeCode = InvoiceTypeCode.Credit,
                InvoiceRows = [new InvoiceRow { Id = 2, Sum = -30 }] },
            new Invoice { Id = 3, InvoiceDate = new DateTime(2026, 11, 1), InvoiceTypeCode = InvoiceTypeCode.Credit,
                InvoiceRows = [new InvoiceRow { Id = 3, Sum = 20 }] });
        await db.SaveChangesAsync();
        var controller = new FinanceController(db, null!, null!);

        var response = await controller.GetUnbookedInvoices(new SearchUnbookedInvoicesRequest());
        var result = Assert.IsType<PagedResultWithTotalsDto<UnbookedInvoiceRowDto, UnbookedInvoiceTotalsDto>>(
            Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(100m, Assert.Single(result.Items, row => row.InvoiceId == 1).Amount);
        Assert.Equal(-30m, Assert.Single(result.Items, row => row.InvoiceId == 2).Amount);
        Assert.Equal(-20m, Assert.Single(result.Items, row => row.InvoiceId == 3).Amount);
        Assert.Equal(50m, result.Totals!.Values.TotalAmount);
    }
}
