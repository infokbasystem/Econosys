import assert from 'node:assert/strict';
import test from 'node:test';
import { formatHistoryQuantity, getHistoryDetailUrl, getHistoryTransportLink } from './orderHistory.js';

test('delivery quantities distinguish inbound, outbound, direct, production and stocktaking', () => {
    assert.equal(formatHistoryQuantity({ type: 'DeliveryToStock', quantity: 4840 }), '+ 4\u00a0840');
    assert.equal(formatHistoryQuantity({ type: 'DeliveryFromStock', quantity: 300 }), '- 300');
    assert.equal(formatHistoryQuantity({ type: 'DeliveryToCustomer', quantity: 300 }), '300');
    assert.equal(formatHistoryQuantity({ type: 'Produced', quantity: 5140 }), '5\u00a0140');
    assert.equal(formatHistoryQuantity({ type: 'StockTaking', quantity: 0 }), '0');
    assert.equal(formatHistoryQuantity({ type: 'Invoice', quantity: 25 }), '25 st.');
});

test('negative adjustments show the actual movement direction', () => {
    assert.equal(formatHistoryQuantity({ type: 'DeliveryToStock', quantity: -50 }), '- 50');
    assert.equal(formatHistoryQuantity({ type: 'DeliveryFromStock', quantity: -50 }), '+ 50');
});

test('transport links take precedence over calloffs and use record IDs not display numbers', () => {
    assert.deepEqual(getHistoryTransportLink({ transportOrderId: 12, transportOrderNr: 2958, callOffId: 45 }), {
        to: '/logistics/transportorder/12', label: '2958',
    });
    assert.deepEqual(getHistoryTransportLink({ callOffId: 45 }), { to: '/logistics/calloff/45', label: '45' });
    assert.equal(getHistoryTransportLink({}), null);
});

test('details target the correct delivery table even when IDs overlap', () => {
    for (const type of ['DeliveryToStock', 'DeliveryFromStock', 'DeliveryToCustomer']) {
        assert.equal(getHistoryDetailUrl({ type, id: 12 }), `/deliveries/${type}/12`);
    }
    assert.equal(getHistoryDetailUrl({ type: 'StockTaking', id: 12 }), '/stocktakingitems/12');
    assert.equal(getHistoryDetailUrl({ type: 'Produced', id: 0 }), null);
    assert.equal(getHistoryDetailUrl({ type: 'Invoice', id: 12 }), null);
});
