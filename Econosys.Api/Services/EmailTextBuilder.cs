using System.Globalization;
using Econosys.Api.Data;
using Econosys.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services;

/// <summary>Builds the legacy default subject and body (MailText + TranslationItem) for a document email.</summary>
public static class EmailTextBuilder
{
    public const int SignatureTranslationCode = -87;

    private static readonly Dictionary<string, string> MailTextItems = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SUPPLIERORDER"] = "SupplierOrder",
        ["INQUIRY"] = "Inquiry",
        ["QUOTATION"] = "Quotation",
        ["CUSTOMERORDER"] = "CustomerOrder",
        ["INVOICE"] = "Invoice",
        ["CALLOFF"] = "CallOff",
        ["CALLOFFCONFIRMATION"] = "CallOffConfirmation",
        ["TRANSPORTORDER"] = "TransportOrder",
    };

    private static readonly Lazy<string> LogoHtml = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Econopack_Logo_Sage.png");
        return File.Exists(path)
            ? $"<img src=\"data:image/png;base64,{Convert.ToBase64String(File.ReadAllBytes(path))}\" alt=\"Econopack\" style=\"width: 160px; height: auto;\">"
            : string.Empty;
    });

    public static async Task<(string Subject, string Body)> BuildAsync(ApplicationDbContext db, string itemType,
        int itemId, int languageId, LegacyUser user, CancellationToken cancellationToken)
    {
        if (!MailTextItems.TryGetValue(itemType, out var item)) return (string.Empty, string.Empty);

        var mailTexts = await db.MailTexts.AsNoTracking()
            .Where(x => x.Item == item)
            .Select(x => new { x.Type, x.TranslationCode })
            .ToListAsync(cancellationToken);
        var subjectCode = mailTexts.FirstOrDefault(x => string.Equals(x.Type, "Subject", StringComparison.OrdinalIgnoreCase))?.TranslationCode;
        var bodyCode = mailTexts.FirstOrDefault(x => string.Equals(x.Type, "Body", StringComparison.OrdinalIgnoreCase))?.TranslationCode;

        var langCode = await db.Languages.AsNoTracking()
            .Where(x => x.Id == languageId).Select(x => x.LanguageCode).FirstOrDefaultAsync(cancellationToken);
        var codes = new[] { subjectCode, bodyCode, SignatureTranslationCode }.Where(x => x.HasValue).ToList();
        var translations = langCode is null
            ? []
            : await db.TranslationItems.AsNoTracking()
                .Where(x => x.LangCode == langCode && codes.Contains(x.TranslationCode))
                .Select(x => new { x.TranslationCode, x.Translation, x.TranslationHtml })
                .ToListAsync(cancellationToken);

        var subject = translations.FirstOrDefault(x => x.TranslationCode == subjectCode)?.Translation ?? string.Empty;
        var body = translations.FirstOrDefault(x => x.TranslationCode == bodyCode)?.TranslationHtml ?? string.Empty;
        if (body.Contains("%SIGNATURE%"))
        {
            var signature = translations.FirstOrDefault(x => x.TranslationCode == SignatureTranslationCode);
            if (signature is not null) body = body.Replace("%SIGNATURE%", signature.TranslationHtml ?? string.Empty);
        }

        var company = await db.CompanyInfos.AsNoTracking()
            .OrderBy(x => x.CompanyId == user.CompanyId ? 0 : 1).ThenBy(x => x.CompanyId)
            .Select(x => new { x.Telephone1, x.Telephone2, x.Email, x.Web })
            .FirstOrDefaultAsync(cancellationToken);
        body = body
            .Replace("%LOGO%", LogoHtml.Value)
            .Replace("%COMPANYDOMESTICPHONE%", company?.Telephone1 ?? string.Empty)
            .Replace("%COMPANYINTERNATIONALPHONE%", company?.Telephone2 ?? string.Empty)
            .Replace("%COMPANYEMAIL%", company?.Email ?? string.Empty)
            .Replace("%COMPANYWEB%", company?.Web ?? string.Empty)
            .Replace("%EMPLOYEENAME%", user.Name ?? string.Empty)
            .Replace("%EMPLOYEETITLE%", (languageId != 1 ? user.TitleEnglish : user.Title) ?? string.Empty)
            .Replace("%EMPLOYEEDIRECTPHONE%", user.DirectPhone ?? string.Empty)
            .Replace("%EMPLOYEEMOBILEPHONE%", user.MobilePhone ?? string.Empty);

        var tokens = await GetDocumentTokensAsync(db, item, itemId, cancellationToken);
        foreach (var (token, value) in tokens.Subject) subject = subject.Replace(token, value ?? string.Empty);
        foreach (var (token, value) in tokens.Body) body = body.Replace(token, value ?? string.Empty);
        return (subject, body);
    }

    private static async Task<(Dictionary<string, string?> Subject, Dictionary<string, string?> Body)> GetDocumentTokensAsync(
        ApplicationDbContext db, string item, int itemId, CancellationToken cancellationToken)
    {
        Dictionary<string, string?> subject = [];
        switch (item)
        {
            case "CallOff":
            case "CallOffConfirmation":
                subject["%NR%"] = itemId.ToString(CultureInfo.InvariantCulture);
                break;
            case "SupplierOrder":
                var supplierOrder = await db.SupplierOrders.AsNoTracking().Where(x => x.Id == itemId)
                    .Select(x => new { x.Id, x.Product, x.CustomerOrderNr, TemplateNr = x.Supplier == null ? null : x.Supplier.SupplierOrderTemplateNr })
                    .FirstOrDefaultAsync(cancellationToken);
                if (supplierOrder is null) break;
                subject["%NR%"] = supplierOrder.Id + (supplierOrder.TemplateNr == 1 ? " / " + supplierOrder.CustomerOrderNr : string.Empty);
                subject["%PRODUKTNAMN%"] = supplierOrder.Product;
                break;
            case "CustomerOrder":
                var customerOrder = await db.CustomerOrders.AsNoTracking().Where(x => x.Id == itemId)
                    .Select(x => new { x.CustomerOrderNr, x.YourOrderNr, x.Product, x.DeliveryDate, x.DeliveryDateWeekMode })
                    .FirstOrDefaultAsync(cancellationToken);
                if (customerOrder is null) break;
                subject["%NR%"] = customerOrder.CustomerOrderNr;
                subject["%ERTORDERNR%"] = customerOrder.YourOrderNr;
                subject["%PRODUKTNAMN%"] = customerOrder.Product;
                var delivery = customerOrder.DeliveryDate is not { } date ? string.Empty
                    : customerOrder.DeliveryDateWeekMode ? "Vecka " + ISOWeek.GetWeekOfYear(date)
                    : date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return (subject, new Dictionary<string, string?>(subject) { ["%LEVERANS%"] = delivery });
            case "Inquiry":
                var inquiryProduct = await db.Inquiries.AsNoTracking().Where(x => x.Id == itemId)
                    .Select(x => x.Product).FirstOrDefaultAsync(cancellationToken);
                subject["%NR%"] = itemId.ToString(CultureInfo.InvariantCulture);
                subject["%PRODUKTNAMN%"] = inquiryProduct;
                break;
            case "Quotation":
                var quotationProduct = await db.Quotations.AsNoTracking().Where(x => x.Id == itemId)
                    .Select(x => x.Product).FirstOrDefaultAsync(cancellationToken);
                subject["%NR%"] = itemId.ToString(CultureInfo.InvariantCulture);
                subject["%PRODUKTNAMN%"] = quotationProduct;
                break;
            case "Invoice":
                var invoiceNr = await db.Invoices.AsNoTracking().Where(x => x.Id == itemId)
                    .Select(x => x.InvoiceNumber).FirstOrDefaultAsync(cancellationToken);
                subject["%NR%"] = invoiceNr?.ToString(CultureInfo.InvariantCulture);
                break;
            case "TransportOrder":
                var transportOrderNr = await db.TransportOrders.AsNoTracking().Where(x => x.Id == itemId)
                    .Select(x => (int?)x.TransportOrderNr).FirstOrDefaultAsync(cancellationToken);
                subject["%NR%"] = transportOrderNr?.ToString(CultureInfo.InvariantCulture);
                break;
        }
        return (subject, subject);
    }
}
