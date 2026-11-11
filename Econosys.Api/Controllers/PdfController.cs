using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telerik.Reporting;
using Telerik.Reporting.Processing;
using Telerik.Reporting.XmlSerialization;
using Econosys.Api.Data;
using Econosys.Api.Models;
using Econosys.Api.Services;

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
        public async Task<IActionResult> GetQuotationPdfById(int id, CancellationToken cancellationToken = default)
        {
            var quotation = await _context.Quotations
                .AsNoTracking()
                .Include(x => x.Customer)
                    .ThenInclude(x => x!.Language)
                .Include(x => x.SalesCurrency)
                .Include(x => x.Unit)
                .Include(x => x.PalletFormat)
                .Include(x => x.Calculation)
                    .ThenInclude(x => x!.SalesCurrency)
                .Include(x => x.Calculation)
                    .ThenInclude(x => x!.Unit)
                .Include(x => x.QuotationRows)
                    .ThenInclude(x => x.CalculationRow)
                        .ThenInclude(x => x!.Calculation)
                            .ThenInclude(x => x!.SalesCurrency)
                .Include(x => x.QuotationRows)
                    .ThenInclude(x => x.CalculationRow)
                        .ThenInclude(x => x!.Calculation)
                            .ThenInclude(x => x!.Unit)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (quotation is null)
            {
                return NotFound("Quotation not found.");
            }

            var rowIds = quotation.QuotationRows.Select(row => row.Id).ToList();
            var costs = await _context.OrderCosts.AsNoTracking()
                .Include(cost => cost.Cost)
                .Where(cost => (cost.QuotationId == id
                    || (cost.QuotationRowId.HasValue && rowIds.Contains(cost.QuotationRowId.Value)))
                    && cost.DoDebit && cost.DoPrintOnQuotation && cost.SupplierOrderId == null)
                .ToListAsync(cancellationToken);
            var calculations = quotation.QuotationRows.Select(row => row.CalculationRow?.Calculation)
                .Append(quotation.Calculation).Where(calculation => calculation != null).ToList();
            var codes = DocumentPdfTranslations.Definitions.Select(item => (int?)item.Code)
                .Concat(new[] { quotation.SalesCurrency?.TranslationCode, quotation.Unit?.TranslationCode,
                    quotation.PalletFormat?.TranslationCode })
                .Concat(calculations.SelectMany(calculation => new[]
                    { calculation!.SalesCurrency?.TranslationCode, calculation.Unit?.TranslationCode }))
                .Concat(costs.Select(cost => (int?)(cost.OutPrice.HasValue
                    ? cost.Cost?.TranslationCodeQuotationKnown : cost.Cost?.TranslationCodeQuotationUnknown)))
                .Where(code => code.HasValue && code != 0).Distinct().ToList();
            var translations = await _context.TranslationItems.AsNoTracking()
                .Where(item => codes.Contains(item.TranslationCode))
                .ToListAsync(cancellationToken);
            var texts = new DocumentPdfTranslations(quotation.Customer?.Language?.LanguageCode, translations);
            var company = await _context.CompanyInfos.AsNoTracking()
                .FirstOrDefaultAsync(item => item.CompanyId == 1, cancellationToken);

            var fileName = $"quotation_{quotation.Id}.pdf";
            return File(QuotationPdfDocument.Generate(quotation, company, texts, costs),
                "application/pdf", fileName, enableRangeProcessing: true);
        }

        [HttpGet("customerorder/{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCustomerOrderPdfById(int id, CancellationToken cancellationToken = default)
        {
            var order = await _context.CustomerOrders.AsNoTracking()
                .Include(item => item.Customer).ThenInclude(item => item!.Language)
                .Include(item => item.SalesCurrency)
                .Include(item => item.Unit)
                .Include(item => item.SelectedCalculationRow).ThenInclude(item => item!.Calculation)
                    .ThenInclude(item => item!.SalesCurrency)
                .Include(item => item.SelectedCalculationRow).ThenInclude(item => item!.Calculation)
                    .ThenInclude(item => item!.Unit)
                .Include(item => item.SupplierOrder).ThenInclude(item => item!.Inventory)
                .Include(item => item.OrderCosts).ThenInclude(item => item.Cost)
                .AsSplitQuery()
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (order == null)
            {
                return NotFound("Customer order not found.");
            }

            var calculation = order.SelectedCalculationRow?.Calculation;
            if (calculation == null && order.CalculationId.HasValue)
            {
                calculation = await _context.Calculations.AsNoTracking()
                    .Include(item => item.SalesCurrency).Include(item => item.Unit)
                    .FirstOrDefaultAsync(item => item.Id == order.CalculationId, cancellationToken);
            }
            var pallet = order.PalletFormatId.HasValue
                ? await _context.PalletFormats.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == order.PalletFormatId, cancellationToken)
                : null;
            var codes = DocumentPdfTranslations.Definitions.Select(item => (int?)item.Code)
                .Concat(new[] { order.SalesCurrency?.TranslationCode, order.Unit?.TranslationCode,
                    calculation?.SalesCurrency?.TranslationCode, calculation?.Unit?.TranslationCode, pallet?.TranslationCode })
                .Concat(order.OrderCosts.Where(cost => cost.DoDebit && cost.DoPrintOnCustomerOrder)
                    .Select(cost => (int?)(cost.OutPrice.HasValue ? cost.Cost?.TranslationCodeCustomerOrderKnown : cost.Cost?.TranslationCodeCustomerOrderUnknown)))
                .Where(code => code.HasValue && code != 0).Distinct().ToList();
            var translations = await _context.TranslationItems.AsNoTracking()
                .Where(item => codes.Contains(item.TranslationCode)).ToListAsync(cancellationToken);
            var texts = new DocumentPdfTranslations(order.Customer?.Language?.LanguageCode, translations);
            var company = await _context.CompanyInfos.AsNoTracking()
                .FirstOrDefaultAsync(item => item.CompanyId == 1, cancellationToken);
            var number = order.CustomerOrderNr ?? order.Id.ToString();
            return File(CustomerOrderPdfDocument.Generate(order, company, texts, pallet, calculation),
                "application/pdf", $"customerorder_{number}.pdf", enableRangeProcessing: true);
        }

        [HttpGet("invoice/{id:int}")]
        public async Task<IActionResult> GetInvoicePdfById(int id)
        {
            var invoice = await _context.Invoices
                .AsNoTracking()
                .Include(x => x.SalesCurrency)
                .Include(x => x.InvoiceRows)
                    .ThenInclude(x => x.Unit)
                .Include(x => x.InvoiceAccountRows)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (invoice is null)
            {
                return NotFound("Invoice not found.");
            }

            var company = await _context.CompanyInfos
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CompanyId == 1);

            var invoiceNumber = invoice.InvoiceNumber?.ToString() ?? invoice.Id.ToString();
            var fileName = $"invoice_{invoiceNumber}.pdf";
            return File(InvoicePdfDocument.Generate(invoice, company), "application/pdf", fileName, enableRangeProcessing: true);
        }

        [HttpGet("inquiry/{id:int}")]
        public async Task<IActionResult> GetInquiryPdfById(int id, CancellationToken cancellationToken = default)
        {
            var inquiry = await _context.Inquiries
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Calculation)
                    .ThenInclude(x => x!.Product)
                .Include(x => x.Calculation)
                    .ThenInclude(x => x!.PurchaseCurrency)
                .Include(x => x.Calculation)
                    .ThenInclude(x => x!.Unit)
                .Include(x => x.InquiryRows)
                    .ThenInclude(x => x.CalculationRow)
                        .ThenInclude(x => x!.Calculation)
                            .ThenInclude(x => x!.Product)
                .Include(x => x.InquiryRows)
                    .ThenInclude(x => x.CalculationRow)
                        .ThenInclude(x => x!.Calculation)
                            .ThenInclude(x => x!.PurchaseCurrency)
                .Include(x => x.InquiryRows)
                    .ThenInclude(x => x.CalculationRow)
                        .ThenInclude(x => x!.Calculation)
                            .ThenInclude(x => x!.Unit)
                .Include(x => x.SelectedInquiryRecipient)
                    .ThenInclude(x => x!.Language)
                .Include(x => x.SelectedInquiryRecipient)
                    .ThenInclude(x => x!.Supplier)
                        .ThenInclude(x => x!.Language)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (inquiry is null)
            {
                return NotFound("Inquiry not found.");
            }

            var calculations = inquiry.InquiryRows.Select(row => row.CalculationRow?.Calculation)
                .Append(inquiry.Calculation).Where(calculation => calculation != null).ToList();
            var codes = DocumentPdfTranslations.Definitions.Select(item => (int?)item.Code)
                .Concat(calculations.SelectMany(calculation => new[]
                    { calculation!.PurchaseCurrency?.TranslationCode, calculation.Unit?.TranslationCode }))
                .Where(code => code.HasValue && code != 0).Distinct().ToList();
            var translations = await _context.TranslationItems.AsNoTracking()
                .Where(item => codes.Contains(item.TranslationCode))
                .ToListAsync(cancellationToken);
            var texts = new DocumentPdfTranslations(InquiryPdfDocument.RecipientLanguageCode(inquiry), translations);
            var company = await _context.CompanyInfos.AsNoTracking()
                .FirstOrDefaultAsync(item => item.CompanyId == 1, cancellationToken);

            var fileName = $"inquiry_{inquiry.Id}.pdf";
            return File(InquiryPdfDocument.Generate(inquiry, company, texts), "application/pdf", fileName, enableRangeProcessing: true);
        }

        [HttpGet("transportorder/{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTransportOrderPdfById(int id, [FromQuery] string? receiverType,
            [FromQuery] int? receiverId, CancellationToken cancellationToken = default)
        {
            var normalizedReceiver = receiverType?.Trim().ToUpperInvariant();
            if (normalizedReceiver is not (TransportOrderPdfDocument.Transporter
                or TransportOrderPdfDocument.Supplier or TransportOrderPdfDocument.Receiver))
            {
                return BadRequest("A valid transport-order PDF receiver type is required.");
            }

            if (normalizedReceiver != TransportOrderPdfDocument.Transporter && receiverId is not > 0)
            {
                return BadRequest("A recipient must be selected for supplier and warehouse PDFs.");
            }

            var order = await _context.TransportOrders
                .AsNoTracking()
                .Include(item => item.Shipper)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (order is null)
            {
                return NotFound("Transport order not found.");
            }

            var deliveries = await _context.TransportOrderDeliveries
                .AsNoTracking()
                .Where(item => item.TransportOrderId == id)
                .Include(item => item.SupplierOrder)
                    .ThenInclude(item => item!.Customer)
                .Include(item => item.DeliveryToStock)
                    .ThenInclude(item => item!.Inventory)
                .Include(item => item.DeliveryToCustomer)
                    .ThenInclude(item => item!.CustomerOrder)
                .Include(item => item.DeliveryFromStock)
                    .ThenInclude(item => item!.CustomerOrder)
                .Include(item => item.DeliveryFromStock)
                    .ThenInclude(item => item!.Inventory)
                .Include(item => item.OmlastInventory)
                .Include(item => item.TransportOrderDeliveryPallets)
                .AsSplitQuery()
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .ToListAsync(cancellationToken);

            if (normalizedReceiver == TransportOrderPdfDocument.Supplier
                && !deliveries.Any(item => item.SupplierOrder?.SupplierFactoryId == receiverId))
            {
                return BadRequest("The selected supplier factory has no deliveries on this transport order.");
            }

            if (normalizedReceiver == TransportOrderPdfDocument.Receiver
                && !deliveries.Any(item => item.DeliveryToStock?.InventoryId == receiverId))
            {
                return BadRequest("The selected warehouse has no deliveries on this transport order.");
            }

            var supplierFactoryIds = deliveries
                .Where(item => item.SupplierOrder?.SupplierFactoryId.HasValue == true)
                .Select(item => item.SupplierOrder!.SupplierFactoryId!.Value)
                .Distinct()
                .ToList();
            var supplierFactories = await _context.SupplierFactories.AsNoTracking()
                .Where(item => supplierFactoryIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
            var palletPlan = deliveries.SelectMany(item => item.TransportOrderDeliveryPallets).ToList();
            var company = await _context.CompanyInfos.AsNoTracking()
                .FirstOrDefaultAsync(item => item.CompanyId == 1, cancellationToken);
            var translationCodes = DocumentPdfTranslations.Definitions
                .Select(definition => (int?)definition.Code)
                .ToList();
            var translations = await _context.TranslationItems.AsNoTracking()
                .Where(item => translationCodes.Contains(item.TranslationCode))
                .ToListAsync(cancellationToken);
            var texts = new DocumentPdfTranslations(null, translations);
            var fileReceiverId = receiverId ?? order.ShipperId ?? 0;
            var fileName = $"Transportorder_{order.TransportOrderNr}_{fileReceiverId}.pdf";

            return File(TransportOrderPdfDocument.Generate(order, company, texts, normalizedReceiver,
                    receiverId, deliveries, supplierFactories, palletPlan),
                "application/pdf", fileName, enableRangeProcessing: true);
        }

        [HttpGet("calloff/{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCallOffPdfById(int id, CancellationToken cancellationToken = default)
        {
            var data = await LoadCallOffPdfDataAsync(id, cancellationToken);
            if (data is null)
            {
                return NotFound("Call-off not found.");
            }

            return File(CallOffPdfDocument.GenerateFreightOrder(data.CallOff, data.Company, data.Texts,
                    data.Deliveries, data.PalletFormatNames),
                "application/pdf", $"calloff_{id}.pdf", enableRangeProcessing: true);
        }

        [HttpGet("calloff-confirmation/{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCallOffConfirmationPdfById(int id, CancellationToken cancellationToken = default)
        {
            var data = await LoadCallOffPdfDataAsync(id, cancellationToken);
            if (data is null)
            {
                return NotFound("Call-off not found.");
            }

            return File(CallOffPdfDocument.GenerateConfirmation(data.CallOff, data.Company, data.Texts,
                    data.Deliveries, data.PalletFormatNames),
                "application/pdf", $"calloffconfirmation_{id}.pdf", enableRangeProcessing: true);
        }

        private sealed record CallOffPdfData(CallOff CallOff, List<CallOffDelivery> Deliveries,
            Dictionary<int, string> PalletFormatNames, CompanyInfo? Company, DocumentPdfTranslations Texts);

        private async Task<CallOffPdfData?> LoadCallOffPdfDataAsync(int id, CancellationToken cancellationToken)
        {
            var callOff = await _context.CallOffs.AsNoTracking()
                .Include(item => item.Shipper)
                .Include(item => item.CustomerDeliveryAddress)
                    .ThenInclude(item => item!.Customer)
                        .ThenInclude(item => item!.Language)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (callOff is null)
            {
                return null;
            }

            var deliveries = await _context.CallOffDeliveries.AsNoTracking()
                .Where(item => item.CallOffId == id)
                .Include(item => item.DeliveryFromStock)
                    .ThenInclude(item => item!.CustomerOrder)
                        .ThenInclude(item => item!.Customer)
                .Include(item => item.DeliveryFromStock)
                    .ThenInclude(item => item!.CustomerOrder)
                        .ThenInclude(item => item!.SalesCurrency)
                .Include(item => item.DeliveryFromStock)
                    .ThenInclude(item => item!.CustomerOrder)
                        .ThenInclude(item => item!.Unit)
                .Include(item => item.DeliveryFromStock)
                    .ThenInclude(item => item!.Inventory)
                .AsSplitQuery()
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Id)
                .ToListAsync(cancellationToken);

            var palletFormatIds = deliveries
                .Where(item => item.DeliveryFromStock?.CustomerOrder?.PalletFormatId.HasValue == true)
                .Select(item => item.DeliveryFromStock!.CustomerOrder!.PalletFormatId!.Value)
                .Distinct()
                .ToList();
            var palletFormatNames = await _context.PalletFormats.AsNoTracking()
                .Where(item => palletFormatIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name ?? string.Empty, cancellationToken);
            var company = await _context.CompanyInfos.AsNoTracking()
                .FirstOrDefaultAsync(item => item.CompanyId == 1, cancellationToken);
            var translationCodes = DocumentPdfTranslations.Definitions
                .Select(item => (int?)item.Code)
                .Concat(deliveries.SelectMany(item => new[]
                {
                    item.DeliveryFromStock?.CustomerOrder?.SalesCurrency?.TranslationCode,
                    item.DeliveryFromStock?.CustomerOrder?.Unit?.TranslationCode,
                }))
                .Where(code => code.HasValue && code != 0)
                .Distinct()
                .ToList();
            var translations = await _context.TranslationItems.AsNoTracking()
                .Where(item => translationCodes.Contains(item.TranslationCode))
                .ToListAsync(cancellationToken);
            var texts = new DocumentPdfTranslations(
                callOff.CustomerDeliveryAddress?.Customer?.Language?.LanguageCode, translations);

            return new CallOffPdfData(callOff, deliveries, palletFormatNames, company, texts);
        }

        [HttpGet("supplierorder/{id:int}")]
        public async Task<IActionResult> GetSupplierOrderPdfById(int id, CancellationToken cancellationToken = default)
        {
            var supplierOrder = await _context.SupplierOrders
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Supplier)
                    .ThenInclude(x => x!.Language)
                .Include(x => x.PurchaseCurrency)
                .Include(x => x.Unit)
                .Include(x => x.Inventory)
                .Include(x => x.SelectedCalculationRow)
                    .ThenInclude(x => x!.Calculation)
                        .ThenInclude(x => x!.PurchaseCurrency)
                .Include(x => x.SelectedCalculationRow)
                    .ThenInclude(x => x!.Calculation)
                        .ThenInclude(x => x!.Unit)
                .Include(x => x.OrderCosts)
                    .ThenInclude(x => x.Cost)
                .Include(x => x.OrderCosts)
                    .ThenInclude(x => x.InPriceCurrency)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (supplierOrder is null)
            {
                return NotFound("Supplier order not found.");
            }

            var palletFormat = supplierOrder.PalletFormatId.HasValue
                ? await _context.PalletFormats.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == supplierOrder.PalletFormatId, cancellationToken)
                : null;
            var previousNumber = supplierOrder.BasedOnSupplierOrderId.HasValue
                ? await _context.SupplierOrders.AsNoTracking()
                    .Where(item => item.Id == supplierOrder.BasedOnSupplierOrderId)
                    .Select(item => item.SupplierOrderNr).FirstOrDefaultAsync(cancellationToken)
                : null;
            var calculation = supplierOrder.SelectedCalculationRow?.Calculation;
            var codes = DocumentPdfTranslations.Definitions.Select(item => (int?)item.Code)
                .Concat(new[] { supplierOrder.PurchaseCurrency?.TranslationCode, supplierOrder.Unit?.TranslationCode,
                    calculation?.PurchaseCurrency?.TranslationCode, calculation?.Unit?.TranslationCode, palletFormat?.TranslationCode })
                .Concat(supplierOrder.OrderCosts.Where(cost => cost.DoPrintOnSupplierOrder)
                    .SelectMany(cost => new[] { cost.InPriceCurrency?.TranslationCode,
                        (int?)(cost.InPrice.HasValue ? cost.Cost?.TranslationCodeSupplierOrderKnown : cost.Cost?.TranslationCodeSupplierOrderUnknown) }))
                .Where(code => code.HasValue && code != 0).Distinct().ToList();
            var translations = await _context.TranslationItems.AsNoTracking()
                .Where(item => codes.Contains(item.TranslationCode)).ToListAsync(cancellationToken);
            var texts = new DocumentPdfTranslations(supplierOrder.Supplier?.Language?.LanguageCode, translations);
            var company = await _context.CompanyInfos.AsNoTracking()
                .FirstOrDefaultAsync(item => item.CompanyId == 1, cancellationToken);

            var fileName = $"supplierorder_{supplierOrder.SupplierOrderNr}.pdf";
            return File(SupplierOrderPdfDocument.Generate(supplierOrder, company, texts, palletFormat, previousNumber),
                "application/pdf", fileName, enableRangeProcessing: true);
        }

    }

}
