import assert from 'node:assert/strict';
import { test } from 'node:test';
import { buildOrderPersonFilters, buildOrderTextFilters } from './orderSearchFilters.js';

const options = [{ value: 1 }, { value: 2 }, { value: 3 }];

test('text filters trim independent terms and disable blank fields', () => {
    assert.deepEqual(buildOrderTextFilters({
        searchTerm: ' 123 ', product: ' Box ', supplierName: ' Paper Mill ', customerName: ' Store ',
    }), {
        searchTerm: '123', product: 'Box', supplierName: 'Paper Mill', customerName: 'Store',
    });
    assert.deepEqual(buildOrderTextFilters({
        searchTerm: '', product: '   ', supplierName: '', customerName: ' Store ',
    }), {
        searchTerm: null, product: null, supplierName: null, customerName: 'Store',
    });
});

test('customer order number filter is trimmed and blank values are disabled', () => {
    const values = { searchTerm: '', product: '', supplierName: '', customerName: '' };
    assert.deepEqual(buildOrderTextFilters({ ...values, yourOrderNr: ' PO-123 ' }), {
        searchTerm: null, product: null, supplierName: null, customerName: null, yourOrderNr: 'PO-123',
    });
    assert.equal(buildOrderTextFilters({ ...values, yourOrderNr: '  ' }).yourOrderNr, null);
});

test('supplier order customer number filter is trimmed and blank values are disabled', () => {
    const values = { searchTerm: '', product: '', supplierName: '', customerName: '' };
    assert.deepEqual(buildOrderTextFilters({ ...values, customerOrderNr: ' PO-123 ' }), {
        searchTerm: null, product: null, supplierName: null, customerName: null, customerOrderNr: 'PO-123',
    });
    assert.equal(buildOrderTextFilters({ ...values, customerOrderNr: '  ' }).customerOrderNr, null);
});

test('default and explicitly selected all omit both person properties', () => {
    assert.deepEqual(buildOrderPersonFilters(null, null, []), {});
    assert.deepEqual(buildOrderPersonFilters(null, null, options), {});
    assert.deepEqual(buildOrderPersonFilters([3, 1, 2], [1, 2, 3], options), {});
});

test('each person filter is included only when it restricts the selection', () => {
    assert.deepEqual(buildOrderPersonFilters([1, 3], [1, 2, 3], options), {
        customerResponsibleUserIds: [1, 3],
    });
    assert.deepEqual(buildOrderPersonFilters(null, [2], options), { createdByIds: [2] });
    assert.deepEqual(buildOrderPersonFilters([1], [2], options), {
        customerResponsibleUserIds: [1],
        createdByIds: [2],
    });
});

test('deselecting all sends empty lists rather than disabling filters', () => {
    assert.deepEqual(buildOrderPersonFilters([], [], options), {
        customerResponsibleUserIds: [],
        createdByIds: [],
    });
});
