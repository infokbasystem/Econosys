import assert from 'node:assert/strict';
import test from 'node:test';
import { getInvoiceCustomerFields, isInvoiceCustomerLocked } from './invoiceUtils.js';

test('standalone invoices allow customer selection', () => {
    assert.equal(isInvoiceCustomerLocked(null), false);
    assert.equal(isInvoiceCustomerLocked({ id: 0, invoiceRows: [{ text: 'Manual row' }] }), false);
    assert.equal(isInvoiceCustomerLocked({
        id: 10,
        invoiceRows: [{ deliveryToCustomerId: null, deliveryFromStockId: 0, orderCostId: null }],
    }), false);
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
