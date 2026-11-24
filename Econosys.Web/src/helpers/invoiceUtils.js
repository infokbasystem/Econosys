import { parseNullableNumber, roundTo2 } from './numberUtils.js';
import { getSwedishTodayDateString, toSwedishDateInputValue } from './dateUtils.js';

export const VAT_PRICE_TYPE_ID = 13;
export const ROUNDING_PRICE_TYPE_ID = 12;
export const CREDIT_INVOICE_TYPE_ID = 5;

export const hasCreditStockDeliveries = (invoice) => (
    Number(invoice?.invoiceTypeCode) === CREDIT_INVOICE_TYPE_ID
    && (invoice?.invoiceRows ?? []).some((row) => Number(row.priceTypeId) === 1 && Number(row.deliveryFromStockId) > 0)
);

export const createCreditInvoiceDraft = (invoice) => ({
    ...invoice,
    id: 0,
    invoiceNumber: null,
    invoiceTypeCode: CREDIT_INVOICE_TYPE_ID,
    creditingInvoiceNumber: invoice.invoiceNumber,
    creditedInvoiceId: invoice.id,
    invoiceDate: `${getSwedishTodayDateString()}T00:00:00`,
    accountDate: null,
    journalNr: null,
    printed: false,
    isSettled: false,
    invoiceOk: false,
    endInvoiced: false,
    created: null,
    createdBy: null,
    createdByUserName: null,
    edited: null,
    editedBy: null,
    editedByUserName: null,
    oldDbId: null,
    invoiceRows: (invoice.invoiceRows ?? []).map((row, index) => ({
        ...row,
        id: 0,
        invoiceId: null,
        oldDbId: null,
        tempId: `credit_row_${index}`,
        unitPrice: row.unitPrice == null ? null : -row.unitPrice,
        sum: row.sum == null ? null : -row.sum,
        weight: row.weight == null ? null : -row.weight,
    })),
    invoiceAccountRows: (invoice.invoiceAccountRows ?? []).map((row, index) => ({
        ...row,
        id: 0,
        invoiceId: null,
        oldDbId: null,
        tempKey: `credit_account_${index}`,
        nrOf: row.nrOf == null ? null : -row.nrOf,
        sum: row.sum == null ? null : -row.sum,
    })),
});

export const getInvoiceRowIdentity = (row) => ((Number(row?.id) || 0) > 0 ? row.id : row.tempId);

export const reorderInvoiceRows = (rows, sourceId, targetId) => {
    const isDerived = (row) => [VAT_PRICE_TYPE_ID, ROUNDING_PRICE_TYPE_ID].includes(Number(row.priceTypeId));
    const editableRows = rows.filter((row) => !isDerived(row));
    const sourceIndex = editableRows.findIndex((row) => getInvoiceRowIdentity(row) === sourceId);
    const targetIndex = editableRows.findIndex((row) => getInvoiceRowIdentity(row) === targetId);
    if (sourceIndex < 0 || targetIndex < 0 || sourceIndex === targetIndex) return rows;

    const [moved] = editableRows.splice(sourceIndex, 1);
    editableRows.splice(targetIndex, 0, moved);
    return [...editableRows, ...rows.filter(isDerived)].map((row, index) => ({
        ...row,
        sortOrder: index + 1,
        invoiceRowNumber: index + 1,
    }));
};

export const getInvoiceDraftMode = (types) => {
    const normalized = types.map((type) => String(type).toLowerCase());
    const palletTypes = normalized.filter((type) => type === 'palletsnow' || type === 'palletsinfuture');
    if (palletTypes.length === 0) return { invoiceMode: 'selection' };
    if (palletTypes.length !== normalized.length || new Set(palletTypes).size !== 1) {
        return { error: 'Välj endast pallrader från samma period, utan leveranser eller orderkostnader.' };
    }
    return { invoiceMode: palletTypes[0] === 'palletsnow' ? 'palletsNow' : 'palletsFuture' };
};

