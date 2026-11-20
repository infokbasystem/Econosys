using System.Xml.Linq;
using Econosys.Api.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xunit;

namespace Econosys.Api.Tests;

public class DeviationPdfTests
{
    public DeviationPdfTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = false;
    }

    [Fact]
    public void DocumentRendersDeviationId()
    {
        var svg = DeviationPdfDocument.CreateDocument(12345).GenerateSvg().Single();
        var renderedText = string.Join(" ", XDocument.Parse(svg).Descendants()
            .Where(element => element.Name.LocalName == "text")
            .Select(element => element.Value));

        Assert.Contains("12345", renderedText);
    }
}
