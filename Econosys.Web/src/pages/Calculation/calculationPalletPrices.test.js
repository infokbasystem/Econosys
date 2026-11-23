import assert from 'node:assert/strict';
import test from 'node:test';
import { applyPalletPrices } from './calculationPalletPrices.js';

const calculation = {
    customer: { id: 1 },
    supplier: { id: 2 },
    palletFormatId: 3,
    palletPriceIn: 100,
    palletPriceOut: 200,
    doDebitPalletPrice: false,
};
const supplierPrices = [{ itemId: 3, price: 125 }];
const customerPrices = [{ itemId: 3, price: 175 }];

test('changing customer or pallet format reloads both prices and enables debiting for a positive selling price', () => {
    const updated = applyPalletPrices(calculation, supplierPrices, customerPrices, true);
    assert.equal(updated.palletPriceIn, 125);
    assert.equal(updated.palletPriceOut, 175);
    assert.equal(updated.doDebitPalletPrice, true);
    assert.equal(calculation.palletPriceIn, 100);
});

test('changing supplier respects debit-off and refreshes the selling price when debit is on', () => {
    assert.equal(applyPalletPrices(calculation, supplierPrices, customerPrices).palletPriceOut, 0);
    const updated = applyPalletPrices({ ...calculation, doDebitPalletPrice: true }, supplierPrices, customerPrices);
    assert.equal(updated.palletPriceIn, 125);
    assert.equal(updated.palletPriceOut, 175);
    assert.equal(updated.doDebitPalletPrice, true);
});

test('zero or blank registered selling price disables debiting', () => {
    for (const price of [0, null]) {
        for (const force of [false, true]) {
            const updated = applyPalletPrices({ ...calculation, doDebitPalletPrice: true },
                supplierPrices, [{ itemId: 3, price }], force);
            assert.equal(updated.palletPriceOut, price);
            assert.equal(updated.doDebitPalletPrice, false);
        }
    }
});

test('missing or duplicate registrations clear the affected price without discarding the other party price', () => {
    for (const prices of [[], [{ itemId: 4, price: 20 }], [...customerPrices, ...customerPrices]]) {
        const noCustomerPrice = applyPalletPrices(calculation, supplierPrices, prices, true);
        assert.equal(noCustomerPrice.palletPriceIn, 125);
        assert.equal(noCustomerPrice.palletPriceOut, null);
        const noSupplierPrice = applyPalletPrices(calculation, prices, customerPrices, true);
        assert.equal(noSupplierPrice.palletPriceIn, null);
        assert.equal(noSupplierPrice.palletPriceOut, 175);
    }
});

test('clearing either party or the pallet format clears its dependent prices', () => {
    const noCustomer = applyPalletPrices({ ...calculation, customer: null }, supplierPrices, customerPrices, true);
    assert.equal(noCustomer.palletPriceIn, 125);
    assert.equal(noCustomer.palletPriceOut, null);
    const noSupplier = applyPalletPrices({ ...calculation, supplier: null }, supplierPrices, customerPrices, true);
    assert.equal(noSupplier.palletPriceIn, null);
    assert.equal(noSupplier.palletPriceOut, 175);
    const noFormat = applyPalletPrices({ ...calculation, palletFormatId: null }, supplierPrices, customerPrices, true);
    assert.equal(noFormat.palletPriceIn, null);
    assert.equal(noFormat.palletPriceOut, null);
});
