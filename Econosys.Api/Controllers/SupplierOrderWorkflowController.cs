using Econosys.Api.Common;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/supplierorder-workflow")]
    public class SupplierOrderWorkflowController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILegacyUserResolutionService _legacyUserResolution;
        private readonly IPalletFormatOptionsService _palletFormatOptionsService;

        public SupplierOrderWorkflowController(
            ApplicationDbContext dbContext,
            ILegacyUserResolutionService legacyUserResolution,
            IPalletFormatOptionsService palletFormatOptionsService)
        {
            _dbContext = dbContext;
            _legacyUserResolution = legacyUserResolution;
            _palletFormatOptionsService = palletFormatOptionsService;
        }

        [HttpGet("calculations/{calculationId:int}")]
        public async Task<ActionResult<SupplierOrderWorkflowOptionsDto>> GetOptions(int calculationId)
        {
            var calculation = await LoadCalculationQuery()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == calculationId);

            if (calculation is null)
            {
                return NotFound(new { message = "Kalkylen kunde inte hittas." });
            }

            var rows = calculation.CalculationRows
                .Where(row => !row.Archived)
                .OrderBy(row => row.Edition)
                .ThenBy(row => row.Id)
                .ToList();
            var options = new SupplierOrderWorkflowOptionsDto
            {
                CalculationRows = rows.Select(row => new SupplierOrderWorkflowRowDto
                {
                    Id = row.Id,
                    Edition = row.Edition,
                    PurchasePrice = row.PurchasePrice,
                    SalesPrice = row.SalesPrice,
                    Storage = row.Storage,
                }).ToList(),
                Inquiries = await _dbContext.Inquiries
                    .AsNoTracking()
                    .Where(inquiry => inquiry.CalculationId == calculationId)
                    .OrderByDescending(inquiry => inquiry.CreatedAt)
                    .Select(inquiry => new SupplierOrderWorkflowDocumentDto
                    {
                        Id = inquiry.Id,
                        CustomerName = inquiry.Customer != null ? inquiry.Customer.Name : inquiry.CustomerName,
                        CreatedAt = inquiry.CreatedAt,
                        SupplierName = inquiry.SelectedInquiryRecipient != null && inquiry.SelectedInquiryRecipient.Supplier != null
                            ? inquiry.SelectedInquiryRecipient.Supplier.Name
                            : null,
                    })
                    .ToListAsync(),
                Quotations = await _dbContext.Quotations
                    .AsNoTracking()
                    .Where(quotation => quotation.CalculationId == calculationId)
                    .OrderByDescending(quotation => quotation.CreatedAt)
                    .Select(quotation => new SupplierOrderWorkflowDocumentDto
                    {
                        Id = quotation.Id,
                        CustomerName = quotation.Customer != null ? quotation.Customer.Name : quotation.CustomerName,
                        CreatedAt = quotation.CreatedAt,
                        SupplierName = quotation.Inquiry != null && quotation.Inquiry.SelectedInquiryRecipient != null
                            && quotation.Inquiry.SelectedInquiryRecipient.Supplier != null
                            ? quotation.Inquiry.SelectedInquiryRecipient.Supplier.Name
                            : null,
                    })
                    .ToListAsync(),
            };

            if (!calculation.PurchaseCurrencyId.HasValue || calculation.PurchaseCurrencyId == 0
                || !calculation.SalesCurrencyId.HasValue || calculation.SalesCurrencyId == 0)
            {
                options.Errors.Add("Inköps- och försäljningsvaluta måste väljas.");
            }
            if (rows.Any(row => !row.PurchasePrice.HasValue || row.PurchasePrice == 0))
            {
                options.Errors.Add("Inköpspris saknas på en eller flera rader.");
            }
            if (rows.Any(row => !row.PercentAddition.HasValue || row.PercentAddition == 0))
            {
                options.Errors.Add("Påslag saknas på en eller flera rader.");
            }
            if (rows.Count == 0)
            {
                options.Errors.Add("Kalkylen saknar aktiva kalkylrader.");
            }
            if (calculation.PalletIsStackable == false && calculation.PalletHeight < 1401)
            {
                options.Warnings.Add("Pallens höjdmått är under 1401 och staplingsbar är ej ikryssad. Kontrollera att uppgifterna stämmer.");
            }
            if (calculation.PalletIsStackable && calculation.PalletHeight > 1400)
            {
                options.Warnings.Add("Pallens höjdmått är över 1400 och staplingsbar är ikryssad. Kontrollera att uppgifterna stämmer.");
            }

            return Ok(options);
        }

        [HttpPost("calculations/{calculationId:int}/inquiries")]
        public async Task<ActionResult<SupplierOrderWorkflowDocumentDto>> CreateInquiryFromCalculation(int calculationId, CancellationToken cancellationToken = default)
        {
            var user = await _legacyUserResolution.ResolveCurrentUserAsync(User);
            if (user is null)
            {
                return Unauthorized(new { message = "Could not map authenticated user to a legacy user by email." });
            }

            var calculation = await LoadCalculationQuery()
                .FirstOrDefaultAsync(x => x.Id == calculationId);
            if (calculation is null)
            {
                return NotFound(new { message = "Kalkylen kunde inte hittas." });
            }
            if (calculation.Product?.MaterialId is null || calculation.Product.ConstructionId is null)
            {
                return BadRequest(new { message = "Material eller konstruktion ej vald på underliggande produkt." });
            }
            if (calculation.Supplier is null || calculation.Customer is null || calculation.Product is null)
            {
                return BadRequest(new { message = "Kalkylen saknar leverantör, kund eller produkt." });
            }

            var settings = await _dbContext.Settings
                .AsNoTracking()
                .FirstOrDefaultAsync(setting => setting.CompanyId == user.CompanyId);
            if (settings is null)
            {
                return BadRequest(new { message = "Företagsinställningar kunde inte hittas." });
            }

            var deliveryAddress = await GetCalculationDeliveryAddressAsync(calculation);
            var product = calculation.Product;
            var snapshot = DocumentProductText.Build(product, await DocumentProductText.LoadTranslationsAsync(
                _dbContext, product, calculation.Supplier.LanguageId, cancellationToken));
            var inquiry = new Inquiry
            {
                CompanyId = user.CompanyId,
                CalculationId = calculation.Id,
                AutoGenerated = true,
                CustomerId = calculation.CustomerId,
                CustomerName = calculation.Customer.Name,
                Product = snapshot.Product,
                Material = snapshot.Material,
                MaterialThickness = snapshot.MaterialThickness,
                Format = snapshot.Format,
                Color = snapshot.Color,
                Varnish = snapshot.Varnish,
                PrintMethod = snapshot.PrintMethod,
                ProductNoteInternal = snapshot.ProductNoteInternal,
                ProductNoteExternal = snapshot.ProductNoteExternal,
                Construction = snapshot.Construction,
                OurRef = user.Name,
                AnswerDueDate = SwedishTime.Now.Date.AddDays(settings.NrOfInquiryAnswerDays ?? 0),
                IsFSC = calculation.Supplier.FscDefault,
                CustomerDeliveryAddressId = deliveryAddress?.Id,
                DeliveryAddressName = deliveryAddress?.Name,
                DeliveryAddress = FormatDeliveryStreet(deliveryAddress),
                DeliveryPostalNr = deliveryAddress?.PostalNr,
                DeliveryPostalAddress = deliveryAddress?.PostalAddress,
                DeliveryCountry = deliveryAddress?.Country,
                CreatedAt = SwedishTime.Now,
                EditedAt = SwedishTime.Now,
                CreatedBy = user.Id,
                EditedBy = user.Id,
            };

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            _dbContext.Inquiries.Add(inquiry);
            await _dbContext.SaveChangesAsync();

            var recipient = new InquiryRecipient
            {
                CompanyId = user.CompanyId,
                InquiryId = inquiry.Id,
                SupplierId = calculation.Supplier.Id,
                LanguageId = calculation.Supplier.LanguageId,
                Note = calculation.LoadingInstruction,
                TermsOfDelivery = calculation.Supplier.TermsOfDelivery,
                TermsOfPayment = calculation.Supplier.TermsOfPayment,
                YourRef = calculation.Supplier.Reference,
            };
            _dbContext.InquiryRecipients.Add(recipient);
            foreach (var row in calculation.CalculationRows.Where(row => !row.Archived))
            {
                _dbContext.InquiryRows.Add(new InquiryRow
                {
                    InquiryId = inquiry.Id,
                    CalculationRowId = row.Id,
                    Edition = row.Edition,
                });
            }
            await _dbContext.SaveChangesAsync();

            inquiry.SelectedInquiryRecipientId = recipient.Id;
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new SupplierOrderWorkflowDocumentDto
            {
                Id = inquiry.Id,
                CustomerName = inquiry.CustomerName,
                CreatedAt = inquiry.CreatedAt,
                SupplierName = calculation.Supplier.Name,
            });
        }

        [HttpPost("calculations/{calculationId:int}/inquiries/{inquiryId:int}/quotations")]
        public async Task<ActionResult<SupplierOrderWorkflowDocumentDto>> CreateQuotationFromCalculation(
            int calculationId,
            int inquiryId,
            CancellationToken cancellationToken = default)
        {
            var user = await _legacyUserResolution.ResolveCurrentUserAsync(User);
            if (user is null)
            {
                return Unauthorized(new { message = "Could not map authenticated user to a legacy user by email." });
            }

            var calculation = await LoadCalculationQuery()
                .FirstOrDefaultAsync(x => x.Id == calculationId);
            if (calculation is null)
            {
                return NotFound(new { message = "Kalkylen kunde inte hittas." });
            }

            var inquiry = await _dbContext.Inquiries
                .FirstOrDefaultAsync(item => item.Id == inquiryId && item.CalculationId == calculationId);
            if (inquiry is null)
            {
                return BadRequest(new { message = "Förfrågan hör inte till den valda kalkylen." });
            }
            if (calculation.Customer is null || calculation.Product is null)
            {
                return BadRequest(new { message = "Kalkylen saknar kund eller produkt." });
            }

            var settings = await _dbContext.Settings
                .AsNoTracking()
                .FirstOrDefaultAsync(setting => setting.CompanyId == user.CompanyId);
            if (settings is null)
            {
                return BadRequest(new { message = "Företagsinställningar kunde inte hittas." });
            }

            var customer = calculation.Customer;
            var product = calculation.Product;
            var snapshot = DocumentProductText.Build(product, await DocumentProductText.LoadTranslationsAsync(
                _dbContext, product, customer.LanguageId, cancellationToken));
            var deliveryAddress = await GetCalculationDeliveryAddressAsync(calculation);
            var quotation = new Quotation
            {
                CompanyId = user.CompanyId,
                CalculationId = calculation.Id,
                InquiryId = inquiry.Id,
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                Address = customer.Address,
                Address2 = customer.Address2,
                PostalNr = customer.PostalNr,
                PostalAddress = customer.PostalAddress,
                Country = customer.Country,
                DeliveryAddressName = deliveryAddress?.Name,
                DeliveryAddress = FormatDeliveryStreet(deliveryAddress),
                DeliveryPostalNr = deliveryAddress?.PostalNr,
                DeliveryPostalAddress = deliveryAddress?.PostalAddress,
                DeliveryCountry = deliveryAddress?.Country,
                CustomerDeliveryAddressId = deliveryAddress?.Id,
                Date = SwedishTime.Now,
                YourReference = customer.Reference,
                OurReference = user.Name,
                Product = snapshot.Product,
                Material = snapshot.Material,
                MaterialThickness = snapshot.MaterialThickness,
                Format = snapshot.Format,
                Color = snapshot.Color,
                Varnish = snapshot.Varnish,
                PrintMethod = snapshot.PrintMethod,
                ProductNoteInternal = snapshot.ProductNoteInternal,
                Construction = snapshot.Construction,
                TermsOfDelivery = customer.TermsOfDelivery,
                TermsOfPayment = customer.TermsOfPayment,
                PaymentDays = customer.PaymentDays,
                Message = settings.DefaultCustomerMessage,
                SalesCurrencyId = calculation.SalesCurrencyId,
                SalesCurrencyRate = calculation.SalesCurrencyRate,
                PurchaseCurrencyId = calculation.PurchaseCurrencyId,
                PurchaseCurrencyRate = calculation.PurchaseCurrencyRate,
                UnitId = calculation.UnitId,
                PalletFormatId = calculation.PalletFormatId,
                EurPallet = calculation.DoDebitPalletPrice,
                ProductMessage = snapshot.ProductNoteExternal,
                IsFsc = inquiry.IsFSC,
                AutoGenerated = true,
                CreatedAt = SwedishTime.Now,
                EditedAt = SwedishTime.Now,
                CreatedBy = user.Id,
                EditedBy = user.Id,
            };

            var decimals = calculation.NrOfCalcDecimalsSales
                ?? calculation.Unit?.NrOfCalcDecimals
                ?? 2;
            foreach (var row in calculation.CalculationRows.Where(row => !row.Archived))
            {
                quotation.QuotationRows.Add(new QuotationRow
                {
                    CompanyId = user.CompanyId,
                    CalculationRowId = row.Id,
                    Edition = row.Edition,
                    Price = Math.Round(row.SalesPrice.GetValueOrDefault(), decimals),
                });
            }

            _dbContext.Quotations.Add(quotation);
            await _dbContext.SaveChangesAsync();
            return Ok(new SupplierOrderWorkflowDocumentDto
            {
                Id = quotation.Id,
                CustomerName = quotation.CustomerName,
                CreatedAt = quotation.CreatedAt,
            });
        }

        [HttpPost("calculations/{calculationId:int}/draft")]
        public async Task<IActionResult> CreateDraft(
            int calculationId,
            [FromBody] CreateSupplierOrderDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            var user = await _legacyUserResolution.ResolveCurrentUserAsync(User);
            if (user is null)
            {
                return Unauthorized(new { message = "Could not map authenticated user to a legacy user by email." });
            }

            var calculation = await LoadCalculationQuery()
                .FirstOrDefaultAsync(x => x.Id == calculationId);
            if (calculation is null)
            {
                return NotFound(new { message = "Kalkylen kunde inte hittas." });
            }

            var row = await _dbContext.CalculationRows
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.CalculationRowId
                    && item.CalculationId == calculationId && !item.Archived);
            if (row is null)
            {
                return BadRequest(new { message = "Välj en aktiv kalkylrad." });
            }

            var inquiry = await _dbContext.Inquiries
                .Include(item => item.SelectedInquiryRecipient)
                    .ThenInclude(recipient => recipient!.Supplier)
                .Include(item => item.Customer)
                .FirstOrDefaultAsync(item => item.Id == request.InquiryId);
            if (inquiry is null)
            {
                return BadRequest(new { message = "Vald förfrågan kunde inte hittas." });
            }
            if (inquiry.CustomerId != calculation.CustomerId)
            {
                return BadRequest(new { message = "Kalkylen och förfrågans kund-id skiljer sig åt. Korrigera detta först." });
            }

            var quote = await _dbContext.Quotations
                .FirstOrDefaultAsync(item => item.Id == request.QuotationId
                    && item.CalculationId == calculationId);
            if (quote is null)
            {
                return BadRequest(new { message = "Valt affärsförslag hör inte till den valda kalkylen." });
            }

            var recipient = inquiry.SelectedInquiryRecipient;
            var supplier = recipient?.Supplier;
            if (recipient is null || supplier is null)
            {
                return BadRequest(new { message = "Ingen vald leverantör på förfrågan." });
            }
            if (calculation.Customer is null)
            {
                return BadRequest(new { message = "Kalkylens kund kunde inte hittas." });
            }
            if (calculation.Customer.LockOrder)
            {
                return BadRequest(new { message = "Låst kund, går ej att skapa nya beställningar!" });
            }

            if (quote.InquiryId != inquiry.Id)
            {
                quote.InquiryId = inquiry.Id;
                quote.EditedAt = SwedishTime.Now;
                quote.EditedBy = user.Id;
                await _dbContext.SaveChangesAsync();
            }

            var customer = calculation.Customer;
            if (calculation.Product is null)
            {
                return BadRequest(new { message = "Kalkylen saknar produkt." });
            }
            var snapshot = DocumentProductText.Build(calculation.Product, await DocumentProductText.LoadTranslationsAsync(
                _dbContext, calculation.Product, supplier.LanguageId, cancellationToken));
            var warnings = new List<string>();
            if (calculation.SupplierId != supplier.Id)
            {
                warnings.Add("Vald leverantör på förfrågan stämmer ej med leverantör på kalkylen.");
            }
            if (customer.PaymentDays.GetValueOrDefault() == 0)
            {
                warnings.Add("OBS! Kund har betalningsvillkor 'Förskott'. Hantera beställning enligt rutin för förskottsbetalning.");
            }

            var supplierFactories = await _dbContext.SupplierFactories
                .AsNoTracking()
                .Where(item => item.SupplierId == supplier.Id)
                .OrderBy(item => item.Id)
                .Select(item => new
                {
                    Id = item.Id,
                    item.Name,
                    item.IsDefault,
                })
                .ToListAsync();
            var selectedFactory = supplierFactories.FirstOrDefault(item => item.IsDefault)
                ?? (supplierFactories.Count == 1 ? supplierFactories[0] : null);
            var supplierFactoryOptions = supplierFactories
                .Select(item => new FilterOptionDto<int>
                {
                    Id = item.Id,
                    Name = !string.IsNullOrWhiteSpace(item.Name) ? item.Name! : $"Fabrik {item.Id}",
                    IsActive = true,
                })
                .ToList();

            var message = string.Empty;
            if (customer.EURPallet || customer.OneWayPallet)
            {
                message = "EUR-pall";
            }

            var latestSupplierOrderId = await _dbContext.SupplierOrders.AsNoTracking()
                .Where(order => order.QuotationId == quote.Id)
                .Select(order => (int?)order.Id)
                .MaxAsync(cancellationToken);
            var quotationCosts = await _dbContext.OrderCosts.AsNoTracking()
                .Include(cost => cost.Cost)
                .Where(cost => cost.QuotationId == quote.Id && cost.SupplierOrderId == latestSupplierOrderId
                    && (cost.Cost == null || !cost.Cost.IsCalculation))
                .OrderBy(cost => cost.Id)
                .ToListAsync(cancellationToken);
            var calculationCosts = await _dbContext.CalculationRowCosts.AsNoTracking()
                .Include(cost => cost.Cost)
                .Where(cost => cost.CalculationRowId == row.Id)
                .OrderBy(cost => cost.Id)
                .ToListAsync(cancellationToken);
            var orderCosts = quotationCosts.Select(cost => new SupplierOrderOrderCostDto
            {
                CalculationRowId = cost.CalculationRowId,
                SupplierId = cost.SupplierId,
                CostId = cost.CostId,
                DoDebit = cost.DoDebit,
                NrOf = cost.NrOf,
                InPrice = cost.InPrice,
                InPriceCurrencyId = cost.InPriceCurrencyId,
                OutPrice = cost.OutPrice,
                Note = cost.Note,
                SupplierName = cost.SupplierName,
                DoPrintOnQuotation = cost.DoPrintOnQuotation,
                DoPrintOnCustomerOrder = cost.DoPrintOnCustomerOrder,
                DoPrintOnSupplierOrder = cost.DoPrintOnSupplierOrder,
                DoInvoiceSeparately = customer.InvoiceCostsSeparately,
                DoInvoiceSeparatelyImmediately = customer.InvoiceCostsSeparatelyImmediately,
            }).Concat(calculationCosts.Select(cost => new SupplierOrderOrderCostDto
            {
                CalculationRowId = cost.CalculationRowId,
                CostId = cost.CostId,
                DoDebit = cost.DoDebit,
                NrOf = cost.NrOf,
                InPrice = cost.InPrice,
                InPriceCurrencyId = cost.InPriceCurrencyId,
                OutPrice = cost.OutPrice,
                DoPrintOnQuotation = cost.Cost?.DoPrintQuotationDefault ?? false,
                DoPrintOnCustomerOrder = cost.Cost?.DoPrintCustomerOrderDefault ?? false,
                DoPrintOnSupplierOrder = cost.Cost?.DoPrintSupplierOrderDefault ?? false,
            })).ToList();

            var draft = new SupplierOrderDto
            {
                Id = 0,
                InquiryId = inquiry.Id,
                QuotationId = quote.Id,
                CalculationId = calculation.Id,
                SupplierId = supplier.Id,
                SupplierName = supplier.Name,
                Address = supplier.Address,
                Address2 = supplier.Address2,
                PostalNr = supplier.PostalNr,
                PostalAddress = supplier.PostalAddress,
                Country = supplier.Country,
                SupplierFactoryId = selectedFactory?.Id,
                CustomerId = inquiry.CustomerId,
                CustomerName = inquiry.Customer?.Name ?? inquiry.CustomerName,
                CustomerDeliveryAddressId = inquiry.CustomerDeliveryAddressId,
                DeliveryAddressName = inquiry.DeliveryAddressName,
                DeliveryAddress = inquiry.DeliveryAddress,
                DeliveryPostalNr = inquiry.DeliveryPostalNr,
                DeliveryPostalAddress = inquiry.DeliveryPostalAddress,
                DeliveryCountry = inquiry.DeliveryCountry,
                Date = SwedishTime.Now,
                YourReference = recipient.YourRef,
                OurReference = user.Name,
                Product = snapshot.Product,
                ProductCode = calculation.Product?.ProductCode,
                Material = snapshot.Material,
                MaterialThickness = snapshot.MaterialThickness,
                Format = snapshot.Format,
                Color = snapshot.Color,
                Varnish = snapshot.Varnish,
                PrintMethod = snapshot.PrintMethod,
                ProductNoteInternal = snapshot.ProductNoteInternal,
                ProductNoteExternal = snapshot.ProductNoteExternal,
                Construction = snapshot.Construction,
                TimeOfDelivery = inquiry.TimeOfDelivery,
                TermsOfDelivery = recipient.TermsOfDelivery,
                TermsOfPayment = recipient.TermsOfPayment,
                Message = message,
                Preparation = inquiry.Preparation,
                UnitId = calculation.UnitId,
                UnitName = calculation.Unit?.Name,
                SelectedCalculationRowId = row.Id,
                Edition = row.Edition,
                PurchasePrice = row.PurchasePrice,
                PurchaseCurrencyId = calculation.PurchaseCurrencyId,
                PurchaseCurrencyName = calculation.PurchaseCurrency?.Name,
                PurchaseCurrencyRate = calculation.PurchaseCurrencyRate,
                LoadingInstruction = string.Join(Environment.NewLine,
                    new[] { calculation.LoadingInstruction, calculation.FreightNote }
                        .Where(value => !string.IsNullOrWhiteSpace(value))),
                HideCustomerInfoOnPrint = true,
                HideCustomerNameOnPrint = true,
                HideProductNameOnPrint = true,
                EconopackTransportResponsible = supplier.EconopackTransportResponsible,
                EurPallet = calculation.DoDebitPalletPrice,
                OneWayPallet = calculation.PalletType == 2,
                PalletFormatId = calculation.PalletFormatId,
                IsFSC = supplier.FscDefault,
                OrderCosts = orderCosts,
            };
            var palletFormats = await _palletFormatOptionsService.GetOptionsAsync(customer.Id, supplier.Id);
            return Ok(new SupplierOrderWorkflowDraftDto
            {
                SupplierOrder = draft,
                Warnings = warnings,
                SupplierFactories = supplierFactoryOptions,
                PalletFormats = palletFormats,
            });
        }

        private IQueryable<Calculation> LoadCalculationQuery()
        {
            return _dbContext.Calculations
                .Include(item => item.CalculationRows)
                .Include(item => item.Product).ThenInclude(product => product!.Material)
                .Include(item => item.Product).ThenInclude(product => product!.Construction)
                .Include(item => item.Product).ThenInclude(product => product!.Varnish)
                .Include(item => item.Product).ThenInclude(product => product!.InternalInfo)
                .Include(item => item.Supplier).ThenInclude(supplier => supplier!.Language)
                .Include(item => item.Customer)
                .Include(item => item.Unit)
                .Include(item => item.PurchaseCurrency)
                .Include(item => item.SalesCurrency);
        }

        private async Task<CustomerDeliveryAddress?> GetCalculationDeliveryAddressAsync(Calculation calculation)
        {
            if (calculation.AutoFreightPrice && calculation.CustomerDeliveryAddressId.HasValue)
            {
                var selected = await _dbContext.CustomerDeliveryAddresses
                    .FirstOrDefaultAsync(address => address.Id == calculation.CustomerDeliveryAddressId.Value);
                if (selected is not null)
                {
                    return selected;
                }
            }

            return await _dbContext.CustomerDeliveryAddresses
                .Where(address => address.CustomerId == calculation.CustomerId && address.IsDefault)
                .OrderBy(address => address.Id)
                .FirstOrDefaultAsync();
        }

        private static string? FormatDeliveryStreet(CustomerDeliveryAddress? address)
        {
            if (address is null)
            {
                return null;
            }

            return string.Join(" ", new[] { address.Address, address.AddressExtra }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        }
    }
}
