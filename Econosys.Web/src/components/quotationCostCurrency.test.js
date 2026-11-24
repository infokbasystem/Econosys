import assert from 'node:assert/strict';
import test from 'node:test';
import { getQuotationCostCurrencyId } from './quotationCostCurrency.js';

test('supplier costs default to the supplier currency without overriding a chosen currency', () => {
    const supplierCost = { isSupplier: true };
    assert.equal(getQuotationCostCurrencyId({ inPriceCurrencyId: null }, supplierCost, 7), 7);
    assert.equal(getQuotationCostCurrencyId({ inPriceCurrencyId: 3 }, supplierCost, 7), 3);
    assert.equal(getQuotationCostCurrencyId({ inPriceCurrencyId: 3 }, supplierCost, null), 3);
    assert.equal(getQuotationCostCurrencyId({ inPriceCurrencyId: null }, supplierCost, null), null);
    assert.equal(getQuotationCostCurrencyId({ inPriceCurrencyId: null }, { isSupplier: false }, 7), null);
});
