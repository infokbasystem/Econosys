import assert from 'node:assert/strict';
import { test } from 'node:test';
import { resizeColumn } from './columnResize.js';

test('resizing changes only the chosen column', () => {
    const widths = [80, 120, 100];
    assert.deepEqual(resizeColumn(widths, 1, 180), [80, 180, 100]);
    assert.deepEqual(widths, [80, 120, 100]);
});

test('resizing clamps the selected column to the minimum width', () => {
    assert.deepEqual(resizeColumn([40, 100, 120], 1, -30), [40, 64, 120]);
    assert.deepEqual(resizeColumn([80, 100], 1, 64), [80, 64]);
});
