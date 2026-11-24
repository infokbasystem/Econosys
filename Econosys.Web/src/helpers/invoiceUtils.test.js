import assert from 'node:assert/strict';
import test from 'node:test';
import { applyInvoiceDerivedRows, computeInvoiceRowSum, createCreditInvoiceDraft, getInvoiceCustomerFields, getInvoiceDraftMode, getInvoiceInventorySource, hasCreditStockDeliveries, isInvoiceCustomerLocked, reorderInvoiceRows } from './invoiceUtils.js';

test('only credit invoices with stock product deliveries offer adjustment', () => {
    assert.equal(hasCreditStockDeliveries(null), false);
    assert.equal(hasCreditStockDeliveries({ invoiceTypeCode: 1, invoiceRows: [{ priceTypeId: 1, deliveryFromStockId: 10 }] }), false);
    assert.equal(hasCreditStockDeliveries({ invoiceTypeCode: 5, invoiceRows: [{ priceTypeId: 1, deliveryToCustomerId: 10 }] }), false);
    assert.equal(hasCreditStockDeliveries({ invoiceTypeCode: 5, invoiceRows: [{ priceTypeId: 107, deliveryFromStockId: 10 }] }), false);
    assert.equal(hasCreditStockDeliveries({ invoiceTypeCode: 5, invoiceRows: [{ priceTypeId: 1, deliveryFromStockId: 10 }] }), true);
});

test('credit draft reverses amounts while preserving delivery quantities and links', () => {
    const invoice = {
        id: 8, invoiceNumber: 123, invoiceTypeCode: 1, accountDate: '2026-01-01', journalNr: 2, isSettled: true, invoiceOk: true, endInvoiced: true,
        invoiceRows: [{ id: 9, invoiceId: 8, priceTypeId: 1, deliveryFromStockId: 10, nrOf: 12, unitPrice: 3, sum: 36, weight: 5 }],
        invoiceAccountRows: [{ id: 11, invoiceId: 8, nrOf: 2, unitPrice: 7, sum: 14 }],
    };
    const original = structuredClone(invoice);
    const draft = createCreditInvoiceDraft(invoice);
    assert.equal(draft.id, 0);
    assert.equal(draft.invoiceTypeCode, 5);
    assert.equal(draft.creditingInvoiceNumber, 123);
    assert.equal(draft.creditedInvoiceId, 8);
    assert.equal(draft.accountDate, null);
    assert.equal(draft.isSettled, false);
    assert.equal(draft.invoiceOk, false);
    assert.equal(draft.endInvoiced, false);
    assert.deepEqual([draft.invoiceRows[0].nrOf, draft.invoiceRows[0].unitPrice, draft.invoiceRows[0].sum, draft.invoiceRows[0].weight], [12, -3, -36, -5]);
    assert.equal(draft.invoiceRows[0].deliveryFromStockId, 10);
    assert.deepEqual([draft.invoiceAccountRows[0].nrOf, draft.invoiceAccountRows[0].unitPrice, draft.invoiceAccountRows[0].sum], [-2, 7, -14]);
    assert.deepEqual(invoice, original);
});

test('standalone invoices allow customer selection', () => {
    assert.equal(isInvoiceCustomerLocked(null), false);
    assert.equal(isInvoiceCustomerLocked({ id: 0, invoiceRows: [{ text: 'Manual row' }] }), false);
    assert.equal(isInvoiceCustomerLocked({
        id: 10,
        invoiceRows: [{ deliveryToCustomerId: null, deliveryFromStockId: 0, orderCostId: null }],
    }), false);
});

test('invoice mode follows the selected legacy source rows without a mode selector', () => {
    assert.deepEqual(getInvoiceDraftMode(['DeliveryToCustomer', 'OrderCost']), { invoiceMode: 'selection' });
    assert.deepEqual(getInvoiceDraftMode(['OrderCostSeparately']), { invoiceMode: 'selection' });
    assert.deepEqual(getInvoiceDraftMode(['PalletsNow']), { invoiceMode: 'palletsNow' });
    assert.deepEqual(getInvoiceDraftMode(['PALLETSINFUTURE']), { invoiceMode: 'palletsFuture' });
    for (const types of [['PalletsNow', 'PalletsInFuture'], ['PalletsNow', 'OrderCost'], ['PalletsInFuture', 'DeliveryFromStock']]) {
        assert.match(getInvoiceDraftMode(types).error, /Välj endast pallrader/);
    }
});

