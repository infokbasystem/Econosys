import assert from 'node:assert/strict';
import test from 'node:test';
import { getDeliveryPalletLabel } from './deliveryEditing.js';

const delivery = { supplierOrderNr: '44436', nrOfPallets: 4, palletIsStackable: true, palletInfo: '4 pallar' };

test('pallet labels use the supplier order number, not the delivery pallet summary', () => {
    assert.equal(getDeliveryPalletLabel({ ...delivery, palletIsStackable: false }), '44436');
    assert.equal(getDeliveryPalletLabel({ ...delivery, supplierOrderNr: 44436, palletIsStackable: false }), '44436');
});

test('each full stack has 2p on a second line', () => {
    assert.equal(getDeliveryPalletLabel(delivery, 0), '44436\n2p');
    assert.equal(getDeliveryPalletLabel(delivery, 1), '44436\n2p');
});

test('the final single pallet in an odd stackable delivery has no 2p label', () => {
    const oddDelivery = { ...delivery, nrOfPallets: 5 };
    assert.equal(getDeliveryPalletLabel(oddDelivery, 0), '44436\n2p');
    assert.equal(getDeliveryPalletLabel(oddDelivery, 1), '44436\n2p');
    assert.equal(getDeliveryPalletLabel(oddDelivery, 2), '44436');
    assert.equal(getDeliveryPalletLabel({ ...delivery, nrOfPallets: 1 }), '44436');
});
