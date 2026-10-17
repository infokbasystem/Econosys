import assert from 'node:assert/strict';
import test from 'node:test';
import { applyApprovalRows, calculatePackagingCost, getApprovalText, getFreightForApproval, updateAndApproveAllSections } from './calculationApprovals.js';

test('approve all chains refreshed rows and preserves each preceding approval', async () => {
    const original = { rows: [{ edition: 2000, nrOfPallets: 1 }] };
    const calls = [];
    const flags = { freight: 'approveFreight', packaging: 'approvePackaging', other: 'approveOtherCosts', storage: 'approveStorage' };
    const result = await updateAndApproveAllSections(original, async (component, draft) => {
        calls.push(component);
        return applyApprovalRows(draft, flags[component], (row) => {
            if (component === 'freight') return { ...row, nrOfPallets: 3 };
            if (component === 'packaging') return {
                ...row, packagingCost: calculatePackagingCost(row, calculation, 1000),
            };
            return { ...row, [component]: 7 };
        });
    });
    assert.deepEqual(calls, ['freight', 'packaging', 'other', 'storage']);
    for (const flag of Object.values(flags)) assert.equal(result[flag], true);
    assert.equal(result.rows[0].packagingCost, 22.5);
    assert.equal(result.rows[0].other, 7);
    assert.equal(result.rows[0].storage, 7);
    assert.deepEqual(original, { rows: [{ edition: 2000, nrOfPallets: 1 }] });
});

test('approve all stops on failure without returning partial results or changing the input', async () => {
    const original = { rows: [] };
    const calls = [];
    await assert.rejects(updateAndApproveAllSections(original, async (component, draft) => {
        calls.push(component);
        if (component === 'other') throw new Error('Lookup failed');
        return { ...draft, [component]: true };
    }), /Lookup failed/);
    assert.deepEqual(calls, ['freight', 'packaging', 'other']);
    assert.deepEqual(original, { rows: [] });
});

const calculation = {
    palletPriceIn: 10,
    palletPriceOut: 5,
    purchaseCurrencyRate: 10,
    salesCurrencyRate: 5,
};
const row = { edition: 2000, nrOfPallets: 2.1 };

test('packaging applies only the deficit, converting currencies and rounding up pallets', () => {
    assert.equal(calculatePackagingCost(row, calculation, 1000), 22.5);
    assert.equal(calculatePackagingCost(row, { ...calculation, palletPriceOut: 20 }, 1000), null);
    assert.equal(calculatePackagingCost(row, { ...calculation, palletPriceOut: 30 }, 1000), null);
    assert.equal(calculatePackagingCost(row, { ...calculation, palletPriceOut: 0 }, 1000), 30);
});

test('packaging refuses missing pallet quantities, editions, units and currency rates', () => {
    for (const nrOfPallets of [null, '', NaN, -1]) {
        assert.throws(() => calculatePackagingCost({ ...row, nrOfPallets }, calculation, 1000));
    }
    assert.throws(() => calculatePackagingCost({ ...row, edition: 0 }, calculation, 1000));
    assert.throws(() => calculatePackagingCost(row, calculation, 0));
    assert.throws(() => calculatePackagingCost(row, { ...calculation, salesCurrencyRate: 0 }, 1000));
    assert.throws(() => calculatePackagingCost(row, { ...calculation, purchaseCurrencyRate: null }, 1000));
});

test('approvals update active rows and preserve archived rows and the empty placeholder', () => {
    const active = { ...row, other: 2 };
    const archived = { ...row, archived: true, other: 3 };
    const placeholder = { isDraft: true, edition: null };
    const draft = { ...calculation, rows: [active, archived, placeholder], freightNote: 'Keep' };
    const result = applyApprovalRows(draft, 'approveOtherCosts', (item) => ({ ...item, other: 7 }));
    assert.equal(result.approveOtherCosts, true);
    assert.equal(result.rows[0].other, 7);
    assert.equal(result.rows[1], archived);
    assert.equal(result.rows[2], placeholder);
    assert.equal(result.freightNote, 'Keep');
    assert.equal(active.other, 2);
    assert.equal(draft.approveOtherCosts, undefined);
});

test('failed row calculation does not approve or partially change the draft', () => {
    const draft = { rows: [row, { ...row, edition: 0 }] };
    assert.throws(() => applyApprovalRows(draft, 'approvePackaging', (item) => ({
        ...item, packagingCost: calculatePackagingCost(item, calculation, 1000),
    })));
    assert.equal(draft.approvePackaging, undefined);
    assert.equal(draft.rows[0].packagingCost, undefined);
});

test('refresh is applied before approving totals, not from old row data', () => {
    const draft = { rows: [{ edition: 1000, costs: [{ outPrice: 1 }] }] };
    const result = applyApprovalRows(draft, 'approveOtherCosts', (item) => {
        const refreshed = { ...item, costs: [{ outPrice: 12 }] };
        return { ...refreshed, other: refreshed.costs.reduce((sum, cost) => sum + cost.outPrice, 0) };
    });
    assert.equal(result.rows[0].other, 12);
    assert.equal(result.approveOtherCosts, true);
    assert.equal(draft.rows[0].costs[0].outPrice, 1);
});

const freightRow = {
    ...row,
    freightPerUnit: 15,
    freightInternationalPerPallet: 100,
    palletCalcFactor: 1,
    calcPalletPriceUsed: true,
};

test('freight approval supports manual, refreshed automatic, FTL and deactivated freight', () => {
    assert.equal(getFreightForApproval(freightRow, { autoFreightPrice: false }), 15);
    assert.equal(getFreightForApproval({ ...freightRow, freightPerUnit: 21 }, { autoFreightPrice: true }), 21);
    assert.equal(getFreightForApproval({
        ...freightRow, calcPalletPriceUsed: false, calcFtlPriceUsed: true, freightPerVehicle: 1000,
    }, {}), 15);
    assert.equal(getFreightForApproval({}, { deactivateFreightCalculation: true }), null);
});

test('freight approval blocks missing prices, pallet factors and invalid computed results', () => {
    for (const overrides of [
        { nrOfPallets: null }, { nrOfPallets: -1 }, { edition: 0 },
        { freightPerUnit: null }, { freightPerUnit: NaN }, { palletCalcFactor: 0 },
        { freightInternationalPerPallet: 0 }, { calcFtlPriceUsed: true, freightPerVehicle: 0 },
    ]) {
        assert.throws(() => getFreightForApproval({ ...freightRow, ...overrides }, {}));
    }
});

test('approval labels distinguish pending, saved and unapproved sections', () => {
    assert.equal(getApprovalText(null, false), 'Ännu ej godkänd');
    const saved = { approvedAt: '2026-10-02T07:34:04+02:00', approvedBy: 'PT' };
    assert.match(getApprovalText(saved, false), /^Godkänd .*2026-10-02.*07:34.* av PT$/);
    assert.equal(getApprovalText(saved, true), 'Godkänns när kalkylen sparas');
    assert.doesNotMatch(getApprovalText({ ...saved, approvedBy: null }, false), / av /);
});