test('invoice rows can move both ways while preserving contents and keeping derived rows last', () => {
    const first = { id: 11, sortOrder: 1, invoiceRowNumber: 50, text: 'Product', priceTypeId: 1, sum: 100, deliveryFromStockId: 30, nrOf: 10 };
    const second = { tempId: 'new-cost', sortOrder: 2, text: 'Cost', priceTypeId: 2, sum: 20, orderCostId: 40 };
    const third = { id: 12, sortOrder: 5, text: 'Freight', priceTypeId: 108, sum: 50, priceKey: 'Freight:60' };
    const vat = { id: 13, priceTypeId: 13, sortOrder: 3, text: 'Moms', sum: 42.5, accountNr: '2611' };
    const rounding = { id: 14, priceTypeId: 12, sortOrder: 4, text: 'Öresutjämning', sum: 0.5 };
    const original = [first, second, vat, rounding, third];
    const snapshot = structuredClone(original);

    const down = reorderInvoiceRows(original, 11, 12);
    assert.deepEqual(down.map((row) => row.text), ['Cost', 'Freight', 'Product', 'Moms', 'Öresutjämning']);
    const up = reorderInvoiceRows(down, 11, 'new-cost');
    assert.deepEqual(up.map((row) => row.text), ['Product', 'Cost', 'Freight', 'Moms', 'Öresutjämning']);
    for (const rows of [down, up]) {
        assert.deepEqual(rows.map((row) => row.sortOrder), [1, 2, 3, 4, 5]);
        assert.deepEqual(rows.map((row) => row.invoiceRowNumber), [1, 2, 3, 4, 5]);
        for (const row of rows) {
            const source = original.find((item) => item.text === row.text);
            assert.deepEqual(row, { ...source, sortOrder: row.sortOrder, invoiceRowNumber: row.invoiceRowNumber });
        }
    }
    assert.deepEqual(original, snapshot);
    assert.deepEqual(
        reorderInvoiceRows(original, 'new-cost', 12).map((row) => row.text),
        ['Product', 'Freight', 'Cost', 'Moms', 'Öresutjämning'],
    );
    assert.equal(reorderInvoiceRows(original, 11, 11), original);
    assert.equal(reorderInvoiceRows(original, 13, 11), original);
    assert.equal(reorderInvoiceRows(original, 11, 13), original);
    assert.equal(reorderInvoiceRows(original, 11, 'missing-row'), original);
});

test('changing stock product row order does not trigger a new inventory valuation', () => {
    const rows = [
        { id: 1, priceTypeId: 1, deliveryFromStockId: 31, nrOf: 100 },
        { id: 2, priceTypeId: 1, deliveryFromStockId: 30, nrOf: 200 },
    ];
    assert.equal(
        getInvoiceInventorySource({ invoiceRows: rows }),
        getInvoiceInventorySource({ invoiceRows: reorderInvoiceRows(rows, 1, 2) }),
    );
});

test('delivery and order cost invoices lock the customer in drafts and saved invoices', () => {
    for (const id of [0, 10]) {
        for (const field of ['deliveryToCustomerId', 'deliveryFromStockId']) {
            assert.equal(isInvoiceCustomerLocked({ id, [field]: 42 }), true);
        }
        for (const field of ['deliveryToCustomerId', 'deliveryFromStockId', 'orderCostId']) {
            assert.equal(isInvoiceCustomerLocked({
                id,
                invoiceRows: [{ text: 'Manual row' }, { [field]: 42 }],
            }), true);
        }
    }
});

test('customer selection maps all customer-dependent invoice header fields', () => {
    assert.deepEqual(getInvoiceCustomerFields({
        id: 42,
        name: 'Customer',
        address: 'Street 1',
        address2: 'Floor 2',
        postalNr: '12345',
        postalAddress: 'City',
        country: 'Sweden',
        vatNr: 'SE123456789001',
        vatRate: 25,
        reference: 'Contact',
        paymentDays: 45,
        termsOfPayment: '45 days',
        currencyId: 2,
        languageId: 3,
        accountNrAccountsReceivable: 1510,
    }), {
        customerId: 42,
        customerName: 'Customer',
        address: 'Street 1',
        address2: 'Floor 2',
        postalNr: '12345',
        postalAddress: 'City',
        country: 'Sweden',
        vatNr: 'SE123456789001',
        vatRate: 0.25,
        yourReference: 'Contact',
        invoiceDays: 45,
        termsOfPayment: '45 days',
        salesCurrencyId: 2,
        languageId: 3,
        account: '1510',
    });
});

test('switching to a customer with missing details clears the previous customer details', () => {
    const previous = getInvoiceCustomerFields({
        id: 1, name: 'Previous', address: 'Old address', address2: 'Old extra address',
        postalNr: '12345', postalAddress: 'Old city', country: 'Old country',
        vatNr: 'Old VAT', reference: 'Old contact', paymentDays: 60,
        termsOfPayment: 'Old terms', currencyId: 2, languageId: 3,
        accountNrAccountsReceivable: 1510,
    });
    const next = { ...previous, ...getInvoiceCustomerFields({ id: 2, name: 'Next' }) };

    assert.deepEqual(next, {
        customerId: 2,
        customerName: 'Next',
        address: '',
        address2: '',
        postalNr: '',
        postalAddress: '',
        country: '',
        vatNr: '',
        vatRate: 0.25,
        yourReference: '',
        invoiceDays: 30,
        termsOfPayment: '',
        salesCurrencyId: null,
        languageId: null,
        account: '',
    });
});