export const isInvoiceCustomerLocked = (invoice) => (
    Number(invoice?.deliveryToCustomerId) > 0
    || Number(invoice?.deliveryFromStockId) > 0
    || (invoice?.invoiceRows ?? []).some((row) => (
        Number(row.deliveryToCustomerId) > 0
        || Number(row.deliveryFromStockId) > 0
        || Number(row.orderCostId) > 0
    ))
);

export const getInvoiceCustomerFields = (customer) => ({
    customerId: customer?.id ?? null,
    customerName: customer?.name ?? '',
    address: customer?.address ?? '',
    address2: customer?.address2 ?? '',
    postalNr: customer?.postalNr ?? '',
    postalAddress: customer?.postalAddress ?? '',
    country: customer?.country ?? '',
    vatNr: customer?.vatNr ?? '',
    vatRate: customer?.vat === false ? 0 : (customer?.vatRate || 25) / 100,
    yourReference: customer?.reference ?? '',
    invoiceDays: customer?.paymentDays ?? 30,
    termsOfPayment: customer?.termsOfPayment ?? '',
    salesCurrencyId: customer?.currencyId ?? null,
    languageId: customer?.languageId ?? null,
    account: customer?.accountNrAccountsReceivable?.toString() ?? '',
});

export const createNewInvoiceModel = () => ({
    id: 0,
    invoiceNumber: null,
    customerName: '',
    address: '',
    postalNr: '',
    postalAddress: '',
    country: '',
    vatNr: '',
    vatRate: 0.25,
    yourReference: '',
    ourReference: '',
    invoiceDate: `${getSwedishTodayDateString()}T00:00:00`,
    invoiceDays: 30,
    termsOfPayment: '30 dagar fran fakturadatum.',
    termsOfDelivery: '',
    salesCurrencyId: null,
    accountDate: null,
    timeOfDelivery: '',
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

const roundEvenTo2 = (value) => {
    const scaled = Math.abs(value) * 100;
    const integer = Math.floor(scaled);
    const fraction = scaled - integer;
    const isHalf = Math.abs(fraction - 0.5) <= Number.EPSILON * Math.max(1, scaled) * 4;
    const rounded = isHalf ? integer + (integer % 2) : Math.round(scaled);
    return rounded === 0 ? 0 : Math.sign(value) * rounded / 100;
};

export const computeInvoiceRowSum = (row, units) => {
    const quantity = parseNullableNumber(row.nrOf) ?? 0;
    const price = parseNullableNumber(row.unitPrice) ?? 0;
    const type = Number(row.priceTypeId);
    if (type === 2) {
        return roundEvenTo2((quantity === -1 ? 1 : quantity) * price);
    }
    if (type === 107 || type === 108) {
        return roundEvenTo2(quantity * price);
    }
    const unit = units.find((item) => item.id === Number(row.unitId));
    const multiplier = parseNullableNumber(unit?.multiplicator) ?? 1;
    return roundTo2(quantity * price / (multiplier > 0 ? multiplier : 1));
};

export const getInvoiceInventorySource = (invoice) => JSON.stringify(
    (invoice?.invoiceRows ?? []).filter((row) => Number(row.priceTypeId) === 1 && Number(row.deliveryFromStockId) > 0)
        .map((row) => ({
            priceTypeId: 1,
            deliveryFromStockId: Number(row.deliveryFromStockId),
            nrOf: parseNullableNumber(row.nrOf),
        }))
        .sort((a, b) => a.deliveryFromStockId - b.deliveryFromStockId || (a.nrOf ?? 0) - (b.nrOf ?? 0)),
);

export const applyInvoiceDerivedRows = (rows, vatRate = 0.25) => {
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

    const vatValue = roundEvenTo2(vatBase * vatRate);
    const totalBeforeRounding = sumExVat + vatValue;
    const roundingTolerance = Number.EPSILON * Math.max(1, Math.abs(totalBeforeRounding)) * 2;
    const roundedTotal = Math.sign(totalBeforeRounding) * Math.round(Math.abs(totalBeforeRounding) + roundingTolerance);
    const roundingValue = roundEvenTo2(roundedTotal - totalBeforeRounding);

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
            text: 'Öresutjämning',
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
