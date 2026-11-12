import assert from 'node:assert/strict';
import test from 'node:test';
import { getAutoPlacement } from './truckLoadingLayout.js';

const area = { areaWidthCm: 240, areaLengthCm: 1350 };
const euro = { widthCm: 80, heightCm: 120 };
const summary = ({ widthCm, heightCm, rotation, positions }) => [widthCm, heightCm, rotation, positions.length];

test('euro pallets go 80 across, filling rows left to right from the front', () => {
    const result = getAutoPlacement([], euro, 4, area);
    assert.deepEqual(summary(result), [80, 120, 0, 4]);
    assert.deepEqual(result.positions, [{ xCm: 0, yCm: 0 }, { xCm: 80, yCm: 0 }, { xCm: 160, yCm: 0 }, { xCm: 0, yCm: 120 }]);
});

test('continues after the rearmost/rightmost pallet instead of filling gaps in front of it', () => {
    const existing = [{ xCm: 0, yCm: 0, widthCm: 80, heightCm: 120 }, { xCm: 0, yCm: 120, widthCm: 80, heightCm: 120 }];
    assert.deepEqual(getAutoPlacement(existing, euro, 3, area).positions, [{ xCm: 80, yCm: 120 }, { xCm: 160, yCm: 120 }, { xCm: 0, yCm: 240 }]);
    const lone = [{ xCm: 160, yCm: 0, widthCm: 80, heightCm: 120 }];
    assert.deepEqual(getAutoPlacement(lone, euro, 1, area).positions, [{ xCm: 0, yCm: 120 }]);
});

test('stops when the truck is full', () => {
    assert.equal(getAutoPlacement([], euro, 100, area).positions.length, 3 * 11);
});

test('non-80 pallets with a 120 side put the 120 side across', () => {
    assert.deepEqual(summary(getAutoPlacement([], { widthCm: 102, heightCm: 120 }, 26, area)), [120, 102, 90, 26]);
    assert.deepEqual(summary(getAutoPlacement([], { widthCm: 60, heightCm: 80 }, 1, area)), [80, 60, 90, 1]);
});

test('240 pallets go 240 across, one per row after the rearmost pallet', () => {
    const existing = [{ xCm: 0, yCm: 0, widthCm: 80, heightCm: 120 }];
    const result = getAutoPlacement(existing, { widthCm: 100, heightCm: 240 }, 20, area);
    assert.deepEqual(summary(result), [240, 100, 90, 12]);
    assert.deepEqual(result.positions.slice(0, 2), [{ xCm: 0, yCm: 120 }, { xCm: 0, yCm: 220 }]);
});

test('other sizes keep their orientation and fill a left and a right column', () => {
    const result = getAutoPlacement([], { widthCm: 100, heightCm: 110 }, 4, area);
    assert.deepEqual(summary(result), [100, 110, 0, 4]);
    assert.deepEqual(result.positions, [{ xCm: 0, yCm: 0 }, { xCm: 140, yCm: 0 }, { xCm: 0, yCm: 110 }, { xCm: 140, yCm: 110 }]);
    const wide = getAutoPlacement([], { widthCm: 130, heightCm: 150 }, 2, area);
    assert.deepEqual(wide.positions, [{ xCm: 0, yCm: 0 }, { xCm: 0, yCm: 150 }]);
});
