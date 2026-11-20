using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Econosys.Api.Services;

public static class DeviationPdfDocument
{
    public static byte[] Generate(int id) => CreateDocument(id).GeneratePdf();

    public static IDocument CreateDocument(int id) =>
        Document.Create(document => document.Page(page =>
        {
            DocumentPdfLayout.Configure(page);
            page.Header().PaddingBottom(24).Element(DocumentPdfLayout.Logo);
            page.Content().PaddingTop(75).AlignCenter()
                .Text(id.ToString(CultureInfo.InvariantCulture));
        }));
}
