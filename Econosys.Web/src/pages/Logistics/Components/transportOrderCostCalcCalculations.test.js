import assert from 'node:assert/strict';
import test from 'node:test';

import {
    distributeResultInternationalCost,
    recalculateCostCalcRow,
} from './transportOrderCostCalcCalculations.js';

const createRow = (overrides = {}) => ({
    id: 1,
    totalNrOfPalletPlaces: 4,
    palletFactor: 1,
    caclulationUsedTotal: 250,
    toInternationalCost: 100,
    transportOrderDomesticCost: 20,
    transportOrderUnloading: 10,
    transportOrderLoading: 5,
    transportOrderOther: 0,
    resultInternationalCost: 80,
    resultDomesticCost: 10,
    resultOther: 5,
    ...overrides,
});

test('recalculates transport totals and downstream result difference after an other-cost edit', () => {
    const row = recalculateCostCalcRow(createRow({ transportOrderOther: '25' }));

    assert.equal(row.transportOrderTotal, 160);
    assert.equal(row.diffTransportOrderCaclulation, 90);
    assert.equal(row.resultTotal, 95);
    assert.equal(row.diffResultTransportOrder, 65);
});

test('recalculates result totals and both result differences', () => {
    const row = recalculateCostCalcRow(createRow({ resultDomesticCost: '30', resultOther: '15' }));

    assert.equal(row.resultTotal, 125);
    assert.equal(row.diffResultCaclulation, 125);
    assert.equal(row.diffResultTransportOrder, 10);
});

test('treats blank numeric inputs as zero without replacing the entered value', () => {
    const row = recalculateCostCalcRow(createRow({ transportOrderOther: '', resultOther: '' }));

    assert.equal(row.transportOrderOther, '');
    assert.equal(row.resultOther, '');
    assert.equal(row.transportOrderTotal, 135);
    assert.equal(row.resultTotal, 90);
});

test('distributes result international cost by pallet area and recalculates rows', () => {
    const rows = distributeResultInternationalCost([
        createRow({ id: 1, totalNrOfPalletPlaces: 2, palletFactor: 1 }),
        createRow({ id: 2, totalNrOfPalletPlaces: 3, palletFactor: 2 }),
    ], '400');

    assert.equal(rows[0].resultInternationalCost, 100);
    assert.equal(rows[1].resultInternationalCost, 300);
    assert.equal(rows[0].resultTotal, 115);
    assert.equal(rows[1].resultTotal, 315);
});

test('distributes zero to every row when total pallet area is zero', () => {
    const rows = distributeResultInternationalCost([
        createRow({ totalNrOfPalletPlaces: 0 }),
        createRow({ id: 2, palletFactor: null }),
    ], '400');

    assert.deepEqual(rows.map((row) => row.resultInternationalCost), [0, 0]);
});