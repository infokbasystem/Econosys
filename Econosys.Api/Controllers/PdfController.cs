using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telerik.Reporting;
using Telerik.Reporting.Processing;
using Telerik.Reporting.Drawing;
using Telerik.Reporting.XmlSerialization;
using Econosys.Api.Data;

namespace Econosys.Api.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    // [Authorize]
    public class PdfController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public PdfController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("deviation/{id:int}")]
        public async Task<IActionResult> GetDeviationPdfById(int id)
        {
            var reportPath = Path.Combine(Directory.GetCurrentDirectory(), "", "Reports", "Deviation.trdx");


            if (!System.IO.File.Exists(reportPath))
            {
                return NotFound("Report file not found.");
            }


            // Load the .trdx report
            Telerik.Reporting.Report report;
            using (var fs = System.IO.File.OpenRead(reportPath))
            {
                var serializer = new ReportXmlSerializer();
                report = (Telerik.Reporting.Report)serializer.Deserialize(fs);
            }


            var deviation = new Econosys.Api.PrintModels.Deviation()
            {
                Id = id
            };



            report.DataSource = deviation;

            // Render PDF
            var processor = new ReportProcessor();
            var reportSource = new Telerik.Reporting.InstanceReportSource { ReportDocument = report };
            var result = processor.RenderReport("PDF", reportSource, null);

            if (result.HasErrors)
                return Problem(string.Join("\n", result.Errors.Select(e => e.Message)));

            string fileName = "filename.pdf"; // trans.Where(t => t.TranslationCode == -25 && t.LanguageId == LanguageId).Select(t => t.Text).FirstOrDefault() ?? "Quotation"
                                              //+ "_" + q.Id + ".pdf";
                                              // Return PDF to browser            
            return File(result.DocumentBytes, "application/pdf", fileName, enableRangeProcessing: true);

        }

        [HttpGet("quotation/{id:int}")]
        public async Task<IActionResult> GetQuotationPdfById(int id)
        {
            var quotation = await _context.Quotations
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Inquiry)
                    .ThenInclude(x => x!.SelectedInquiryRecipient)
                        .ThenInclude(x => x!.Supplier)
                .Include(x => x.Inquiry)
                    .ThenInclude(x => x!.Customer)
                        .ThenInclude(x => x!.ResponsibleUser)
                .Include(x => x.CreatedByUser)
                .Include(x => x.EditedByUser)
                .Include(x => x.QuotationRows)
                    .ThenInclude(x => x.CalculationRow)
                        .ThenInclude(x => x!.Calculation)
                            .ThenInclude(x => x!.SalesCurrency)
                .Include(x => x.QuotationRows)
                    .ThenInclude(x => x.CalculationRow)
                        .ThenInclude(x => x!.Calculation)
                            .ThenInclude(x => x!.Unit)
                .Include(x => x.OrderCosts)
                    .ThenInclude(x => x.CustomerOrder)
                .Include(x => x.OrderCosts)
                    .ThenInclude(x => x.InvoiceRows)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (quotation is null)
            {
                return NotFound("Quotation not found.");
            }

            var report = new Telerik.Reporting.Report
            {
                Name = "QuotationReport",
                PageSettings =
                {
                    PaperKind = System.Drawing.Printing.PaperKind.A4,
                    Margins = new MarginsU(Unit.Cm(1.5), Unit.Cm(1.5), Unit.Cm(1.5), Unit.Cm(1.5))
                },
                Width = Unit.Cm(18)
            };

            var detail = new Telerik.Reporting.DetailSection
            {
                Height = Unit.Cm(28)
            };

            detail.Items.Add(new Telerik.Reporting.TextBox
            {
                Name = "QuotationSummary",
                Value = BuildQuotationSummary(quotation),
                Location = new PointU(Unit.Cm(0), Unit.Cm(0)),
                Size = new SizeU(Unit.Cm(18), Unit.Cm(27)),
                CanGrow = true,
                Style = { Font = { Name = "Arial", Size = Unit.Point(10) } }
            });

            report.Items.Add(detail);

            var processor = new ReportProcessor();
            var reportSource = new InstanceReportSource { ReportDocument = report };
            var result = processor.RenderReport("PDF", reportSource, null);

            if (result.HasErrors)
            {
                return Problem(string.Join("\n", result.Errors.Select(e => e.Message)));
            }

            var fileName = $"quotation_{quotation.Id}.pdf";
            return File(result.DocumentBytes, "application/pdf", fileName, enableRangeProcessing: true);
        }

        [HttpGet("invoice/{id:int}")]
        public async Task<IActionResult> GetInvoicePdfById(int id)
        {
            var reportPath = Path.Combine(Directory.GetCurrentDirectory(), "", "Reports", "Invoice.trdx");

            if (!System.IO.File.Exists(reportPath))
            {
                return NotFound("Report file not found.");
            }

            var invoice = await _context.Invoices
                .AsNoTracking()
                .Include(x => x.InvoiceRows)
                .Include(x => x.InvoiceAccountRows)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (invoice is null)
            {
                return NotFound("Invoice not found.");
            }

            Telerik.Reporting.Report report;
            using (var fs = System.IO.File.OpenRead(reportPath))
            {
                var serializer = new ReportXmlSerializer();
                report = (Telerik.Reporting.Report)serializer.Deserialize(fs);
            }

            report.DataSource = new[] { invoice };

            var processor = new ReportProcessor();
            var reportSource = new Telerik.Reporting.InstanceReportSource { ReportDocument = report };
            var result = processor.RenderReport("PDF", reportSource, null);

            if (result.HasErrors)
            {
                return Problem(string.Join("\n", result.Errors.Select(e => e.Message)));
            }

            var invoiceNumber = invoice.InvoiceNumber?.ToString() ?? invoice.Id.ToString();
            var fileName = $"invoice_{invoiceNumber}.pdf";
            return File(result.DocumentBytes, "application/pdf", fileName, enableRangeProcessing: true);
        }

        [HttpGet("inquiry/{id:int}")]
        public async Task<IActionResult> GetInquiryPdfById(int id)
        {
            var inquiry = await _context.Inquiries
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.SelectedInquiryRecipient)
                    .ThenInclude(x => x!.Supplier)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (inquiry is null)
            {
                return NotFound("Inquiry not found.");
            }

            var report = new Telerik.Reporting.Report
            {
                Name = "InquiryReport",
                PageSettings =
                {
                    PaperKind = System.Drawing.Printing.PaperKind.A4,
                    Margins = new MarginsU(Unit.Cm(1.5), Unit.Cm(1.5), Unit.Cm(1.5), Unit.Cm(1.5))
                },
                Width = Unit.Cm(18)
            };

            var detail = new Telerik.Reporting.DetailSection
            {
                Height = Unit.Cm(26)
            };

            detail.Items.Add(new Telerik.Reporting.TextBox
            {
                Name = "InquirySummary",
                Value = BuildInquirySummary(inquiry),
                Location = new PointU(Unit.Cm(0), Unit.Cm(0)),
                Size = new SizeU(Unit.Cm(18), Unit.Cm(25)),
                CanGrow = true,
                Style = { Font = { Name = "Arial", Size = Unit.Point(10) } }
            });

            report.Items.Add(detail);

            var processor = new ReportProcessor();
            var reportSource = new InstanceReportSource { ReportDocument = report };
            var result = processor.RenderReport("PDF", reportSource, null);

            if (result.HasErrors)
            {
                return Problem(string.Join("\n", result.Errors.Select(e => e.Message)));
            }

            var fileName = $"inquiry_{inquiry.Id}.pdf";
            return File(result.DocumentBytes, "application/pdf", fileName, enableRangeProcessing: true);
        }

        private static string BuildInquirySummary(Econosys.Api.Models.Inquiry inquiry)
        {
            var lines = new List<string>
            {
                $"Förfrågan {inquiry.Id}",
                $"Kund: {inquiry.Customer?.Name ?? inquiry.CustomerName ?? string.Empty}",
                $"Leverantör: {inquiry.SelectedInquiryRecipient?.Supplier?.Name ?? string.Empty}",
                $"Produkt: {inquiry.Product ?? string.Empty}",
                $"Material: {inquiry.Material ?? string.Empty}",
                $"Format: {inquiry.Format ?? string.Empty}",
                $"Färg: {inquiry.Color ?? string.Empty}",
                $"Konstruktion: {inquiry.Construction ?? string.Empty}",
                $"Leveransadress: {inquiry.DeliveryAddressName ?? string.Empty}",
                $"Svar senast: {(inquiry.AnswerDueDate.HasValue ? inquiry.AnswerDueDate.Value.ToString("yyyy-MM-dd") : string.Empty)}",
                $"Vår referens: {inquiry.OurRef ?? string.Empty}",
                $"Förberedelse: {inquiry.Preparation ?? string.Empty}",
                $"Leveranstid: {inquiry.TimeOfDelivery ?? string.Empty}",
            };

            return string.Join(Environment.NewLine, lines);
        }

        private static string BuildQuotationSummary(Econosys.Api.Models.Quotation quotation)
        {
            var rows = quotation.QuotationRows
                .OrderBy(x => x.Id)
                .Select(row =>
                {
                    var unitName = row.CalculationRow?.Calculation?.Unit?.Name ?? string.Empty;
                    var currencyName = row.CalculationRow?.Calculation?.SalesCurrency?.Name ?? string.Empty;
                    var unitLabel = string.IsNullOrWhiteSpace(unitName) ? string.Empty : $" {unitName}";
                    var edition = row.Edition?.ToString() ?? string.Empty;
                    var price = row.Price?.ToString() ?? string.Empty;

                    return $"Rada {edition}: {price} {currencyName}{unitLabel}".Trim();
                })
                .ToList();

            var orderCosts = quotation.OrderCosts
                .OrderBy(x => x.Id)
                .Select(cost =>
                {
                    var supplierName = cost.SupplierName ?? cost.CustomerOrder?.CustomerName ?? string.Empty;
                    var note = cost.Note ?? string.Empty;
                    return $"Kostnad {cost.Id}: {supplierName} {note}".Trim();
                })
                .ToList();

            var lines = new List<string>
            {
                $"Offert {quotation.Id}",
                $"Kund: {quotation.Customer?.Name ?? quotation.CustomerName ?? string.Empty}",
                $"Leverantör: {quotation.Inquiry?.SelectedInquiryRecipient?.Supplier?.Name ?? string.Empty}",
                $"Säljare: {quotation.Inquiry?.Customer?.ResponsibleUser?.Name ?? string.Empty}",
                $"Produkt: {quotation.Product ?? string.Empty}",
                $"Material: {quotation.Material ?? string.Empty}",
                $"Tjocklek: {quotation.MaterialThickness ?? string.Empty}",
                $"Format: {quotation.Format ?? string.Empty}",
                $"Färg: {quotation.Color ?? string.Empty}",
                $"Konstruktion: {quotation.Construction ?? string.Empty}",
                $"Leveransadress: {(quotation.HideDeliveryAddressOnPrint ? string.Empty : (quotation.DeliveryAddressName ?? string.Empty))}",
                $"Er referens: {quotation.YourReference ?? string.Empty}",
                $"Vår referens: {quotation.OurReference ?? string.Empty}",
                $"Leveranstid: {quotation.TimeOfDelivery ?? string.Empty}",
                $"Lev.villkor: {quotation.TermsOfDelivery ?? string.Empty}",
                $"Bet.villkor: {quotation.TermsOfPayment ?? string.Empty}",
                $"Meddelande: {quotation.Message ?? string.Empty}",
                $"FSC: {(quotation.IsFsc ? "Ja" : "Nej")}",
                $"Datum: {(quotation.Date.HasValue ? quotation.Date.Value.ToString("yyyy-MM-dd") : string.Empty)}",
                $"Dölj leveransadress: {(quotation.HideDeliveryAddressOnPrint ? "Ja" : "Nej")}",
            };

            if (rows.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Offertrader:");
                lines.AddRange(rows);
            }

            if (orderCosts.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Orderkostnader:");
                lines.AddRange(orderCosts);
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

}

