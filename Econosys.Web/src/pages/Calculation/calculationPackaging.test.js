import assert from 'node:assert/strict';
import test from 'node:test';
import { updateAutomaticLoadingInstruction } from './calculationPackaging.js';

const packaging = {
    automaticLoadingInstruction: true,
    palletLength: 1200,
    palletWidth: 800,
    palletHeight: 1300,
    editionPerPallet: 1000,
    loadingInstruction: 'Saved instruction',
};

const edit = (field, value, previous = packaging) =>
    updateAutomaticLoadingInstruction(previous, { ...previous, [field]: value });

test('regenerates on each packaging edit with the legacy wording', () => {
    const cases = [
        ['palletLength', 1400, '1400x800x1300 - 1000pcs/pallet'],
        ['palletWidth', 1000, '1200x1000x1300 - 1000pcs/pallet'],
        ['palletHeight', 1500, '1200x800x1500 - 1000pcs/pallet'],
        ['editionPerPallet', 1200, '1200x800x1300 - 1200pcs/pallet'],
        ['palletIsStackable', true, '1200x800x1300 - 1000pcs/pallet - 2 pallets in height on truck'],
        ['palletFormatId', 45, '1200x800x1300 - 1000pcs/pallet'],
    ];
    for (const [field, value, expected] of cases) {
        assert.equal(edit(field, value).loadingInstruction, expected);
    }
});

test('generates immediately when automatic mode is enabled', () => {
    assert.equal(edit('automaticLoadingInstruction', true, {
        ...packaging, automaticLoadingInstruction: false,
    }).loadingInstruction, '1200x800x1300 - 1000pcs/pallet');
});

test('includes bundles and outer boxes with legacy spacing and line breaks', () => {
    const previous = { ...packaging, doBuntas: true, nrOfPerBundle: 25 };
    const next = edit('isYtterforpackning', true, { ...previous, nrOfPerOuterPackaging: 100 });
    assert.equal(next.loadingInstruction,
        '1200x800x1300 - 1000pcs/pallet\nBundled: 25 pcs/bundle, 40 bundles/pallet - Packed in outer boxes: 100pcs/box, 10 boxes/pallet');
    assert.equal(edit('doBuntas', false, next).loadingInstruction,
        '1200x800x1300 - 1000pcs/pallet\nPacked in outer boxes: 100pcs/box, 10 boxes/pallet');
    assert.equal(edit('isYtterforpackning', false, next).loadingInstruction,
        '1200x800x1300 - 1000pcs/pallet\nBundled: 25 pcs/bundle, 40 bundles/pallet');
});

test('updates package quantities and truncates incomplete packages like the legacy integer calculation', () => {
    assert.match(edit('nrOfPerBundle', 30, { ...packaging, doBuntas: true }).loadingInstruction,
        /Bundled: 30 pcs\/bundle, 33 bundles\/pallet$/);
    assert.match(edit('nrOfPerOuterPackaging', 60, { ...packaging, isYtterforpackning: true }).loadingInstruction,
        /Packed in outer boxes: 60pcs\/box, 16 boxes\/pallet$/);
});

test('handles empty dimensions and zero or missing package quantities without invalid numbers', () => {
    assert.equal(edit('palletHeight', null).loadingInstruction, '1200x800x - 1000pcs/pallet');
    for (const quantity of [0, null, '']) {
        assert.match(edit('nrOfPerBundle', quantity, { ...packaging, doBuntas: true }).loadingInstruction,
            / bundles\/pallet$/);
        assert.doesNotMatch(edit('nrOfPerBundle', quantity, { ...packaging, doBuntas: true }).loadingInstruction,
            /NaN|Infinity|null|undefined/);
    }
});

test('preserves manual instructions when automatic mode is off or turned off', () => {
    assert.equal(edit('palletHeight', 1400, {
        ...packaging, automaticLoadingInstruction: false,
    }).loadingInstruction, 'Saved instruction');
    assert.equal(edit('automaticLoadingInstruction', false).loadingInstruction, 'Saved instruction');
});

test('does not regenerate saved instructions on load or unrelated edits', () => {
    assert.equal(updateAutomaticLoadingInstruction(packaging, { ...packaging }).loadingInstruction, 'Saved instruction');
    assert.equal(edit('freightNote', 'Note').loadingInstruction, 'Saved instruction');
    assert.equal(packaging.loadingInstruction, 'Saved instruction');
});
