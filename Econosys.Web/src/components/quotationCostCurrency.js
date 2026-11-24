export const getQuotationCostCurrencyId = (row, selectedCost, supplierCurrencyId) => (
    row.inPriceCurrencyId ?? (selectedCost?.isSupplier ? supplierCurrencyId ?? null : null)
);
