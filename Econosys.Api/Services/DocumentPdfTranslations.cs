using System.Globalization;
using Econosys.Api.Models;

namespace Econosys.Api.Services;

public sealed class DocumentPdfTranslations
{
    public const int Note = -1101;
    public const int Additions = -1102;
    public const int ToolingDisclaimer = -1103;
    public const int PaymentDisclaimer = -1104;
    public const int TaxApproval = -1105;
    public const int VatNumber = -1106;
    public const int CompanyCountry = -1107;
    public const int InquiryInfo = -1108;
    public const int InquiryToolingCost = -1109;
    public const int PalletUnit = -1110;
    public const int ConfirmedStatus = -1111;
    public const int WeekLabel = -1112;
    public const int DesiredDelivery = -1113;
    public const int SupplierFormat = -1114;
    public const int CallOffFrom = -1115;
    public const int InquiryReference = -1116;
    public const int CustomerCallOff = -1117;
    public const int TransportOrderNumber = -1118;
    public const int TransporterLabel = -1119;
    public const int AttentionLabel = -1120;
    public const int SenderReferenceLabel = -1121;
    public const int LoadingDateLabel = -1122;
    public const int DeliveryDateLabel = -1123;
    public const int PlacesOfDeliveryLabel = -1124;
    public const int DeliveryAddressLabel = -1125;
    public const int SupplierOrderNumberLabel = -1126;
    public const int PalletSchemeLabel = -1127;

    public sealed record Definition(int Code, string Label, string Swedish, string English);

    public static IReadOnlyList<Definition> Definitions { get; } = Array.AsReadOnly(new[]
    {
        new Definition(-1, "Produkt", "Produkt", "Product"),
        new Definition(-2, "Format", "Format LBH", "Size LWH"),
        new Definition(-3, "Material", "Material", "Material"),
        new Definition(-4, "Konstruktion", "Konstruktion", "Construction"),
        new Definition(-5, "F\u00e4rger", "Antal f\u00e4rger", "Colours"),
        new Definition(-8, "Er referens", "Er referens", "Your reference"),
        new Definition(-9, "Ert ordernummer", "Ert ordernr.", "Your order no."),
        new Definition(-10, "V\u00e5r referens", "V\u00e5r referens", "Our reference"),
        new Definition(-24, "Bank", "Bank", "Bank"),
        new Definition(-25, "Bankgiro", "Bankgiro", "Bank giro"),
        new Definition(-27, "Ordererk\u00e4nnande", "Ordererk\u00e4nnande", "Order acknowledgement"),
        new Definition(-28, "Datum", "Datum", "Date"),
        new Definition(-37, "Leverans", "Ber\u00e4knad leverans", "Estimated delivery"),
        new Definition(-38, "Leveransvillkor", "Leveransvillkor", "Delivery terms"),
        new Definition(-39, "Betalningsvillkor", "Betalningsvillkor", "Payment terms"),
        new Definition(-40, "Godsm\u00e4rkning", "Godsm\u00e4rkning", "Goods marking"),
        new Definition(-41, "Antal", "Antal", "Quantity"),
        new Definition(-42, "F\u00f6rfr\u00e5gan", "F\u00f6rfr\u00e5gan", "Inquiry"),
        new Definition(-43, "F\u00f6reg\u00e5ende order", "F\u00f6reg\u00e5ende order", "Previous order"),
        new Definition(-44, "Verktyg", "Verktyg", "Tooling"),
        new Definition(-45, "Best\u00e4llning", "Best\u00e4llning", "Purchase order"),
        new Definition(-46, "Aff\u00e4rsf\u00f6rslag", "Aff\u00e4rsf\u00f6rslag", "Quotation"),
        new Definition(-47, "Svar senast", "Svar senast", "Reply by"),
        new Definition(-48, "Bekr\u00e4ftelse", "Bekr\u00e4ftelse", "Confirmation"),
        new Definition(-49, "Leveransadress", "Leveransadress", "Delivery address"),
        new Definition(-50, "Moms", "Exkl. merv\u00e4rdesskatt", "Excluding VAT"),
        new Definition(-66, "EUR-pall", "EUR-pall", "EUR pallet"),
        new Definition(-78, "Eng\u00e5ngspall", "Eng\u00e5ngspall", "One-way pallet"),
        new Definition(-79, "Nonstop-pall", "Nonstop-pall", "Non-stop pallet"),
        new Definition(-88, "Packningsinstruktion", "Packningsinstruktion", "Packing instructions"),
        new Definition(-90, "Kund", "Kund", "Customer"),
        new Definition(-93, "Giltighetstext", "", ""),
        new Definition(-94, "Verktygens lagringstid", "Stansverktyg och klich\u00e9er sparas i 18 m\u00e5nader efter sista produktionsdatum, d\u00e4refter \u00e5tervinns alla verktyg.",
            "Die-cutting tools and printing plates are stored for 18 months after the last production date, after which all tools are recycled."),
        new Definition(-95, "Paketleverans", "Paketleverans", "Parcel delivery"),
        new Definition(-96, "FSC", "FSC-certifierat", "FSC certified"),
        new Definition(-97, "Kostnadens offertrad", "Avser offertrad", "Applies to quotation row"),
        new Definition(-98, "Och pris", "och pris", "and price"),
        new Definition(-99, "Kostnad f\u00f6r", "Kostnad f\u00f6r", "Cost for"),
        new Definition(-100, "Tillkommer", "tillkommer", "will be added"),
        new Definition(-101, "Styck", "st", "pcs"),
        new Definition(Note, "Notering", "Notering", "Note"),
        new Definition(Additions, "Till\u00e4gg", "Till\u00e4gg", "Additional costs"),
        new Definition(ToolingDisclaimer, "Text under kostnader",
            "Verktygskostnader fastst\u00e4lls vid produktion och kan komma att justeras.",
            "Tooling costs are confirmed during production and may be adjusted."),
        new Definition(PaymentDisclaimer, "Text under betalningsvillkor",
            "Dr\u00f6jsm\u00e5lsr\u00e4nta debiteras enligt allm\u00e4nna villkor.",
            "Late payment interest is charged in accordance with the general terms and conditions."),
        new Definition(TaxApproval, "F-skatt", "\u00e4r godk\u00e4nd f\u00f6r F-skatt.", "is registered for Swedish F-tax."),
        new Definition(VatNumber, "VAT-nummer", "VAT-nr", "VAT no."),
        new Definition(CompanyCountry, "F\u00f6retagets land", "Sverige", "Sweden"),
        new Definition(InquiryInfo, "F\u00f6rfr\u00e5gan, radinfo", "Info", "Info"),
        new Definition(InquiryToolingCost, "F\u00f6rfr\u00e5gan, verktygskostnad", "Kostnad f\u00f6r stansverktyg", "Die-cutting tooling cost"),
        new Definition(PalletUnit, "Pallenhet", "pall", "pallet"),
        new Definition(ConfirmedStatus, "Bekr\u00e4ftad status", "Bekr\u00e4ftad", "Confirmed"),
        new Definition(WeekLabel, "Vecka", "vecka", "week"),
        new Definition(DesiredDelivery, "\u00d6nskad leverans", "\u00d6nskad leverans", "Requested delivery"),
        new Definition(SupplierFormat, "Best\u00e4llning, format", "Format", "Size"),
        new Definition(CallOffFrom, "F\u00f6r avrop", "F\u00f6r avrop fr.o.m:", "For call-off from:"),
        new Definition(InquiryReference, "F\u00f6rfr\u00e5gan, referensnummer", "F\u00f6rfr\u00e5gan nr.", "Inquiry no."),
        new Definition(CustomerCallOff, "Kundorder, avrop", "f\u00f6r avrop", "for call-off"),
        new Definition(TransportOrderNumber, "Transportordernummer", "Truck Order no", "Truck order no."),
        new Definition(TransporterLabel, "Transport\u00f6r", "Transport\u00f6r", "Transporter"),
        new Definition(AttentionLabel, "Att.", "Att.", "Att."),
        new Definition(SenderReferenceLabel, "Avs\u00e4ndarens referens", "Avs\u00e4ndarens ref.", "Sender ref."),
        new Definition(LoadingDateLabel, "Lastningsdatum", "Lastningsdatum", "Date of loading"),
        new Definition(DeliveryDateLabel, "Leveransdatum", "Leveransdatum", "Date of delivery"),
        new Definition(PlacesOfDeliveryLabel, "Leveransplatser", "Leveransplatser", "Places of delivery"),
        new Definition(DeliveryAddressLabel, "Leveransadress", "Leveransadress", "Delivery address"),
        new Definition(SupplierOrderNumberLabel, "Leverant\u00f6rsordernummer", "Leverant\u00f6rsorder", "Supplier order"),
        new Definition(PalletSchemeLabel, "Pallplan", "Pallplan", "Pallet scheme")
    });

