import assert from 'node:assert/strict';
import test from 'node:test';
import { sortTransportOrderOverviewRows } from './transportOrderOverviewSorting.js';

const sortValues = (values, direction = 'asc') =>
    sortTransportOrderOverviewRows(values.map((value) => ({ value })), { key: 'value', direction })
        .map((row) => row.value);

test('preserves server ordering until a header is selected and never mutates rows', () => {
    const rows = [{ value: 10 }, { value: 2 }];
    assert.equal(sortTransportOrderOverviewRows(rows, { key: null, direction: 'asc' }), rows);
    assert.deepEqual(sortTransportOrderOverviewRows(rows, { key: 'value', direction: 'asc' }), [rows[1], rows[0]]);
    assert.deepEqual(rows.map((row) => row.value), [10, 2]);
});

test('sorts quantities and fractional pallets numerically in both directions', () => {
    assert.deepEqual(sortValues([10, 2.5, 0, 2.05]), [0, 2.05, 2.5, 10]);
    assert.deepEqual(sortValues([10, 2.5, 0, 2.05], 'desc'), [10, 2.5, 2.05, 0]);
});

test('sorts order numbers naturally and names using Swedish alphabetical order', () => {
    assert.deepEqual(sortValues(['100', '20', '3']), ['3', '20', '100']);
    assert.deepEqual(sortValues(['\u00d6rebro', 'Zebra', '\u00c4ngelholm', 'Alfa', '\u00c5bo']),
        ['Alfa', 'Zebra', '\u00c5bo', '\u00c4ngelholm', '\u00d6rebro']);
});

test('sorts ISO delivery dates chronologically and week labels naturally', () => {
    assert.deepEqual(sortValues(['2026-11-02', '2025-12-31', '2026-02-10']),
        ['2025-12-31', '2026-02-10', '2026-11-02']);
    assert.deepEqual(sortValues(['v. 12', 'v. 3', 'v. 9']), ['v. 3', 'v. 9', 'v. 12']);
});

for (const key of ['deliveryTime', 'confirmedDeliveryTime']) {
    test(`${key} interleaves week-mode Mondays with exact dates in both directions`, () => {
        const rows = [
            { id: 'later', [key]: '2026-11-04', [`${key}SortDate`]: '2026-11-04' },
            { id: 'week', [key]: 'v. 45', [`${key}SortDate`]: '2026-11-02' },
            { id: 'earlier', [key]: '2026-11-01', [`${key}SortDate`]: '2026-11-01' },
            { id: 'missing', [key]: '', [`${key}SortDate`]: null },
            { id: 'monday', [key]: '2026-11-02', [`${key}SortDate`]: '2026-11-02' },
        ];
        assert.deepEqual(sortTransportOrderOverviewRows(rows, { key, direction: 'asc' }).map((row) => row.id),
            ['earlier', 'week', 'monday', 'later', 'missing']);
        assert.deepEqual(sortTransportOrderOverviewRows(rows, { key, direction: 'desc' }).map((row) => row.id),
            ['later', 'week', 'monday', 'earlier', 'missing']);
    });

    test(`${key} uses the stored year, including weeks spanning New Year`, () => {
        const rows = [
            { id: 'next-year', [key]: 'v. 1', [`${key}SortDate`]: '2027-01-04' },
            { id: 'last-day', [key]: '2025-12-31', [`${key}SortDate`]: '2025-12-31' },
            { id: 'new-year-week', [key]: 'v. 1', [`${key}SortDate`]: '2025-12-29' },
        ];
        assert.deepEqual(sortTransportOrderOverviewRows(rows, { key, direction: 'asc' }).map((row) => row.id),
            ['new-year-week', 'last-day', 'next-year']);
    });
}

test('keeps missing values last in both directions without treating zero as missing', () => {
    assert.deepEqual(sortValues([null, 10, '', 0, undefined, 2]), [0, 2, 10, null, '', undefined]);
    assert.deepEqual(sortValues([null, 10, '', 0, undefined, 2], 'desc'), [10, 2, 0, null, '', undefined]);
});

test('keeps equal rows stable and sorts only the supplied filtered rows', () => {
    const first = { id: 1, value: 'Alfa' };
    const second = { id: 2, value: 'alfa' };
    const excluded = { id: 3, value: 'Beta' };
    const filteredRows = [first, excluded, second].filter((row) => row.id !== 3);
    assert.deepEqual(sortTransportOrderOverviewRows(filteredRows, { key: 'value', direction: 'desc' }), [first, second]);
});
