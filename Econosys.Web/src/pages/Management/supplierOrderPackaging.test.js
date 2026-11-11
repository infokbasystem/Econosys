import assert from 'node:assert/strict';
import test from 'node:test';
import { fetchSupplierOrderPackaging } from './supplierOrderPackaging.js';

test('uses the inquiry calculation for legacy orders and includes freight notes and EUR pallets', async () => {
    const requests = [];
    const client = {
        async get(url) {
            requests.push(url);
            return { data: url === '/inquiries/7'
                ? { calculationId: 12 }
                : { loadingInstruction: 'Pack on pallet', freightNote: 'Stack two high', palletType: 1 } };
        },
    };
    const order = { inquiryId: 7, calculationId: 99, loadingInstruction: 'Old', message: 'Keep' };
    assert.deepEqual(await fetchSupplierOrderPackaging(client, order), {
        loadingInstruction: 'Pack on pallet\nStack two high', eurPallet: true, oneWayPallet: false,
    });
    assert.deepEqual(requests, ['/inquiries/7', '/calculations/12/aggregate']);
    assert.equal(order.loadingInstruction, 'Old');
    assert.equal(order.message, 'Keep');
});

test('uses the direct calculation for new orders and refreshes on every request', async () => {
    let requests = 0;
    const client = {
        async get(url) {
            assert.equal(url, '/calculations/12/aggregate');
            requests++;
            return { data: { loadingInstruction: `Instruction ${requests}`, palletType: 2 } };
        },
    };
    const order = { calculationId: 12 };
    assert.deepEqual(await fetchSupplierOrderPackaging(client, order), {
        loadingInstruction: 'Instruction 1', eurPallet: false, oneWayPallet: true,
    });
    assert.equal((await fetchSupplierOrderPackaging(client, order)).loadingInstruction, 'Instruction 2');
});

test('handles missing instruction, freight note and pallet type without adding blank lines', async () => {
    for (const [loadingInstruction, freightNote, expected] of [
        [null, 'Freight only', 'Freight only'],
        ['Instruction only', null, 'Instruction only'],
        ['', '  ', ''],
    ]) {
        const client = { get: async () => ({ data: { loadingInstruction, freightNote, palletType: null } }) };
        assert.deepEqual(await fetchSupplierOrderPackaging(client, { calculationId: 12 }), {
            loadingInstruction: expected, eurPallet: false, oneWayPallet: false,
        });
    }
});

test('reports missing linked calculations without fetching unrelated calculations', async () => {
    const unusedClient = { get: async () => assert.fail('Unexpected request') };
    await assert.rejects(fetchSupplierOrderPackaging(unusedClient, {}), /saknar en kopplad kalkyl/);
    const client = {
        async get(url) {
            assert.equal(url, '/inquiries/7');
            return { data: { calculationId: null } };
        },
    };
    await assert.rejects(fetchSupplierOrderPackaging(client, { inquiryId: 7 }), /saknar en kopplad kalkyl/);
});

test('propagates inquiry and calculation request failures', async () => {
    const failure = new Error('Request failed');
    const client = { get: async () => { throw failure; } };
    await assert.rejects(fetchSupplierOrderPackaging(client, { inquiryId: 7 }), (error) => error === failure);
    await assert.rejects(fetchSupplierOrderPackaging(client, { calculationId: 12 }), (error) => error === failure);
});
