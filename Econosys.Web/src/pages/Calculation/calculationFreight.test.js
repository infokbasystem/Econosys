import assert from 'node:assert/strict';
import test from 'node:test';
import { changeFreightMode, getAutomaticFreightGeometry, getFreightEditState, getFreightMode, getFreightWarningMessage, parseFreightFactors } from './calculationFreight.js';

test('transport-responsible suppliers warn for either or both disabled automatic freight settings', () => {
    const supplier = { econopackTransportResponsible: true };
    for (const autoFreightCalc of [true, false]) {
        for (const autoFreightPrice of [true, false]) {
            const calculation = { autoFreightCalc, autoFreightPrice };
            const message = getFreightWarningMessage(calculation, supplier);
            assert.equal(Boolean(message), !autoFreightCalc || !autoFreightPrice);
            assert.equal(message.includes('Automatisk fraktberäkning'), !autoFreightCalc);
            assert.equal(message.includes('Automatiskt fraktpris'), !autoFreightPrice);
            assert.equal(getFreightWarningMessage({ ...calculation, deactivateFreightCalculation: true }, supplier), message);
        }
    }
});

test('freight warnings require explicit supplier responsibility and disabled settings', () => {
    const calculation = { autoFreightCalc: false, autoFreightPrice: false };
    for (const supplier of [null, {}, { econopackTransportResponsible: false }]) {
        assert.equal(getFreightWarningMessage(calculation, supplier), '');
    }
    const supplier = { econopackTransportResponsible: true };
    assert.equal(getFreightWarningMessage(null, supplier), '');
    assert.equal(getFreightWarningMessage({}, supplier), '');
});

const createCalculation = () => ({
    autoFreightCalc: true,
    autoFreightPrice: true,
    deactivateFreightCalculation: false,
    palletsPerTruck: 66,
    editionPerPallet: 100,
    palletIsStackable: true,
    supplierFactoryId: 4,
    customerDeliveryAddressId: 8,
    deliveryAddressCountryCode: 'SE',
    deliveryAddressPostalNr: 12345,
    freightPerPallet: 100,
    freightPerVehicle: 5000,
    loadingPerDelivery: 200,
    rows: [
        { edition: 1000, nrOfPallets: 10, freight: 300, freightTotal: 3000, freightPerUnit: 300,
            freightDomesticPerPallet: 100, freightInternationalPerPallet: 200, freightPerVehicle: 5000,
            freightUnloading: 200, preCalcAdditionPercent: 10 },
        { archived: true, freight: 150, preCalcAdditionPercent: 5 },
        { isDraft: true, edition: null, freight: null },
    ],
});

test('opening a saved calculation does not apply freight defaults or recalculate saved rows', () => {
    const saved = {
        ...createCalculation(),
        supplier: { id: 4, name: 'Supplier' },
        customer: { id: 8, name: 'Customer' },
        supplierFactoryId: null,
        customerDeliveryAddressId: null,
        palletLength: 1200,
        palletWidth: 800,
        unitId: 1,
    };
    assert.deepEqual(getFreightEditState(structuredClone(saved), saved), {
        selectDefaultFactory: false,
        selectDefaultAddress: false,
        recalculateGeometry: false,
    });
    assert.deepEqual(getFreightEditState({ ...saved, freightNote: 'Edited note' }, saved), {
        selectDefaultFactory: false,
        selectDefaultAddress: false,
        recalculateGeometry: false,
    });
});

test('freight defaults are applied only after enabling automatic prices or changing the related party', () => {
    const saved = { ...createCalculation(), supplier: { id: 4 }, customer: { id: 8 } };
    assert.deepEqual(getFreightEditState({ ...saved, supplier: { id: 5 } }, saved), {
        selectDefaultFactory: true,
        selectDefaultAddress: false,
        recalculateGeometry: false,
    });
    assert.deepEqual(getFreightEditState({ ...saved, customer: { id: 9 } }, saved), {
        selectDefaultFactory: false,
        selectDefaultAddress: true,
        recalculateGeometry: false,
    });
    assert.deepEqual(getFreightEditState(saved, { ...saved, autoFreightPrice: false }), {
        selectDefaultFactory: true,
        selectDefaultAddress: true,
        recalculateGeometry: false,
    });
});

test('relevant freight edits still recalculate geometry and saving resets that state', () => {
    const saved = { ...createCalculation(), palletLength: 1200, palletWidth: 800, unitId: 1 };
    const edits = {
        autoFreightCalc: false,
        deactivateFreightCalculation: true,
        palletLength: 1400,
        palletWidth: 1000,
        palletIsStackable: false,
        unitId: 2,
    };
    for (const [field, value] of Object.entries(edits)) {
        const edited = { ...saved, [field]: value };
        assert.equal(getFreightEditState(edited, saved).recalculateGeometry, true, field);
        assert.equal(getFreightEditState(edited, structuredClone(edited)).recalculateGeometry, false, field);
    }
});

test('deactivation switches off both automatic modes, clears freight and marks the applied result for approval', () => {
    const original = createCalculation();
    const result = changeFreightMode(original, 'deactivateFreightCalculation', true);
    assert.deepEqual(getFreightMode(result), { active: false, automaticCalculation: false, automaticPrices: false });
    for (const field of ['freightPerPallet', 'freightPerVehicle', 'loadingPerDelivery',
        'supplierFactoryId', 'customerDeliveryAddressId']) assert.equal(result[field], null);
    assert.equal(result.deliveryAddressCountryCode, '');
    assert.equal(result.deliveryAddressPostalNr, null);
    assert.equal(result.rows[0].freight, null);
    for (const field of ['freightTotal', 'freightPerUnit', 'freightDomesticPerPallet', 'freightInternationalPerPallet',
        'freightPerVehicle', 'freightUnloading', 'preCalcAdditionPercent']) assert.equal(result.rows[0][field], 0);
    assert.equal(result.rows[0].nrOfPallets, 10);
    assert.equal(result.palletsPerTruck, 0);
    assert.equal(result.approveFreight, true);
    assert.equal(result.rows[1], original.rows[1]);
    assert.equal(result.rows[2], original.rows[2]);
    assert.equal(original.autoFreightPrice, true);
    assert.equal(original.rows[0].freight, 300);
});