    private static readonly IReadOnlyDictionary<int, Definition> Defaults = Definitions.ToDictionary(item => item.Code);
    private readonly Dictionary<(int Code, string Language), string> translations;
    public string LanguageCode { get; }
    public CultureInfo Culture { get; }

    public DocumentPdfTranslations(string? languageCode, IEnumerable<TranslationItem>? items = null)
    {
        LanguageCode = Normalize(languageCode);
        try
        {
            Culture = CultureInfo.GetCultureInfo(LanguageCode == "SV" ? "sv-SE" : LanguageCode);
        }
        catch (CultureNotFoundException)
        {
            Culture = CultureInfo.GetCultureInfo("sv-SE");
        }

        translations = (items ?? [])
            .Where(item => item.TranslationCode.HasValue && !string.IsNullOrWhiteSpace(item.LangCode)
                && !string.IsNullOrWhiteSpace(item.Translation))
            .OrderBy(item => item.Id)
            .GroupBy(item => (item.TranslationCode!.Value, Normalize(item.LangCode)))
            .ToDictionary(group => group.Key, group => group.First().Translation!);
    }

    public string Text(int code, string fallback = "")
    {
        if (translations.TryGetValue((code, LanguageCode), out var translated))
        {
            return Clean(code, translated);
        }

        if (Defaults.TryGetValue(code, out var definition) && LanguageCode is "EN" or "EN-GB" or "EN-US")
        {
            return definition.English;
        }

        if (translations.TryGetValue((code, "SV"), out var swedish))
        {
            return Clean(code, swedish);
        }

        return definition?.Swedish ?? fallback;
    }

    public string Number(double? value, int decimals = 0) =>
        value?.ToString($"N{Math.Clamp(decimals, 0, 15)}", Culture) ?? "";

    public string Money(decimal? value, string currency) =>
        value.HasValue ? $"{value.Value.ToString("N2", Culture)} {currency}".TrimEnd() : "";

    private static string Clean(int code, string value) =>
        code == -93 && value.Trim().Equals("QuotationValidityText", StringComparison.OrdinalIgnoreCase) ? "" : value;

    private static string Normalize(string? code) =>
        string.IsNullOrWhiteSpace(code) ? "SV" : code.Trim().ToUpperInvariant() switch
        {
            "SW" or "SWE" or "SV-SE" => "SV",
            "ENG" => "EN",
            var normalized => normalized
        };
}