namespace Econosys.Api.Services;

/// <summary>Localized PDF file names (LanguageId 1 or unset = Swedish).</summary>
public static class PdfFileNames
{
    public static string Create(string swedishPrefix, string englishPrefix, int? languageId, object? number) =>
        Create(languageId.GetValueOrDefault(1) == 1 ? swedishPrefix : englishPrefix, number);

    public static string Create(string title, object? number) => $"{title} {number}.pdf";

    public static string SupplierOrder(int id, int? supplierLanguageId) =>
        Create("Beställning", "Supplier order", supplierLanguageId, id);

    public static string Inquiry(int id, int? supplierLanguageId) =>
        Create("Förfrågan", "Inquiry", supplierLanguageId, id);

    public static string Quotation(int id, int? customerLanguageId) =>
        Create("Affärsförslag", "Proposal", customerLanguageId, id);

    public static string CustomerOrder(int id, string? customerOrderNr, int? customerLanguageId) =>
        Create("Ordererkännande", "Customer order", customerLanguageId, customerOrderNr ?? id.ToString());

    public static string Invoice(int invoiceNumber, int? customerLanguageId) =>
        Create("Faktura", "Invoice", customerLanguageId, invoiceNumber);

    public static string Deviation(string number, int? customerLanguageId) =>
        Create("Avvikelse", "Deviation", customerLanguageId, number);

    public static string CallOff(bool confirmation, int? customerLanguageId, int id) =>
        Create(confirmation ? "Avropsbekräftelse" : "Avrop",
            confirmation ? "Call-off confirmation" : "Call-off", customerLanguageId, id);
}
