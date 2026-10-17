import { formatDateTime } from '../../helpers/dateUtils.js';

export const updateAndApproveAllSections = async (calculation, updateSection) => {
    let next = calculation;
    // Freight refresh supplies the pallet counts used by packaging.
    for (const component of ['freight', 'packaging', 'other', 'storage']) {
        next = await updateSection(component, next);
    }
    return next;
};

export const getApprovalText = (approval, pending) => {
    if (pending) return 'Godkänns när kalkylen sparas';
    return approval?.approvedAt
        ? `Godkänd ${formatDateTime(approval.approvedAt)}${approval.approvedBy ? ` av ${approval.approvedBy}` : ''}`
        : 'Ännu ej godkänd';
};

export const calculatePackagingCost = (row, calculation, unitMultiplier) => {
    if (row.nrOfPallets == null || row.nrOfPallets === '' || !Number.isFinite(Number(row.nrOfPallets))
        || Number(row.nrOfPallets) < 0) {
        throw new Error('Det saknas giltigt antal pall på en eller flera rader.');
    }
    if (!(Number(row.edition) > 0) || !(unitMultiplier > 0)
        || !(Number(calculation.salesCurrencyRate) > 0) || !(Number(calculation.purchaseCurrencyRate) > 0)) {
        throw new Error('Upplaga, enhet och valutakurser måste vara giltiga innan packningen kan godkännas.');
    }
    const deficit = Number(calculation.palletPriceIn ?? 0) * Number(calculation.purchaseCurrencyRate)
        / Number(calculation.salesCurrencyRate) - Number(calculation.palletPriceOut ?? 0);
    if (!Number.isFinite(deficit)) throw new Error('Pallpriserna är ogiltiga.');
    if (deficit <= 0) return null;
    return deficit * Math.ceil(Number(row.nrOfPallets)) * unitMultiplier / Number(row.edition);
};

export const getFreightForApproval = (row, calculation) => {
    if (calculation.deactivateFreightCalculation) return null;
    if (row.nrOfPallets == null || !(Number(row.edition) > 0)
        || !Number.isFinite(Number(row.nrOfPallets)) || Number(row.nrOfPallets) < 0
        || row.freightPerUnit == null || !Number.isFinite(Number(row.freightPerUnit))) {
        throw new Error('Antal pall eller beräknat fraktpris saknas på en eller flera rader.');
    }
    if (row.calcPalletPriceUsed && !(Number(row.palletCalcFactor) > 0)) {
        throw new Error('Pallfaktor saknas på en eller flera rader.');
    }
    if ((row.calcPalletPriceUsed
        && !(Number(row.freightDomesticPerPallet ?? 0) + Number(row.freightInternationalPerPallet ?? 0) > 0))
        || (row.calcFtlPriceUsed && !(Number(row.freightPerVehicle) > 0))) {
        throw new Error('Beräknat transportpris saknas på en eller flera rader.');
    }
    return row.freightPerUnit;
};

export const applyApprovalRows = (calculation, flag, updateRow) => ({
    ...calculation,
    rows: calculation.rows.map((row) => row.archived || row.isDraft ? row : updateRow(row)),
    [flag]: true,
});
