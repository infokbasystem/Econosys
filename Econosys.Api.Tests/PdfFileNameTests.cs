using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Econosys.Api.Tests;

public class PdfFileNameTests
{
    [Theory]
    [InlineData("supplierorder", 1, "Beställning 7.pdf")]
    [InlineData("supplierorder", 2, "Supplier order 7.pdf")]
    [InlineData("inquiry", null, "Förfrågan 7.pdf")]
    [InlineData("inquiry", 2, "Inquiry 7.pdf")]
    [InlineData("quotation", 1, "Affärsförslag 7.pdf")]
    [InlineData("quotation", 2, "Proposal 7.pdf")]
    [InlineData("customerorder", 1, "Ordererkännande 39697.pdf")]
    [InlineData("customerorder", 2, "Customer order 39697.pdf")]
    public async Task FileNameEndpointFollowsLegacyLanguage(string documentType, int? languageId, string expected)
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Suppliers.Add(new Supplier { Id = 3, LanguageId = languageId });
        db.Customers.Add(new Customer { Id = 4, LanguageId = languageId });
        db.SupplierOrders.Add(new SupplierOrder { Id = 7, SupplierId = 3 });
        db.Inquiries.Add(new Inquiry { Id = 7, SelectedInquiryRecipientId = 5 });
        db.InquiryRecipients.Add(new InquiryRecipient { Id = 5, InquiryId = 7, SupplierId = 3 });
        db.Quotations.Add(new Quotation { Id = 7, CustomerId = 4 });
        db.CustomerOrders.Add(new CustomerOrder { Id = 7, CustomerId = 4, CustomerOrderNr = "39697" });
        await db.SaveChangesAsync();

        var result = await new PdfController(db).GetPdfFileName(documentType, 7);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expected, Assert.IsType<PdfFileNameResponse>(ok.Value).FileName);
    }

    [Fact]
    public async Task FileNameEndpointRejectsUnknownTypeAndMissingDocument()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var controller = new PdfController(db);

        Assert.IsType<BadRequestObjectResult>((await controller.GetPdfFileName("unknown", 1)).Result);
        Assert.IsType<NotFoundObjectResult>((await controller.GetPdfFileName("supplierorder", 1)).Result);
    }

    [Fact]
    public void OtherPdfNamesUseLocalizedTitlesAndSpaces()
    {
        Assert.Equal("Faktura 42.pdf", PdfFileNames.Invoice(42, 1));
        Assert.Equal("Invoice 42.pdf", PdfFileNames.Invoice(42, 2));
        Assert.Equal("Avvikelse AV-42.pdf", PdfFileNames.Deviation("AV-42", null));
        Assert.Equal("Deviation AV-42.pdf", PdfFileNames.Deviation("AV-42", 2));
        Assert.Equal("Avrop 7.pdf", PdfFileNames.CallOff(false, null, 7));
        Assert.Equal("Call-off confirmation 7.pdf", PdfFileNames.CallOff(true, 2, 7));
    }
}
