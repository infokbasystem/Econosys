import assert from 'node:assert/strict';
import test from 'node:test';
import { getVisibleNavigationIds } from './orderNavigationVisibility.js';

test('limits each type within each parent to its three newest IDs and preserves the current path', () => {
    const tree = {
        currentEntityType: 'customerorder',
        currentEntityId: 2,
        calculations: [
            {
                id: 1,
                inquiries: [
                    {
                        id: 1,
                        quotations: [{
                            id: 1,
                            supplierOrders: [{
                                id: 1,
                                customerOrders: [{ id: 2, isCurrent: true }, { id: 3 }],
                            }],
                        }],
                    },
                ],
            },
            {
                id: 2,
                inquiries: [{ id: 2 }, { id: 3 }, { id: 4 }, { id: 5 }],
            },
            { id: 3 },
            { id: 4 },
            { id: 5 },
        ],
    };

    const visible = getVisibleNavigationIds(tree, 'customerOrder', 2);

    assert.deepEqual([...visible.calculation], [5, 4, 3, 1]);
    assert.deepEqual([...visible.inquiry], [1, 5, 4, 3]);
    assert.deepEqual([...visible.quotation], [1]);
    assert.deepEqual([...visible.supplierOrder], [1]);
    assert.deepEqual([...visible.customerOrder], [3, 2]);
});

test('shows a calculation’s single inquiry even when other calculations have newer inquiries', () => {
    const tree = {
        currentEntityType: 'customerOrder',
        currentEntityId: 34887,
        calculations: [
            { id: 18108, inquiries: [{ id: 20000 }] },
            {
                id: 18487,
                inquiries: [{
                    id: 23519,
                    supplierOrders: [{ id: 44631, customerOrders: [{ id: 34887 }] }],
                }],
            },
            { id: 23388, inquiries: [{ id: 28056 }, { id: 28057 }, { id: 28058 }, { id: 28059 }] },
        ],
    };
    const visible = getVisibleNavigationIds(tree);

    assert.equal(visible.inquiry.has(20000), true);
    assert.equal(visible.inquiry.has(23519), true);
    assert.equal(visible.inquiry.has(28056), false);
    assert.equal(visible.inquiry.has(28057), true);
    assert.equal(visible.inquiry.has(28058), true);
    assert.equal(visible.inquiry.has(28059), true);
});

test('limits sibling collections independently at deeper levels', () => {
    const tree = {
        calculations: [{
            id: 1,
            quotations: [
                { id: 1, supplierOrders: [{ id: 1 }] },
                { id: 2, supplierOrders: [{ id: 2 }, { id: 3 }, { id: 4 }, { id: 5 }] },
            ],
        }],
    };
    const visible = getVisibleNavigationIds(tree, 'product', 1);
    assert.deepEqual([...visible.supplierOrder], [1, 5, 4, 3]);
});

test('keeps only current-path nodes when fewer than three latest items exist', () => {
    const visible = getVisibleNavigationIds({
        currentEntityType: 'inquiry',
        currentEntityId: 8,
        calculations: [{
            id: 7,
            inquiries: [{ id: 8, isCurrent: true }],
        }],
    });

    assert.deepEqual([...visible.calculation], [7]);
    assert.deepEqual([...visible.inquiry], [8]);
});

test('handles missing trees and does not mutate the input tree', () => {
    assert.deepEqual(getVisibleNavigationIds(null, 'calculation', 1), {});

    const calculations = [{ id: 3 }, { id: 1 }, { id: 2 }, { id: 4 }];
    const tree = { calculations };
    assert.deepEqual([...getVisibleNavigationIds(tree, 'product', 0).calculation], [4, 3, 2]);
    assert.deepEqual(calculations.map(({ id }) => id), [3, 1, 2, 4]);
});
