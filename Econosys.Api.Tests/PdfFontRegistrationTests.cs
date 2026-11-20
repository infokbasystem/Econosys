using Econosys.Api.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xunit;

namespace Econosys.Api.Tests;

public class PdfFontRegistrationTests
{
    public PdfFontRegistrationTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void RegistersDmSansForPdfGeneration()
    {
        PdfFontRegistration.Register();

        var pdf = Document.Create(document => document.Page(page =>
        {
            page.DefaultTextStyle(style => style.FontFamily("DM Sans"));
            page.Content().Text(text =>
            {
                text.Span("Regular ");
                text.Span("Bold ").Bold();
                text.Span("SemiBold ").SemiBold();
                text.Span("Italic").Italic();
                text.Span("BoldItalic").Bold().Italic();
                text.Span("SemiBoldItalic").SemiBold().Italic();
            });
        })).GeneratePdf();

        var pdfContent = System.Text.Encoding.Latin1.GetString(pdf);
        Assert.Contains("DMSans-Regular", pdfContent);
        Assert.Contains("DMSans-Bold", pdfContent);
        Assert.Contains("DMSans-SemiBold", pdfContent);
        Assert.Contains("DMSans-Italic", pdfContent);
        Assert.Contains("DMSans-BoldItalic", pdfContent);
        Assert.Contains("DMSans-SemiBoldItalic", pdfContent);
    }
}
