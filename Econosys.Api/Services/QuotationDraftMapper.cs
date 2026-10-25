using Econosys.Api.DTOs;
using Econosys.Api.Models;

namespace Econosys.Api.Services;

public static class QuotationDraftMapper
{
    public static QuotationDto Map(Calculation calculation, Inquiry inquiry,
        CustomerDeliveryAddress? address, LegacyUser user, DateTime today,
        string? defaultMessage, decimal? palletPrice, IReadOnlyDictionary<int, string?> translations)
    {
        var product = calculation.Product;
        var customer = calculation.Customer;
        var supplier = inquiry.SelectedInquiryRecipient?.Supplier;
        return new QuotationDto
        {
            CompanyId = calculation.CompanyId,
            CalculationId = calculation.Id,
            InquiryId = inquiry.Id,
            CustomerId = calculation.CustomerId,
            CustomerName = customer?.Name,
            CustomerEmail = customer?.Email,
            Address = customer?.Address,
            Address2 = customer?.Address2,
            PostalNr = customer?.PostalNr,
            PostalAddress = customer?.PostalAddress,
            Country = customer?.Country,
            CustomerDeliveryAddressId = address?.Id,
            DeliveryAddressName = address?.Name,
            DeliveryAddress = calculation.AutoFreightPrice && address != null
                ? string.Join(" ", new[] { address.Address, address.AddressExtra }
                    .Where(value => !string.IsNullOrWhiteSpace(value)))
                : address?.Address,
            DeliveryPostalNr = address?.PostalNr,
            DeliveryPostalAddress = address?.PostalAddress,
            DeliveryCountry = address?.Country,
            Date = today.Date,
            YourReference = customer?.Reference,
            OurReference = user.Name,
            TermsOfDelivery = customer?.TermsOfDelivery,
            TermsOfPayment = customer?.TermsOfPayment,
            PaymentDays = customer?.PaymentDays,
            Message = defaultMessage,
            Product = product?.Name,
            ProductMessage = product?.NoteExternal,
            Material = DocumentProductText.Translate(product?.Material?.TranslationCode, product?.Material?.Name, translations),
            Construction = DocumentProductText.Translate(product?.Construction?.TranslationCode, product?.Construction?.Name, translations),
            Format = DocumentProductText.Format(product),
            // Legacy quotations translate material/construction, but retain the registered varnish name.
            Color = DocumentProductText.Color(product, new Dictionary<int, string?>()),
            IsFsc = inquiry.IsFSC,
            SelectedSupplierId = supplier?.Id,
            SelectedSupplierName = supplier?.Name,
            SellerName = inquiry.Customer?.ResponsibleUser?.Name,
            UnitId = calculation.UnitId,
            SalesCurrencyId = calculation.SalesCurrencyId,
            SalesCurrencyRate = calculation.SalesCurrencyRate,
            PurchaseCurrencyId = calculation.PurchaseCurrencyId,
            PurchaseCurrencyRate = calculation.PurchaseCurrencyRate,
            EurPallet = calculation.DoDebitPalletPrice,
            PalletFormatId = calculation.PalletFormatId,
            EurPalletValue = palletPrice.HasValue ? (double)palletPrice.Value : null,
            QuotationRows = calculation.CalculationRows.Where(row => !row.Archived)
                .OrderBy(row => row.Id).Select(row => MapRow(calculation, row)).ToList(),
        };
    }

    public static QuotationRowDto MapRow(Calculation calculation, CalculationRow row) => new()
    {
        Id = -row.Id,
        CalculationRowId = row.Id,
        Edition = row.Edition,
        Price = Math.Round(row.SalesPrice ?? 0,
            Math.Clamp(calculation.NrOfCalcDecimalsSales ?? calculation.Unit?.NrOfCalcDecimals ?? 2, 0, 15)),
        SalesCurrencyName = calculation.SalesCurrency?.Name,
        UnitName = calculation.Unit?.Name,
        UnitMultiplicator = calculation.Unit?.Multiplicator,
    };
}
