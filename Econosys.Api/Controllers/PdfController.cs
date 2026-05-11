using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telerik.Reporting;
using Telerik.Reporting.Processing;
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
    }

}

