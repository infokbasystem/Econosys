import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { CALCULATION_ROW_EDIT_GROUPS, buildCalculationRowEditValues, createCalculationRowEditDraft } from './calculationRowEditFields.js';

const fields = CALCULATION_ROW_EDIT_GROUPS.flatMap((group) => group.fields);
const values = Object.fromEntries(fields.map(({ name, type }) => [name, type === 'boolean' ? false : null]));

test('the modal covers every editable API business field exactly once, with matching types', () => {
    const dto = readFileSync(new URL('../../../Econosys.Api/DTOs/CalculationRowEditDto.cs', import.meta.url), 'utf8')
        .split('record CalculationRowEditValues')[1];
    const properties = [...dto.matchAll(/public (\w+\??) (\w+) \{ get; init; \}/g)];
    assert.equal(fields.length, 103);
    assert.equal(new Set(fields.map(({ name }) => name)).size, fields.length);
    assert.deepEqual(fields.map(({ name }) => name).sort(), properties.map(([, , name]) => name[0].toLowerCase() + name.slice(1)).sort());
    for (const [, type, property] of properties) {
        const field = fields.find(({ name }) => name === property[0].toLowerCase() + property.slice(1));
        if (type === 'bool') assert.equal(field.type, 'boolean');
        if (type === 'int?') assert.equal(field.type, 'integer');
        if (type === 'string?') assert.equal(field.type, 'text');
    }
});

test('nulls, booleans, signs and precision round trip without recalculation', () => {
    const input = { ...values, purchasePrice: 1e-7, freight: -12.123456789, archivedPriceListPriceFtl: 1234.12345, archived: true, info: 'Note' };
    assert.deepEqual(buildCalculationRowEditValues(createCalculationRowEditDraft(input)), input);
    const draft = createCalculationRowEditDraft(values);
    draft.purchasePrice = '-1 234,12345';
    draft.edition = '1280';
    draft.salesPrice = '0';
    const payload = buildCalculationRowEditValues(draft);
    assert.equal(payload.purchasePrice, -1234.12345);
    assert.equal(payload.edition, 1280);
    assert.equal(payload.salesPrice, 0);
    assert.equal(payload.percentAddition, null);
});

test('rejects invalid numeric input rather than truncating or silently using a default', () => {
    for (const invalid of ['12oops', 'Infinity', 'NaN', '--2', '1,2,3', ' ']) {
        assert.throws(() => buildCalculationRowEditValues({ ...createCalculationRowEditDraft(values), freight: invalid }), /giltigt tal/);
    }
    for (const invalid of ['1.5', '2147483648', '-2147483649']) {
        assert.throws(() => buildCalculationRowEditValues({ ...createCalculationRowEditDraft(values), edition: invalid }), /heltal/);
    }
});

test('rejects incomplete responses and keeps identity out of the payload', () => {
    assert.throws(() => createCalculationRowEditDraft({}), /fält/);
    const payload = buildCalculationRowEditValues({ ...createCalculationRowEditDraft(values), id: 999, calculationId: 999, companyId: 999 });
    assert.equal('id' in payload, false);
    assert.equal('calculationId' in payload, false);
    assert.equal('companyId' in payload, false);
});
