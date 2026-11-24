using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
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

        /// <summary>
        /// Gets the legacy PDF file name for a language-dependent document, for clients that cannot read
        /// the Content-Disposition header of the PDF response.
        /// </summary>
        [HttpGet("filename/{documentType}/{id:int:min(1)}")]
        [ProducesResponseType(typeof(PdfFileNameResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PdfFileNameResponse>> GetPdfFileName(string documentType, int id,
            CancellationToken cancellationToken = default)
        {
            string? fileName;
            switch (documentType.ToLowerInvariant())
            {
                case "supplierorder":
                    fileName = await _context.SupplierOrders.AsNoTracking().Where(item => item.Id == id)
                        .Select(item => new { item.Id, LanguageId = item.Supplier == null ? null : item.Supplier.LanguageId })
                        .Select(item => PdfFileNames.SupplierOrder(item.Id, item.LanguageId))
                        .FirstOrDefaultAsync(cancellationToken);
                    break;
                case "inquiry":
                    fileName = await _context.Inquiries.AsNoTracking().Where(item => item.Id == id)
                        .Select(item => new
                        {
                            item.Id,
                            LanguageId = item.SelectedInquiryRecipient == null || item.SelectedInquiryRecipient.Supplier == null
                                ? null : item.SelectedInquiryRecipient.Supplier.LanguageId,
                        })
                        .Select(item => PdfFileNames.Inquiry(item.Id, item.LanguageId))
                        .FirstOrDefaultAsync(cancellationToken);
                    break;
                case "quotation":
                    fileName = await _context.Quotations.AsNoTracking().Where(item => item.Id == id)
                        .Select(item => new { item.Id, LanguageId = item.Customer == null ? null : item.Customer.LanguageId })
                        .Select(item => PdfFileNames.Quotation(item.Id, item.LanguageId))
                        .FirstOrDefaultAsync(cancellationToken);
                    break;
                case "customerorder":
                    fileName = await _context.CustomerOrders.AsNoTracking().Where(item => item.Id == id)
                        .Select(item => new
                        {
                            item.Id,
                            item.CustomerOrderNr,
                            LanguageId = item.Customer == null ? null : item.Customer.LanguageId,
                        })
                        .Select(item => PdfFileNames.CustomerOrder(item.Id, item.CustomerOrderNr, item.LanguageId))
                        .FirstOrDefaultAsync(cancellationToken);
                    break;
                default:
                    return BadRequest(new ProblemDetails { Detail = "Okänd dokumenttyp." });
            }

            return fileName is null
                ? NotFound(new ProblemDetails { Detail = "Dokumentet finns inte." })
                : Ok(new PdfFileNameResponse(fileName));
        }

        [HttpGet("deviation/{id:int}")]
        public async Task<IActionResult> GetDeviationPdfById(int id, CancellationToken cancellationToken = default)
        {
            var deviation = await _context.Deviations.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new
                {
                    item.DeviationNr,
                    LanguageId = item.Customer == null ? null : item.Customer.LanguageId,
                })
                .FirstOrDefaultAsync(cancellationToken);
            var fileName = PdfFileNames.Deviation(deviation?.DeviationNr ?? id.ToString(), deviation?.LanguageId);
            return File(DeviationPdfDocument.Generate(id), "application/pdf", fileName, enableRangeProcessing: true);
        }

        /// <summary>Returns the quotation PDF with the current company customer message and the selected cost version (unlinked by default).</summary>
        [HttpGet("quotation/{id:int}")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "application/pdf")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetQuotationPdfById(int id,
            [FromQuery, Range(1, int.MaxValue)] int? supplierOrderId = null, CancellationToken cancellationToken = default)
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

            var costs = await _context.OrderCosts.AsNoTracking()
                .Include(cost => cost.Cost)
                .Where(cost => cost.QuotationId == id && cost.SupplierOrderId == supplierOrderId)
                .ToListAsync(cancellationToken);
            if (supplierOrderId.HasValue && costs.Count == 0)
            {
                return NotFound(new ProblemDetails { Detail = "Kostnadsversionen finns inte för affärsförslaget." });
            }

            var calculations = quotation.QuotationRows.Select(row => row.CalculationRow?.Calculation)
                .Append(quotation.Calculation).Where(calculation => calculation != null).ToList();
            var codes = DocumentPdfTranslations.Definitions.Select(item => (int?)item.Code)
                .Concat(new[] { quotation.SalesCurrency?.TranslationCode, quotation.Unit?.TranslationCode,
                    quotation.PalletFormat?.TranslationCode })
                .Concat(calculations.SelectMany(calculation => new[]
                    { calculation!.SalesCurrency?.TranslationCode, calculation.Unit?.TranslationCode }))
                .Concat(costs.Where(cost => cost.DoDebit).Select(QuotationPdfDocument.CostTranslationCode))
                .Where(code => code.HasValue && code != 0).Distinct().ToList();
            var translations = await _context.TranslationItems.AsNoTracking()
                .Where(item => codes.Contains(item.TranslationCode))
                .ToListAsync(cancellationToken);
            var texts = new DocumentPdfTranslations(quotation.Customer?.Language?.LanguageCode, translations);
            var company = await _context.CompanyInfos.AsNoTracking()
                .FirstOrDefaultAsync(item => item.CompanyId == 1, cancellationToken);
            var customerMessage = await _context.Settings.AsNoTracking()
                .Where(item => item.CompanyId == quotation.CompanyId)
                .OrderBy(item => item.Id)
                .Select(item => item.DefaultCustomerMessage)
                .FirstOrDefaultAsync(cancellationToken);

            var fileName = PdfFileNames.Quotation(quotation.Id, quotation.Customer?.LanguageId);
            return File(QuotationPdfDocument.Generate(quotation, company, texts, customerMessage, costs, supplierOrderId),
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
            var quotationIncludesPallet = order.EurPallet && order.QuotationId is int quotationId
                && await _context.Quotations.AsNoTracking()
                    .AnyAsync(item => item.Id == quotationId && item.EurPallet, cancellationToken);
            var pallet = quotationIncludesPallet && order.PalletFormatId.HasValue
                ? await _context.PalletFormats.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == order.PalletFormatId, cancellationToken)
                : null;
            var codes = DocumentPdfTranslations.Definitions.Select(item => (int?)item.Code)
                .Concat(new[] { order.SalesCurrency?.TranslationCode, order.Unit?.TranslationCode,
                    calculation?.SalesCurrency?.TranslationCode, calculation?.Unit?.TranslationCode, pallet?.TranslationCode })
                .Concat(CustomerOrderPdfDocument.PrintableCosts(order).Select(CustomerOrderPdfDocument.CostTranslationCode))
                .Where(code => code.HasValue && code != 0).Distinct().ToList();
            var translations = await _context.TranslationItems.AsNoTracking()
                .Where(item => codes.Contains(item.TranslationCode)).ToListAsync(cancellationToken);
            var texts = new DocumentPdfTranslations(order.Customer?.Language?.LanguageCode, translations);
            var company = await _context.CompanyInfos.AsNoTracking()
                .FirstOrDefaultAsync(item => item.CompanyId == 1, cancellationToken);
            return File(CustomerOrderPdfDocument.Generate(order, company, texts, pallet, calculation, quotationIncludesPallet),
                "application/pdf", PdfFileNames.CustomerOrder(order.Id, order.CustomerOrderNr, order.Customer?.LanguageId),
                enableRangeProcessing: true);
        }

        [HttpGet("invoice/{id:int}")]
        public async Task<IActionResult> GetInvoicePdfById(int id)
        {
            var invoice = await _context.Invoices
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.SalesCurrency)
                .Include(x => x.InvoiceRows)
                    .ThenInclude(x => x.Unit)
                .Include(x => x.InvoiceRows)
                    .ThenInclude(x => x.DeliveryToCustomer)
                .Include(x => x.InvoiceRows)
                    .ThenInclude(x => x.DeliveryFromStock)
                .Include(x => x.InvoiceAccountRows)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (invoice is null)
            {
                return NotFound("Invoice not found.");
            }

            var company = await _context.CompanyInfos
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CompanyId == 1);
            var vatInfo = await _context.Settings
                .AsNoTracking()
                .Where(x => x.CompanyId == 1)
                .OrderBy(x => x.Id)
                .Select(x => x.VATInfo)
                .FirstOrDefaultAsync();

            var invoiceNumber = invoice.InvoiceNumber ?? invoice.Id;
            var fileName = PdfFileNames.Invoice(invoiceNumber, invoice.LanguageId);
            return File(InvoicePdfDocument.Generate(invoice, company, vatInfo), "application/pdf", fileName, enableRangeProcessing: true);
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

            var fileName = PdfFileNames.Inquiry(inquiry.Id, inquiry.SelectedInquiryRecipient?.Supplier?.LanguageId);
            return File(InquiryPdfDocument.Generate(inquiry, company, texts), "application/pdf", fileName, enableRangeProcessing: true);
        }

        [HttpGet("transportorder/{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTransportOrderPdfById(int id, [FromQuery] string? receiverType,
            [FromQuery] int? receiverId, [FromQuery] int[]? receiverIds = null,
            CancellationToken cancellationToken = default)
        {
            var normalizedReceiver = receiverType?.Trim().ToUpperInvariant();
            if (normalizedReceiver is not (TransportOrderPdfDocument.Transporter
                or TransportOrderPdfDocument.Supplier or TransportOrderPdfDocument.Receiver))
            {
                return BadRequest("A valid transport-order PDF receiver type is required.");
            }

            if (normalizedReceiver != TransportOrderPdfDocument.Supplier && receiverIds is { Length: > 0 })
            {
                return BadRequest("Multiple recipients are only supported for supplier PDFs.");
            }

            var selectedReceiverIds = (receiverIds ?? [])
                .Concat(receiverId.HasValue ? [receiverId.Value] : Array.Empty<int>())
                .Distinct()
                .ToArray();
            if (normalizedReceiver != TransportOrderPdfDocument.Transporter
                && (selectedReceiverIds.Length == 0 || selectedReceiverIds.Any(value => value <= 0)))
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
                && selectedReceiverIds.Any(selectedId =>
                    !deliveries.Any(item => item.SupplierOrder?.SupplierFactoryId == selectedId)))
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
            var fileReceiverId = selectedReceiverIds.Length > 0
                ? string.Join("-", selectedReceiverIds)
                : (order.ShipperId ?? 0).ToString();
            var fileName = PdfFileNames.Create("Transportorder", $"{order.TransportOrderNr} {fileReceiverId}");

            return File(TransportOrderPdfDocument.Generate(order, company, texts, normalizedReceiver,
                    selectedReceiverIds, deliveries, supplierFactories, palletPlan),
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
                "application/pdf", PdfFileNames.CallOff(false,
                    data.CallOff.CustomerDeliveryAddress?.Customer?.LanguageId, id), enableRangeProcessing: true);
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
                "application/pdf", PdfFileNames.CallOff(true,
                    data.CallOff.CustomerDeliveryAddress?.Customer?.LanguageId, id), enableRangeProcessing: true);
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

            var fileName = PdfFileNames.SupplierOrder(supplierOrder.Id, supplierOrder.Supplier?.LanguageId);
            return File(SupplierOrderPdfDocument.Generate(supplierOrder, company, texts, palletFormat, previousNumber),
                "application/pdf", fileName, enableRangeProcessing: true);
        }

    }

}
