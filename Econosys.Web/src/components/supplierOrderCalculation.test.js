import assert from 'node:assert/strict';
import test from 'node:test';
import { resolveSupplierOrderCalculationId } from './supplierOrderCalculation.js';

test('resolves a legacy supplier order through its inquiry', async () => {
    const requests = [];
    const client = {
        async get(url) {
            requests.push(url);
            return { data: { calculationId: 20479 } };
        },
    };

    assert.equal(await resolveSupplierOrderCalculationId(client, {
        inquiryId: 26204, quotationId: 24300, supplierOrderId: 44950, selectedCalculationRowId: 108850,
    }), 20479);
    assert.deepEqual(requests, ['/inquiries/26204']);
});

test('uses quotation or selected calculation row when earlier links have no calculation', async () => {
    const requests = [];
    const client = {
        async get(url) {
            requests.push(url);
            return { data: { calculationId: url.includes('calculation-row') ? 12 : null } };
        },
    };

    assert.equal(await resolveSupplierOrderCalculationId(client, {
        inquiryId: 1, quotationId: 2, supplierOrderId: 3, selectedCalculationRowId: 4,
    }), 12);
    assert.deepEqual(requests, ['/inquiries/1', '/quotations/2', '/supplierorders/3/calculation-row']);
    requests.length = 0;
    client.get = async (url) => {
        requests.push(url);
        return { data: { calculationId: 15 } };
    };
    assert.equal(await resolveSupplierOrderCalculationId(client, { quotationId: 2 }), 15);
    assert.deepEqual(requests, ['/quotations/2']);
});

test('reports missing links and propagates request failures', async () => {
    await assert.rejects(
        resolveSupplierOrderCalculationId({ get: async () => ({ data: { calculationId: null } }) }, { inquiryId: 1 }),
        /saknar en kopplad kalkyl/,
    );
    const error = new Error('Request failed');
    await assert.rejects(
        resolveSupplierOrderCalculationId({ get: async () => { throw error; } }, { inquiryId: 1 }),
        (actual) => actual === error,
    );
});

test('resolves customer orders from their quotation before the linked supplier order', async () => {
    const requests = [];
    const client = {
        async get(url) {
            requests.push(url);
            return { data: { calculationId: 12 } };
        },
    };

    assert.equal(await resolveSupplierOrderCalculationId(client, { quotationId: 2, linkedSupplierOrderId: 3 }), 12);
    assert.deepEqual(requests, ['/quotations/2']);
});

test('resolves the linked supplier order calculation or its legacy links', async () => {
    const responses = {
        '/quotations/2': { calculationId: null },
        '/supplierorders/3': { calculationId: null, inquiryId: 4, quotationId: 2, selectedCalculationRowId: 5 },
        '/inquiries/4': { calculationId: null },
        '/supplierorders/3/calculation-row': { calculationId: 12 },
    };
    const requests = [];
    const client = {
        async get(url) {
            requests.push(url);
            assert.ok(url in responses, `Unexpected request: ${url}`);
            return { data: responses[url] };
        },
    };

    assert.equal(await resolveSupplierOrderCalculationId(client, { quotationId: 2, linkedSupplierOrderId: 3 }), 12);
    assert.deepEqual(requests, ['/quotations/2', '/supplierorders/3', '/inquiries/4', '/supplierorders/3/calculation-row']);

    requests.length = 0;
    responses['/supplierorders/3'].calculationId = 18;
    assert.equal(await resolveSupplierOrderCalculationId(client, { linkedSupplierOrderId: 3 }), 18);
    assert.deepEqual(requests, ['/supplierorders/3']);
});

test('reports missing or inaccessible linked supplier orders', async () => {
    await assert.rejects(
        resolveSupplierOrderCalculationId({ get: async () => ({ data: {} }) }, { linkedSupplierOrderId: 3 }),
        /saknar en kopplad kalkyl/,
    );
    const error = new Error('Supplier order unavailable');
    await assert.rejects(
        resolveSupplierOrderCalculationId({ get: async () => { throw error; } }, { linkedSupplierOrderId: 3 }),
        (actual) => actual === error,
    );
});
