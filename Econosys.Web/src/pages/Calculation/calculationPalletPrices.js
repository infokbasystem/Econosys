const findPrice = (prices, palletFormatId) => {
    const matches = prices.filter((price) => Number(price.itemId) === Number(palletFormatId));
    return matches.length === 1 ? matches[0] : null;
};

export const applyPalletPrices = (calculation, supplierPrices, customerPrices, forceDebit = false) => {
    const supplierPrice = calculation.palletFormatId && calculation.supplier?.id
        ? findPrice(supplierPrices, calculation.palletFormatId)
        : null;
    const customerPrice = calculation.palletFormatId && calculation.customer?.id
        ? findPrice(customerPrices, calculation.palletFormatId)
        : null;
    const next = { ...calculation, palletPriceIn: supplierPrice?.price ?? null, palletPriceOut: null };

    if (calculation.palletFormatId && calculation.customer?.id) {
        if (!calculation.doDebitPalletPrice && !forceDebit) {
            next.palletPriceOut = 0;
        } else if (customerPrice) {
            next.palletPriceOut = customerPrice.price;
            if (forceDebit) {
                next.doDebitPalletPrice = Number(customerPrice.price ?? 0) > 0;
            } else if (Number(customerPrice.price ?? 0) === 0) {
                next.doDebitPalletPrice = false;
            }
        }
    }

    return next;
};
