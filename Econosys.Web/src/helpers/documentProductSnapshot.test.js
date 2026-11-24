import { test } from 'node:test';
import assert from 'node:assert/strict';
import { canRefreshDocumentProduct, hasDocumentProductDifference, mergeDocumentProductSnapshot } from './documentProductSnapshot.js';

test('comparison treats missing values as empty but detects translated and note changes', () => {
    assert.equal(hasDocumentProductDifference({ color: null }, { color: '' }), false);
    assert.equal(hasDocumentProductDifference({ material: 'Paper' }, { material: 'Papier' }), true);
    assert.equal(hasDocumentProductDifference({ printMethod: 'Offset' }, { printMethod: 'Flexo' }), true);
    assert.equal(hasDocumentProductDifference({ productNoteExternal: 'Old' }, { productNoteExternal: 'New' }), true);
});

test('refresh merges snapshots without overwriting unrelated edits', () => {
    const response = { saved: { product: 'New', productNoteExternal: 'External' }, edited: 'now' };
    assert.deepEqual(
        mergeDocumentProductSnapshot({ product: 'Old', message: 'Keep', editedAt: 'before' }, 'quotations', response),
        { product: 'New', message: 'Keep', editedAt: 'now', productMessage: 'External' },
    );
    assert.deepEqual(
        mergeDocumentProductSnapshot({ product: 'Old', message: 'Keep', edited: 'before' }, 'supplierorders', response),
        { product: 'New', message: 'Keep', edited: 'now', productNoteExternal: 'External' },
    );
});

test('refresh is blocked for unsaved documents, pending edits and in-flight requests', () => {
    assert.equal(canRefreshDocumentProduct({ id: 1, changed: true }), true);
    assert.equal(canRefreshDocumentProduct({ id: 0, changed: true }), false);
    assert.equal(canRefreshDocumentProduct({ id: 1, changed: true, dirty: true }), false);
    assert.equal(canRefreshDocumentProduct({ id: 1, changed: true, refreshing: true }), false);
});
