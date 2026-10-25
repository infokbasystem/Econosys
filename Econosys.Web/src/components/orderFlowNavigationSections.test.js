import assert from 'node:assert/strict';
import test from 'node:test';
import { getOrderFlowSections } from './orderFlowNavigationSections.js';

test('groups in legacy order, hides empty groups and uses calculation-specific headings', () => {
    const sections = getOrderFlowSections([
        { type: 'customerOrder', id: 6 },
        { type: 'inquiry', id: 3 },
        { type: 'product', id: 1 },
        { type: 'calculation', id: 2 },
    ], 'calculation');

    assert.deepEqual(sections.map(({ type }) => type), ['product', 'calculation', 'inquiry', 'customerOrder']);
    assert.deepEqual(sections.map(({ label }) => label), [
        'PRODUKTER', 'AKTUELL KALKYL', 'FÖRFRÅGNINGAR', 'AKTUELLT ORDERERKÄNNANDE',
    ]);
});

test('shows only the latest inquiry and quotation in calculations without dropping older records', () => {
    const types = ['inquiry', 'quotation', 'supplierOrder', 'customerOrder'];
    const items = types.flatMap((type) => Array.from({ length: 12 }, (_, index) => ({ type, id: index + 1 })));
    const sections = getOrderFlowSections(items, 'calculation');

    assert.deepEqual(sections.map(({ limit }) => limit), [1, 1, 8, 8]);
    for (const section of sections) {
        assert.equal(section.items.length, 12);
        assert.deepEqual(section.items.map(({ id }) => id), Array.from({ length: 12 }, (_, index) => 12 - index));
        assert.equal(section.items.slice(0, section.limit)[0].id, 12);
    }
    assert.equal(items[0].id, 1);
});

test('preserves legacy show-more thresholds outside calculations', () => {
    const types = ['inquiry', 'quotation', 'supplierOrder', 'customerOrder'];
    const items = types.map((type) => ({ type, id: 1 }));

    for (const currentEntityType of ['product', ...types]) {
        const sections = getOrderFlowSections(items, currentEntityType);
        assert.deepEqual(sections.map(({ limit }) => limit), [3, 3, 8, 8]);
    }
});

test('keeps single inquiry and quotation sections within the calculation display limit', () => {
    const sections = getOrderFlowSections([
        { type: 'inquiry', id: 2 },
        { type: 'quotation', id: 3 },
    ], 'calculation');

    for (const section of sections) {
        assert.equal(section.limit, 1);
        assert.equal(section.items.length > section.limit, false);
    }
});

test('orders calculations by supplier, customer, then descending id', () => {
    const items = [
        { type: 'calculation', id: 1, supplierName: 'Z', customerName: 'B' },
        { type: 'calculation', id: 2, supplierName: 'A', customerName: 'B' },
        { type: 'calculation', id: 3, supplierName: 'A', customerName: 'A' },
        { type: 'calculation', id: 4, supplierName: 'A', customerName: 'A' },
    ];
    assert.deepEqual(getOrderFlowSections(items, 'product')[0].items.map(({ id }) => id), [4, 3, 2, 1]);
    assert.deepEqual(getOrderFlowSections([], 'calculation'), []);
});
