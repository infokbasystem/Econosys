using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Fluent;
using Xunit;

namespace Econosys.Api.Tests;

public class QuotationPdfTests
{
    public QuotationPdfTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseEnvironmentFonts = false;
    }

    [Theory]
    [InlineData(null, "Kund")]
    [InlineData(" en ", "Customer")]
    [InlineData("SW", "Kund")]
    [InlineData("unknown-language", "Kund")]
    public void LanguageDefaultsAreSafe(string? language, string expected)
    {
        Assert.Equal(expected, new DocumentPdfTranslations(language).Text(-90));
    }

    [Fact]
    public void TranslationsAreNormalizedAndDeterministic()
    {
        var translations = new DocumentPdfTranslations(" en ", new[]
        {
            new TranslationItem { Id = 2, TranslationCode = -90, LangCode = "EN", Translation = "Later" },
            new TranslationItem { Id = 1, TranslationCode = -90, LangCode = " en ", Translation = "Recipient" },
            new TranslationItem { Id = 3, TranslationCode = 200, LangCode = "SV", Translation = "Fallback" },
            new TranslationItem { Id = 4, TranslationCode = -93, LangCode = "EN", Translation = "QuotationValidityText" }
        });
        Assert.Equal("Recipient", translations.Text(-90));
        Assert.Equal("Fallback", translations.Text(200, "Master"));
        Assert.Equal("Master", translations.Text(201, "Master"));
        Assert.Equal("", translations.Text(-93));
    }

    [Fact]
    public void InvoiceStillRendersWithSharedLayout()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var bytes = InvoicePdfDocument.Generate(new Invoice
        {
            Id = 1,
            InvoiceNumber = 123,
            CustomerName = "Customer",
            InvoiceDate = new DateTime(2026, 11, 9)
        }, new CompanyInfo { CompanyName = "Econopack" }, "Configured tax text");

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        Assert.True(bytes.Length > 1000);
    }

    [Fact]
    public void InvoicePdfPrintsDeliveryDateAndTerms()
    {
        PdfFontRegistration.Register();
        var invoice = new Invoice
        {
            AccountDate = new DateTime(2026, 11, 1),
            InvoiceDate = new DateTime(2026, 11, 2),
            TimeOfDelivery = "2026-11-30",
            TermsOfDelivery = "DAP",
        };
        var text = XDocument.Parse(InvoicePdfDocument.CreateDocument(invoice, null, null).GenerateSvg().Single())
            .Descendants()
            .Where(element => element.Name.LocalName == "text")
            .Select(element => element.Value.Trim())
            .ToList();

        Assert.Contains("Leveransdatum", text);
        Assert.Contains("2026-11-30", text);
        Assert.Contains("Leveransvillkor", text);
        Assert.Contains("DAP", text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SlattDeliveryRowsMergeIntoTheirParent(bool fromStock)
    {
        PdfFontRegistration.Register();
        var invoice = CreateInvoiceWithSlattDelivery(fromStock);
        var svg = XDocument.Parse(InvoicePdfDocument.CreateDocument(invoice, null, null).GenerateSvg().Single());

        Assert.Single(svg.Descendants(), element => element.Name.LocalName == "text" && element.Value.Trim() == "Product");
        Assert.Contains("4052", svg.Root!.Value);
        Assert.Contains(svg.Descendants(), element =>
            element.Name.LocalName == "text" && element.Value.Contains("452", StringComparison.Ordinal));
        Assert.Contains("Vikt: 37 kg", svg.Root.Value);
    }

    [Fact]
    public void SlattDeliveryRowsRespectDoNotAggregateWithParent()
    {
        PdfFontRegistration.Register();
        var invoice = CreateInvoiceWithSlattDelivery(fromStock: false, doNotAggregateWithParent: true);
        var svg = XDocument.Parse(InvoicePdfDocument.CreateDocument(invoice, null, null).GenerateSvg().Single());

        Assert.Equal(2, svg.Descendants().Count(element =>
            element.Name.LocalName == "text" && element.Value.Trim() == "Product"));
    }

    [Fact]
    public void InvoicePalletRowsWithSamePriceAndDescriptionMergeTogether()
    {
        PdfFontRegistration.Register();
        var invoice = new Invoice
        {
            InvoiceRows =
            [
                new InvoiceRow { Id = 1, PriceTypeId = 107, Text = "EUR-pall", NrOf = 9, UnitPrice = 145, Sum = 1305 },
                new InvoiceRow { Id = 2, PriceTypeId = 107, Text = "EUR-pall", NrOf = 1, UnitPrice = 145, Sum = 145 }
            ]
        };
        var svg = XDocument.Parse(InvoicePdfDocument.CreateDocument(invoice, null, null).GenerateSvg().Single());
        var text = svg.Descendants().Where(element => element.Name.LocalName == "text").ToList();

        Assert.Single(text, element => element.Value.Trim() == "EUR-pall");
        Assert.Contains(text, element => element.Value.Trim() == "10");
        Assert.Contains(text, element =>
            new string(element.Value.Where(char.IsDigit).ToArray()) == "145000");
    }

    private static Invoice CreateInvoiceWithSlattDelivery(bool fromStock, bool doNotAggregateWithParent = false)
    {
        var rows = new List<InvoiceRow>();
        if (fromStock)
        {
            var parent = new DeliveryFromStock { Id = 1 };
            var slatt = new DeliveryFromStock { Id = 2, IsSlattPallet = true, ParentDeliveryId = 1 };
            rows.AddRange([
                new InvoiceRow
                {
                    Id = 1, SortOrder = 20, DeliveryFromStockId = 1, DeliveryFromStock = parent,
                    Text = "Product\nVikt: 2 kg", NrOf = 4000, UnitPrice = 100, Sum = 400, Weight = 2
                },
                new InvoiceRow
                {
                    Id = 2, SortOrder = 10, DeliveryFromStockId = 2, DeliveryFromStock = slatt,
                    Text = "Product\nVikt: 35 kg", NrOf = 52, UnitPrice = 100, Sum = 52, Weight = 35,
                    DoNotAggregateWithParent = doNotAggregateWithParent
                }
            ]);
        }
        else
        {
            var parent = new DeliveryToCustomer { Id = 1 };
            var slatt = new DeliveryToCustomer { Id = 2, IsSlattPallet = true, ParentDeliveryId = 1 };
            rows.AddRange([
                new InvoiceRow
                {
                    Id = 1, SortOrder = 20, DeliveryToCustomerId = 1, DeliveryToCustomer = parent,
                    Text = "Product\nVikt: 2 kg", NrOf = 4000, UnitPrice = 100, Sum = 400, Weight = 2
                },
                new InvoiceRow
                {
                    Id = 2, SortOrder = 10, DeliveryToCustomerId = 2, DeliveryToCustomer = slatt,
                    Text = "Product\nVikt: 35 kg", NrOf = 52, UnitPrice = 100, Sum = 52, Weight = 35,
                    DoNotAggregateWithParent = doNotAggregateWithParent
                }
            ]);
        }

        return new Invoice { InvoiceRows = rows };
    }

    [Fact]
    public void InvoiceDatesEndAtTheSameRightEdge()
    {
        PdfFontRegistration.Register();
        var date = new DateTime(2026, 11, 10);
        var document = InvoicePdfDocument.CreateDocument(new Invoice { InvoiceDate = date, InvoiceDays = 0 }, null, null);
        var dates = XDocument.Parse(document.GenerateSvg().Single()).Descendants()
            .Where(element => element.Name.LocalName == "text" && element.Value.Trim() == "2026-11-10")
            .Take(2)
            .Select(FirstGlyphX)
            .ToArray();

        Assert.Equal(2, dates.Length);
        Assert.InRange(Math.Abs(dates[0] - dates[1]), 0, 0.01);
    }

    [Fact]
    public void InvoiceTitleAndNumberAreRightAligned()
    {
        PdfFontRegistration.Register();
        var document = InvoicePdfDocument.CreateDocument(new Invoice { InvoiceNumber = 19 }, null, null);
        var reference = Document.Create(document => document.Page(page =>
        {
            DocumentPdfLayout.Configure(page);
            page.Content().Column(column =>
            {
                column.Item().AlignRight().Text("Faktura").FontSize(16).SemiBold();
                column.Item().AlignRight().Text("19").FontSize(16).SemiBold();
            });
        }));

        var invoiceText = XDocument.Parse(document.GenerateSvg().Single());
        var referenceText = XDocument.Parse(reference.GenerateSvg().Single());
        foreach (var value in new[] { "Faktura", "19" })
        {
            var actual = Assert.Single(invoiceText.Descendants(), element => element.Name.LocalName == "text" && element.Value.Trim() == value);
            var expected = Assert.Single(referenceText.Descendants(), element => element.Name.LocalName == "text" && element.Value.Trim() == value);
            Assert.InRange(Math.Abs(FirstGlyphX(actual) - FirstGlyphX(expected)), 0, 0.01);
        }
    }

    [Fact]
    public void CreditInvoiceIdentifiesTheOriginalInvoice()
    {
        PdfFontRegistration.Register();
        var document = InvoicePdfDocument.CreateDocument(new Invoice
        {
            InvoiceTypeCode = InvoiceTypeCode.Credit,
            InvoiceNumber = 20,
            CreditingInvoiceNumber = 19,
        }, null, null);
        var text = XDocument.Parse(document.GenerateSvg().Single()).Descendants()
            .Where(element => element.Name.LocalName == "text")
            .Select(element => element.Value.Trim()).ToArray();
        Assert.Contains("Kreditfaktura", text);
        Assert.Contains("Krediterar nr 19", text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedPdfLayoutUsesNarrowerSideMarginsWithoutChangingVerticalMargins(bool printForScanning)
    {
        const string fill = "#123456";
        const double pointsPerCentimetre = 72 / 2.54;
        var document = Document.Create(document => document.Page(page =>
        {
            DocumentPdfLayout.Configure(page, printForScanning);
            page.Content().Background(fill).Extend().Text("");
        }));

        var svg = XDocument.Parse(document.GenerateSvg().Single());
        var content = Assert.Single(svg.Descendants(), element => element.Name.LocalName == "rect"
            && string.Equals((string?)element.Attribute("fill"), fill, StringComparison.OrdinalIgnoreCase));
        var translation = content.Attribute("transform")!.Value["translate(".Length..^1].Split(' ');
        var left = double.Parse(translation[0], CultureInfo.InvariantCulture);
        var top = double.Parse(translation[1], CultureInfo.InvariantCulture);
        var right = PageSizes.A4.Width - left - (double)content.Attribute("width")!;
        var bottom = PageSizes.A4.Height - top - (double)content.Attribute("height")!;

        Assert.Equal(0.85 * pointsPerCentimetre, left, 2);
        Assert.Equal(0.85 * pointsPerCentimetre, right, 2);
        Assert.Equal(1.5 * pointsPerCentimetre, top, 2);
        Assert.Equal(1.5 * pointsPerCentimetre, bottom, 2);
    }

    private static double FirstGlyphX(XElement element)
    {
        var translation = element.Attribute("transform")!.Value["translate(".Length..].Split(' ')[0];
        var firstGlyph = element.Attribute("x")!.Value.Split(',')[0];
        return double.Parse(translation, CultureInfo.InvariantCulture)
            + double.Parse(firstGlyph, CultureInfo.InvariantCulture);
    }

    [Fact]
    public void InvoiceSummaryAmountsAlignWithLineTotals()
    {
        PdfFontRegistration.Register();
        var invoice = new Invoice
        {
            InvoiceRows =
            [
                new() { Id = 1, Sum = 10 },
                new() { Id = 2, Sum = 20 },
                new() { Id = 3, Sum = 30 },
                new() { Id = 4, Sum = -60 },
                new() { Id = 5, Sum = 10, PriceTypeId = 13 },
                new() { Id = 6, Sum = 20, PriceTypeId = 12 }
            ]
        };
        var document = InvoicePdfDocument.CreateDocument(invoice, null, null);
        var text = XDocument.Parse(document.GenerateSvg().Single()).Descendants()
            .Where(element => element.Name.LocalName == "text").ToList();

        foreach (var amount in new[] { 10m, 20m, 30m })
        {
            var matches = text.Where(element => element.Value.Trim() == $"{amount:N2} SEK").ToArray();
            Assert.Equal(2, matches.Length);
            Assert.InRange(Math.Abs(FirstGlyphX(matches[0]) - FirstGlyphX(matches[1])), 0, 0.01);
        }
    }

    [Fact]
    public void InvoiceScanningModeUsesBlackInkAndNoBackground()
    {
        PdfFontRegistration.Register();
        var invoice = new Invoice
        {
            Customer = new Customer { PrintForDocumentScanning = true },
            InvoiceRows = [new InvoiceRow { Text = "Test product", Sum = 100 }]
        };
        var document = InvoicePdfDocument.CreateDocument(invoice, new CompanyInfo { Bank = "Test Bank" }, "Configured tax text");
        var svg = XDocument.Parse(document.GenerateSvg().Single());
        var image = Assert.Single(svg.Descendants(), element => element.Name.LocalName == "image");
        var imageData = Assert.Single(image.Attributes(), attribute => attribute.Name.LocalName == "href").Value;
        using var logo = SkiaSharp.SKBitmap.Decode(Convert.FromBase64String(imageData[(imageData.IndexOf(',') + 1)..]));
        var visiblePixels = logo.Pixels.Where(pixel => pixel.Alpha > 0).ToArray();
        Assert.NotEmpty(visiblePixels);
        Assert.True(visiblePixels.All(pixel => pixel.Red == 0 && pixel.Green == 0 && pixel.Blue == 0));

        var colors = svg.Descendants().Attributes()
            .Where(attribute => attribute.Name.LocalName is "fill" or "stroke")
            .Select(attribute => attribute.Value.ToUpperInvariant()).Distinct().ToList();
        Assert.All(colors, color => Assert.Contains(color, new[] { "#000000", "#FFFFFF", "NONE" }));
        Assert.Contains("Configured tax text", svg.Root!.Value);
        Assert.Contains("Test Bank", svg.Root.Value);

        invoice.Customer.PrintForDocumentScanning = false;
        var standard = XDocument.Parse(InvoicePdfDocument.CreateDocument(invoice, null, null).GenerateSvg().Single());
        Assert.Equal(2, standard.Descendants().Count(element => element.Name.LocalName == "image"));
        Assert.Contains(standard.Descendants().Attributes("fill"), attribute =>
            string.Equals(attribute.Value, "#f1f4f3", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InvoiceRoundsOnlyTheOuterEdgesOfTheShadedHeader()
    {
        PdfFontRegistration.Register();
        var document = InvoicePdfDocument.CreateDocument(new Invoice(), null, null);
        var svg = XDocument.Parse(document.GenerateSvg().Single());
        var headerShapes = svg.Descendants()
            .Where(element => string.Equals((string?)element.Attribute("fill"), "#f1f4f3", StringComparison.OrdinalIgnoreCase))
            .ToList();

        string? FindClip(XElement shape) =>
            shape.Ancestors().Attributes("clip-path").FirstOrDefault()?.Value;

        void AssertTwoRoundedCorners(XElement shape)
        {
            var clip = Assert.Single(svg.Descendants(), element => element.Name.LocalName == "clipPath"
                && $"url(#{(string?)element.Attribute("id")})" == FindClip(shape));
            var path = Assert.Single(clip.Elements(), element => element.Name.LocalName == "path");
            Assert.Equal(2, ((string?)path.Attribute("d") ?? "").Count(command => command == 'C'));
        }

        Assert.Collection(headerShapes,
            AssertTwoRoundedCorners,
            shape => Assert.Null(FindClip(shape)),
            shape => Assert.Null(FindClip(shape)),
            AssertTwoRoundedCorners);
    }

    [Fact]
    public void InvoiceFooterRendersConfiguredContactDetailsAndItalicTaxInfo()
    {
        const string vatInfo = "Configured tax text";
        PdfFontRegistration.Register();
        var company = new CompanyInfo
        {
            Address = "Testgatan 19", ZipCode = "431 49", PostalAddress = "Mölndal",
            Telephone1 = "0313633502", Telephone2 = "+46 (0)31 363 35 02",
            Email = "test@example.com", Web = "www.example.com", Bank = "Test Bank"
        };
        var document = Document.Create(document =>
        {
            document.Page(page =>
            {
                DocumentPdfLayout.Configure(page);
                page.Footer().Element(container => DocumentPdfLayout.InvoiceFooter(container, company, vatInfo));
            });
        });
        var textElements = XDocument.Parse(document.GenerateSvg().Single()).Descendants()
            .Where(element => element.Name.LocalName == "text").ToList();
        var renderedText = string.Join(" ", textElements.Select(element => element.Value));

        Assert.Contains("Testgatan 19, 431 49 Mölndal \u2022 Sverige \u2022 T +46 (0)31 363 35 02", renderedText);
        Assert.Contains("\u2022 test@example.com \u2022 www.example.com", renderedText);
        Assert.DoesNotContain("0313633502", renderedText);
        Assert.Contains("Test Bank", renderedText);
        Assert.Contains(vatInfo, renderedText);
        Assert.DoesNotContain("F-skatt", renderedText);
        Assert.All(textElements, element =>
            Assert.Equal("#4b5563", element.AncestorsAndSelf().Attributes("fill").FirstOrDefault()?.Value, ignoreCase: true));
        Assert.Contains("DMSans-Italic", Encoding.Latin1.GetString(document.GeneratePdf()));
    }

    [Theory]
    [InlineData("SV")]
    [InlineData("EN")]
    public void QuotationRendersOnePage(string language)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var quotation = Fixture();
        var document = QuotationPdfDocument.CreateDocument(quotation, new CompanyInfo
        {
            CompanyName = "Econopack", Address = "Jolengatan 19", ZipCode = "431 49", PostalAddress = "Molndal",
            Email = "info@econopack.se", Web = "www.econopack.se"
        }, new DocumentPdfTranslations(language));

        Assert.Single(document.GenerateImages());
        var bytes = document.GeneratePdf();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        var artifactDirectory = Environment.GetEnvironmentVariable("ECONOSYS_PDF_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(artifactDirectory))
        {
            Directory.CreateDirectory(artifactDirectory);
            File.WriteAllBytes(Path.Combine(artifactDirectory, $"quotation-{language}.pdf"), bytes);
            File.WriteAllBytes(Path.Combine(artifactDirectory, $"quotation-{language}.png"), document.GenerateImages().Single());
            File.WriteAllText(Path.Combine(artifactDirectory, $"quotation-{language}.svg"), document.GenerateSvg().Single());
        }
    }

    [Fact]
    public void SavedPricesAndCalculationInfoArePrintedWithoutRecalculation()
    {
        var quotation = Fixture();
        var content = QuotationPdfDocument.BuildContent(quotation, new DocumentPdfTranslations("EN"));
        Assert.Equal("SEK/1000pcs", content.PriceHeading);
        Assert.Equal("7,230", content.Prices[0].Price);
        Assert.Equal("2+1 colours", content.Prices[0].Note);
        Assert.Equal("6,400", content.Prices[0].Quantity);
    }

    [Fact]
    public void LongContentPaginates()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var quotation = Fixture();
        quotation.ProductMessage = string.Join("\n", Enumerable.Repeat("A long product note that must wrap without clipping.", 65));
        quotation.QuotationRows = Enumerable.Range(1, 100).Select(id => new QuotationRow
        {
            Id = id, Edition = id * 1000, Price = 123.45,
            CalculationRow = new CalculationRow { Info = "2+1 colours, detailed specification" }
        }).ToList();
        var pages = QuotationPdfDocument.CreateDocument(quotation, null, new DocumentPdfTranslations("SV")).GenerateImages().ToList();
        Assert.InRange(pages.Count, 2, 15);
    }

    [Fact]
    public void CostsUseSelectedVersionAndLegacyDebitAndWordingRules()
    {
        var quotation = Fixture();
        var costType = new Cost { Name = "Tooling", TranslationCodeQuotationKnown = 501, TranslationCodeQuotationUnknown = 502 };
        quotation.OrderCosts = new List<OrderCost>
        {
            new() { Id = 1, DoDebit = true, DoPrintOnQuotation = true, Cost = costType, OutPrice = 4503, CalculationRowId = 50 },
            new() { Id = 2, DoDebit = true, DoPrintOnQuotation = true, Cost = costType, OutPrice = 0 },
            new() { Id = 3, DoDebit = true, DoPrintOnQuotation = true, Cost = costType },
            new() { Id = 4, DoDebit = false, DoPrintOnQuotation = true, Cost = costType, OutPrice = 100 },
            new() { Id = 5, DoDebit = true, DoPrintOnQuotation = false, Cost = costType, OutPrice = 100 },
            new() { Id = 6, DoDebit = true, SupplierOrderId = 20, Cost = costType, OutPrice = 125, NrOf = 2 },
            new() { Id = 7, DoDebit = true, SupplierOrderId = 21, Cost = costType, OutPrice = 700 },
            new() { Id = 8, DoDebit = true, Cost = costType, OutPrice = 125, NrOf = 2 }
        };
        var texts = new DocumentPdfTranslations("EN", new[]
        {
            new TranslationItem { TranslationCode = 501, LangCode = "EN", Translation = "Known tooling" },
            new TranslationItem { TranslationCode = 502, LangCode = "EN", Translation = "Tooling will be added" }
        });
        var costs = QuotationPdfDocument.BuildContent(quotation, texts).Costs;
        Assert.Equal(5, costs.Count);
        Assert.Contains(costs, cost => cost.Description == "Known tooling" && cost.Amount == "0 SEK");
        Assert.Contains(costs, cost => cost.Description == "Known tooling" && cost.Amount == "100 SEK");
        Assert.Contains(costs, cost => cost.Description == "Tooling will be added" && cost.Amount == "250 SEK");
        Assert.Contains(costs, cost => cost.Description == "Tooling will be added" && cost.Amount == "");
        Assert.Contains(costs, cost => cost.Amount == "4,503 SEK" && cost.EditionInfo.Contains("6400 pcs and price 7,230"));

        var selected = Assert.Single(QuotationPdfDocument.BuildContent(quotation, texts, supplierOrderId: 20).Costs);
        Assert.Equal("Known tooling", selected.Description);
        Assert.Equal("250 SEK", selected.Amount);
    }

    [Theory]
    [InlineData("SW", "Svensk verktygstext")]
    [InlineData("EN", "English tool retention text")]
    public void ToolRetentionTextIsRenderedAfterCostsUsingTheSelectedLanguage(string language, string translatedText)
    {
        var quotation = Fixture();
        quotation.HideExtraCostsOnPrint = true;
        quotation.OrderCosts.Add(new OrderCost
        {
            DoDebit = true, DoPrintOnQuotation = false, OutPrice = 100,
            Cost = new Cost { Name = "Tooling", DoPrintScrapToolsTextOnCustomerOrder = true }
        });
        var texts = new DocumentPdfTranslations(language, new[]
        {
            new TranslationItem { TranslationCode = -94, LangCode = language, Translation = translatedText }
        });
        var svg = QuotationPdfDocument.CreateDocument(quotation, null, texts, "Customer greeting").GenerateSvg().Single();
        var elements = XDocument.Parse(svg).Descendants()
            .Where(element => element.Name.LocalName == "text").Select(element => element.Value).ToList();
        var cost = elements.FindIndex(value => value.Contains("Tooling"));
        var retention = elements.FindIndex(value => value.Contains(translatedText));
        var greeting = elements.FindIndex(value => value.Contains("Customer greeting"));

        Assert.True(cost >= 0 && retention > cost && greeting > retention);
    }

    [Fact]
    public void ToolRetentionTextRequiresDebitedFlaggedCostInSelectedVersion()
    {
        var quotation = Fixture();
        var flagged = new Cost { Name = "Flagged", DoPrintScrapToolsTextOnCustomerOrder = true };
        var unflagged = new Cost { Name = "Unflagged" };
        var costs = new List<OrderCost>
        {
            new() { DoDebit = false, Cost = flagged },
            new() { DoDebit = true, Cost = unflagged },
            new() { DoDebit = true, SupplierOrderId = 20, Cost = flagged },
            new() { DoDebit = true, SupplierOrderId = 21, Cost = null }
        };
        var texts = new DocumentPdfTranslations("EN");

        Assert.False(QuotationPdfDocument.BuildContent(quotation, texts, costs).ShowToolRetentionText);
        Assert.True(QuotationPdfDocument.BuildContent(quotation, texts, costs, 20).ShowToolRetentionText);
        Assert.False(QuotationPdfDocument.BuildContent(quotation, texts, costs, 21).ShowToolRetentionText);

        var svg = QuotationPdfDocument.CreateDocument(quotation, null, texts, orderCosts: costs).GenerateSvg().Single();
        var rendered = string.Join(" ", XDocument.Parse(svg).Descendants()
            .Where(element => element.Name.LocalName == "text").Select(element => element.Value));
        Assert.DoesNotContain("Die-cutting tools and printing plates are stored", rendered);
    }

    [Fact]
    public void PalletUnknownPriceAndQuantityCostsAreNotInvented()
    {
        var quotation = Fixture();
        quotation.EurPallet = true;
        quotation.PalletFormat = new PalletFormat { TranslationCode = -66 };
        quotation.EurPalletValue = 50;
        quotation.OrderCosts.Add(new OrderCost
        {
            Id = 1, DoDebit = true, DoPrintOnQuotation = true, OutPrice = 20, NrOf = 3,
            Cost = new Cost { Name = "Plates", IsNrOf = true }
        });
        var costs = QuotationPdfDocument.BuildContent(quotation, new DocumentPdfTranslations("EN")).Costs;
        Assert.Equal("", costs[0].Amount);
        Assert.Contains("will be added", costs[0].Description);
        Assert.Equal("3 pcs. * 20 = 60 SEK", costs[1].Amount);
        quotation.NrOfEurPallet = 2;
        Assert.Equal("2 pcs. * 50 = 100 SEK", QuotationPdfDocument.BuildContent(quotation, new DocumentPdfTranslations("EN")).Costs[0].Amount);
        quotation.EurPalletValue = 0;
        Assert.Empty(QuotationPdfDocument.BuildContent(quotation, new DocumentPdfTranslations("EN")).Costs[0].Amount);
    }

    [Fact]
    public void RowPrintSelectionOnlyHidesTheChosenCalculationInfo()
    {
        var quotation = Fixture();
        var rows = quotation.QuotationRows.OrderBy(row => row.Id).ToList();
        rows[0].CalculationRow!.Info = "HIDDEN ROW INFO";
        rows[0].PrintInfo = false;
        rows[1].CalculationRow = new CalculationRow { Info = "VISIBLE ROW INFO" };
        var texts = new DocumentPdfTranslations("EN");
        var content = QuotationPdfDocument.BuildContent(quotation, texts);
        Assert.Empty(content.Prices[0].Note);
        Assert.Equal("VISIBLE ROW INFO", content.Prices[1].Note);
        var svg = QuotationPdfDocument.CreateDocument(quotation, null, texts).GenerateSvg().Single();
        var rendered = string.Join(" ", XDocument.Parse(svg).Descendants()
            .Where(element => element.Name.LocalName == "text").Select(element => element.Value));
        Assert.DoesNotContain("HIDDEN ROW INFO", rendered);
        Assert.Contains("VISIBLE ROW INFO", rendered);
        rows[0].PrintInfo = true;
        Assert.Equal("HIDDEN ROW INFO", QuotationPdfDocument.BuildContent(quotation, texts).Prices[0].Note);
    }

    [Theory]
    [InlineData("SV", "tillkommer")]
    [InlineData("EN", "extra")]
    public void HiddenExtraCostsReplaceAmountsButNotQuotationPrices(string language, string replacement)
    {
        var quotation = Fixture();
        quotation.EurPallet = true;
        quotation.NrOfEurPallet = 2;
        quotation.EurPalletValue = 50;
        quotation.OrderCosts = new List<OrderCost>
        {
            new() { Id = 1, DoDebit = true, OutPrice = 222, Cost = new Cost { Name = "Tooling" } },
            new() { Id = 2, DoDebit = true, OutPrice = 333, NrOf = 3, Cost = new Cost { Name = "Plates", IsNrOf = true } },
            new() { Id = 3, DoDebit = true, Cost = new Cost { Name = "Unknown" } },
            new() { Id = 4, DoDebit = true, SupplierOrderId = 20, OutPrice = 777, Cost = new Cost { Name = "Version" } }
        };
        var texts = new DocumentPdfTranslations(language);
        var original = QuotationPdfDocument.BuildContent(quotation, texts);
        Assert.All(original.Costs.Take(3), cost => Assert.NotEmpty(cost.Amount));

        quotation.HideExtraCostsOnPrint = true;
        var hidden = QuotationPdfDocument.BuildContent(quotation, texts);
        Assert.All(hidden.Costs.Take(3), cost => Assert.Equal(replacement, cost.Amount));
        Assert.Empty(hidden.Costs[3].Amount);
        Assert.Equal(original.Prices, hidden.Prices);
        Assert.Equal(original.Costs.Select(cost => cost.Description), hidden.Costs.Select(cost => cost.Description));
        Assert.Equal(replacement, QuotationPdfDocument.BuildContent(quotation, texts, supplierOrderId: 20).Costs[1].Amount);

        var svg = QuotationPdfDocument.CreateDocument(quotation, null, texts).GenerateSvg().Single();
        var rendered = XDocument.Parse(svg).Descendants().Where(element => element.Name.LocalName == "text")
            .Select(element => element.Value).ToList();
        Assert.Equal(3, rendered.Count(value => value.Trim() == replacement));
        Assert.DoesNotContain(rendered, value => value.Contains("222 SEK") || value.Contains("999 SEK"));
        Assert.Equal(222, quotation.OrderCosts.First().OutPrice);
    }

    [Fact]
    public async Task PdfEndpointAndTranslationSettingsAreReadOnly()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        await using var db = CreateContext();
        var quotation = Fixture();
        quotation.Customer = new Customer { Id = 20, Name = "Live name", Language = new Language { Id = 2, LanguageCode = "EN" } };
        quotation.OrderCosts.Add(new OrderCost { SupplierOrderId = 20, DoDebit = true, OutPrice = 100 });
        db.Quotations.Add(quotation);
        db.TranslationItems.Add(new TranslationItem
        {
            TranslationCode = DocumentPdfTranslations.ToolingDisclaimer, LangCode = "EN", Translation = "Custom tooling text"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var pdfController = new PdfController(db);
        var result = Assert.IsType<FileContentResult>(await pdfController.GetQuotationPdfById(quotation.Id));
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("Proposal 25691.pdf", result.FileDownloadName);
        Assert.True(result.EnableRangeProcessing);
        Assert.IsType<FileContentResult>(await pdfController.GetQuotationPdfById(quotation.Id, supplierOrderId: 20));
        Assert.IsType<NotFoundObjectResult>(await pdfController.GetQuotationPdfById(quotation.Id, supplierOrderId: 21));
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.IsType<NotFoundObjectResult>(await pdfController.GetQuotationPdfById(-1));

        var settingsController = new TranslationItemsController(db, NullLogger<TranslationItemsController>.Instance);
        var settings = Assert.IsType<TranslationItemsController.DocumentTextSettingsResponse>(
            Assert.IsType<OkObjectResult>((await settingsController.GetDocumentTexts(default)).Result).Value);
        Assert.Contains("EN", settings.Languages);
        Assert.Contains(settings.Definitions, definition => definition.Code == DocumentPdfTranslations.ToolingDisclaimer);
        Assert.Equal("Custom tooling text", Assert.Single(settings.Items).Translation);
        Assert.Equal(1, await db.TranslationItems.CountAsync());
        Assert.False((await db.Quotations.SingleAsync()).Printed);
    }

    [Fact]
    public async Task SavedStandardTextChangesAreLoadedOnNextRead()
    {
        await using var db = CreateContext();
        var controller = new TranslationItemsController(db, NullLogger<TranslationItemsController>.Instance);
        var request = new BatchUpsertTranslationItemsRequest
        {
            Items = new List<BatchUpsertTranslationItemRequestItem>
            {
                new() { TranslationCode = DocumentPdfTranslations.ToolingDisclaimer, LangCode = "SW", Translation = "Saved Swedish text" },
                new() { TranslationCode = DocumentPdfTranslations.PaymentDisclaimer, LangCode = "EN", Translation = "Saved English text" }
            }
        };
        Assert.IsType<OkObjectResult>((await controller.BatchUpsert(request)).Result);
        var texts = new DocumentPdfTranslations("SW", await db.TranslationItems.AsNoTracking().ToListAsync());
        Assert.Equal("Saved Swedish text", texts.Text(DocumentPdfTranslations.ToolingDisclaimer));
        request.Items[0].Translation = "Updated Swedish text";
        await controller.BatchUpsert(request);
        texts = new DocumentPdfTranslations("SW", await db.TranslationItems.AsNoTracking().ToListAsync());
        Assert.Equal("Updated Swedish text", texts.Text(DocumentPdfTranslations.ToolingDisclaimer));
        Assert.Equal(2, await db.TranslationItems.CountAsync());
    }

    [Fact]
    public void MissingPricesStayBlankAndPrecisionUsesQuotationCalculation()
    {
        var quotation = Fixture();
        quotation.Calculation = new Calculation { NrOfCalcDecimalsSales = 3 };
        quotation.QuotationRows.First().Price = 12.3456;
        quotation.QuotationRows.Last().Price = null;
        var content = QuotationPdfDocument.BuildContent(quotation, new DocumentPdfTranslations("EN"));
        Assert.Equal("12.346", content.Prices[0].Price);
        Assert.Equal("", content.Prices[1].Price);
        Assert.Equal("", content.Prices[1].Note);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RenderedTextUsesSnapshotsAndProtectsInternalInformation(bool hideDelivery)
    {
        var quotation = Fixture();
        quotation.HideDeliveryAddressOnPrint = hideDelivery;
        quotation.DeliveryAddressName = "UNIQUE DELIVERY ADDRESS";
        quotation.Customer = new Customer { Name = "LIVE CUSTOMER NOT SNAPSHOT" };
        quotation.IsFsc = true;
        quotation.NoteInternal = "SECRET QUOTATION NOTE";
        quotation.Message = "OLD QUOTATION MESSAGE";
        quotation.OrderCosts.Add(new OrderCost
        {
            Id = 1, DoDebit = true, DoPrintOnQuotation = true, OutPrice = 4503,
            SupplierName = "SECRET SUPPLIER", Note = "SECRET COST NOTE",
            Cost = new Cost { Name = "Tooling" }
        });
        var texts = new DocumentPdfTranslations("EN", new[]
        {
            new TranslationItem { TranslationCode = DocumentPdfTranslations.ToolingDisclaimer,
                LangCode = "EN", Translation = "CUSTOM TOOLING DISCLAIMER" }
        });
        var svg = QuotationPdfDocument.CreateDocument(quotation, null, texts,
            customerMessage: hideDelivery ? " \n " : "Company greeting\n\nCompany signature").GenerateSvg().Single();
        var renderedText = string.Join(" ", XDocument.Parse(svg).Descendants()
            .Where(element => element.Name.LocalName == "text").Select(element => element.Value));
        Assert.Contains("AB Tingstad Papper", renderedText);
        Assert.Contains("Quotation", renderedText);
        Assert.DoesNotContain("CUSTOM TOOLING DISCLAIMER", renderedText);
        Assert.DoesNotContain("OLD QUOTATION MESSAGE", renderedText);
        Assert.Equal(!hideDelivery, renderedText.Contains("Company greeting"));
        Assert.Equal(!hideDelivery, renderedText.Contains("Company signature"));
        Assert.Contains("2+1 colours", renderedText);
        Assert.Contains("FSC certified", renderedText);
        Assert.Equal(!hideDelivery, renderedText.Contains("UNIQUE DELIVERY ADDRESS"));
        Assert.DoesNotContain("SECRET", renderedText);
        Assert.DoesNotContain("LIVE CUSTOMER", renderedText);
    }

    [Fact]
    public void VatAndValidityNotesAppearDirectlyUnderThePriceTable()
    {
        var quotation = Fixture();
        quotation.OrderCosts.Add(new OrderCost
        {
            DoDebit = true, CalculationRowId = 50, Cost = new Cost { Name = "Tooling" }
        });
        const string validity = "Affärsförslagets giltighet, 15 dagar";
        const string greeting = "Med vänlig hälsning";
        var texts = new DocumentPdfTranslations("SW",
            new[] { new TranslationItem { TranslationCode = -93, LangCode = "SW", Translation = validity } });
        var svg = QuotationPdfDocument.CreateDocument(quotation, null, texts, greeting).GenerateSvg().Single();
        var elements = XDocument.Parse(svg).Descendants().Where(element => element.Name.LocalName == "text").ToList();
        var lastQuantity = Assert.Single(elements, element => element.Value.Trim().Replace('\u00a0', ' ') == "12 800");
        var vatNote = Assert.Single(elements, element => element.Value.Trim() == "Exkl. mervärdesskatt");
        var validityNote = Assert.Single(elements, element => element.Value.Trim() == validity);
        var additionsHeading = Assert.Single(elements, element => element.Value.Trim() == texts.Text(DocumentPdfTranslations.Additions));
        var signature = Assert.Single(elements, element => element.Value.Trim() == greeting);

        Assert.NotNull(lastQuantity.Attribute("font-size"));
        Assert.Equal(lastQuantity.Attribute("font-size")!.Value, vatNote.Attribute("font-size")?.Value);
        Assert.Equal(lastQuantity.Attribute("font-size")!.Value, validityNote.Attribute("font-size")?.Value);
        Assert.InRange(Math.Abs(FirstGlyphX(lastQuantity) - FirstGlyphX(vatNote)), 0, 0.01);
        Assert.InRange(Math.Abs(FirstGlyphX(vatNote) - FirstGlyphX(validityNote)), 0, 0.01);
        Assert.True(elements.IndexOf(lastQuantity) < elements.IndexOf(vatNote));
        Assert.True(elements.IndexOf(vatNote) < elements.IndexOf(validityNote));
        Assert.True(elements.IndexOf(validityNote) < elements.IndexOf(additionsHeading));
        Assert.True(elements.IndexOf(additionsHeading) < elements.IndexOf(signature));
    }

    [Fact]
    public void DeliveryAndPaymentTermsLeaveSpaceAboveFooter()
    {
        var company = new CompanyInfo { Address = "Jolengatan 19" };
        var texts = new DocumentPdfTranslations("EN");
        var svg = QuotationPdfDocument.CreateDocument(Fixture(), company, texts).GenerateSvg().Single();
        var elements = XDocument.Parse(svg).Descendants().Where(element => element.Name.LocalName == "text").ToList();
        var paymentNote = Assert.Single(elements, element => element.Value.Trim() == texts.Text(DocumentPdfTranslations.PaymentDisclaimer));
        var footerAddress = Assert.Single(elements, element => element.Value.Contains("Jolengatan 19", StringComparison.Ordinal));

        static double VerticalPosition(XElement element)
        {
            var translation = element.Attribute("transform")!.Value["translate(".Length..^1].Split(' ');
            var baseline = element.Attribute("y")!.Value.Split(',')[0];
            return double.Parse(translation[1], CultureInfo.InvariantCulture)
                + double.Parse(baseline, CultureInfo.InvariantCulture);
        }

        Assert.True(VerticalPosition(footerAddress) - VerticalPosition(paymentNote) >= 2 * 72 / 2.54);
    }

    [Fact]
    public void NonInvoiceFooterMatchesTwoContactLinesAndExcludesBankDetails()
    {
        var company = new CompanyInfo
        {
            CompanyName = "Econopack", Address = "Jolengatan 19", ZipCode = "431 49",
            PostalAddress = "M\u00f6lndal", Telephone1 = "0313633502", Telephone2 = "+46 (0)31 363 35 02",
            Email = "info@econopack.se", Web = "www.econopack.se",
            Bank = "INVOICE BANK", BG = "INVOICE GIRO", VATNr = "INVOICE VAT", IBAN = "INVOICE IBAN"
        };
        var lines = DocumentPdfLayout.ContactLines(company);
        Assert.Equal("Jolengatan 19, 431 49 M\u00f6lndal \u2022 Sverige \u2022 T +46 (0)31 363 35 02", lines.Address);
        Assert.Equal("\u2022 info@econopack.se \u2022 www.econopack.se", lines.Contact);
        var document = QuotationPdfDocument.CreateDocument(Fixture(), company, new DocumentPdfTranslations("SW"));
        Assert.Single(document.GenerateImages());
        var rendered = string.Join(" ", XDocument.Parse(document.GenerateSvg().Single()).Descendants()
            .Where(element => element.Name.LocalName == "text").Select(element => element.Value));
        Assert.Contains(lines.Address, rendered);
        Assert.Contains(lines.Contact, rendered);
        Assert.DoesNotContain("INVOICE", rendered);
        Assert.DoesNotContain("F-skatt", rendered);
        Assert.Contains("Sweden", DocumentPdfLayout.ContactLines(company, new DocumentPdfTranslations("EN").Text).Address);
    }

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private static Quotation Fixture() => new()
    {
        Id = 25691, Date = new DateTime(2026, 5, 22), CustomerName = "AB Tingstad Papper",
        Address = "Box 13013", PostalNr = "40251", PostalAddress = "Goteborg", Country = "Sverige",
        DeliveryAddressName = "AB Tingstad Papper", DeliveryAddress = "Box 13013",
        DeliveryPostalNr = "40251", DeliveryPostalAddress = "Goteborg", DeliveryCountry = "Sverige",
        OurReference = "Petra Toll", YourReference = "Johan Larsson", Product = "1968993 - Vanster",
        Format = "123x45x67mm", Construction = "O201, Slitslada", Material = "Vit/Brun, B-flute",
        ProductMessage = "1200x800x1300mm 680st/pall\nA longer note that wraps naturally within the product column.",
        TimeOfDelivery = "2026-05-02", TermsOfDelivery = "DAP", TermsOfPayment = "30 dagar fran fakturadatum",
        SalesCurrency = new Currency { Name = "SEK" }, Unit = new Models.Unit { Name = "1000pcs", NrOfCalcDecimals = 0 },
        QuotationRows = new List<QuotationRow>
        {
            new() { Id = 1, Edition = 6400, Price = 7230, CalculationRowId = 50,
                CalculationRow = new CalculationRow { Info = "2+1 colours", SalesPrice = 99999 } },
            new() { Id = 2, Edition = 12800, Price = 4203 }
        }
    };
}