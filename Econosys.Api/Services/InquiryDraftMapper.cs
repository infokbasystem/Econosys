using Econosys.Api.DTOs;
using Econosys.Api.Models;

namespace Econosys.Api.Services;

public static class InquiryDraftMapper
{
    public static InquiryDto Map(Calculation calculation, CustomerDeliveryAddress? address,
        LegacyUser user, DateTime today, short? answerDays,
        IReadOnlyDictionary<int, string?> translations)
    {
        var product = calculation.Product;
        var supplier = calculation.Supplier;

        return new InquiryDto
        {
            CompanyId = calculation.CompanyId,
            CalculationId = calculation.Id,
            CustomerId = calculation.CustomerId,
            CustomerName = calculation.Customer?.Name,
            CustomerEmail = calculation.Customer?.Email,
            Product = product?.Name,
            Material = DocumentProductText.Translate(product?.Material?.TranslationCode, product?.Material?.Name, translations),
            Construction = DocumentProductText.Translate(product?.Construction?.TranslationCode, product?.Construction?.Name, translations),
            Format = DocumentProductText.Format(product),
            Color = DocumentProductText.Color(product, translations),
            CustomerDeliveryAddressId = address?.Id,
            DeliveryAddressName = address?.Name,
            DeliveryAddress = address == null ? null
                : string.Join(" ", new[] { address.Address, address.AddressExtra }
                    .Where(value => !string.IsNullOrWhiteSpace(value))),
            DeliveryPostalNr = address?.PostalNr,
            DeliveryPostalAddress = address?.PostalAddress,
            DeliveryCountry = address?.Country,
            OurRef = user.Name,
            AnswerDueDate = answerDays.HasValue ? today.Date.AddDays(answerDays.Value) : null,
            IsFSC = supplier?.FscDefault ?? false,
            SelectedSupplierId = supplier?.Id,
            SelectedSupplierName = supplier?.Name,
            SelectedSupplierEmail = supplier?.Email,
            SelectedRecipientYourRef = supplier?.Reference,
            SelectedRecipientTermsOfDelivery = supplier?.TermsOfDelivery,
            SelectedRecipientTermsOfPayment = supplier?.TermsOfPayment,
            SelectedRecipientLanguageId = supplier?.LanguageId,
            SelectedRecipientLanguageName = supplier?.Language?.Name,
            PurchaseCurrencyName = calculation.PurchaseCurrency?.Name,
            SalesCurrencyName = calculation.SalesCurrency?.Name,
            InquiryRows = calculation.CalculationRows.Where(row => !row.Archived)
                .OrderBy(row => row.Id)
                .Select(row => new InquiryRowDto
                {
                    Id = -row.Id,
                    CalculationRowId = row.Id,
                    Edition = row.Edition,
                    PurchasePrice = row.PurchasePrice,
                    Freight = row.Freight,
                }).ToList(),
        };
    }
}
