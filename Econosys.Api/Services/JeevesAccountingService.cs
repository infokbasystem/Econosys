using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Econosys.Api.Data;
using Econosys.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services
{
    public interface IJeevesAccountingService
    {
        Task<JeevesExportResult> ExportInvoiceAsync(int invoiceId, bool useTestApi, CancellationToken cancellationToken);
        Task<JeevesExportResult> ExportCustomerAsync(int customerId, bool useTestApi, CancellationToken cancellationToken);
    }

    public sealed record JeevesExportResult(bool Succeeded, string Message)
    {
        public static JeevesExportResult Success() => new(true, "Exporterad till Jeeves");
        public static JeevesExportResult Failure(string message) => new(false, message);
    }

    public class JeevesAccountingService : IJeevesAccountingService
    {
        private const string JeevesApiKeyPurpose = "Econosys.Jeeves.ApiKey.v1";
        private const string JeevesTestApiKeyPurpose = "Econosys.Jeeves.TestApiKey.v1";
        private readonly ApplicationDbContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IProtectedSecretService _protectedSecretService;
        private readonly ILogger<JeevesAccountingService> _logger;

        public JeevesAccountingService(
            ApplicationDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            IProtectedSecretService protectedSecretService,
            ILogger<JeevesAccountingService> logger)
        {
            _dbContext = dbContext;
            _httpClientFactory = httpClientFactory;
            _protectedSecretService = protectedSecretService;
            _logger = logger;
        }

        public async Task<JeevesExportResult> ExportCustomerAsync(int customerId, bool useTestApi, CancellationToken cancellationToken)
        {
            var settings = await _dbContext.Settings.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            var endpoint = useTestApi ? settings?.JeevesTestApiEndpoint : settings?.JeevesApiEndpoint;
            var protectedApiKey = useTestApi ? settings?.JeevesTestApiKeyProtected : settings?.JeevesApiKeyProtected;
            var keyPurpose = useTestApi ? JeevesTestApiKeyPurpose : JeevesApiKeyPurpose;

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(protectedApiKey))
            {
                return JeevesExportResult.Failure(useTestApi
                    ? "Jeeves test-API är inte konfigurerat."
                    : "Jeeves API är inte konfigurerat.");
            }

            string apiKey;
            try
            {
                apiKey = _protectedSecretService.Unprotect(keyPurpose, protectedApiKey);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not decrypt the {Environment} Jeeves API key", useTestApi ? "test" : "production");
                return JeevesExportResult.Failure("Jeeves API-nyckeln kunde inte läsas. Spara nyckeln igen i företagsinställningarna.");
            }

            var customer = await _dbContext.Customers
                .AsNoTracking()
                .Include(x => x.ResponsibleUser)
                .Include(x => x.CustomerContactPersons)
                .FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);

            if (customer is null)
            {
                return JeevesExportResult.Failure("Kunden finns inte längre.");
            }

            var vatCode = customer.EU ? "5" : customer.Export ? "7" : "1";
            var countryCode = string.IsNullOrWhiteSpace(customer.Country) ? "SE" : customer.Country;
            var customerPayload = new
            {
                customerNo = customer.Id.ToString(CultureInfo.InvariantCulture),
                orgnr = customer.OrgNr ?? string.Empty,
                customerName = customer.Name ?? string.Empty,
                vatNumber = customer.VATNr ?? string.Empty,
                address = customer.Address ?? string.Empty,
                addressCity = customer.PostalAddress ?? string.Empty,
                postalCode = customer.PostalNr ?? string.Empty,
                country = countryCode,
                mobileNr = customer.Telephone2 ?? string.Empty,
                faxNr = customer.Fax ?? string.Empty,
                reference = customer.Reference ?? "-",
                salesperson = customer.ResponsibleUser?.Name ?? "-",
                vatCode,
                termsOfPayment = customer.PaymentDays?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                contacts = customer.CustomerContactPersons
                    .OrderBy(contact => contact.Id)
                    .Select(contact => new
                    {
                        contactName = contact.CustomerContactPersonName ?? contact.ContactPerson ?? string.Empty,
                        id = contact.Id,
                        email = contact.Email ?? string.Empty,
                        telephone = contact.Telephone ?? string.Empty,
                        mobile = contact.Cellphone ?? string.Empty,
                        title = contact.Title ?? string.Empty
                    })
                    .ToList()
            };

            var httpClient = _httpClientFactory.CreateClient("Jeeves");
            httpClient.DefaultRequestHeaders.Add("X-API-KEY", apiKey);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var response = await httpClient.PostAsJsonAsync(endpoint.TrimEnd('/') + "/api/customer", customerPayload, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return JeevesExportResult.Failure(GetFriendlyErrorMessage(response.StatusCode, "kunden"));
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return JeevesExportResult.Failure("Jeeves svarade inte i tid. Försök igen senare.");
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(exception, "Could not send customer {CustomerId} to Jeeves", customerId);
                return JeevesExportResult.Failure("Kunde inte ansluta till Jeeves. Kontrollera API-endpointen och försök igen.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected Jeeves export failure for customer {CustomerId}", customerId);
                return JeevesExportResult.Failure("Ett oväntat fel uppstod vid export till Jeeves. Försök igen senare.");
            }

            return JeevesExportResult.Success();
        }

        public async Task<JeevesExportResult> ExportInvoiceAsync(int invoiceId, bool useTestApi, CancellationToken cancellationToken)
        {
            var settings = await _dbContext.Settings.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            var endpoint = useTestApi ? settings?.JeevesTestApiEndpoint : settings?.JeevesApiEndpoint;
            var protectedApiKey = useTestApi ? settings?.JeevesTestApiKeyProtected : settings?.JeevesApiKeyProtected;
            var keyPurpose = useTestApi ? JeevesTestApiKeyPurpose : JeevesApiKeyPurpose;

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(protectedApiKey))
            {
                return JeevesExportResult.Failure(useTestApi
                    ? "Jeeves test-API är inte konfigurerat."
                    : "Jeeves API är inte konfigurerat.");
            }

            string apiKey;
            try
            {
                apiKey = _protectedSecretService.Unprotect(keyPurpose, protectedApiKey);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not decrypt the {Environment} Jeeves API key", useTestApi ? "test" : "production");
                return JeevesExportResult.Failure("Jeeves API-nyckeln kunde inte läsas. Spara nyckeln igen i företagsinställningarna.");
            }

            var invoice = await _dbContext.Invoices
                .AsNoTracking()
                .Include(x => x.InvoiceRows)
                .Include(x => x.InvoiceAccountRows)
                .Include(x => x.Customer)
                    .ThenInclude(x => x!.ResponsibleUser)
                .Include(x => x.SalesCurrency)
                .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);

            if (invoice is null)
            {
                return JeevesExportResult.Failure("Fakturan finns inte längre.");
            }

            if (invoice.Customer is null || invoice.CustomerId is null)
            {
                return JeevesExportResult.Failure("Fakturan saknar kund och kan inte exporteras till Jeeves.");
            }

            if (invoice.InvoiceNumber is null || invoice.InvoiceDate is null)
            {
                return JeevesExportResult.Failure("Fakturan saknar fakturanummer eller fakturadatum.");
            }

            var contacts = await _dbContext.CustomerContactPersons
                .AsNoTracking()
                .Where(x => x.CustomerId == invoice.CustomerId)
                .OrderBy(x => x.Id)
                .ToListAsync(cancellationToken);

            var customer = invoice.Customer;
            var vatCode = customer.EU ? "5" : customer.Export ? "7" : "1";
            var countryCode = string.IsNullOrWhiteSpace(customer.Country) ? "SE" : customer.Country;
            var salesRows = invoice.InvoiceRows
                .Where(x => x.PriceTypeId != 12 && x.PriceTypeId != 13)
                .OrderBy(x => x.InvoiceRowNumber ?? int.MaxValue)
                .ThenBy(x => x.SortOrder ?? int.MaxValue)
                .ThenBy(x => x.Id)
                .ToList();

            var customerPayload = new
            {
                customerNo = customer.Id.ToString(CultureInfo.InvariantCulture),
                orgnr = customer.OrgNr ?? string.Empty,
                customerName = customer.Name ?? invoice.CustomerName ?? string.Empty,
                vatNumber = customer.VATNr ?? invoice.VatNr ?? string.Empty,
                address = customer.Address ?? invoice.Address ?? string.Empty,
                addressCity = customer.PostalAddress ?? invoice.PostalAddress ?? string.Empty,
                postalCode = customer.PostalNr ?? invoice.PostalNr ?? string.Empty,
                country = countryCode,
                mobileNr = customer.Telephone2 ?? string.Empty,
                faxNr = customer.Fax ?? string.Empty,
                reference = customer.Reference ?? "-",
                salesperson = customer.ResponsibleUser?.Name ?? "-",
                vatCode,
                termsOfPayment = customer.PaymentDays?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                contacts = contacts.Select(contact => new
                {
                    contactName = contact.CustomerContactPersonName ?? contact.ContactPerson ?? string.Empty,
                    id = contact.Id,
                    email = contact.Email ?? string.Empty,
                    telephone = contact.Telephone ?? string.Empty,
                    mobile = contact.Cellphone ?? string.Empty,
                    title = contact.Title ?? string.Empty
                }).ToList()
            };

            var invoicePayload = new
            {
                invoiceNo = invoice.InvoiceNumber.Value.ToString(CultureInfo.InvariantCulture),
                invoiceDate = invoice.InvoiceDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                typeCode = invoice.InvoiceTypeCode == InvoiceTypeCode.Credit ? "0" : "101",
                dueDate = invoice.InvoiceDate.Value.AddDays(invoice.InvoiceDays ?? 0).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                customerNo = customer.Id.ToString(CultureInfo.InvariantCulture),
                country = countryCode,
                invoiceAmount = Number(salesRows.Sum(x => Amount(x))).ToString(CultureInfo.InvariantCulture),
                vatAmount = Number(invoice.InvoiceRows.Where(x => x.PriceTypeId == 13).Sum(x => Amount(x))).ToString(CultureInfo.InvariantCulture),
                account = invoice.Account ?? string.Empty,
                vatNo = invoice.VatNr ?? customer.VATNr ?? string.Empty,
                creditingInvoiceNo = invoice.CreditingInvoiceNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                yourReference = invoice.YourReference ?? string.Empty,
                isCancelled = "false",
                ourReference = invoice.OurReference ?? string.Empty,
                costCenter = string.IsNullOrWhiteSpace(invoice.CostCenter) ? "-" : invoice.CostCenter,
                postalNo = invoice.PostalNr ?? customer.PostalNr ?? string.Empty,
                currencyCode = invoice.SalesCurrency?.Name ?? string.Empty,
                oresutj = Number(invoice.InvoiceRows.Where(x => x.PriceTypeId == 12).Sum(x => Amount(x))).ToString(CultureInfo.InvariantCulture),
                invoiceRows = salesRows.Select((row, index) => new
                {
                    invoiceNo = invoice.InvoiceNumber.Value.ToString(CultureInfo.InvariantCulture),
                    customerNo = customer.Id.ToString(CultureInfo.InvariantCulture),
                    invoiceRowNo = (index + 1).ToString(CultureInfo.InvariantCulture),
                    rowType = string.Empty,
                    rowText = row.Text ?? string.Empty,
                    amount = Number(Amount(row)).ToString(CultureInfo.InvariantCulture),
                    vatCode,
                    accountNo = row.AccountNr ?? string.Empty,
                    costCenter = row.CostCenter ?? string.Empty,
                    weight = row.Weight ?? 0m,
                    quantity = Convert.ToDecimal(row.NrOf ?? 0d, CultureInfo.InvariantCulture),
                    countryCode
                }).ToList(),
                transactions = invoice.InvoiceAccountRows.Select(row => new
                {
                    accountNo = row.AccountNr ?? string.Empty,
                    rowType = string.Empty,
                    rowText = row.Text ?? string.Empty,
                    amount = Number(-Amount(row)).ToString(CultureInfo.InvariantCulture),
                    currency = invoice.SalesCurrency?.Name ?? string.Empty,
                    vatCode = string.Empty,
                    costCenter = row.CostCenter ?? string.Empty,
                    deliveryDate = invoice.InvoiceDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    faktNr = invoice.InvoiceNumber.Value
                }).ToList()
            };

            var apiBase = endpoint.TrimEnd('/') + "/api/";
            var httpClient = _httpClientFactory.CreateClient("Jeeves");
            httpClient.DefaultRequestHeaders.Add("X-API-KEY", apiKey);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try
            {
                var customerResponse = await httpClient.PostAsJsonAsync(apiBase + "customer", customerPayload, cancellationToken);
                if (!customerResponse.IsSuccessStatusCode)
                {
                    return JeevesExportResult.Failure(GetFriendlyErrorMessage(customerResponse.StatusCode, "kunden"));
                }

                var invoiceResponse = await httpClient.PostAsJsonAsync(apiBase + "invoice", invoicePayload, cancellationToken);
                if (!invoiceResponse.IsSuccessStatusCode)
                {
                    return JeevesExportResult.Failure(GetFriendlyErrorMessage(invoiceResponse.StatusCode, "fakturan"));
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return JeevesExportResult.Failure("Jeeves svarade inte i tid. Försök igen senare.");
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(exception, "Could not send invoice {InvoiceId} to Jeeves", invoiceId);
                return JeevesExportResult.Failure("Kunde inte ansluta till Jeeves. Kontrollera API-endpointen och försök igen.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected Jeeves export failure for invoice {InvoiceId}", invoiceId);
                return JeevesExportResult.Failure("Ett oväntat fel uppstod vid export till Jeeves. Försök igen senare.");
            }

            return JeevesExportResult.Success();
        }

        private static decimal Amount(InvoiceRow row) => Convert.ToDecimal(row.Sum ?? ((row.NrOf ?? 0d) * (row.UnitPrice ?? 0d)), CultureInfo.InvariantCulture);
        private static decimal Amount(InvoiceAccountRow row) => Convert.ToDecimal(row.Sum ?? ((row.NrOf ?? 0d) * (row.UnitPrice ?? 0d)), CultureInfo.InvariantCulture);
        private static decimal Number(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private static string GetFriendlyErrorMessage(System.Net.HttpStatusCode statusCode, string resource)
        {
            return statusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                    => "Jeeves avvisade API-nyckeln. Kontrollera inställningarna.",
                System.Net.HttpStatusCode.NotFound
                    => "Jeeves API-endpointen kunde inte hittas. Kontrollera inställningarna.",
                System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.UnprocessableEntity
                    => $"Jeeves kunde inte ta emot {resource}. Kontrollera fakturauppgifterna.",
                System.Net.HttpStatusCode.TooManyRequests
                    => "Jeeves tar emot för många förfrågningar just nu. Försök igen senare.",
                _ when (int)statusCode >= 500
                    => "Jeeves har ett tillfälligt fel. Försök igen senare.",
                _ => $"Jeeves kunde inte exportera {resource}. Försök igen senare."
            };
        }
    }
}