test('reactivation leaves cleared charges and automatic modes off until explicitly enabled', () => {
    const cleared = changeFreightMode(createCalculation(), 'deactivateFreightCalculation', true);
    assert.equal(changeFreightMode(cleared, 'autoFreightCalc', true), cleared);
    assert.equal(changeFreightMode(cleared, 'autoFreightPrice', true), cleared);
    const active = changeFreightMode(cleared, 'deactivateFreightCalculation', false);
    assert.deepEqual(getFreightMode(active), { active: true, automaticCalculation: false, automaticPrices: false });
    assert.equal(active.rows[0].freightPerUnit, 0);
});

test('automatic calculation and automatic pricing remain independent in all combinations', () => {
    for (const automaticCalculation of [false, true]) {
        for (const automaticPrices of [false, true]) {
            const result = { ...createCalculation(), autoFreightCalc: automaticCalculation, autoFreightPrice: automaticPrices };
            assert.deepEqual(getFreightMode(result), { active: true, automaticCalculation, automaticPrices });
            const stopped = { ...result, deactivateFreightCalculation: true };
            assert.deepEqual(getFreightMode(stopped), { active: false, automaticCalculation: false, automaticPrices: false });
        }
    }
});

test('disabling automatic calculation clears capacity without changing prices or enabling sales freight changes', () => {
    const original = createCalculation();
    const result = changeFreightMode(original, 'autoFreightCalc', false);
    assert.equal(result.palletsPerTruck, 0);
    assert.equal(result.autoFreightPrice, true);
    assert.equal(result.rows[0].freight, 300);
});

test('manual pricing clears the route and additions, preserving editable prices and archived rows', () => {
    const original = createCalculation();
    const result = changeFreightMode(original, 'autoFreightPrice', false);
    assert.equal(result.autoFreightCalc, true);
    assert.equal(result.supplierFactoryId, null);
    assert.equal(result.customerDeliveryAddressId, null);
    assert.equal(result.rows[0].preCalcAdditionPercent, 0);
    assert.equal(result.rows[0].freightInternationalPerPallet, 200);
    assert.equal(result.rows[0].freight, 300);
    assert.equal(result.rows[1], original.rows[1]);
    assert.equal(result.rows[2], original.rows[2]);
    const automatic = changeFreightMode(result, 'autoFreightPrice', true);
    assert.equal(automatic.freightPerPallet, null);
    assert.equal(automatic.freightPerVehicle, null);
    assert.equal(automatic.loadingPerDelivery, 200);
});

const table = (overrides = {}) => ({
    lengthFrom: 1199, lengthTo: 1200, widthFrom: 799, widthTo: 800,
    palletFactors: [...Array(34).fill('1,25'), '33'].join(';'),
    ...overrides,
});

test('factor lookup uses legacy exclusive lower and inclusive upper boundaries', () => {
    const values = parseFreightFactors([
        table(),
        table({ lengthFrom: 1200, lengthTo: 1201 }),
        table({ widthFrom: 800, widthTo: 801 }),
    ], 1200, 800);
    assert.equal(values[0], 1.25);
    assert.equal(values[34], 33);
    assert.throws(() => parseFreightFactors([], 1200, 800));
    assert.throws(() => parseFreightFactors([table(), table()], 1200, 800));
    assert.throws(() => parseFreightFactors([table({ palletFactors: '1;NaN;33' })], 1200, 800));
});

test('empty factor slots retain their index rather than shifting truck capacity', () => {
    const parts = [...Array(34).fill('1'), '33'];
    parts[3] = '';
    const factors = parseFreightFactors([table({ palletFactors: parts.join(';') })], 1200, 800);
    assert.equal(factors[3], null);
    assert.equal(factors[34], 33);
    assert.throws(() => parseFreightFactors([table({ palletFactors: [...Array(34).fill('1'), ''].join(';') })], 1200, 800));
});

test('automatic geometry recalculates quantities, stacked capacity, full trucks, remainder and factors', () => {
    const factors = parseFreightFactors([table()], 1200, 800);
    const calculation = createCalculation();
    assert.deepEqual(getAutomaticFreightGeometry({ edition: 6700 }, calculation, factors), {
        palletsPerTruck: 66, nrOfPallets: 67, nrOfVehicles: 1, palletCalcFactor: 1.25,
    });
    assert.deepEqual(getAutomaticFreightGeometry({ edition: 6600 }, calculation, factors), {
        palletsPerTruck: 66, nrOfPallets: 66, nrOfVehicles: 1, palletCalcFactor: 1,
    });
    const unstacked = getAutomaticFreightGeometry({ edition: 6700 }, { ...calculation, palletIsStackable: false }, factors);
    assert.equal(unstacked.palletsPerTruck, 33);
    assert.equal(unstacked.nrOfVehicles, 2);
    const missingQuantity = getAutomaticFreightGeometry({ edition: 1000 }, { ...calculation, editionPerPallet: null }, factors);
    assert.equal(missingQuantity.nrOfPallets, null);
});
