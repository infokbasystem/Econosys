import assert from 'node:assert/strict';
import test from 'node:test';
import { buildCallOffSavePayload, saveSentFreightOrder } from './calloffSave.js';

const savedCallOff = {
    callOffForm: {
        shipperId: '4',
        reference: 'Saved reference',
        note: '',
        deliveryDate: '2026-11-12',
        deliveryStatus: 0,
        isSentToShipper: false,
        doDebitFreight: true,
        freightCostToDebit: '125.5',
        customerDeliveryAddressId: '',
    },
    callOffDeliveryList: [
        { id: 8, note: 'Saved note', nrOfPalletPlaces: '2', deliveryFromStockId: 20 },
    ],
};

test('a sent freight order saves planned status using the saved call-off and its version', async () => {
    const snapshot = structuredClone(savedCallOff);
    let request;
    const version = await saveSentFreightOrder({
        put: async (url, payload) => {
            request = { url, payload };
            return { data: { version: 'version-after-send', callOff: { deliveryStatus: 1, isSentToShipper: true } } };
        },
    }, 42, snapshot, 'version-before-send');

    assert.equal(version, 'version-after-send');
    assert.equal(request.url, '/calloff/42/aggregate');
    assert.deepEqual(request.payload, {
        shipperId: 4,
        reference: 'Saved reference',
        note: null,
        deliveryDate: '2026-11-12',
        deliveryStatus: 1,
        isSentToShipper: true,
        doDebitFreight: true,
        freightCostToDebit: 125.5,
        customerDeliveryAddressId: null,
        callOffDeliveryList: [
            { id: 8, note: 'Saved note', nrOfPalletPlaces: 2, deliveryFromStockId: null, newDelivery: null },
        ],
        version: 'version-before-send',
    });
    assert.deepEqual(snapshot, savedCallOff);
});

test('a failed or unconfirmed status save is not treated as successful', async () => {
    const failure = new Error('Version conflict');
    await assert.rejects(
        saveSentFreightOrder({ put: async () => { throw failure; } }, 42, savedCallOff, 'v1'),
        (error) => error === failure,
    );
    await assert.rejects(
        saveSentFreightOrder({ put: async () => ({ data: { version: 'v2', callOff: { deliveryStatus: 0, isSentToShipper: true } } }) }, 42, savedCallOff, 'v1'),
        /could not be confirmed/,
    );
    await assert.rejects(
        saveSentFreightOrder({ put: async () => ({ data: { callOff: { deliveryStatus: 1, isSentToShipper: true } } }) }, 42, savedCallOff, 'v1'),
        /could not be confirmed/,
    );
});

test('a normal save retains the selected status and unsent flag', () => {
    const payload = buildCallOffSavePayload(savedCallOff);

    assert.equal(payload.deliveryStatus, 0);
    assert.equal(payload.isSentToShipper, false);
});
