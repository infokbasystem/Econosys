using System.Xml.Linq;
using Econosys.Api.Models;
using Econosys.Api.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xunit;

namespace Econosys.Api.Tests;

public class DocumentProductPdfTests
{
    [Theory]
    [InlineData("inquiry")]
    [InlineData("quotation")]
    [InlineData("customerorder")]
    [InlineData("supplierorder")]
    public void PrintsSavedVarnishAndExternalNoteButNeverInternalFields(string type)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = false;
        var texts = new DocumentPdfTranslations("EN");
        IDocument document = type switch
        {
            "inquiry" => InquiryPdfDocument.CreateDocument(new Inquiry
            {
                Color = "4", Varnish = "Saved varnish", PrintMethod = "SECRET METHOD",
                ProductNoteInternal = "SECRET INTERNAL", ProductNoteExternal = "Saved external note",
            }, null, texts),
            "quotation" => QuotationPdfDocument.CreateDocument(new Quotation
            {
                Color = "4", Varnish = "Saved varnish", PrintMethod = "SECRET METHOD",
                ProductNoteInternal = "SECRET INTERNAL", ProductMessage = "Saved external note",
            }, null, texts),
            "customerorder" => CustomerOrderPdfDocument.CreateDocument(new CustomerOrder
            {
                Color = "4", Varnish = "Saved varnish", PrintMethod = "SECRET METHOD",
                ProductNoteInternal = "SECRET INTERNAL", ProductMessage = "Saved external note",
            }, null, texts),
            _ => SupplierOrderPdfDocument.CreateDocument(new SupplierOrder
            {
                Color = "4", Varnish = "Saved varnish", PrintMethod = "SECRET METHOD",
                ProductNoteInternal = "SECRET INTERNAL", ProductNoteExternal = "Saved external note", Message = "Order message",
            }, null, texts),
        };
        var text = string.Join(" ", document.GenerateSvg().SelectMany(svg => XDocument.Parse(svg).Descendants())
            .Where(element => element.Name.LocalName == "text").Select(element => element.Value));
        Assert.Contains("4 + Saved varnish", text);
        Assert.Contains("Saved external note", text);
        Assert.DoesNotContain("SECRET METHOD", text);
        Assert.DoesNotContain("SECRET INTERNAL", text);
        if (type == "supplierorder") Assert.Contains("Order message", text);
    }

    [Fact]
    public void LegacyColorIsNotModifiedWhenSeparateVarnishIsEmpty()
    {
        Assert.Equal("4 + existing varnish", DocumentProductText.ColorWithVarnish("4 + existing varnish", null));
        Assert.Equal("Varnish", DocumentProductText.ColorWithVarnish(null, "Varnish"));
    }
}
