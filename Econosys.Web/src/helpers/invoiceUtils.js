import { parseNullableNumber, roundTo2 } from './numberUtils';
import { getSwedishTodayDateString, toSwedishDateInputValue } from './dateUtils';

export const VAT_PRICE_TYPE_ID = 13;
export const ROUNDING_PRICE_TYPE_ID = 12;

export const createNewInvoiceModel = () => ({
    id: 0,
    invoiceNumber: null,
    customerName: '',
    address: '',
    postalNr: '',
    postalAddress: '',
    country: '',
    vatNr: '',
    yourReference: '',
    ourReference: '',
    invoiceDate: `${getSwedishTodayDateString()}T00:00:00`,
    invoiceDays: 30,
    termsOfPayment: '30 dagar fran fakturadatum.',
    salesCurrencyId: null,
    accountDate: null,
    journalNr: null,
    account: '',
    costCenter: '',
    invoiceOk: false,
    invoiceAccountRows: [],
    invoiceRows: [
        {
            tempId: `temp_${Date.now()}`,
            text: '',
            weight: null,
            nrOf: null,
            unitPrice: null,
            sum: null,
            accountNr: '',
            costCenter: '',
            vatGround: true,
            unitId: null,
            calculate: true,
        },
    ],
});

export const toDateInputValue = (value) => {
    return toSwedishDateInputValue(value);
};

export const autoResizeTextarea = (element) => {
    if (!element) return;
    element.style.height = 'auto';
    element.style.height = `${element.scrollHeight}px`;
};

export const applyInvoiceDerivedRows = (rows) => {
    const nextRows = Array.isArray(rows) ? rows.map((row) => ({ ...row })) : [];

    const vatRowIndex = nextRows.findIndex((row) => Number(row?.priceTypeId) === VAT_PRICE_TYPE_ID);
    const roundingRowIndex = nextRows.findIndex((row) => Number(row?.priceTypeId) === ROUNDING_PRICE_TYPE_ID);

    let sumExVat = 0;
    let vatBase = 0;

    nextRows.forEach((row, index) => {
        if (index === vatRowIndex || index === roundingRowIndex) {
            return;
        }

        const rowSum = parseNullableNumber(row?.sum) ?? 0;
        sumExVat += rowSum;

        if (row?.vatGround) {
            vatBase += rowSum;
        }
    });

    const vatValue = roundTo2(vatBase * 0.25);
    const totalBeforeRounding = sumExVat + vatValue;
    const roundedTotal = Math.round(totalBeforeRounding);
    const roundingValue = roundTo2(roundedTotal - totalBeforeRounding);

    const maxSortOrder = nextRows.reduce((max, row) => Math.max(max, row?.sortOrder ?? 0), 0);

    if (vatRowIndex >= 0) {
        nextRows[vatRowIndex] = {
            ...nextRows[vatRowIndex],
            sum: vatValue,
        };
    } else {
        nextRows.push({
            tempId: `temp_vat_${Date.now()}`,
            priceTypeId: VAT_PRICE_TYPE_ID,
            text: 'Moms',
            sum: vatValue,
            accountNr: '',
            costCenter: '',
            vatGround: false,
            calculate: false,
            sortOrder: maxSortOrder + 1,
        });
    }

    if (roundingRowIndex >= 0) {
        nextRows[roundingRowIndex] = {
            ...nextRows[roundingRowIndex],
            sum: roundingValue,
        };
    } else {
        nextRows.push({
            tempId: `temp_rounding_${Date.now()}`,
            priceTypeId: ROUNDING_PRICE_TYPE_ID,
            text: 'Öresavrundning',
            sum: roundingValue,
            accountNr: '',
            costCenter: '',
            vatGround: false,
            calculate: false,
            sortOrder: maxSortOrder + 2,
        });
    }

    return nextRows;
};