test('clearing a customer resets related fields without changing other invoice data', () => {
    const invoice = {
        id: 10, customerId: 42, customerName: 'Previous', customerOrderId: 5,
        invoiceRows: [{ id: 1, sum: 100 }], invoiceAccountRows: [{ sum: 100 }],
        yourOrderNr: 'PO-1', ourReference: 'Employee', invoiceDate: '2026-10-27',
    };
    const next = { ...invoice, ...getInvoiceCustomerFields(null) };

    assert.equal(next.customerId, null);
    assert.equal(next.customerName, '');
    assert.equal(next.invoiceRows, invoice.invoiceRows);
    assert.equal(next.invoiceAccountRows, invoice.invoiceAccountRows);
    assert.equal(next.customerOrderId, invoice.customerOrderId);
    assert.equal(next.yourOrderNr, invoice.yourOrderNr);
    assert.equal(next.ourReference, invoice.ourReference);
    assert.equal(next.invoiceDate, invoice.invoiceDate);
});

test('zero payment days is preserved', () => {
    assert.equal(getInvoiceCustomerFields({ id: 42, paymentDays: 0 }).invoiceDays, 0);
});

test('VAT settings follow the customer, including exemption and the legacy default rate', () => {
    assert.equal(getInvoiceCustomerFields({ vat: false, vatRate: 25 }).vatRate, 0);
    assert.equal(getInvoiceCustomerFields({ vat: true, vatRate: 12 }).vatRate, 0.12);
    assert.equal(getInvoiceCustomerFields({ vat: true, vatRate: 0 }).vatRate, 0.25);
    assert.equal(getInvoiceCustomerFields({ vat: true, vatRate: null }).vatRate, 0.25);
});

test('derived rows use customer VAT and bank rounding without mutating source rows', () => {
    const source = [{ sum: 12.5, vatGround: true }, { sum: 2, vatGround: false }];
    for (const [rate, vat, rounding] of [[0.25, 3.12, 0.38], [0.12, 1.5, 0], [0, 0, 0.5]]) {
        const rows = applyInvoiceDerivedRows(source, rate);
        assert.equal(rows.find((row) => row.priceTypeId === 13).sum, vat);
        assert.equal(rows.find((row) => row.priceTypeId === 12).sum, rounding);
        assert.equal(rows.find((row) => row.priceTypeId === 12).text, 'Öresutjämning');
    }
    assert.deepEqual(source, [{ sum: 12.5, vatGround: true }, { sum: 2, vatGround: false }]);
});

test('negative and VAT-exempt totals round halves away from zero', () => {
    for (const [sum, expected] of [[2.5, 0.5], [-2.5, -0.5], [-2.6, -0.4], [2.4, -0.4]]) {
        const rows = applyInvoiceDerivedRows([{ sum, vatGround: false }], 0);
        assert.equal(rows.find((row) => row.priceTypeId === 12).sum, expected);
        assert.equal(rows.find((row) => row.priceTypeId === 13).sum, 0);
    }
});

test('bank rounding is stable at fractional-cent midpoints for both signs', () => {
    for (const [sum, vat, rounding] of [[4.06, 1.02, -0.08], [-4.06, -1.02, 0.08]]) {
        const rows = applyInvoiceDerivedRows([{ sum, vatGround: true }], 0.25);
        assert.equal(rows.find((row) => row.priceTypeId === 13).sum, vat);
        assert.equal(rows.find((row) => row.priceTypeId === 12).sum, rounding);
    }
});

test('recalculating rows preserves configured accounts and does not duplicate derived rows', () => {
    const source = [
        { sum: 100, vatGround: true },
        { priceTypeId: 13, text: 'Moms', sum: 25, accountNr: '2611' },
        { priceTypeId: 12, text: 'Existing rounding text', sum: 0, accountNr: '3740' },
    ];
    const rows = applyInvoiceDerivedRows(applyInvoiceDerivedRows(source, 0.12), 0);
    assert.equal(rows.length, 3);
    assert.equal(rows[1].sum, 0);
    assert.equal(rows[1].accountNr, '2611');
    assert.equal(rows[2].accountNr, '3740');
    assert.equal(rows[2].text, 'Existing rounding text');
});

test('fixed costs, quantity costs, pallets and freight ignore the product unit divisor', () => {
    const units = [{ id: 1, multiplicator: 1000 }];
    for (const [priceTypeId, nrOf, expected] of [[2, -1, 25], [2, 1.5, 37.5], [107, 3, 75], [108, 1, 25], [1, 2000, 50]]) {
        assert.equal(computeInvoiceRowSum({ priceTypeId, nrOf, unitPrice: 25, unitId: 1 }, units), expected);
    }
});

test('inventory preview inputs include only stock product quantities, not pallet or text edits', () => {
    const product = { priceTypeId: 1, deliveryFromStockId: 30, nrOf: 1000, text: 'Product' };
    const invoice = { invoiceRows: [product, { priceTypeId: 107, deliveryFromStockId: 30, nrOf: 2 }] };
    assert.deepEqual(JSON.parse(getInvoiceInventorySource(invoice)), [{ priceTypeId: 1, deliveryFromStockId: 30, nrOf: 1000 }]);
    product.text = 'Edited';
    const key = getInvoiceInventorySource(invoice);
    product.nrOf = 2000;
    assert.notEqual(getInvoiceInventorySource(invoice), key);
});
