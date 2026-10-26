import { useCallback, useEffect, useId, useRef, useState } from 'react';
import { Link, useBlocker, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { ArrowLeft, ChevronDown, ExternalLink, FilePlus2, Info as InfoIcon, Minus, PanelLeftClose, PanelLeftOpen, Pencil, Plus, RefreshCw, Save, Send, ShoppingCart, Trash2 } from 'lucide-react';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import ActionButton from '../../components/ActionButton';
import ConfirmationModal from '../../components/ConfirmationModal';
import CalculationRowEditModal from '../../components/CalculationRowEditModal';
import LabeledCheckbox from '../../components/LabeledCheckbox';
import LabeledSelect from '../../components/LabeledSelect';
import LabeledInput from '../../components/LabeledInput';
import LabeledReactSelect from '../../components/LabeledReactSelect';
import LabeledTextArea from '../../components/LabeledTextArea';
import NumberInput from '../../components/NumberInput';
import SelectCircleCheckbox from '../../components/SelectCircleCheckbox';
import SearchCustomer from '../../components/SearchCustomer';
import SearchSupplier from '../../components/SearchSupplier';
import SegmentedFilter from '../../components/SegmentedFilter';
import apiClient from '../../config/apiClient';
import { formatDateTime } from '../../helpers/dateUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';
import { calculatePackagesPerPallet, updateAutomaticLoadingInstruction } from './calculationPackaging';
import { applyApprovalRows, calculatePackagingCost, getApprovalText, getFreightForApproval, updateAndApproveAllSections } from './calculationApprovals';
import { changeFreightMode, getAutomaticFreightGeometry, getFreightEditState, getFreightMode, getFreightWarningMessage, parseFreightFactors } from './calculationFreight';
import CalculationNavigation from './CalculationNavigation';
import { handleCalculationTableKeyDown } from './calculationTableNavigation';

const EMPTY_ROWS = [];
const INFO_PANEL_WIDE_QUERY = '(min-width: 96rem)';
const NUMBER_FORMAT = new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 5 });
const VIEW_OPTIONS = [
    { value: 'active', label: 'Aktuella' },
    { value: 'archived', label: 'Arkiverade' },
    { value: 'all', label: 'Alla' },
];
const APPROVAL_LABELS = { packaging: 'Packning', freight: 'Frakt', other: 'Övrigt', storage: 'Lagring' };
const EMPTY_CALCULATION_ROW_KEY = 'draft-row';
const SALES_COLUMNS = [
    { field: 'edition', label: 'Upplaga', precision: 0, width: '50px' },
    { field: 'nrOfPallets', label: 'Ant pall', width: '80px' },
    { field: 'purchasePrice', label: 'Ink.pris', width: '50px' },
    { field: 'convertedPurchasePrice', label: 'Omräknat', width: '50px' },
    { field: 'freight', label: 'Frakt', width: '50px' },
    { field: 'other', label: 'Övrigt', width: '50px' },
    { field: 'storage', label: 'Lagring', width: '50px' },
    { field: 'packagingCost', label: 'Pallkost', width: '50px' },
    { field: 'totalCost', label: 'Totalt', width: '50px' },
    { field: 'percentAddition', label: 'Påslag %', width: '50px' },
    { field: 'salesPrice', label: 'Fsg.pris', width: '70px' },
    { field: 'contribution', label: 'TB SEK', width: '50px' },
    { field: 'totalContribution', label: 'TB tot SEK', width: '60px' },
];
const FREIGHT_COLUMNS = [
    { field: 'edition', label: 'Upplaga', width: '66px' },
    { field: 'nrOfPallets', label: 'Ant pall', width: '70px' },
    { field: 'palletPlaces', label: 'Pall.pl', width: '56px' },
    { field: 'nrOfVehicles', label: 'Ant bilar', width: '62px' },
    { field: 'customerDeliveryAddressId', label: 'Lev.adress', width: '220px' },
    { field: 'freightDomesticPerPallet', label: 'Frakt/pp inr', width: '84px' },
    { field: 'freightInternationalPerPallet', label: 'Frakt/pp utr', width: '84px' },
    { field: 'palletCalcFactor', label: 'Pallfakt', width: '66px' },
    { field: 'freightUnloading', label: 'Lossn.kost', width: '76px' },
    { field: 'totalFreightPerPlace', label: 'Tot frakt/pp', width: '84px' },
    { field: 'freightPerVehicle', label: 'Frakt/bil', width: '84px' },
    { field: 'preCalcAdditionPercent', label: 'Påslag', width: '66px' },
    { field: 'freightTotal', label: 'Totalt SEK', width: '88px' },
    { field: 'freightPerUnit', label: 'Tot /enhet', width: '80px' },
];
const FREIGHT_LOOKUP_FIELDS = new Set(['nrOfPallets', 'nrOfVehicles', 'customerDeliveryAddressId']);
const FREIGHT_RECALC_FIELDS = new Set(['editionPerPallet', 'autoFreightCalc', 'palletIsStackable', 'palletLength', 'palletWidth']);
const COST_COLUMNS = [
    { field: 'edition', label: 'Upplaga', width: '80px', align: 'right' },
    { field: 'costId', label: 'Kostnad', width: '150px', align: 'left' },
    { field: 'nrOf', label: 'Ant', width: '56px', align: 'center' },
    { field: 'inPrice', label: 'Inpris', width: '150px', align: 'left' },
    { field: 'doDebit', label: 'Deb', width: '44px', align: 'center' },
    { field: 'outPrice', label: 'Utpris', width: '130px', align: 'right' },
    { field: 'price', label: 'Kost SEK', width: '90px', align: 'right' },
    { field: 'pricePerUnit', label: 'SEK/enh', width: '90px', align: 'right' },
    { field: 'totalPricePerUnit', label: 'Tot SEK/enh', width: '90px', align: 'right' },
    { field: 'costTypeText', label: 'Kommentar', width: '190px', align: 'left' },
];
const STORAGE_COLUMNS = [
    { field: 'edition', label: 'Upplaga', precision: 0, width: '80px' },
    { field: 'storageNrOfPallets', label: 'Ant pall', width: '80px' },
    { field: 'storageMonths', label: 'Ant. månader', width: '105px' },
    { field: 'storageUnloading', label: 'Lossning', width: '95px' },
    { field: 'storageAmount', label: 'Lagring', width: '95px' },
    { field: 'storageInterest', label: 'Ränta', width: '95px' },
    { field: 'storageFreight', label: 'Frakt till kund', width: '115px' },
    { field: 'storageTotal', label: 'Totalt SEK', width: '95px' },
    { field: 'storageTotalPerUnit', label: 'Tot/enhet', width: '95px' },
];
const INFO_COLUMN_WIDTH = '16%';

const createCostItem = (clientKey, cost = {}) => ({
    clientKey,
    id: cost.id ?? 0,
    costId: cost.costId ?? null,
    costTypeText: cost.costTypeText ?? '',
    nrOf: cost.nrOf ?? null,
    inPrice: cost.inPrice ?? null,
    inPriceCurrencyId: cost.inPriceCurrencyId ?? null,
    outPrice: cost.outPrice ?? null,
    doDebit: Boolean(cost.doDebit),
});

const createRowCosts = (row, rowKey) => {
    const costs = (row.costs ?? []).map((cost) => createCostItem(`cost-${cost.id}`, cost));
    return row.archived || costs.length > 0 ? costs : [createCostItem(`cost-new-${rowKey}`)];
};

const createCalculationDraft = (data) => ({
    isArchived: data.isArchived ?? false,
    customer: data.customer ? { id: data.customer.id, name: data.customer.name ?? '' } : null,
    supplier: data.supplier ? { id: data.supplier.id, name: data.supplier.name ?? '' } : null,
    deletedRowIds: [],
    approveOtherCosts: false,
    approveStorage: false,
    approvePackaging: false,
    approveFreight: false,
    rows: [
        ...(data.rows ?? []).map((row) => {
            const clientKey = `row-${row.id}`;
            return {
                ...row,
                clientKey,
                isDraft: false,
                costs: createRowCosts(row, clientKey),
            };
        }),
        createEmptyCalculationRow(EMPTY_CALCULATION_ROW_KEY, getFreightRowDefaults(data)),
    ],
    unitId: data.unitId,
    purchaseCurrencyId: data.purchaseCurrencyId,
    salesCurrencyId: data.salesCurrencyId,
    purchaseCurrencyRate: data.purchaseCurrencyRate,
    salesCurrencyRate: data.salesCurrencyRate,
    currencyRate: data.currencyRate,
    purchaseDecimals: data.purchaseDecimals,
    salesDecimals: data.salesDecimals,
    lockOutPrice: data.lockOutPrice,
    calculationFollowUp: data.calculationFollowUp ?? '',
    otherCostNote: data.otherCostNote ?? '',
    freightNote: data.freightNote ?? '',
    loadingInstruction: data.loadingInstruction ?? '',
    automaticLoadingInstruction: data.automaticLoadingInstruction,
    autoFreightCalc: data.autoFreightCalc,
    autoFreightPrice: data.autoFreightPrice,
    deactivateFreightCalculation: data.deactivateFreightCalculation,
    supplierFactoryId: data.supplierFactoryId,
    customerDeliveryAddressId: data.customerDeliveryAddressId,
    freightPerPallet: data.freightPerPallet,
    freightPerVehicle: data.freightPerVehicle,
    loadingPerDelivery: data.loadingPerDelivery,
    deliveryAddressCountryCode: data.deliveryAddressCountryCode ?? '',
    deliveryAddressPostalNr: data.deliveryAddressPostalNr,
    palletFormatId: data.palletFormatId,
    palletLength: data.palletLength,
    palletWidth: data.palletWidth,
    palletHeight: data.palletHeight,
    editionPerPallet: data.editionPerPallet,
    palletIsStackable: data.palletIsStackable,
    palletsPerTruck: data.palletsPerTruck,
    palletCalcFactor: data.palletCalcFactor,
    palletPriceIn: data.palletPriceIn,
    palletPriceOut: data.palletPriceOut,
    doDebitPalletPrice: data.doDebitPalletPrice,
    doBuntas: data.doBuntas,
    nrOfPerBundle: data.nrOfPerBundle,
    isYtterforpackning: data.isYtterforpackning,
    nrOfPerOuterPackaging: data.nrOfPerOuterPackaging,
    calcStorage: data.calcStorage,
    loadingPerPallet: data.loadingPerPallet,
    storagePerM2: data.storagePerM2,
    interest: data.interest,
    freightFromStockPerPallet: data.freightFromStockPerPallet,
});

const getFreightRowDefaults = (calculation) => ({
    freightInternationalPerPallet: calculation.freightPerPallet ?? null,
    freightPerVehicle: calculation.freightPerVehicle ?? null,
    freightUnloading: calculation.loadingPerDelivery ?? null,
});

const createEmptyCalculationRow = (clientKey, freightDefaults = {}) => ({
    id: 0,
    clientKey,
    isDraft: true,
    archived: false,
    edition: null,
    purchasePrice: null,
    freight: null,
    other: null,
    storage: null,
    packagingCost: null,
    percentAddition: null,
    salesPrice: null,
    convertedPurchasePrice: null,
    totalCost: null,
    contribution: null,
    totalContribution: null,
    info: '',
    costs: [],
    documents: [],
    ...freightDefaults,
});

const hasCalculationRowInput = (row) => [
    row.edition,
    row.purchasePrice,
    row.freight,
    row.other,
    row.storage,
    row.packagingCost,
    row.percentAddition,
    row.salesPrice,
    row.info,
].some((value) => value != null && value !== '');

const toCalculationUpdateRequest = (draft) => {
    const { customer, supplier, rows, deletedRowIds, ...fields } = draft;
    return {
        ...fields,
        customerId: customer?.id ?? null,
        supplierId: supplier?.id ?? null,
        rows: (rows ?? [])
            .filter((row) => !row.archived && !row.isDraft && hasCalculationRowInput(row))
            .map((row) => ({
                id: row.id > 0 ? row.id : null,
                edition: row.edition,
                purchasePrice: row.purchasePrice,
                freight: row.freight,
                other: row.other,
                storage: row.storage,
                packagingCost: row.packagingCost,
                percentAddition: row.percentAddition,
                salesPrice: row.salesPrice,
                nrOfPallets: row.nrOfPallets,
                nrOfVehicles: row.nrOfVehicles,
                palletCalcFactor: row.palletCalcFactor,
                freightDomesticPerPallet: row.freightDomesticPerPallet,
                freightInternationalPerPallet: row.freightInternationalPerPallet,
                freightPerVehicle: row.freightPerVehicle,
                freightUnloading: row.freightUnloading,
                freightTotal: row.freightTotal,
                freightPerUnit: row.freightPerUnit,
                preCalcAdditionPercent: row.preCalcAdditionPercent,
                calcPalletPriceUsed: row.calcPalletPriceUsed,
                calcFtlPriceUsed: row.calcFtlPriceUsed,
                customerDeliveryAddressId: row.customerDeliveryAddressId ?? null,
                storageNrOfPallets: row.storageNrOfPallets,
                storageMonths: row.storageMonths,
                costs: (row.costs ?? [])
                    .filter((cost) => cost.costId != null)
                    .map((cost) => ({
                        id: cost.id > 0 ? cost.id : null,
                        costId: cost.costId,
                        costTypeText: cost.costTypeText || null,
                        nrOf: cost.nrOf,
                        inPrice: cost.inPrice,
                        inPriceCurrencyId: cost.inPriceCurrencyId,
                        outPrice: cost.outPrice,
                        doDebit: Boolean(cost.doDebit),
                    })),
                info: row.info ?? '',
            })),
        deletedRowIds: deletedRowIds ?? [],
    };
};

const roundToDecimals = (value, decimals) => {
    const factor = 10 ** decimals;
    return Math.round((value + Number.EPSILON) * factor) / factor;
};

const recalculateSalesRow = (row, calculation, unitMultiplier) => {
    const purchase = Number(row.purchasePrice ?? 0);
    const currencyRate = Number(calculation.currencyRate ?? 0);
    const freight = Number(row.freight ?? 0);
    const other = Number(row.other ?? 0);
    const storage = Number(row.storage ?? 0);
    const packaging = Number(row.packagingCost ?? 0);
    const totalCost = purchase * currencyRate + freight + other + storage + packaging;
    let salesPrice = Number(row.salesPrice ?? 0);
    let percentAddition = Number(row.percentAddition ?? 0);

    if (calculation.lockOutPrice) {
        salesPrice = totalCost * (1 + percentAddition / 100);
    } else {
        percentAddition = totalCost === 0 ? 0 : 100 * (salesPrice / totalCost - 1);
    }

    const salesRate = Number(calculation.salesCurrencyRate) || 1;
    const decimals = Math.min(10, Math.max(0, Number(calculation.salesDecimals) || 0));
    const contribution = roundToDecimals((salesPrice - totalCost) * salesRate, decimals);
    const safeUnitMultiplier = Number(unitMultiplier) > 0 ? Number(unitMultiplier) : 1;
    const totalContribution = row.edition
        ? roundToDecimals(contribution * Number(row.edition) / safeUnitMultiplier, decimals)
        : null;

    return {
        ...row,
        convertedPurchasePrice: purchase * currencyRate,
        totalCost,
        percentAddition,
        salesPrice,
        contribution,
        totalContribution,
    };
};

const toNumber = (value) => {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
};

const isAutomaticFreightCalc = (calculation) => Boolean(calculation.autoFreightCalc)
    && !calculation.deactivateFreightCalculation
    && toNumber(calculation.palletsPerTruck) > 0;

const getPalletPlaces = (row, calculation) => {
    const pallets = toNumber(row.nrOfPallets);
    const divisor = calculation.palletIsStackable ? 2 : 1;
    if (isAutomaticFreightCalc(calculation)) {
        return Math.ceil((Math.ceil(pallets) % toNumber(calculation.palletsPerTruck)) / divisor);
    }
    return Math.ceil(pallets / divisor);
};

const getFreightPerPalletPlace = (row, calculation) => {
    const domestic = toNumber(row.freightDomesticPerPallet);
    const international = toNumber(row.freightInternationalPerPallet);
    if (domestic === 0 && international === 0) return 0;
    const places = getPalletPlaces(row, calculation);
    const factor = row.palletCalcFactor == null ? 1 : toNumber(row.palletCalcFactor);
    return (domestic + international) * factor
        + (places > 0 ? toNumber(row.freightUnloading) / places : 0);
};

const deriveNrOfPallets = (row, calculation) => {
    const editionPerPallet = toNumber(calculation.editionPerPallet);
    if (editionPerPallet <= 0) return row;
    return {
        ...row,
        nrOfPallets: row.edition == null || row.edition === '' ? null : toNumber(row.edition) / editionPerPallet,
    };
};

// Mirrors CalculationPricingService.CalculateFreight on the server.
const recalculateFreightRow = (row, calculation, unitMultiplier) => {
    if (calculation.deactivateFreightCalculation) return row;
    const isAutomatic = isAutomaticFreightCalc(calculation);
    const pallets = toNumber(row.nrOfPallets);
    const nrOfVehicles = isAutomatic
        ? Math.floor(Math.ceil(pallets) / toNumber(calculation.palletsPerTruck))
        : row.nrOfVehicles;
    const next = { ...row, nrOfVehicles };
    const places = getPalletPlaces(next, calculation);
    const factor = next.palletCalcFactor == null ? 1 : toNumber(next.palletCalcFactor);
    const perVehicle = toNumber(next.freightPerVehicle);
    const vehicles = toNumber(nrOfVehicles);
    const mixedPrice = perVehicle * vehicles
        + (toNumber(next.freightDomesticPerPallet) + toNumber(next.freightInternationalPerPallet)) * places * factor
        + (places > 0 ? toNumber(next.freightUnloading) : 0);
    const allFtlPrice = perVehicle * (vehicles + (places > 0 ? 1 : 0));
    const isAllFtl = isAutomatic && allFtlPrice < mixedPrice;
    const decimals = Math.min(10, Math.max(0, Number(calculation.salesDecimals) || 0));
    const total = roundToDecimals(isAllFtl ? allFtlPrice : mixedPrice, decimals)
        * (1 + toNumber(next.preCalcAdditionPercent) / 100);
    const salesRate = toNumber(calculation.salesCurrencyRate);
    const edition = toNumber(next.edition);
    const perUnit = edition > 0 && salesRate > 0 && Number(unitMultiplier) > 0
        ? roundToDecimals(total / edition * Number(unitMultiplier) / salesRate, decimals)
        : null;

    return {
        ...next,
        freightTotal: total,
        freightPerUnit: perUnit,
        calcPalletPriceUsed: !isAllFtl && (isAutomatic ? places > 0 : pallets > 0),
        calcFtlPriceUsed: isAllFtl || vehicles >= 1,
    };
};

const calculateStorageRow = (row, calculation, unitMultiplier, unitDecimals) => {
    const decimals = Math.min(10, Math.max(0, Number(unitDecimals) || 0));
    const pallets = toNumber(row.storageNrOfPallets);
    const months = toNumber(row.storageMonths);
    const edition = toNumber(row.edition);
    const safeMultiplier = Number(unitMultiplier) > 0 ? Number(unitMultiplier) : 1;
    const purchaseRate = calculation.purchaseCurrencyRate == null ? 1 : toNumber(calculation.purchaseCurrencyRate);
    const salesRate = calculation.salesCurrencyRate == null ? 1 : toNumber(calculation.salesCurrencyRate);
    const unloading = roundToDecimals(toNumber(calculation.loadingPerPallet) * pallets, decimals);
    const storage = roundToDecimals(toNumber(calculation.storagePerM2) * pallets * months / 2, decimals);
    const interest = roundToDecimals(
        toNumber(calculation.interest) / 100 / 12 * months / 2 * edition / safeMultiplier
        * (toNumber(row.purchasePrice) * purchaseRate + toNumber(row.freight) + toNumber(row.other)),
        decimals,
    );
    const freight = roundToDecimals(pallets * toNumber(calculation.freightFromStockPerPallet), decimals);
    const total = unloading + storage + interest + freight;
    const perUnit = edition > 0 && salesRate > 0
        ? roundToDecimals(total / edition * safeMultiplier / salesRate, decimals)
        : 0;

    return {
        unloading,
        storage,
        interest,
        freight,
        total,
        perUnit,
    };
};

const getAdjustedEdition = (edition, editionPerPallet, direction) => {
    const perPallet = toNumber(editionPerPallet);
    const usedPallets = toNumber(edition) / (perPallet > 0 ? perPallet : 1);
    const isWhole = usedPallets % 1 === 0;
    if (direction > 0) {
        return Math.trunc((isWhole ? usedPallets + 1 : Math.ceil(usedPallets)) * perPallet);
    }
    return Math.trunc((isWhole ? Math.max(usedPallets - 1, 0) : Math.trunc(usedPallets)) * perPallet);
};

const getRegistryCurrencyRate = (currencyId, currencyOptions) => {
    const currency = currencyOptions.find((item) => Number(item.id) === Number(currencyId));
    if (!currency) return null;
    return currency.rateToSek == null ? 1 : toNumber(currency.rateToSek);
};

// A cost in the sales currency uses the calculation's own rate instead of the registry rate.
const getCostCurrencyRate = (currencyId, calculation, currencyOptions) => {
    if (currencyId == null) return null;
    if (calculation.salesCurrencyId != null && Number(calculation.salesCurrencyId) === Number(currencyId)) {
        return calculation.salesCurrencyRate == null ? 1 : toNumber(calculation.salesCurrencyRate);
    }
    return getRegistryCurrencyRate(currencyId, currencyOptions);
};

const calculateCostRow = (cost, row, calculation, unitMultiplier, costOption, currencyOptions) => {
    const outRate = calculation.salesCurrencyRate == null ? 1 : toNumber(calculation.salesCurrencyRate);
    const inRate = getCostCurrencyRate(cost.inPriceCurrencyId, calculation, currencyOptions);
    if (!inRate || !outRate) return { price: null, pricePerUnit: null };

    const multiplier = costOption && costOption.provisionPercent == null && costOption.isNrOf
        ? toNumber(cost.nrOf)
        : 1;
    const price = multiplier * (toNumber(cost.inPrice) * inRate / outRate - toNumber(cost.outPrice));
    const edition = toNumber(row.edition);
    const pricePerUnit = edition > 0 && Number(unitMultiplier) > 0
        ? price * Number(unitMultiplier) / edition
        : null;
    return { price, pricePerUnit };
};

const getProvisionFromCost = (cost, row, unitMultiplier) => {
    const purchaseTotal = toNumber(row.purchasePrice) * toNumber(row.edition);
    if (purchaseTotal === 0 || !(Number(unitMultiplier) > 0)) return 0;
    return 100 * Number(unitMultiplier) * (toNumber(cost.inPrice) / purchaseTotal);
};

const getProvisionInPrice = (provisionPercent, row, calculation, currencyId, currencyOptions, unitMultiplier) => {
    const registryRate = getRegistryCurrencyRate(currencyId, currencyOptions);
    if (registryRate == null || registryRate === 0 || !(Number(unitMultiplier) > 0)) return null;
    const purchaseRate = calculation.purchaseCurrencyRate == null ? 1 : toNumber(calculation.purchaseCurrencyRate);
    return toNumber(provisionPercent) / 100
        * toNumber(row.purchasePrice)
        * (purchaseRate / registryRate)
        * (toNumber(row.edition) / Number(unitMultiplier));
};

const valuesDiffer = (first, second) => {
    if (first == null && second == null) return false;
    return Math.abs(toNumber(first) - toNumber(second)) > 1e-9;
};

const applyCostChange = (cost, field, value, row, calculation, unitMultiplier, context) => {
    const next = { ...cost, [field]: value };
    if (field === 'doDebit' && !value) next.outPrice = 0;
    if (field !== 'costId') return next;

    const option = context.costOptions.find((item) => Number(item.id) === Number(value));
    if (!option) return next;

    const hasProvision = option.provisionPercent != null;
    next.costTypeText = hasProvision ? `${option.provisionPercent}% INK-pris` : option.costTypeText ?? '';
    if (hasProvision || !option.isNrOf) next.nrOf = null;
    next.doDebit = Boolean(option.isDebitDefault);
    next.inPriceCurrencyId = option.isSupplier ? context.supplierCurrencyId ?? null : null;

    if (hasProvision && next.inPriceCurrencyId != null) {
        next.inPrice = getProvisionInPrice(
            option.provisionPercent,
            row,
            calculation,
            next.inPriceCurrencyId,
            context.currencyOptions,
            unitMultiplier,
        );
    } else if (context.supplierId != null) {
        const matches = context.supplierPrices.filter((price) => Number(price.costId) === Number(option.id));
        if (matches.length === 1) next.inPrice = matches[0].defaultInPrice;
    }

    if (context.customerId != null) {
        const matches = context.customerPrices.filter((price) => Number(price.costId) === Number(option.id));
        if (matches.length === 1) next.outPrice = matches[0].defaultOutPrice;
    } else {
        next.outPrice = null;
    }
    return next;
};

// New editions start with the supplier's auto-added costs, or one blank cost row.
const createInitialCosts = (row, calculation, unitMultiplier, context) => {
    const items = context.supplierPrices
        .filter((price) => price.calculationAutoAddSupplier)
        .map((price) => {
            const option = context.costOptions.find((item) => Number(item.id) === Number(price.costId));
            if (!option) return null;
            const hasProvision = option.provisionPercent != null;
            const currencyId = hasProvision
                ? calculation.purchaseCurrencyId ?? null
                : option.isSupplier ? context.supplierCurrencyId ?? null : null;
            return {
                costId: option.id,
                costTypeText: hasProvision ? `${option.provisionPercent}% INK-pris` : '',
                nrOf: option.isNrOf ? 1 : null,
                inPrice: hasProvision
                    ? getProvisionInPrice(option.provisionPercent, row, calculation, currencyId, context.currencyOptions, unitMultiplier)
                    : price.defaultInPrice,
                inPriceCurrencyId: currencyId,
                doDebit: Boolean(option.isDebitDefault),
            };
        })
        .filter(Boolean)
        .map((cost, index) => createCostItem(`cost-new-${row.clientKey}-${index}`, cost));
    return items.length > 0 ? items : [createCostItem(`cost-new-${row.clientKey}`)];
};

const getOtherCostWarnings = (rows, unitMultiplier, context) => {
    const warnings = [];
    const addWarning = (text) => {
        if (!warnings.includes(text)) warnings.push(text);
    };
    rows.filter((row) => !row.archived && !row.isDraft).forEach((row) => {
        (row.costs ?? []).filter((cost) => cost.costId != null).forEach((cost) => {
            const option = context.costOptions.find((item) => Number(item.id) === Number(cost.costId));
            const customerPrice = context.customerPrices.find((price) => Number(price.costId) === Number(cost.costId));
            const supplierPrice = context.supplierPrices.find((price) => Number(price.costId) === Number(cost.costId));
            if (customerPrice && valuesDiffer(customerPrice.defaultOutPrice, cost.outPrice)) {
                addWarning('Utpris skiljer sig från register.');
            }
            if (option && option.provisionPercent == null && supplierPrice
                && valuesDiffer(supplierPrice.defaultInPrice, cost.inPrice)) {
                addWarning('Inpris skiljer sig från register.');
            } else if (option && option.provisionPercent != null
                && valuesDiffer(option.provisionPercent, roundToDecimals(getProvisionFromCost(cost, row, unitMultiplier), 1))) {
                addWarning('Provision skiljer sig från register.');
            }
        });
    });
    return warnings.join(' ');
};

// Pulls the registry prices and commissions into every saved cost row.
const refreshOtherCosts = (row, calculation, unitMultiplier, context) => ({
    ...row,
    costs: (row.costs ?? []).map((cost) => {
        if (cost.costId == null) return cost;
        const option = context.costOptions.find((item) => Number(item.id) === Number(cost.costId));
        const customerPrice = context.customerPrices.find((price) => Number(price.costId) === Number(cost.costId));
        const supplierPrice = context.supplierPrices.find((price) => Number(price.costId) === Number(cost.costId));
        const next = { ...cost };
        if (customerPrice && valuesDiffer(customerPrice.defaultOutPrice, cost.outPrice)) {
            next.outPrice = customerPrice.defaultOutPrice;
        }
        if (option && option.provisionPercent == null && supplierPrice
            && valuesDiffer(supplierPrice.defaultInPrice, cost.inPrice)) {
            next.inPrice = supplierPrice.defaultInPrice;
        } else if (option && option.provisionPercent != null
            && valuesDiffer(option.provisionPercent, roundToDecimals(getProvisionFromCost(cost, row, unitMultiplier), 1))
            && Number(unitMultiplier) > 0) {
            next.inPrice = toNumber(row.purchasePrice) * toNumber(row.edition) * toNumber(option.provisionPercent)
                / 100 / Number(unitMultiplier);
        }
        return next;
    }),
});

const numberText = (value, decimals) => {
    if (value == null || value === '' || !Number.isFinite(Number(value))) return '-';
    return decimals == null
        ? NUMBER_FORMAT.format(Number(value))
        : Number(value).toLocaleString('sv-SE', {
            minimumFractionDigits: decimals,
            maximumFractionDigits: decimals,
        });
};

const SavedField = ({ label, value, numeric = false, integerOnly = false, editing = false, onChange, maxLength }) => (
    <LabeledInput
        label={label}
        labelWidth="w-32"
        type={editing && numeric ? 'number' : 'text'}
        integerOnly={integerOnly}
        value={numeric && editing ? value ?? '' : numeric ? numberText(value) : String(value ?? '')}
        onChange={onChange}
        readOnly={!editing}
        maxLength={maxLength}
        margintop="0"
        aria-label={label}
    />
);

const SavedFlag = ({ label, checked, editing = false, onChange }) => (
    <LabeledCheckbox label={label} checked={Boolean(checked)} disabled={!editing} onChange={onChange} color="red" />
);

const ProductInfoRow = ({ label, value, numeric = false }) => (
    <div className="min-w-0">
        <dt className="text-tiny tracking-wide text-black">{label}</dt>
        <dd className="mt-1 whitespace-pre-wrap break-words text-xs text-red-500">
            {numeric ? numberText(value) : (value ?? '') === '' ? '' : value}
        </dd>
    </div>
);

const CompactSelect = ({ value, items, onChange, className, ariaLabel, isDisabled = false, openInNewTabUrl, openInNewTabLabel }) => (
    <div className={`min-w-0 ${className}`}>
        <LabeledReactSelect
            label=""
            labelWidth="w-0"
            inputWidth="w-full"
            margintop="0"
            value={value ?? ''}
            items={items}
            onChange={onChange}
            aria-label={ariaLabel}
            isClearable={false}
            isSearchable={false}
            isDisabled={isDisabled}
            openInNewTabUrl={openInNewTabUrl}
            openInNewTabLabel={openInNewTabLabel}
            placeholder="Ej vald"
        />
    </div>
);

const CompactNumberInput = ({
    value,
    onChange,
    className,
    ariaLabel,
    integerOnly = false,
    centerText = false,
    prefix,
    suffix,
    inputWidth = 'w-20',
    suffixPaddingWidth,
    disabled = false,
    style,
}) => (
    <div className={`min-w-0 ${className}`}>
        <LabeledInput
            label=""
            labelWidth="w-0"
            inputWidth={inputWidth}
            margintop="0"
            type="number"
            value={value ?? ''}
            onChange={onChange}
            integerOnly={integerOnly}
            disabled={disabled}
            aria-label={ariaLabel}
            prefix={prefix}
            suffix={suffix}
            suffixPaddingWidth={suffixPaddingWidth}
            style={centerText ? { ...style, textAlign: 'center' } : style}
        />
    </div>
);

const FreightHeaderField = ({ label, value, onChange, integerOnly = false, disabled = false }) => (
    <div className="min-w-0">
        <label className="ml-2 mb-1 block truncate text-xs text-gray-700">{label}</label>
        <CompactNumberInput
            ariaLabel={label}
            value={value}
            onChange={onChange}
            integerOnly={integerOnly}
            disabled={disabled}
        />
    </div>
);

const CalculationSection = ({ title, children, initiallyOpen = false, className = '', onApprove, approvalText, actionDisabled = false, actionPending = false }) => {
    const [isOpen, setIsOpen] = useState(initiallyOpen);
    const contentId = useId();

    return (
        <section className={`min-w-0 mt-5 ${isOpen
            ? 'rounded-sm border border-gray-200 bg-stone-50 px-5 pt-2 pb-1 shadow-sm'
            : 'px-5 pt-2 pb-1'} ${className}`}>
            <div className={`mb-4 flex flex-wrap items-center gap-x-5 gap-y-2 ${isOpen ? 'justify-between' : ''}`}>
                <div className="flex items-center gap-2 w-150">
                    <button
                        type="button"
                        className="inline-flex w-fit items-center gap-1 text-xs font-semibold uppercase text-sky-700 decoration-sky-300 underline-offset-2 hover:text-sky-800 hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700"
                        aria-expanded={isOpen}
                        aria-controls={contentId}
                        onClick={() => setIsOpen((open) => !open)}
                    >
                        <ChevronDown
                            size={16}
                            aria-hidden="true"
                            className={`shrink-0 transition-transform ${isOpen ? 'rotate-0' : 'rotate-270'}`}
                        />
                        {title}
                    </button>
                </div>
                <div className="flex items-center gap-2">
                    {onApprove ? (
                        <button
                            type="button"
                            onClick={onApprove}
                            disabled={actionDisabled}
                            aria-label={`Uppdatera och godkänn ${title}`}
                            aria-busy={actionPending}
                            className="inline-flex items-center gap-1 text-xs font-medium text-sky-700 hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-700 disabled:cursor-not-allowed disabled:text-gray-400"
                        >
                            <RefreshCw size={14} aria-hidden="true" className={actionPending ? 'animate-spin' : ''} />
                            Uppdatera och godkänn
                        </button>
                    ) : null}
                    {approvalText ? <span role="status" className="text-xs text-gray-600">{approvalText}</span> : null}
                </div>
            </div>
            <div
                id={contentId}
                className={`grid transition-[grid-template-rows,opacity] duration-300 ease-in-out ${isOpen ? 'grid-rows-[1fr] opacity-100' : 'grid-rows-[0fr] opacity-0'
                    }`}
                inert={!isOpen}
            >
                <div className="min-h-0 overflow-hidden">
                    {children}
                </div>
            </div>
        </section>
    );
};

const EditionTable = ({ rows, columns, decimals, name, infoWidth = INFO_COLUMN_WIDTH }) => (
    <div className="min-w-0 overflow-x-auto" role="region" aria-label={name} tabIndex={0}>
        <table className="w-full min-w-max text-xs tabular-nums" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
            <thead>
                <tr className="border-b border-gray-300 text-gray-500 uppercase text-tiny">
                    {columns.map(({ field, label, width }) => (
                        <th key={field} scope="col" style={{ width }} className="px-3 py-1 text-right font-normal">
                            {label}
                        </th>
                    ))}
                    <th scope="col" style={{ width: infoWidth }} className="px-3 py-1 text-left font-normal">
                        Info
                    </th>
                </tr>
            </thead>
            <tbody>
                {rows.length === 0 ? (
                    <tr>
                        <td colSpan={columns.length + 1} className="py-6 text-center text-gray-400">
                            Inga kalkylrader.
                        </td>
                    </tr>
                ) : (
                    rows.map((row) => (
                        <tr key={row.id} className="border-b border-gray-200 text-gray-800 hover:bg-white/60">
                            {columns.map(({ field, precision, width }) => (
                                <td key={field} style={{ width }} className="px-3 py-2 text-right">
                                    {numberText(row[field], precision ?? decimals)}
                                </td>
                            ))}
                            <td style={{ width: infoWidth }} className="max-w-56 whitespace-normal px-3 py-2 text-gray-500">
                                {row.archived ? <span className="mr-2 text-amber-700">Arkiverad</span> : null}
                                {row.info}
                            </td>
                        </tr>
                    ))
                )}
            </tbody>
        </table>
    </div>
);

const PalletEditionButtons = ({ row, editionPerPallet, onAdjustEdition }) => (
    <span className="inline-flex shrink-0">
        {[1, -1].map((direction) => {
            const label = direction > 0 ? 'Öka upplagan med en pall' : 'Minska upplagan med en pall';
            const Icon = direction > 0 ? Plus : Minus;
            return (
                <button
                    key={direction}
                    type="button"
                    onClick={() => onAdjustEdition(row.clientKey, direction)}
                    disabled={row.archived || !(toNumber(editionPerPallet) > 0)}
                    title={label}
                    aria-label={`${label}, upplaga ${row.edition ?? 'ny rad'}`}
                    className="inline-flex w-6 items-center justify-center text-gray-600 hover:bg-gray-100 disabled:cursor-not-allowed disabled:text-gray-300"
                >
                    <Icon size={14} aria-hidden="true" />
                </button>
            );
        })}
    </span>
);

const FreightRowsTable = ({
    rows,
    calculation,
    addressItems,
    onRowChange,
    onAdjustEdition,
}) => {
    const salesDecimals = calculation.salesDecimals ?? 0;
    const { active, automaticCalculation: isAutoCalc, automaticPrices: isAutoPrice } = getFreightMode(calculation);
    const isDeactivated = !active;
    const labelOf = (field) => FREIGHT_COLUMNS.find((column) => column.field === field)?.label ?? field;

    return (
        <div className="min-w-0 overflow-x-auto" role="region" aria-label="Fraktberäkning" tabIndex={0}>
            <table onKeyDownCapture={handleCalculationTableKeyDown} className="w-full min-w-max border-collapse text-xs tabular-nums" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                <thead>
                    <tr className="border-b border-gray-300 text-gray-500 text-tiny uppercase">
                        {FREIGHT_COLUMNS.map(({ field, label, width }) => (
                            <th key={field} scope="col" style={{ width }} className="px-2 py-1 font-normal text-right">{label}</th>
                        ))}
                        <th scope="col" style={{ width: '56px' }} className="px-2 py-1 font-normal"></th>
                    </tr>
                </thead>
                <tbody className="">
                    {rows.length === 0 ? (
                        <tr>
                            <td colSpan={FREIGHT_COLUMNS.length + 1} className="px-2 py-4 text-center text-gray-400">
                                Inga kalkylrader.
                            </td>
                        </tr>
                    ) : rows.map((row) => {
                        const isLocked = row.archived || row.isDraft;
                        const rowLabel = `upplaga ${row.edition ?? 'ny rad'}`;
                        const numberCell = (field, { decimals, enabled = false, value = row[field], textClass = '' }) => (
                            <div className={`absolute inset-0 border border-transparent ${enabled ? 'bg-white focus-within:border-blue-400' : 'bg-gray-100'}`}>
                                <NumberInput
                                    rowId={row.clientKey}
                                    field={field}
                                    value={value ?? ''}
                                    decimals={decimals}
                                    disabled={!enabled}
                                    onChange={onRowChange}
                                    className={`h-full w-full border-0 px-2 text-right text-xs focus:outline-none ${textClass}`}
                                    aria-label={`${labelOf(field)}, ${rowLabel}`}
                                />
                            </div>
                        );
                        const cells = {
                            edition: numberCell('edition', { decimals: 0 }),
                            nrOfPallets: numberCell('nrOfPallets', {
                                decimals: 2,
                                enabled: !isLocked && (isDeactivated || !isAutoCalc),
                            }),
                            palletPlaces: numberCell('palletPlaces', {
                                decimals: 0,
                                value: row.nrOfPallets == null ? null : getPalletPlaces(row, calculation),
                            }),
                            nrOfVehicles: numberCell('nrOfVehicles', {
                                decimals: 0,
                                enabled: !isLocked && !isDeactivated && !isAutoCalc,
                            }),
                            customerDeliveryAddressId: (
                                <div className={`${isLocked || isDeactivated || !isAutoPrice ? 'bg-gray-100' : 'bg-white'}`}>
                                    <LabeledSelect
                                        label=""
                                        labelWidth="w-0"
                                        margintop="0"
                                        value={row.customerDeliveryAddressId ?? ''}
                                        items={addressItems}
                                        onChange={(value) => onRowChange(
                                            row.clientKey,
                                            'customerDeliveryAddressId',
                                            value === '' ? null : Number(value),
                                        )}
                                        disabled={isLocked || isDeactivated || !isAutoPrice}
                                        aria-label={`${labelOf('customerDeliveryAddressId')}, ${rowLabel}`}
                                    />
                                </div>
                            ),
                            freightDomesticPerPallet: numberCell('freightDomesticPerPallet', {
                                decimals: salesDecimals,
                                enabled: !isLocked && !isDeactivated && !isAutoPrice,
                            }),
                            freightInternationalPerPallet: numberCell('freightInternationalPerPallet', {
                                decimals: salesDecimals,
                                enabled: !isLocked && !isDeactivated && !isAutoPrice,
                            }),
                            palletCalcFactor: numberCell('palletCalcFactor', { decimals: 2 }),
                            freightUnloading: numberCell('freightUnloading', {
                                decimals: salesDecimals,
                                enabled: !isLocked && !isDeactivated,
                            }),
                            totalFreightPerPlace: numberCell('totalFreightPerPlace', {
                                decimals: salesDecimals,
                                value: getFreightPerPalletPlace(row, calculation),
                                textClass: row.calcPalletPriceUsed ? 'text-green-700 font-semibold' : '',
                            }),
                            freightPerVehicle: numberCell('freightPerVehicle', {
                                decimals: salesDecimals,
                                enabled: !isLocked && !isDeactivated && !isAutoPrice,
                                textClass: row.calcFtlPriceUsed ? 'text-green-700 font-semibold' : '',
                            }),
                            preCalcAdditionPercent: (
                                <div className="absolute inset-0 bg-gray-100 px-2 py-0.5 text-right text-gray-700">
                                    {`${numberText(row.preCalcAdditionPercent ?? 0, 2)}%`}
                                </div>
                            ),
                            freightTotal: numberCell('freightTotal', { decimals: salesDecimals }),
                            freightPerUnit: numberCell('freightPerUnit', { decimals: salesDecimals }),
                        };

                        return (
                            <tr key={row.clientKey} className="h-6 border-b border-gray-100 text-gray-800">
                                {FREIGHT_COLUMNS.map(({ field, width }) => (
                                    <td key={field} style={{ width }} className="relative h-5 px-0 text-right">
                                        {cells[field]}
                                    </td>
                                ))}
                                <td className="w-14 px-0 py-0 text-center">
                                    <PalletEditionButtons row={row} editionPerPallet={calculation.editionPerPallet} onAdjustEdition={onAdjustEdition} />
                                </td>
                            </tr>
                        );
                    })}
                </tbody>
            </table>
        </div>
    );
};

const StorageRowsTable = ({ rows, calculation, unitMultiplier, decimals, onRowChange }) => {
    const storageRows = rows.filter((row) => !row.isDraft);

    return (
        <div
            className="min-w-0 overflow-x-auto"
            role="region"
            aria-label="Lagringskostnader"
            tabIndex={0}
        >
            <table onKeyDownCapture={handleCalculationTableKeyDown} className="w-full min-w-max border-collapse text-xs tabular-nums" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                <thead>
                    <tr className="border-b border-gray-300 text-gray-500 text-tiny uppercase">
                        {STORAGE_COLUMNS.map(({ field, label, width }) => (
                            <th key={field} scope="col" style={{ width }} className="px-2 py-1 font-normal text-right">{label}</th>
                        ))}
                    </tr>
                </thead>
                <tbody className="bg-white">
                    {storageRows.length === 0 ? (
                        <tr>
                            <td colSpan={STORAGE_COLUMNS.length} className="px-2 py-4 text-center text-gray-400">
                                Inga kalkylrader.
                            </td>
                        </tr>
                    ) : storageRows.map((row) => {
                        const derived = calculateStorageRow(row, calculation, unitMultiplier, decimals);
                        const values = {
                            edition: numberText(row.edition, 0),
                            storageNrOfPallets: null,
                            storageMonths: null,
                            storageUnloading: numberText(derived.unloading, decimals),
                            storageAmount: numberText(derived.storage, decimals),
                            storageInterest: numberText(derived.interest, decimals),
                            storageFreight: numberText(derived.freight, decimals),
                            storageTotal: numberText(derived.total, decimals),
                            storageTotalPerUnit: numberText(derived.perUnit, decimals),
                        };

                        return (
                            <tr key={row.clientKey} className="h-6 border-b border-gray-100 text-gray-800">
                                {STORAGE_COLUMNS.map(({ field, width }) => (
                                    <td key={field} style={{ width }} className="relative h-5 px-0 text-right">
                                        {field === 'storageNrOfPallets' || field === 'storageMonths' ? (
                                            <div className={`absolute inset-0 border border-transparent ${row.archived ? 'bg-gray-100' : 'bg-white focus-within:border-blue-400'}`}>
                                                <NumberInput
                                                    rowId={row.clientKey}
                                                    field={field}
                                                    value={row[field] ?? ''}
                                                    decimals={field === 'storageMonths' ? 1 : 0}
                                                    disabled={row.archived}
                                                    onChange={onRowChange}
                                                    className="h-full w-full border-0 px-2 text-right text-xs focus:outline-none"
                                                    aria-label={`${field === 'storageNrOfPallets' ? 'Ant pall' : 'Antal månader'}, upplaga ${row.edition}`}
                                                />
                                            </div>
                                        ) : (
                                            <div className="absolute inset-0 bg-gray-100 px-2 py-1 text-right text-gray-700">{values[field]}</div>
                                        )}
                                    </td>
                                ))}
                            </tr>
                        );
                    })}
                </tbody>
            </table>
        </div>
    );
};

const CostNumberCell = ({ cost, field, label, decimals, enabled = false, hideZeroDecimals = false, value = cost[field], onChange }) => (
    <div className={`absolute inset-0 border border-transparent ${enabled ? 'bg-white focus-within:border-blue-400' : 'bg-gray-100'}`}>
        <NumberInput
            rowId={cost.clientKey}
            field={field}
            value={value ?? ''}
            decimals={decimals}
            hideZeroDecimals={hideZeroDecimals}
            disabled={!enabled}
            onChange={onChange}
            className="h-full w-full border-0 px-2 text-right text-xs focus:outline-none"
            aria-label={label}
        />
    </div>
);

const CalculationCostsTable = ({
    rows,
    calculation,
    unitMultiplier,
    costOptions,
    currencyOptions,
    onCostChange,
    onAddCost,
    onRemoveCost,
}) => {
    const salesDecimals = calculation.salesDecimals ?? 0;
    const costItems = [
        { id: '', name: '' },
        ...costOptions.map((option) => ({ id: option.id, name: option.name, isActive: option.isActive })),
    ];
    const currencyItems = [
        { id: '', name: '' },
        ...currencyOptions.map((currency) => ({ id: currency.id, name: currency.name })),
    ];
    const costRows = rows.filter((row) => !row.isDraft && (row.costs ?? []).length > 0);

    return (
        <div className="min-w-0 overflow-x-auto" role="region" aria-label="Övriga kostnader" tabIndex={0}>
            <table onKeyDownCapture={handleCalculationTableKeyDown} className="w-full min-w-max border-collapse text-xs tabular-nums" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                <thead>
                    <tr className="border-b border-gray-300 text-gray-500 text-tiny uppercase">
                        {COST_COLUMNS.map(({ field, label, width, align }) => (
                            <th key={field} scope="col" style={{ width, textAlign: align }} className="px-2 py-1 font-normal">{label}</th>
                        ))}
                        <th scope="col" style={{ width: '56px' }} className="px-2 py-1 font-normal"></th>
                    </tr>
                </thead>
                <tbody className="bg-white">
                    {costRows.length === 0 ? (
                        <tr>
                            <td colSpan={COST_COLUMNS.length + 1} className="px-2 py-4 text-center text-gray-400">
                                Inga övriga kostnader.
                            </td>
                        </tr>
                    ) : costRows.flatMap((row) => {
                        const calculated = row.costs.map((cost) => {
                            const option = costOptions.find((item) => Number(item.id) === Number(cost.costId));
                            return { cost, option, ...calculateCostRow(cost, row, calculation, unitMultiplier, option, currencyOptions) };
                        });
                        const totalPerUnit = calculated.reduce((sum, item) => sum + toNumber(item.pricePerUnit), 0);

                        return calculated.map(({ cost, option, price, pricePerUnit }, index) => {
                            const isRowEnabled = !row.archived && cost.costId != null;
                            const isNrOfEnabled = isRowEnabled && Boolean(option?.isNrOf);
                            const rowLabel = `upplaga ${row.edition ?? ''}, ${option?.name ?? 'ny kostnad'}`;
                            const cells = {
                                edition: (
                                    <CostNumberCell cost={cost} field="edition" value={row.edition} label={`Upplaga, ${rowLabel}`} decimals={0} />
                                ),
                                costId: (
                                    <LabeledSelect
                                        label=""
                                        labelWidth="w-0"
                                        margintop="0"
                                        value={cost.costId ?? ''}
                                        items={costItems}
                                        onChange={(value) => onCostChange(cost.clientKey, 'costId', value === '' ? null : Number(value))}
                                        disabled={row.archived}
                                        aria-label={`Kostnad, ${rowLabel}`}
                                    />
                                ),
                                nrOf: (
                                    <CostNumberCell
                                        cost={cost}
                                        field="nrOf"
                                        label={`Antal, ${rowLabel}`}
                                        decimals={0}
                                        enabled={isNrOfEnabled}
                                        onChange={onCostChange}
                                    />
                                ),
                                inPrice: (
                                    <div className="absolute inset-0 flex">
                                        <div className="relative w-20 shrink-0">
                                            <CostNumberCell
                                                cost={cost}
                                                field="inPrice"
                                                label={`Inpris, ${rowLabel}`}
                                                decimals={2}
                                                hideZeroDecimals
                                                enabled={isRowEnabled}
                                                onChange={onCostChange}
                                            />
                                        </div>
                                        <LabeledSelect
                                            label=""
                                            labelWidth="w-0"
                                            margintop="0"
                                            value={cost.inPriceCurrencyId ?? ''}
                                            items={currencyItems}
                                            onChange={(value) => onCostChange(cost.clientKey, 'inPriceCurrencyId', value === '' ? null : Number(value))}
                                            disabled={!isRowEnabled}
                                            aria-label={`Inprisvaluta, ${rowLabel}`}
                                        />
                                    </div>
                                ),
                                doDebit: (
                                    <div className="flex h-full items-center bg-gray-100" style={{ justifyContent: 'var(--cost-justify)' }}>
                                        <SelectCircleCheckbox
                                            checked={cost.doDebit}
                                            disabled={!isRowEnabled}
                                            ariaLabel={`Debitera, ${rowLabel}`}
                                            onChange={() => onCostChange(cost.clientKey, 'doDebit', !cost.doDebit)}
                                        />
                                    </div>
                                ),
                                outPrice: (
                                    <div className="absolute inset-0 flex items-center gap-1 bg-gray-100" style={{ justifyContent: 'var(--cost-justify)' }}>
                                        <span className="w-8 text-gray-600">{cost.nrOf == null ? '' : numberText(cost.nrOf, 0)}</span>
                                        <span className="text-gray-500">*</span>
                                        <div className="relative h-full w-16">
                                            <CostNumberCell
                                                cost={cost}
                                                field="outPrice"
                                                label={`Utpris, ${rowLabel}`}
                                                decimals={2}
                                                hideZeroDecimals
                                                enabled={isRowEnabled && cost.doDebit}
                                                onChange={onCostChange}
                                            />
                                        </div>
                                    </div>
                                ),
                                price: (
                                    <CostNumberCell cost={cost} field="price" value={price} label={`Kostnad SEK, ${rowLabel}`} decimals={salesDecimals} />
                                ),
                                pricePerUnit: (
                                    <CostNumberCell cost={cost} field="pricePerUnit" value={pricePerUnit} label={`SEK per enhet, ${rowLabel}`} decimals={salesDecimals} />
                                ),
                                totalPricePerUnit: (
                                    <CostNumberCell
                                        cost={cost}
                                        field="totalPricePerUnit"
                                        value={index === calculated.length - 1 ? totalPerUnit : null}
                                        label={`Totalt SEK per enhet, ${rowLabel}`}
                                        decimals={salesDecimals}
                                    />
                                ),
                                costTypeText: (
                                    <input
                                        type="text"
                                        value={cost.costTypeText ?? ''}
                                        maxLength={100}
                                        disabled={row.archived}
                                        onChange={(event) => onCostChange(cost.clientKey, 'costTypeText', event.target.value)}
                                        aria-label={`Kommentar, ${rowLabel}`}
                                        className="h-6 w-full border border-transparent bg-white px-2 text-left text-xs text-gray-700 focus:border-blue-400 focus:outline-none disabled:bg-gray-50"
                                    />
                                ),
                            };

                            return (
                                <tr key={cost.clientKey} className="h-6 border-b border-gray-100 text-gray-800">
                                    {COST_COLUMNS.map(({ field, width, align }) => (
                                        <td
                                            key={field}
                                            style={{
                                                width,
                                                textAlign: align,
                                                '--cost-justify': align === 'left' ? 'flex-start' : align === 'right' ? 'flex-end' : 'center',
                                            }}
                                            className="relative h-5 px-0 [&_input]:[text-align:inherit] [&_select]:[text-align:inherit]"
                                        >
                                            {cells[field]}
                                        </td>
                                    ))}
                                    <td className="w-14 px-0 py-0 text-center">
                                        <button
                                            type="button"
                                            onClick={() => onAddCost(cost.clientKey)}
                                            disabled={row.archived}
                                            title="Lägg till kostnad"
                                            aria-label={`Lägg till kostnad efter ${rowLabel}`}
                                            className="inline-flex w-6 items-center justify-center text-gray-600 hover:bg-gray-100 disabled:cursor-not-allowed disabled:text-gray-300"
                                        >
                                            <Plus size={14} aria-hidden="true" />
                                        </button>
                                        <button
                                            type="button"
                                            onClick={() => onRemoveCost(cost.clientKey)}
                                            disabled={row.archived}
                                            title="Ta bort kostnad"
                                            aria-label={`Ta bort kostnad, ${rowLabel}`}
                                            className="inline-flex w-6 items-center justify-center text-gray-600 hover:bg-gray-100 disabled:cursor-not-allowed disabled:text-gray-300"
                                        >
                                            <Minus size={14} aria-hidden="true" />
                                        </button>
                                    </td>
                                </tr>
                            );
                        });
                    })}
                </tbody>
            </table>
        </div>
    );
};

const CalculationRowsTable = ({
    rows,
    columns,
    purchaseDecimals,
    salesDecimals,
    lockOutPrice,
    deactivateFreightCalculation,
    autoFreightCalc,
    editionPerPallet,
    onRowChange,
    onFreightRowChange,
    onAdjustEdition,
    onDeleteRow,
}) => {
    const editableFields = new Set([
        'edition',
        'purchasePrice',
        'freight',
        'other',
        'storage',
        'packagingCost',
        'percentAddition',
        'salesPrice',
    ]);
    const calculatedFields = new Set([
        'convertedPurchasePrice',
        'totalCost',
        'contribution',
        'totalContribution',
    ]);

    return (
        <div className="min-w-0 overflow-x-auto" role="region" aria-label="Kalkylrader" tabIndex={0}>
            <table onKeyDownCapture={handleCalculationTableKeyDown} className="w-full min-w-max border-collapse text-xs tabular-nums" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                <thead>
                    <tr className="border-b border-gray-300 text-gray-500 text-tiny uppercase">
                        {columns.map(({ field, label, width }) => (
                            <th key={field} scope="col" style={{ width }} className="px-2 py-1 font-normal text-right">{label}</th>
                        ))}
                        <th scope="col" style={{ width: INFO_COLUMN_WIDTH }} className="pl-3 py-1 font-normal text-left">Info</th>
                        <th scope="col" style={{ width: '40px' }} className="px-2 py-1 font-normal text-center"></th>
                    </tr>
                </thead>
                <tbody className="bg-white">
                    {rows.length === 0 ? (
                        <tr>
                            <td colSpan={columns.length + 2} className="px-2 py-4 text-center text-gray-400">
                                Inga kalkylrader.
                            </td>
                        </tr>
                    ) : rows.map((row) => (
                        <tr key={row.clientKey} className="border-b border-gray-100 text-gray-800 h-6">
                            {columns.map(({ field, width }) => {
                                if (field === 'nrOfPallets') {
                                    const disabled = row.archived || row.isDraft
                                        || (!deactivateFreightCalculation && autoFreightCalc);
                                    return (
                                        <td key={field} style={{ width }} className="relative h-5 px-0 text-right">
                                            <div className="absolute inset-0 flex items-stretch">
                                                <div className={`min-w-0 flex-1 border border-transparent ${disabled ? 'bg-gray-100' : 'bg-white focus-within:border-blue-400'}`}>
                                                    <NumberInput
                                                        rowId={row.clientKey}
                                                        field={field}
                                                        value={row.nrOfPallets ?? ''}
                                                        decimals={2}
                                                        disabled={disabled}
                                                        onChange={onFreightRowChange}
                                                        className="h-full w-full border-0 px-2 text-right text-xs focus:outline-none"
                                                        aria-label={`Ant pall, upplaga ${row.edition ?? 'ny rad'}`}
                                                    />
                                                </div>
                                                <PalletEditionButtons row={row} editionPerPallet={editionPerPallet} onAdjustEdition={onAdjustEdition} />
                                            </div>
                                        </td>
                                    );
                                }
                                const isEditable = editableFields.has(field);
                                const isCalculated = calculatedFields.has(field);
                                const disabled = row.archived
                                    || isCalculated
                                    || (field === 'percentAddition' && !lockOutPrice)
                                    || (field === 'salesPrice' && lockOutPrice)
                                    || (field === 'freight' && deactivateFreightCalculation);
                                const decimals = field === 'edition'
                                    ? 0
                                    : field === 'purchasePrice'
                                        ? purchaseDecimals ?? 0
                                        : field === 'percentAddition'
                                            ? 2
                                            : salesDecimals ?? 0;

                                return (
                                    <td key={field} style={{ width }} className="relative h-5 px-0 text-right">
                                        {isEditable || isCalculated ? (
                                            <div className={`absolute inset-0 border border-transparent ${disabled ? 'bg-gray-100' : 'bg-white'} ${!disabled ? 'focus-within:border-blue-400' : ''}`}>
                                                <NumberInput
                                                    rowId={row.clientKey}
                                                    field={field}
                                                    value={row[field] ?? ''}
                                                    decimals={decimals}
                                                    disabled={disabled}
                                                    onChange={onRowChange}
                                                    className="h-full w-full border-0 px-2 text-right text-xs focus:outline-none"
                                                    aria-label={`${columns.find((column) => column.field === field)?.label ?? field}, upplaga ${row.edition ?? 'ny rad'}`}
                                                />
                                            </div>
                                        ) : (
                                            <div className="px-3 py-1 text-right text-gray-700">
                                                {numberText(row[field], decimals)}
                                            </div>
                                        )}
                                    </td>
                                );
                            })}
                            <td style={{ width: INFO_COLUMN_WIDTH }} className="px-0 py-0 text-left">
                                <input
                                    type="text"
                                    value={row.info ?? ''}
                                    disabled={row.archived}
                                    onChange={(event) => onRowChange(row.clientKey, 'info', event.target.value)}
                                    aria-label={`Info, upplaga ${row.edition ?? 'ny rad'}`}
                                    className="h-6 w-full border border-transparent bg-white px-2 text-left text-xs text-gray-700 focus:border-blue-400 focus:outline-none disabled:bg-gray-50"
                                />
                            </td>
                            <td className="w-10 px-0 py-0 text-center">
                                <button
                                    type="button"
                                    onClick={() => onDeleteRow(row.clientKey)}
                                    disabled={row.archived}
                                    title="Ta bort rad"
                                    aria-label={`Ta bort kalkylrad, upplaga ${row.edition ?? 'ny rad'}`}
                                    className="inline-flex w-8 items-center justify-center text-red-600 hover:bg-red-50 disabled:cursor-not-allowed disabled:text-gray-300"
                                >
                                    <Trash2 size={14} aria-hidden="true" />
                                </button>
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
};

const CalculationHistoryTable = ({ rows, purchaseDecimals, salesDecimals, isLoading, error, onEdit }) => {
    if (isLoading) {
        return <p className="py-4 text-center text-xs text-gray-400">Laddar historik...</p>;
    }
    if (error) {
        return <p role="alert" className="py-4 text-center text-xs text-red-600">{error}</p>;
    }
    if (rows.length === 0) {
        return <p className="py-4 text-center text-xs text-gray-400">Ingen historik.</p>;
    }

    const historyNumber = (value, decimals) => value == null ? '' : numberText(value, decimals);
    const currencyLabel = (name, rate) => name
        ? `${name} (${numberText(rate ?? 0, 2)})`
        : '';
    const historyColumns = [
        { field: 'date', label: 'Datum', width: '85px', align: 'left' },
        { field: 'supplierOrderNr', label: 'Best.nr', width: '70px', align: 'left' },
        { field: 'customerOrderNr', label: 'OE.nr', width: '70px', align: 'left' },
        { field: 'deliveryTime', label: 'Lev.tid', width: '80px', align: 'left' },
        { field: 'edition', label: 'Upplaga', width: '85px', align: 'right' },
        { field: 'purchasePrice', label: 'Ink pris', width: '85px', align: 'right' },
        { field: 'purchaseCurrency', label: 'Valuta', width: '90px', align: 'left' },
        { field: 'freightCost', label: 'Frakt', width: '75px', align: 'right' },
        { field: 'otherCost', label: 'Övrigt', width: '75px', align: 'right' },
        { field: 'storageCost', label: 'Lagring', width: '75px', align: 'right' },
        { field: 'packagingCost', label: 'Pallkost', width: '75px', align: 'right' },
        { field: 'purchaseCostTotal', label: 'Ink tot', width: '95px', align: 'right' },
        { field: 'percentAddition', label: 'Påslag', width: '80px', align: 'right' },
        { field: 'salesPrice', label: 'Fsg pris', width: '90px', align: 'right' },
        { field: 'salesCurrency', label: 'Valuta', width: '90px', align: 'left' },
        { field: 'tbTotalSek', label: 'TB tot SEK', width: '90px', align: 'right' },
        { field: 'info', label: '', width: '28px', align: 'center' },
        { field: 'edit', label: '', width: '36px', align: 'center' },
    ];
    const alignmentClass = {
        left: 'text-left',
        center: 'text-center',
        right: 'text-right',
    };

    return (
        <div className="min-w-0 overflow-x-auto" role="region" aria-label="Historik" tabIndex={0}>
            <table className="w-full border-collapse text-xs tabular-nums" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                <thead>
                    <tr className="border-b border-gray-300 text-gray-500 text-tiny uppercase">
                        {historyColumns.map(({ field, label, width, align }) => (
                            <th key={field} scope="col" style={{ width }} className={`px-2 py-1 font-normal ${alignmentClass[align]}`}>{label}</th>
                        ))}
                    </tr>
                </thead>
                <tbody className="bg-white">
                    {rows.map((row) => (
                        <tr
                            key={row.customerOrderId ? `customer-${row.customerOrderId}` : `supplier-${row.supplierOrderId}`}
                            className="h-6 border-b border-gray-100 odd:bg-white even:bg-gray-50 text-gray-800 transition-colors hover:bg-lime-100"
                        >
                            <td className={`px-2 py-1 ${alignmentClass.left}`}>{row.createdDate ?? ''}</td>
                            <td className={`px-2 py-1 ${alignmentClass.left}`}>
                                {row.supplierOrderId ? (
                                    <Link
                                        to={`/order/supplierorders/${row.supplierOrderId}`}
                                        target="_blank"
                                        rel="noopener noreferrer"
                                        className="text-sky-700 decoration-sky-300 underline-offset-2 hover:underline hover:text-sky-800"
                                    >
                                        {row.supplierOrderNr ?? row.supplierOrderId}
                                    </Link>
                                ) : null}
                            </td>
                            <td className={`px-2 py-1 ${alignmentClass.left}`}>
                                {row.customerOrderId ? (
                                    <Link
                                        to={`/order/customerorders/${row.customerOrderId}`}
                                        target="_blank"
                                        rel="noopener noreferrer"
                                        className="text-sky-700 decoration-sky-300 underline-offset-2 hover:underline hover:text-sky-800"
                                    >
                                        {row.customerOrderNr ?? row.customerOrderId}
                                    </Link>
                                ) : null}
                            </td>
                            <td className={`px-2 py-1 ${alignmentClass.left}`}>{row.deliveryTime ?? ''}</td>
                            <td className={`px-2 py-1 ${alignmentClass.right}`}>{historyNumber(row.edition, 0)}</td>
                            <td className={`px-2 py-1 ${alignmentClass.right}`}>{historyNumber(row.purchasePrice, purchaseDecimals)}</td>
                            <td className={`px-2 py-1 ${alignmentClass.left}`}>{currencyLabel(row.purchaseCurrencyName, row.purchaseCurrencyRate)}</td>
                            <td className={`px-2 py-1 ${alignmentClass.right}`}>{historyNumber(row.freightCost, salesDecimals)}</td>
                            <td className={`px-2 py-1 ${alignmentClass.right}`}>{historyNumber(row.otherCost, salesDecimals)}</td>
                            <td className={`px-2 py-1 ${alignmentClass.right}`}>{historyNumber(row.storageCost, salesDecimals)}</td>
                            <td className={`px-2 py-1 ${alignmentClass.right}`}>{historyNumber(row.packagingCost, salesDecimals)}</td>
                            <td className={`px-2 py-1 ${alignmentClass.right}`}>{historyNumber(row.purchaseCostTotal, salesDecimals)}</td>
                            <td className={`px-2 py-1 font-semibold ${alignmentClass.right}`}>{historyNumber(row.percentAddition, 2)}</td>
                            <td className={`px-2 py-1 font-semibold ${alignmentClass.right}`}>{historyNumber(row.salesPrice, salesDecimals)}</td>
                            <td className={`px-2 py-1 ${alignmentClass.left}`}>{currencyLabel(row.salesCurrencyName, row.salesCurrencyRate)}</td>
                            <td className={`px-2 py-1 ${alignmentClass.right}`}>{historyNumber(row.tbTotalSek, salesDecimals)}</td>
                            <td className={`px-2 py-1 ${alignmentClass.center}`}>
                                {row.info ? (
                                    <button
                                        type="button"
                                        title={row.info}
                                        aria-label={`Visa historikvarning: ${row.info}`}
                                        className="inline-flex items-center justify-center text-amber-700"
                                    >
                                        <InfoIcon size={14} aria-hidden="true" />
                                    </button>
                                ) : null}
                            </td>
                            <td className="px-2 py-0 text-center">
                                <button
                                    type="button"
                                    data-history-edit={row.customerOrderId ? `customer-${row.customerOrderId}` : `supplier-${row.supplierOrderId}`}
                                    disabled={!row.supplierOrderId || !row.selectedCalculationRowId}
                                    aria-label={`Redigera kalkylrad för beställning ${row.supplierOrderNr ?? row.supplierOrderId ?? ''}`}
                                    title={row.supplierOrderId && row.selectedCalculationRowId ? 'Redigera kopplad kalkylrad' : 'Beställningen saknar en kopplad kalkylrad'}
                                    onClick={() => onEdit(row)}
                                    className="inline-flex items-center justify-center rounded-sm p-1 text-sky-700 hover:bg-sky-50 focus-visible:outline-2 focus-visible:outline-lime-700 disabled:text-gray-300 disabled:cursor-not-allowed"
                                >
                                    <Pencil size={14} aria-hidden="true" />
                                </button>
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
};

const CalculationPage = ({ id, productId }) => {
    const navigate = useNavigate();
    const [result, setResult] = useState(null);
    const [historyResult, setHistoryResult] = useState(null);
    const [historyRevision, setHistoryRevision] = useState(0);
    const [editingHistoryRow, setEditingHistoryRow] = useState(null);
    const historyFocusRef = useRef(null);
    const [unitOptions, setUnitOptions] = useState([]);
    const [currencyOptions, setCurrencyOptions] = useState([]);
    const [palletFormatOptions, setPalletFormatOptions] = useState([]);
    const [supplierFactoryOptions, setSupplierFactoryOptions] = useState([]);
    const [customerDeliveryAddressOptions, setCustomerDeliveryAddressOptions] = useState([]);
    const [expanded, setExpanded] = useState(() => window.matchMedia(INFO_PANEL_WIDE_QUERY).matches);
    const [rowView, setRowView] = useState('active');
    const [draft, setDraft] = useState(null);
    const [isSaving, setIsSaving] = useState(false);
    const [isRefreshingFreight, setIsRefreshingFreight] = useState(false);
    const [isLoadingFreightFactors, setIsLoadingFreightFactors] = useState(false);
    const freightFactorsRef = useRef(null);
    const [freightFactorError, setFreightFactorError] = useState('');
    const [sectionAction, setSectionAction] = useState(null);
    const sectionActionRef = useRef(false);
    const currentDraftRef = useRef(null);
    const [pendingFreightRefreshKeys, setPendingFreightRefreshKeys] = useState(null);
    const [costOptions, setCostOptions] = useState([]);
    const [costPrices, setCostPrices] = useState([]);
    const [supplierCurrencyId, setSupplierCurrencyId] = useState(null);
    const [isCustomerSelectionPending, setIsCustomerSelectionPending] = useState(false);
    const [isSupplierSelectionPending, setIsSupplierSelectionPending] = useState(false);
    const [customerPickerReset, setCustomerPickerReset] = useState(0);
    const [messages, setMessages] = useState([]);
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const [freightWarning, setFreightWarning] = useState(null);
    const isSavingRef = useRef(false);
    const freightRefreshSequenceRef = useRef(0);
    const currencyRefreshSequenceRef = useRef({ purchase: 0, sales: 0 });
    const pendingAutoFreightRefreshRef = useRef(false);
    const manualFreightDestinationRef = useRef(false);
    const rowDraftSequenceRef = useRef(0);
    const costSequenceRef = useRef(0);
    const messagesRef = useRef([]);
    const infoPanelAutoCloseRef = useRef(null);
    const isInfoPanelExpandedRef = useRef(expanded);
    const skipUnsavedCheckRef = useRef(false);
    const isNew = id === 'new';
    const validProductId = /^\d+$/.test(productId ?? '') && Number(productId) > 0 && Number(productId) <= 2147483647;
    const validId = /^\d+$/.test(id ?? '') && Number(id) > 0 && Number(id) <= 2147483647;
    const validRequest = validId || (isNew && validProductId);
    const requestKey = isNew ? `calculation:new:${productId}:detail` : `calculation:${id}:detail`;
    const historyKey = `calculation:${id}:history:${historyRevision}`;
    const loading = validRequest && result?.key !== requestKey;
    const data = result?.key === requestKey ? result.data : null;
    const error = !validRequest ? 'Ogiltigt kalkyl- eller produktnummer.' : result?.key === requestKey ? result.error : '';
    const historyLoading = validId && historyResult?.key !== historyKey;
    const historyRows = historyResult?.key === historyKey ? historyResult.rows : EMPTY_ROWS;
    const historyError = historyResult?.key === historyKey ? historyResult.error : '';
    const closeHistoryEditor = () => {
        setEditingHistoryRow(null);
        setHistoryRevision((value) => value + 1);
    };

    useEffect(() => {
        if (historyLoading || editingHistoryRow || !historyFocusRef.current) return;
        const trigger = document.querySelector(`[data-history-edit="${historyFocusRef.current}"]`);
        if (trigger) {
            trigger.focus();
            historyFocusRef.current = null;
        }
    }, [historyLoading, editingHistoryRow, historyResult]);
    const baselineDraft = data ? createCalculationDraft(data) : null;
    const formDraft = draft ?? baselineDraft;
    const freightEditState = getFreightEditState(formDraft, data);
    currentDraftRef.current = formDraft;
    const bundlesPerPallet = calculatePackagesPerPallet(formDraft?.editionPerPallet, formDraft?.nrOfPerBundle);
    const outerPackagesPerPallet = calculatePackagesPerPallet(formDraft?.editionPerPallet, formDraft?.nrOfPerOuterPackaging);
    const selectedSupplierId = formDraft?.supplier?.id ?? null;
    const selectedCustomerId = formDraft?.customer?.id ?? null;
    const isDirty = Boolean(draft && baselineDraft && JSON.stringify(draft) !== JSON.stringify(baselineDraft));
    const hasUnsavedChanges = useCallback(
        () => !skipUnsavedCheckRef.current && (isDirty || Boolean(isNew && data)),
        [isDirty, isNew, data],
    );
    const blocker = useBlocker(hasUnsavedChanges);
    const allRows = formDraft?.rows ?? EMPTY_ROWS;
    const rows = allRows.filter((row) => rowView === 'all' || row.archived === (rowView === 'archived'));
    const productDimensions = [data?.product?.length, data?.product?.width, data?.product?.height]
        .filter((value) => value != null && value !== '')
        .join(' x ');

    const flashInfoPanel = useCallback((hasIssue = false) => {
        const wasAlreadyExpanded = isInfoPanelExpandedRef.current;
        if (infoPanelAutoCloseRef.current) {
            clearTimeout(infoPanelAutoCloseRef.current);
            infoPanelAutoCloseRef.current = null;
        }

        const keepOpen = hasIssue || messagesRef.current.some((message) =>
            message.type === 'error' || message.type === 'warning');
        isInfoPanelExpandedRef.current = true;
        setExpanded(true);
        if (!keepOpen && !wasAlreadyExpanded) {
            infoPanelAutoCloseRef.current = setTimeout(() => {
                isInfoPanelExpandedRef.current = false;
                setExpanded(false);
                infoPanelAutoCloseRef.current = null;
            }, 3000);
        }
    }, []);

    const toggleInfoPanel = () => {
        const nextExpanded = !isInfoPanelExpandedRef.current;
        isInfoPanelExpandedRef.current = nextExpanded;
        if (infoPanelAutoCloseRef.current) {
            clearTimeout(infoPanelAutoCloseRef.current);
            infoPanelAutoCloseRef.current = null;
        }
        setExpanded(nextExpanded);
    };

    const replaceMessages = useCallback((nextMessages) => {
        messagesRef.current = nextMessages;
        setMessages(nextMessages);
        flashInfoPanel(nextMessages.some((message) => message.type === 'error' || message.type === 'warning'));
    }, [flashInfoPanel]);

    const appendMessage = useCallback((type, text) => {
        replaceMessages([...messagesRef.current, { type, text }]);
    }, [replaceMessages]);

    const clearMessages = useCallback(() => {
        messagesRef.current = [];
        setMessages([]);
        if (infoPanelAutoCloseRef.current) {
            clearTimeout(infoPanelAutoCloseRef.current);
            infoPanelAutoCloseRef.current = null;
        }
    }, []);

    const handleCustomerSelectionPending = (pending) => {
        setIsCustomerSelectionPending(pending);
        if (pending) {
            flashInfoPanel(true);
        } else {
            flashInfoPanel(false);
        }
    };

    const handleSupplierSelectionPending = (pending) => {
        setIsSupplierSelectionPending(pending);
    };

    const hasPendingPartySelection = isCustomerSelectionPending || isSupplierSelectionPending;

    const purchaseCurrency = currencyOptions.find((currency) => currency.id === formDraft?.purchaseCurrencyId);
    const salesCurrency = currencyOptions.find((currency) => currency.id === formDraft?.salesCurrencyId);
    const purchaseCurrencyWarning = purchaseCurrency?.rateToSek != null
        && Number(purchaseCurrency.rateToSek) !== Number(formDraft?.purchaseCurrencyRate ?? 0)
        ? 'Kurs avviker från register, uppdatera!'
        : '';
    const salesCurrencyWarning = salesCurrency?.rateToSek != null
        && Number(salesCurrency.rateToSek) !== Number(formDraft?.salesCurrencyRate ?? 0)
        ? 'Kurs avviker från register, uppdatera!'
        : '';
    const currencyWarnings = [
        purchaseCurrencyWarning ? { type: 'warning', text: `Inköpskurs: ${purchaseCurrencyWarning}` } : null,
        salesCurrencyWarning ? { type: 'warning', text: `Försäljningskurs: ${salesCurrencyWarning}` } : null,
    ].filter(Boolean);
    const panelMessages = [
        ...(error && !messages.some((message) => message.text === error) ? [{ type: 'error', text: error }] : []),
        ...currencyWarnings,
        ...messages,
    ];
    const approvalTextFor = (component, flag) => getApprovalText(
        data?.approvals?.find((approval) => approval.component === component), formDraft?.[flag],
    );
    const palletPriceInSek = Number(formDraft?.palletPriceIn ?? 0) * Number(formDraft?.purchaseCurrencyRate ?? 0);
    const palletPriceOutSek = Number(formDraft?.palletPriceOut ?? 0) * Number(formDraft?.salesCurrencyRate ?? 0);
    const palletDebitDifference = palletPriceOutSek - palletPriceInSek;

    useEffect(() => () => {
        if (infoPanelAutoCloseRef.current) {
            clearTimeout(infoPanelAutoCloseRef.current);
        }
    }, []);

    useEffect(() => {
        if (!validRequest) return;
        let active = true;
        const currencyRefreshSequence = currencyRefreshSequenceRef.current;
        getSharedRequest(requestKey, () => apiClient.get(isNew
            ? `/calculations/from-product/${productId}/draft`
            : `/calculations/${id}/aggregate`))
            .then((response) => {
                if (active) {
                    setResult({ key: requestKey, data: response.data, error: '' });
                    setDraft(createCalculationDraft(response.data));
                    setIsCustomerSelectionPending(false);
                    setIsSupplierSelectionPending(false);
                }
            })
            .catch((loadError) => {
                if (!active) return;
                const errorText = loadError.response?.status === 404
                    ? 'Kalkylen kunde inte hittas.'
                    : 'Kunde inte hämta kalkylen.';
                setResult({
                    key: requestKey,
                    data: null,
                    error: errorText,
                });
                replaceMessages([{ type: 'error', text: errorText }]);
            });
        return () => {
            active = false;
            ++currencyRefreshSequence.purchase;
            ++currencyRefreshSequence.sales;
        };
    }, [id, isNew, productId, replaceMessages, requestKey, validRequest]);

    useEffect(() => {
        setFreightWarning(null);
        if (!data?.supplier?.id || (data.autoFreightCalc !== false && data.autoFreightPrice !== false)) return undefined;

        let active = true;
        const supplierId = data.supplier.id;
        getSharedRequest(`suppliers:${supplierId}:details`, () => apiClient.get(`/suppliers/${supplierId}/details`))
            .then((response) => {
                if (!active) return;
                const message = getFreightWarningMessage(data, response.data.supplier);
                if (message) setFreightWarning({ calculation: data, message });
            })
            .catch((lookupError) => {
                if (!active) return;
                console.error('Failed to check calculation transport responsibility:', lookupError);
                appendMessage('error', 'Kunde inte kontrollera leverantörens transportansvar för fraktvarningen.');
            });
        return () => {
            active = false;
        };
    }, [appendMessage, data]);

    useEffect(() => {
        if (!validId) return;
        let active = true;
        getSharedRequest(historyKey, () => apiClient.get(`/calculations/${id}/history`))
            .then((response) => {
                if (!Array.isArray(response.data)) throw new Error('Invalid calculation history response.');
                if (active) {
                    setHistoryResult({
                        key: historyKey,
                        rows: response.data,
                        error: '',
                    });
                }
            })
            .catch((historyLoadError) => {
                console.error('Failed to load calculation history:', historyLoadError);
                if (active) {
                    setHistoryResult({ key: historyKey, rows: [], error: 'Historiken kunde inte hämtas.' });
                }
            });
        return () => {
            active = false;
        };
    }, [historyKey, id, validId]);

    useEffect(() => {
        let active = true;
        Promise.all([
            getSharedRequest('calculations:unit-options', () => apiClient.post('/units/search', {
                pagination: { pageNumber: 1, pageSize: 500 },
                orderBy: [{ field: 'id', direction: 'asc' }],
            })),
            getSharedRequest('calculations:currency-options', () => apiClient.post('/currencies/search', {
                pagination: { pageNumber: 1, pageSize: 500 },
                orderBy: [{ field: 'id', direction: 'asc' }],
            })),
        ]).then(([unitsResponse, currenciesResponse]) => {
            if (!active) return;
            setUnitOptions(unitsResponse?.data?.items ?? []);
            setCurrencyOptions(currenciesResponse?.data?.items ?? []);
        }).catch((optionsError) => {
            console.error('Failed to load calculation options:', optionsError);
        });
        return () => {
            active = false;
        };
    }, []);

    useEffect(() => {
        setPalletFormatOptions([]);
        if (!selectedCustomerId || !selectedSupplierId) return undefined;

        let active = true;
        getSharedRequest(
            `calculations:palletformat-options:${selectedCustomerId}:${selectedSupplierId}`,
            () => apiClient.get('/palletformats/searchwithprices', {
                params: { customerId: selectedCustomerId, supplierId: selectedSupplierId },
            }),
        ).then((palletFormatsResponse) => {
            if (!active) return;
            const mappedPalletFormats = (Array.isArray(palletFormatsResponse?.data) ? palletFormatsResponse.data : [])
                .map((item) => ({
                    ...item,
                    id: item?.id ?? item?.Id,
                    name: item?.name ?? item?.Name,
                    width: item?.width ?? item?.Width,
                    height: item?.height ?? item?.Height,
                    supplierPrice: item?.supplierPrice ?? item?.SupplierPrice,
                    customerPrice: item?.customerPrice ?? item?.CustomerPrice,
                    isActive: item?.active ?? item?.Active,
                }))
                .filter((item) => item.id != null && String(item.name ?? '').trim() !== '');

            setPalletFormatOptions([{ id: '', name: 'Välj packning' }, ...mappedPalletFormats]);
        }).catch((optionsError) => {
            if (!active) return;
            console.error('Failed to load calculation pallet formats:', optionsError);
            appendMessage('error', 'Kunde inte hämta pall/packning för vald kund och leverantör.');
        });
        return () => {
            active = false;
        };
    }, [appendMessage, selectedCustomerId, selectedSupplierId]);

    useEffect(() => {
        if (!selectedSupplierId) {
            setSupplierFactoryOptions([]);
            setSupplierCurrencyId(null);
            return undefined;
        }

        let active = true;
        getSharedRequest(`suppliers:${selectedSupplierId}:details`, () => apiClient.get(`/suppliers/${selectedSupplierId}/details`))
            .then((response) => {
                if (!active) return;
                setSupplierFactoryOptions(response?.data?.factories ?? []);
                setSupplierCurrencyId(response?.data?.supplier?.currencyId ?? null);
            })
            .catch((lookupError) => {
                console.error('Failed to load calculation supplier factories:', lookupError);
                if (active) {
                    setSupplierFactoryOptions([]);
                    setSupplierCurrencyId(null);
                }
            });

        return () => {
            active = false;
        };
    }, [selectedSupplierId]);

    useEffect(() => {
        let active = true;
        getSharedRequest('calculations:cost-options', () => apiClient.post('/costs/search', {}))
            .then((response) => {
                if (active) setCostOptions(Array.isArray(response?.data) ? response.data : []);
            })
            .catch((lookupError) => {
                console.error('Failed to load calculation costs:', lookupError);
            });
        return () => {
            active = false;
        };
    }, []);

    useEffect(() => {
        if (!selectedCustomerId && !selectedSupplierId) {
            setCostPrices([]);
            return undefined;
        }

        let active = true;
        getSharedRequest(
            `calculations:cost-prices:${selectedCustomerId ?? ''}:${selectedSupplierId ?? ''}`,
            () => apiClient.get('/calculations/cost-prices', {
                params: { customerId: selectedCustomerId, supplierId: selectedSupplierId },
            }),
        )
            .then((response) => {
                if (active) setCostPrices(Array.isArray(response?.data) ? response.data : []);
            })
            .catch((lookupError) => {
                console.error('Failed to load calculation cost prices:', lookupError);
                if (active) setCostPrices([]);
            });

        return () => {
            active = false;
        };
    }, [selectedCustomerId, selectedSupplierId]);

    useEffect(() => {
        if (!selectedCustomerId) {
            setCustomerDeliveryAddressOptions([]);
            return undefined;
        }

        let active = true;
        getSharedRequest(`customers:${selectedCustomerId}`, () => apiClient.get(`/customers/${selectedCustomerId}`))
            .then((response) => {
                if (active) setCustomerDeliveryAddressOptions(response?.data?.deliveryAddresses ?? []);
            })
            .catch((lookupError) => {
                console.error('Failed to load calculation delivery addresses:', lookupError);
                if (active) setCustomerDeliveryAddressOptions([]);
            });

        return () => {
            active = false;
        };
    }, [selectedCustomerId]);

    useEffect(() => {
        if (!freightEditState.selectDefaultFactory && !pendingAutoFreightRefreshRef.current) return;
        if (formDraft?.deactivateFreightCalculation || !formDraft?.autoFreightPrice || formDraft.supplierFactoryId != null) return;
        const defaultFactory = supplierFactoryOptions.find((factory) => factory.isDefault);
        if (!defaultFactory) return;
        setDraft((previous) => previous?.autoFreightPrice && previous.supplierFactoryId == null
            ? { ...previous, supplierFactoryId: defaultFactory.id }
            : previous);
    }, [freightEditState.selectDefaultFactory, formDraft?.autoFreightPrice, formDraft?.deactivateFreightCalculation, formDraft?.supplierFactoryId, supplierFactoryOptions]);

    useEffect(() => {
        if (!freightEditState.selectDefaultAddress && !pendingAutoFreightRefreshRef.current) return;
        if (manualFreightDestinationRef.current || formDraft?.deactivateFreightCalculation || !formDraft?.autoFreightPrice || formDraft.customerDeliveryAddressId != null) return;
        const defaultAddress = customerDeliveryAddressOptions.find((address) => address.isDefault);
        if (!defaultAddress) return;
        const postalValue = defaultAddress.postalNrValue ?? defaultAddress.postalNr;
        const parsedPostalValue = postalValue == null || postalValue === ''
            ? null
            : Number.parseInt(String(postalValue).replace(/\s/g, ''), 10);
        setDraft((previous) => previous?.autoFreightPrice && previous.customerDeliveryAddressId == null
            ? {
                ...previous,
                customerDeliveryAddressId: defaultAddress.id,
                deliveryAddressCountryCode: defaultAddress.countryCode ?? '',
                deliveryAddressPostalNr: Number.isFinite(parsedPostalValue) ? parsedPostalValue : null,
            }
            : previous);
    }, [customerDeliveryAddressOptions, freightEditState.selectDefaultAddress, formDraft?.autoFreightPrice, formDraft?.deactivateFreightCalculation, formDraft?.customerDeliveryAddressId]);

    const handlePalletFormatChange = (value) => {
        const palletFormatId = value === '' ? null : Number(value);

        setDraft((previous) => {
            if (!previous) return previous;

            if (palletFormatId == null) {
                return updateAutomaticLoadingInstruction(previous, {
                    ...previous,
                    palletFormatId: null,
                    palletLength: null,
                    palletWidth: null,
                });
            }

            const selectedPalletFormat = (Array.isArray(palletFormatOptions) ? palletFormatOptions : [])
                .find((option) => Number(option.id) === palletFormatId);

            const next = {
                ...previous,
                palletFormatId,
            };

            if (selectedPalletFormat) {
                const length = selectedPalletFormat.width == null ? null : Number(selectedPalletFormat.width);
                const width = selectedPalletFormat.height == null ? null : Number(selectedPalletFormat.height);
                const inPrice = selectedPalletFormat.supplierPrice == null ? null : Number(selectedPalletFormat.supplierPrice);
                const outPrice = selectedPalletFormat.customerPrice == null ? null : Number(selectedPalletFormat.customerPrice);

                if (Number.isFinite(length)) next.palletLength = length;
                if (Number.isFinite(width)) next.palletWidth = width;
                if (Number.isFinite(inPrice)) next.palletPriceIn = inPrice;
                if (Number.isFinite(outPrice)) next.palletPriceOut = outPrice;
            }

            return updateAutomaticLoadingInstruction(previous, next);
        });
    };

    useEffect(() => {
        if (blocker.state === 'blocked') {
            setShowUnsavedWarning(true);
        }
    }, [blocker.state]);

    useEffect(() => {
        const handleBeforeUnload = (event) => {
            if (!hasUnsavedChanges()) return;

            event.preventDefault();
            event.returnValue = '';
            return '';
        };

        window.addEventListener('beforeunload', handleBeforeUnload);
        return () => window.removeEventListener('beforeunload', handleBeforeUnload);
    }, [hasUnsavedChanges]);

    const handleBack = () => {
        if (window.history.length > 1) navigate(-1);
        else navigate('/order/calculations');
    };

    const openCalculationDocument = (path) => {
        const documentUrl = new URL(path, window.location.origin);
        documentUrl.searchParams.set('calculationId', String(data.id));
        navigate(`${documentUrl.pathname}${documentUrl.search}`);
    };

    const getUnitMultiplier = (unitId) => unitOptions.find((unit) => Number(unit.id) === Number(unitId))?.multiplicator
        ?? data?.unitMultiplier
        ?? 1;

    const recalculateDraftFreight = (row, calculation, unitMultiplier) => {
        if (row.archived || row.isDraft) return row;
        const { automaticCalculation } = getFreightMode(calculation);
        const length = Math.max(Number(calculation.palletLength), Number(calculation.palletWidth));
        const width = Math.min(Number(calculation.palletLength), Number(calculation.palletWidth));
        const cached = freightFactorsRef.current;
        if (automaticCalculation && cached?.key === `${length}:${width}`) {
            const geometry = getAutomaticFreightGeometry(row, calculation, cached.values);
            return recalculateFreightRow({ ...row, ...geometry }, { ...calculation, palletsPerTruck: geometry.palletsPerTruck }, unitMultiplier);
        }
        return recalculateFreightRow(row, calculation, unitMultiplier);
    };

    const queueFreightRefresh = (clientKeys) => {
        if (!formDraft?.autoFreightPrice || formDraft.deactivateFreightCalculation) return;
        setPendingFreightRefreshKeys((previous) => new Set([...(previous ?? []), ...clientKeys]));
    };

    const updateDraft = (field, value) => {
        if (field === 'deliveryAddressCountryCode' || field === 'deliveryAddressPostalNr') {
            manualFreightDestinationRef.current = true;
            queueFreightRefresh((formDraft?.rows ?? []).filter((row) => !row.archived && !row.isDraft).map((row) => row.clientKey));
        }
        if (FREIGHT_RECALC_FIELDS.has(field)) {
            queueFreightRefresh((formDraft?.rows ?? []).filter((row) => !row.archived).map((row) => row.clientKey));
        }
        setDraft((previous) => {
            if (!previous) return previous;
            const next = { ...previous, [field]: value };
            if (field === 'deliveryAddressCountryCode' || field === 'deliveryAddressPostalNr') next.customerDeliveryAddressId = null;
            if (FREIGHT_RECALC_FIELDS.has(field)) {
                const freightUnitMultiplier = getUnitMultiplier(next.unitId);
                next.rows = (next.rows ?? []).map((row) => (
                    row.archived
                        ? row
                        : recalculateDraftFreight(deriveNrOfPallets(row, next), next, freightUnitMultiplier)
                ));
            }
            if (field === 'purchaseCurrencyRate' || field === 'salesCurrencyRate') {
                const purchaseRate = next.purchaseCurrencyRate == null || next.purchaseCurrencyRate === ''
                    ? null
                    : Number(next.purchaseCurrencyRate);
                const salesRate = Number(next.salesCurrencyRate ?? 0);
                if (salesRate !== 0) next.currencyRate = purchaseRate == null ? null : purchaseRate / salesRate;
            }
            if ([
                'currencyRate',
                'purchaseCurrencyRate',
                'salesCurrencyRate',
                'salesDecimals',
                'lockOutPrice',
                'unitId',
            ].includes(field)) {
                const unitMultiplier = unitOptions.find((unit) => unit.id === next.unitId)?.multiplicator
                    ?? data?.unitMultiplier
                    ?? 1;
                next.rows = (next.rows ?? []).map((row) => (
                    row.archived ? row : recalculateSalesRow(row, next, unitMultiplier)
                ));
            }
            return updateAutomaticLoadingInstruction(previous, next);
        });
    };

    const updateField = (field) => (value) => updateDraft(field, value);

    const handleFreightModeChange = (field) => (enabled) => {
        if (field !== 'deactivateFreightCalculation' && formDraft?.deactivateFreightCalculation) return;
        clearMessages();
        ++freightRefreshSequenceRef.current;
        setIsRefreshingFreight(false);
        setPendingFreightRefreshKeys(null);
        pendingAutoFreightRefreshRef.current = field === 'autoFreightPrice' ? enabled
            : field === 'autoFreightCalc' && Boolean(formDraft?.autoFreightPrice);
        if (field === 'autoFreightPrice' && enabled) manualFreightDestinationRef.current = false;
        setDraft((previous) => {
            if (!previous) return previous;
            const next = changeFreightMode(previous, field, enabled);
            const multiplier = getUnitMultiplier(next.unitId);
            next.rows = next.rows.map((row) => row.archived || row.isDraft ? row
                : next.deactivateFreightCalculation
                    ? recalculateSalesRow(row, next, multiplier)
                    : recalculateDraftFreight(field === 'autoFreightCalc' ? deriveNrOfPallets(row, next) : row, next, multiplier));
            return next;
        });
    };

    useEffect(() => {
        if (!formDraft?.autoFreightCalc || formDraft.deactivateFreightCalculation) {
            setIsLoadingFreightFactors(false);
            setFreightFactorError('');
            return undefined;
        }
        const length = Math.max(Number(formDraft.palletLength), Number(formDraft.palletWidth));
        const width = Math.min(Number(formDraft.palletLength), Number(formDraft.palletWidth));
        let active = true;
        setIsLoadingFreightFactors(true);
        setFreightFactorError('');
        const key = `${length}:${width}`;
        const load = async () => {
            try {
                if (length < 1200 || length > 13600 || width < 800 || width > 2430) {
                    throw new Error('Pallens mått saknar stöd för automatisk fraktberäkning.');
                }
                const response = await getSharedRequest(`calculation:pallet-factors:${key}`, () => apiClient.post('/palletfactors/search', {
                    lengthFrom: length, lengthTo: length, widthFrom: width, widthTo: width,
                    pageNumber: 1, pageSize: 100,
                }));
                const values = parseFreightFactors(response.data.items, length, width);
                if (!active) return;
                freightFactorsRef.current = { key, values };
                if (!freightEditState.recalculateGeometry) return;
                setDraft((previous) => {
                    if (!previous || !previous.autoFreightCalc || previous.deactivateFreightCalculation) return previous;
                    const capacity = values[34] * (previous.palletIsStackable ? 2 : 1);
                    const next = { ...previous, palletsPerTruck: capacity };
                    const multiplier = unitOptions.find((unit) => Number(unit.id) === Number(next.unitId))?.multiplicator
                        ?? data?.unitMultiplier ?? 1;
                    next.rows = previous.rows.map((row) => row.archived || row.isDraft ? row : recalculateFreightRow({
                        ...row, ...getAutomaticFreightGeometry(row, next, values),
                    }, next, multiplier));
                    return next;
                });
                if (formDraft.autoFreightPrice) pendingAutoFreightRefreshRef.current = true;
            } catch (factorError) {
                if (!active) return;
                console.error('Failed to calculate freight pallet factors:', factorError);
                freightFactorsRef.current = null;
                setFreightFactorError(factorError.message);
                if (!freightEditState.recalculateGeometry) return;
                setDraft((previous) => previous ? {
                    ...previous, palletsPerTruck: null,
                    rows: previous.rows.map((row) => row.archived || row.isDraft ? row : { ...row, palletCalcFactor: null }),
                } : previous);
            } finally {
                if (active) setIsLoadingFreightFactors(false);
            }
        };
        void load();
        return () => { active = false; };
    }, [formDraft?.autoFreightCalc, formDraft?.deactivateFreightCalculation, formDraft?.palletLength,
        formDraft?.palletWidth, formDraft?.palletIsStackable, formDraft?.autoFreightPrice, unitOptions, data?.unitMultiplier,
        freightEditState.recalculateGeometry]);

    const handleCalculationCustomerSelect = (customer) => {
        ++currencyRefreshSequenceRef.current.sales;
        manualFreightDestinationRef.current = false;
        setDraft((previous) => {
            if (!previous) return previous;
            const nextCustomer = customer ? { id: customer.id, name: customer.name ?? '' } : null;
            if (previous.customer?.id === nextCustomer?.id) {
                return { ...previous, customer: nextCustomer };
            }
            return {
                ...previous,
                customer: nextCustomer,
                customerDeliveryAddressId: null,
                deliveryAddressCountryCode: '',
                deliveryAddressPostalNr: null,
            };
        });
        if (customer?.currencyId != null) {
            void handleCurrencyChange('sales', customer.currencyId);
        }
    };

    const handleCalculationSupplierSelect = (supplier) => {
        ++currencyRefreshSequenceRef.current.purchase;
        setDraft((previous) => {
            if (!previous) return previous;
            const nextSupplier = supplier ? { id: supplier.id, name: supplier.name ?? '' } : null;
            if (previous.supplier?.id === nextSupplier?.id) {
                return { ...previous, supplier: nextSupplier };
            }
            return { ...previous, supplier: nextSupplier, supplierFactoryId: null };
        });
        if (supplier?.currencyId != null) {
            void handleCurrencyChange('purchase', supplier.currencyId);
        }
    };

    const handleSupplierFactoryChange = (value) => {
        updateDraft('supplierFactoryId', value === '' ? null : Number(value));
        queueFreightRefresh((formDraft?.rows ?? []).filter((row) => !row.archived && !row.isDraft).map((row) => row.clientKey));
    };

    const handleDeliveryAddressChange = (value) => {
        manualFreightDestinationRef.current = false;
        const selectedAddress = customerDeliveryAddressOptions.find((address) => String(address.id) === String(value));
        const postalValue = selectedAddress?.postalNrValue ?? selectedAddress?.postalNr;
        const parsedPostalValue = postalValue == null || postalValue === ''
            ? null
            : Number.parseInt(String(postalValue).replace(/\s/g, ''), 10);

        setDraft((previous) => previous ? {
            ...previous,
            customerDeliveryAddressId: value === '' ? null : Number(value),
            deliveryAddressCountryCode: selectedAddress?.countryCode ?? '',
            deliveryAddressPostalNr: Number.isFinite(parsedPostalValue) ? parsedPostalValue : null,
        } : previous);
        queueFreightRefresh((formDraft?.rows ?? []).filter((row) => !row.archived && !row.isDraft).map((row) => row.clientKey));
    };

    const supplierFactoryItems = [
        { id: '', name: 'Välj fabrik' },
        ...supplierFactoryOptions
            .filter((factory) => factory.id != null)
            .map((factory) => ({ id: factory.id, name: factory.name ?? '' })),
    ];
    const deliveryAddressItems = [
        { id: '', name: 'Välj leveransadress' },
        ...customerDeliveryAddressOptions
            .filter((address) => address.id != null)
            .map((address) => ({
                id: address.id,
                name: [address.name, address.address].filter(Boolean).join(', ') || `Adress ${address.id}`,
            })),
    ];

    const rowAddressItems = [{ id: '', name: '' }, ...deliveryAddressItems.slice(1)];

    const costContext = {
        costOptions,
        currencyOptions,
        supplierCurrencyId,
        customerId: selectedCustomerId,
        supplierId: selectedSupplierId,
        customerPrices: costPrices.filter((price) => price.customerId != null),
        supplierPrices: costPrices.filter((price) => price.supplierId != null),
    };
    const otherCostUnitMultiplier = getUnitMultiplier(formDraft?.unitId);
    const selectedStorageUnit = unitOptions.find((unit) => Number(unit.id) === Number(formDraft?.unitId));
    const storageUnitMultiplier = Number(selectedStorageUnit?.multiplicator) > 0
        ? Number(selectedStorageUnit.multiplicator)
        : 1;
    const storageUnitDecimals = selectedStorageUnit?.nrOfCalcDecimals ?? 0;
    const otherCostWarning = formDraft
        ? getOtherCostWarnings(formDraft.rows, otherCostUnitMultiplier, costContext)
        : '';
    const handleCalculationRowChange = (clientKey, field, value) => {
        const targetRow = formDraft?.rows.find((row) => row.clientKey === clientKey);
        if (!targetRow || targetRow.archived || (targetRow[field] ?? null) === (value ?? null)) return;
        const unitMultiplier = unitOptions.find((unit) => unit.id === formDraft?.unitId)?.multiplicator
            ?? data?.unitMultiplier
            ?? 1;
        setDraft((previous) => {
            if (!previous) return previous;
            const targetRow = previous.rows.find((row) => row.clientKey === clientKey);
            if (!targetRow || targetRow.archived) return previous;

            const activateDraft = targetRow.isDraft && value != null && value !== '';
            const nextRows = previous.rows.map((row) => {
                if (row.clientKey !== clientKey) return row;
                const updatedRow = recalculateSalesRow({
                    ...row,
                    [field]: value,
                    isDraft: activateDraft ? false : row.isDraft,
                }, previous, unitMultiplier);
                const costedRow = activateDraft
                    ? { ...updatedRow, costs: createInitialCosts(updatedRow, previous, unitMultiplier, costContext) }
                    : updatedRow;
                return field === 'edition'
                    ? recalculateDraftFreight(deriveNrOfPallets(costedRow, previous), previous, unitMultiplier)
                    : costedRow;
            });

            if (activateDraft) {
                rowDraftSequenceRef.current += 1;
                nextRows.push(createEmptyCalculationRow(
                    `draft-row-${rowDraftSequenceRef.current}`,
                    getFreightRowDefaults(previous),
                ));
            }

            return { ...previous, rows: nextRows };
        });
        if (field === 'edition') queueFreightRefresh([clientKey]);
    };

    const handleFreightRowChange = (clientKey, field, value) => {
        const targetRow = formDraft?.rows.find((row) => row.clientKey === clientKey);
        if (!targetRow || targetRow.archived || (targetRow[field] ?? null) === (value ?? null)) return;
        const unitMultiplier = getUnitMultiplier(formDraft?.unitId);
        setDraft((previous) => {
            if (!previous) return previous;
            const nextRows = previous.rows.map((row) => (
                row.clientKey === clientKey && !row.archived
                    ? recalculateDraftFreight({ ...row, [field]: value }, previous, unitMultiplier)
                    : row
            ));
            return { ...previous, rows: nextRows };
        });
        if (FREIGHT_LOOKUP_FIELDS.has(field)) queueFreightRefresh([clientKey]);
    };

    const handleAdjustFreightEdition = (clientKey, direction) => {
        const row = formDraft?.rows.find((item) => item.clientKey === clientKey);
        if (!row) return;
        const nextEdition = getAdjustedEdition(row.edition, formDraft.editionPerPallet, direction);
        if (nextEdition > 0) handleCalculationRowChange(clientKey, 'edition', nextEdition);
    };

    const handleFreightDefaultChange = (field, rowField) => (value) => {
        const unitMultiplier = getUnitMultiplier(formDraft?.unitId);
        setDraft((previous) => {
            if (!previous) return previous;
            const next = { ...previous, [field]: value };
            next.rows = previous.rows.map((row) => (
                row.archived
                    ? row
                    : recalculateDraftFreight({ ...row, [rowField]: value }, next, unitMultiplier)
            ));
            return next;
        });
    };

    const handleStorageRowChange = (clientKey, field, value) => {
        setDraft((previous) => {
            if (!previous) return previous;
            return {
                ...previous,
                rows: previous.rows.map((row) => (
                    row.clientKey === clientKey && !row.archived
                        ? { ...row, [field]: value }
                        : row
                )),
            };
        });
    };

    const updateCostRows = (updateRow) => {
        setDraft((previous) => {
            if (!previous) return previous;
            return {
                ...previous,
                rows: previous.rows.map((row) => (row.archived || row.isDraft ? row : updateRow(row, previous))),
            };
        });
    };

    const handleCostChange = (costKey, field, value) => {
        const unitMultiplier = getUnitMultiplier(formDraft?.unitId);
        updateCostRows((row, calculation) => {
            if (!row.costs?.some((cost) => cost.clientKey === costKey)) return row;
            return {
                ...row,
                costs: row.costs.map((cost) => (
                    cost.clientKey === costKey
                        ? applyCostChange(cost, field, value, row, calculation, unitMultiplier, costContext)
                        : cost
                )),
            };
        });
    };

    const handleAddCost = (costKey) => {
        costSequenceRef.current += 1;
        const newKey = `cost-new-${costSequenceRef.current}`;
        updateCostRows((row) => {
            const index = row.costs?.findIndex((cost) => cost.clientKey === costKey) ?? -1;
            if (index < 0) return row;
            const costs = [...row.costs];
            costs.splice(index + 1, 0, createCostItem(newKey));
            return { ...row, costs };
        });
    };

    const handleRemoveCost = (costKey) => {
        updateCostRows((row) => {
            if (!row.costs?.some((cost) => cost.clientKey === costKey)) return row;
            return {
                ...row,
                costs: row.costs.length <= 1
                    ? [createCostItem(costKey)]
                    : row.costs.filter((cost) => cost.clientKey !== costKey),
            };
        });
    };

    const handleDeleteCalculationRow = (clientKey) => {
        setDraft((previous) => {
            if (!previous) return previous;
            const row = previous.rows.find((item) => item.clientKey === clientKey);
            if (!row || row.archived) return previous;

            const nextRows = previous.rows.filter((item) => item.clientKey !== clientKey);
            if (!nextRows.some((item) => item.isDraft)) {
                rowDraftSequenceRef.current += 1;
                nextRows.push(createEmptyCalculationRow(
                    `draft-row-${rowDraftSequenceRef.current}`,
                    getFreightRowDefaults(previous),
                ));
            }

            const deletedRowIds = row.id > 0
                ? [...new Set([...(previous.deletedRowIds ?? []), row.id])]
                : previous.deletedRowIds ?? [];
            return { ...previous, rows: nextRows, deletedRowIds };
        });
    };

    const applyRegistryCurrencyRate = (kind, rate) => {
        const rateField = kind === 'purchase' ? 'purchaseCurrencyRate' : 'salesCurrencyRate';
        updateDraft(rateField, rate ?? 0);
    };

    const refreshCurrencyRate = async (kind, currencyId) => {
        const sequence = ++currencyRefreshSequenceRef.current[kind];
        if (currencyId == null) {
            applyRegistryCurrencyRate(kind, 0);
            return;
        }

        try {
            const response = await apiClient.get(`/currencies/${currencyId}`);
            if (currencyRefreshSequenceRef.current[kind] !== sequence) return;
            applyRegistryCurrencyRate(kind, response?.data?.rateToSek);
        } catch (currencyError) {
            if (currencyRefreshSequenceRef.current[kind] !== sequence) return;
            console.error('Failed to refresh calculation currency rate:', currencyError);
            appendMessage('error', 'Valutakursen kunde inte hämtas från registret.');
        }
    };

    const handleCurrencyChange = async (kind, value) => {
        const idField = kind === 'purchase' ? 'purchaseCurrencyId' : 'salesCurrencyId';
        const currencyId = value == null || value === '' ? null : Number(value);
        updateDraft(idField, currencyId);
        await refreshCurrencyRate(kind, currencyId);
    };

    const handleCurrencyRefresh = (kind) => {
        const currencyId = kind === 'purchase'
            ? formDraft?.purchaseCurrencyId
            : formDraft?.salesCurrencyId;
        return refreshCurrencyRate(kind, currencyId);
    };

    const handleRefreshFreightPrices = useCallback(async ({ rowKeys, silent = false, forApproval = false, calculation = formDraft } = {}) => {
        if (!calculation || isRefreshingFreight || isLoadingFreightFactors || calculation.deactivateFreightCalculation || !calculation.autoFreightPrice
            || (sectionActionRef.current && !forApproval)) return;

        const formDraft = calculation;
        const sequence = ++freightRefreshSequenceRef.current;
        const unitMultiplier = unitOptions.find((unit) => Number(unit.id) === Number(formDraft.unitId))?.multiplicator
            ?? data?.unitMultiplier
            ?? 1;
        setIsRefreshingFreight(true);
        clearMessages();

        try {
            const response = await apiClient.post(isNew ? '/calculations/draft/freight-refresh' : `/calculations/${id}/freight-refresh`, {
                customerId: formDraft.customer?.id ?? null,
                supplierId: formDraft.supplier?.id ?? null,
                supplierFactoryId: formDraft.supplierFactoryId ?? null,
                customerDeliveryAddressId: formDraft.customerDeliveryAddressId ?? null,
                deliveryAddressCountryCode: formDraft.deliveryAddressCountryCode ?? '',
                deliveryAddressPostalNr: formDraft.deliveryAddressPostalNr ?? null,
                autoFreightCalc: Boolean(formDraft.autoFreightCalc),
                palletIsStackable: Boolean(formDraft.palletIsStackable),
                editionPerPallet: formDraft.editionPerPallet ?? null,
                palletsPerTruck: formDraft.palletsPerTruck ?? null,
                palletLength: formDraft.palletLength ?? null,
                palletWidth: formDraft.palletWidth ?? null,
                unitMultiplier,
                salesCurrencyRate: formDraft.salesCurrencyRate ?? null,
                salesDecimals: Number(formDraft.salesDecimals ?? 0),
                rows: formDraft.rows
                    .filter((row) => !row.archived && Number(row.edition) > 0 && (!rowKeys || rowKeys.has(row.clientKey)))
                    .map((row) => ({
                        clientKey: row.clientKey,
                        id: row.id > 0 ? row.id : null,
                        edition: Number(row.edition),
                        nrOfPallets: row.nrOfPallets ?? null,
                        nrOfVehicles: row.nrOfVehicles ?? null,
                        palletCalcFactor: row.palletCalcFactor ?? null,
                        customerDeliveryAddressId: row.customerDeliveryAddressId ?? null,
                    })),
            });

            if (sequence !== freightRefreshSequenceRef.current) return;
            const refreshedRows = Array.isArray(response?.data?.rows) ? response.data.rows : [];
            const resultByKey = new Map(refreshedRows.map((row) => [row.clientKey, row]));
            if (forApproval) {
                if ((response?.data?.warnings ?? []).length > 0) {
                    throw new Error(response.data.warnings.join(' '));
                }
                if (formDraft.rows.some((row) => !row.archived && !row.isDraft
                    && Number(resultByKey.get(row.clientKey)?.edition) !== Number(row.edition))) {
                    throw new Error('Fraktpriser saknas för en eller flera kalkylrader.');
                }
            }
            const applyRefresh = (previous) => {
                if (!previous) return previous;
                const nextRows = previous.rows.map((row) => {
                    if (row.archived || row.isDraft) return row;
                    const refreshed = resultByKey.get(row.clientKey);
                    if (!refreshed || Number(row.edition) !== Number(refreshed.edition)) return row;
                    const refreshedPrices = {
                        palletCalcFactor: refreshed.palletCalcFactor,
                        freightDomesticPerPallet: refreshed.freightDomesticPerPallet,
                        freightInternationalPerPallet: refreshed.freightInternationalPerPallet,
                        freightPerVehicle: refreshed.freightPerVehicle,
                        freightUnloading: refreshed.freightUnloading,
                        preCalcAdditionPercent: refreshed.preCalcAdditionPercent,
                    };
                    // Silent refreshes keep user-typed pallets/vehicles and leave the sales-row freight untouched.
                    if (silent) {
                        return recalculateFreightRow({ ...row, ...refreshedPrices }, previous, unitMultiplier);
                    }
                    return recalculateSalesRow({
                        ...row,
                        ...refreshedPrices,
                        freight: forApproval ? getFreightForApproval(refreshed, previous) : refreshed.freightPerUnit,
                        nrOfPallets: refreshed.nrOfPallets,
                        nrOfVehicles: refreshed.nrOfVehicles,
                        freightTotal: refreshed.freightTotal,
                        freightPerUnit: refreshed.freightPerUnit,
                        calcPalletPriceUsed: refreshed.calcPalletPriceUsed,
                        calcFtlPriceUsed: refreshed.calcFtlPriceUsed,
                    }, previous, unitMultiplier);
                });
                return { ...previous, rows: nextRows, ...(forApproval ? { approveFreight: true } : {}) };
            };
            if (forApproval) return applyRefresh(formDraft);
            setDraft(applyRefresh);

            for (const warning of response?.data?.warnings ?? []) {
                appendMessage('warning', warning);
            }
            if (!silent && (response?.data?.warnings ?? []).length === 0) {
                appendMessage('info', 'Fraktpriserna uppdaterades.');
            }
        } catch (refreshError) {
            if (forApproval) throw refreshError;
            console.error('Failed to refresh calculation freight prices:', refreshError);
            appendMessage('error', refreshError.response?.data?.message ?? 'Fraktpriserna kunde inte uppdateras.');
        } finally {
            if (sequence === freightRefreshSequenceRef.current) setIsRefreshingFreight(false);
        }
    }, [appendMessage, clearMessages, data?.unitMultiplier, formDraft, id, isNew, isRefreshingFreight, isLoadingFreightFactors, unitOptions]);

    const handleSectionApproval = async (component) => {
        if (!formDraft || sectionActionRef.current || isSavingRef.current || isRefreshingFreight || isLoadingFreightFactors || hasPendingPartySelection) return;
        const snapshot = formDraft;
        const selectedUnit = unitOptions.find((unit) => Number(unit.id) === Number(snapshot.unitId));
        const unitMultiplier = Number(selectedUnit?.multiplicator ?? data?.unitMultiplier);
        sectionActionRef.current = true;
        setSectionAction(component);
        setPendingFreightRefreshKeys(null);
        pendingAutoFreightRefreshRef.current = false;
        clearMessages();
        try {
            if (!(unitMultiplier > 0) || !(Number(snapshot.salesCurrencyRate) > 0)) {
                throw new Error('Enhet och försäljningskurs måste vara giltiga innan kostnaderna kan godkännas.');
            }
            const warnings = [];
            const updateSection = async (component, snapshot) => {
                let next;
                if (component === 'packaging') {
                    if (!snapshot.customer?.id || !snapshot.supplier?.id || !snapshot.palletFormatId) {
                        throw new Error('Välj kund, leverantör och pall/packning först.');
                    }
                    const response = await apiClient.get('/palletformats/searchwithprices', {
                        params: { customerId: snapshot.customer.id, supplierId: snapshot.supplier.id },
                    });
                    const format = response.data.find((item) => Number(item.id) === Number(snapshot.palletFormatId));
                    if (!format) throw new Error('Pall/packning saknar koppling till vald kund och leverantör.');
                    const updated = {
                        ...snapshot,
                        palletPriceIn: format.supplierPrice,
                        palletPriceOut: snapshot.doDebitPalletPrice ? format.customerPrice : 0,
                    };
                    if (snapshot.doDebitPalletPrice && Number(format.customerPrice ?? 0) === 0) {
                        updated.doDebitPalletPrice = false;
                        warnings.push('Pallpriset är 0 i registret.');
                    }
                    if (currentDraftRef.current === formDraft) {
                        setPalletFormatOptions([{ id: '', name: 'Välj packning' }, ...response.data.map((item) => ({
                            ...item, isActive: item.active,
                        }))]);
                    }
                    next = applyApprovalRows(updated, 'approvePackaging', (row) => {
                        const packagingCost = calculatePackagingCost(row, updated, unitMultiplier);
                        if (Number(updated.editionPerPallet) > 0
                            && Math.ceil(Number(row.nrOfPallets)) !== Math.ceil(Number(row.edition) / Number(updated.editionPerPallet))) {
                            warnings.push(`Antalet pall för upplaga ${row.edition} stämmer inte med antal/pall.`);
                        }
                        return recalculateSalesRow({ ...row, packagingCost }, updated, unitMultiplier);
                    });
                } else if (component === 'freight') {
                    if (snapshot.autoFreightCalc && !snapshot.deactivateFreightCalculation && freightFactorError) {
                        throw new Error(freightFactorError);
                    }
                    if (snapshot.autoFreightPrice && !snapshot.deactivateFreightCalculation) {
                        next = await handleRefreshFreightPrices({ forApproval: true, calculation: snapshot });
                    } else {
                        next = applyApprovalRows(snapshot, 'approveFreight', (row) => {
                            const refreshed = recalculateDraftFreight(row, snapshot, unitMultiplier);
                            return recalculateSalesRow({
                                ...refreshed,
                                freight: getFreightForApproval(refreshed, snapshot),
                            }, snapshot, unitMultiplier);
                        });
                    }
                } else if (component === 'other') {
                    const [optionsResponse, pricesResponse] = await Promise.all([
                        apiClient.post('/costs/search', {}),
                        apiClient.get('/calculations/cost-prices', {
                            params: { customerId: snapshot.customer?.id, supplierId: snapshot.supplier?.id },
                        }),
                    ]);
                    const options = optionsResponse.data;
                    const prices = pricesResponse.data;
                    const context = {
                        ...costContext,
                        costOptions: options,
                        customerPrices: prices.filter((price) => price.customerId != null),
                        supplierPrices: prices.filter((price) => price.supplierId != null),
                    };
                    next = applyApprovalRows(snapshot, 'approveOtherCosts', (row) => {
                        const updated = refreshOtherCosts(row, snapshot, unitMultiplier, context);
                        const other = updated.costs.reduce((sum, cost) => {
                            const option = options.find((item) => Number(item.id) === Number(cost.costId));
                            if (cost.costId != null && !option) {
                                throw new Error('En vald kostnad saknas i registret.');
                            }
                            const { pricePerUnit } = calculateCostRow(cost, updated, snapshot, unitMultiplier, option, currencyOptions);
                            return sum + toNumber(pricePerUnit);
                        }, 0);
                        return recalculateSalesRow({ ...updated, other }, snapshot, unitMultiplier);
                    });
                    if (currentDraftRef.current === formDraft) {
                        setCostOptions(options);
                        setCostPrices(prices);
                    }
                } else if (component === 'storage') {
                    const response = await apiClient.get('/calculationvariables');
                    const interest = response.data?.interest;
                    if (interest == null || !Number.isFinite(Number(interest)) || Number(interest) < 0) {
                        throw new Error('Kalkylränta saknas eller är ogiltig i registret.');
                    }
                    const updated = { ...snapshot, interest };
                    next = applyApprovalRows(updated, 'approveStorage', (row) => {
                        const { perUnit } = calculateStorageRow(row, updated, unitMultiplier, selectedUnit?.nrOfCalcDecimals ?? 0);
                        return recalculateSalesRow({ ...row, storage: perUnit }, updated, unitMultiplier);
                    });
                }
                if (currentDraftRef.current !== formDraft) {
                    throw new Error('Kalkylen ändrades under uppdateringen. Uppdatera och godkänn igen.');
                }
                if (!next) throw new Error('Kostnaderna kunde inte uppdateras och godkännas.');
                return next;
            };
            const next = component === 'all'
                ? await updateAndApproveAllSections(snapshot, updateSection)
                : await updateSection(component, snapshot);
            if (currentDraftRef.current !== snapshot) {
                throw new Error('Kalkylen ändrades under uppdateringen. Uppdatera och godkänn igen.');
            }
            if (!next) throw new Error('Kostnaderna kunde inte uppdateras och godkännas.');
            setDraft((previous) => previous === snapshot ? next : previous);
            warnings.forEach((warning) => appendMessage('warning', warning));
        } catch (approvalError) {
            console.error('Failed to update and approve calculation section:', approvalError);
            appendMessage('error', approvalError.response?.data?.message ?? approvalError.message ?? 'Kunde inte uppdatera och godkänna.');
        } finally {
            sectionActionRef.current = false;
            setSectionAction(null);
        }
    };

    const sectionApprovalProps = (component, flag) => ({
        onApprove: () => handleSectionApproval(component),
        approvalText: approvalTextFor(component, flag),
        actionDisabled: Boolean(sectionAction) || isSaving || isRefreshingFreight || isLoadingFreightFactors || hasPendingPartySelection,
        actionPending: sectionAction === component || sectionAction === 'all',
    });

    useEffect(() => {
        if (sectionAction || isSaving || isRefreshingFreight || isLoadingFreightFactors || freightFactorError
            || formDraft?.deactivateFreightCalculation || !pendingAutoFreightRefreshRef.current || !formDraft?.autoFreightPrice) return;
        if (formDraft.supplierFactoryId == null
            || (formDraft.customerDeliveryAddressId == null && (!formDraft.deliveryAddressCountryCode || formDraft.deliveryAddressPostalNr == null))) return;
        pendingAutoFreightRefreshRef.current = false;
        void handleRefreshFreightPrices({ silent: true });
    }, [
        formDraft?.autoFreightPrice,
        formDraft?.customerDeliveryAddressId,
        formDraft?.supplierFactoryId,
        formDraft?.deliveryAddressCountryCode,
        formDraft?.deliveryAddressPostalNr,
        handleRefreshFreightPrices,
        sectionAction,
        isSaving,
        isRefreshingFreight,
        isLoadingFreightFactors,
        freightFactorError,
        formDraft?.deactivateFreightCalculation,
    ]);

    // handleRefreshFreightPrices changes on every draft edit, so this effect doubles as a debounce.
    useEffect(() => {
        if (!pendingFreightRefreshKeys || isRefreshingFreight || isLoadingFreightFactors || freightFactorError || sectionAction || isSaving) return undefined;
        const timer = window.setTimeout(() => {
            setPendingFreightRefreshKeys(null);
            void handleRefreshFreightPrices({ rowKeys: pendingFreightRefreshKeys, silent: true });
        }, 400);
        return () => window.clearTimeout(timer);
    }, [handleRefreshFreightPrices, isRefreshingFreight, isLoadingFreightFactors, freightFactorError, pendingFreightRefreshKeys, sectionAction, isSaving]);

    const headerField = (label, field, numeric = false, integerOnly = false, maxLength) => (
        <SavedField
            label={label}
            value={formDraft?.[field]}
            numeric={numeric}
            integerOnly={integerOnly}
            editing
            onChange={updateField(field)}
            maxLength={maxLength}
        />
    );

    const headerFlag = (label, field) => (
        <SavedFlag
            label={label}
            checked={formDraft?.[field]}
            editing
            onChange={updateField(field)}
        />
    );

    const handleSave = async () => {
        if (!formDraft || hasPendingPartySelection || isSavingRef.current || sectionActionRef.current || isRefreshingFreight || isLoadingFreightFactors) return;

        isSavingRef.current = true;
        setIsSaving(true);
        clearMessages();
        try {
            const payload = toCalculationUpdateRequest(formDraft);
            const response = isNew
                ? await apiClient.post(`/calculations/from-product/${productId}`, payload)
                : await apiClient.put(`/calculations/${id}/aggregate`, payload);
            const savedData = response?.data;
            if (!(savedData?.id > 0)) {
                throw new Error('The calculation save response did not include a valid calculation ID.');
            }
            if (isNew) {
                skipUnsavedCheckRef.current = true;
                navigate(`/order/calculations/${savedData.id}`, { replace: true });
                return;
            }
            setResult({ key: requestKey, data: savedData, error: '' });
            setDraft(createCalculationDraft(savedData));
            setIsCustomerSelectionPending(false);
            setIsSupplierSelectionPending(false);
            setCustomerPickerReset((previous) => previous + 1);
            appendMessage('info', 'Kalkylen sparades.');
        } catch (saveError) {
            console.error('Failed to save calculation:', saveError);
            appendMessage('error', 'Kalkylen kunde inte sparas.');
        } finally {
            setIsSaving(false);
            isSavingRef.current = false;
        }
    };

    const handleUnsavedWarningConfirm = () => {
        setShowUnsavedWarning(false);
        blocker.proceed();
    };

    const handleUnsavedWarningAbort = () => {
        setShowUnsavedWarning(false);
        if (blocker.state === 'blocked') {
            blocker.reset();
        }
    };

    return (
        <div className="relative min-w-0 px-3 pb-10 md:px-8 py-2">
            <ConfirmationModal
                isOpen={Boolean(freightWarning && freightWarning.calculation === data)}
                onClose={() => setFreightWarning(null)}
                onConfirm={() => setFreightWarning(null)}
                title="Varning – fraktinställningar"
                message={freightWarning?.message}
                confirmText="OK"
                cancelText=""
            />
            <ConfirmationModal
                isOpen={showUnsavedWarning}
                onClose={handleUnsavedWarningAbort}
                onConfirm={handleUnsavedWarningConfirm}
                title="Osparade ändringar"
                message="Det finns osparade ändringar, vill du ändå fortsätta?"
                confirmText="Fortsätt ändå"
                cancelText="Avbryt"
                isDestructive={false}
            />
            <div
                className={`grid min-h-0 min-w-0 flex-1 gap-6 transition-[grid-template-columns] duration-300 ease-in-out ${expanded
                    ? 'lg:grid-cols-[320px_minmax(0,1fr)] lg:gap-8'
                    : 'lg:grid-cols-[0px_minmax(0,1fr)] lg:gap-6'
                    } lg:items-start`}
            >
                <div
                    id="calculation-info-panel"
                    className={`min-w-0 overflow-hidden lg:self-stretch lg:overflow-visible ${expanded ? 'lg:relative lg:after:absolute lg:after:top-8 lg:after:right-0 lg:after:bottom-0 lg:after:border-r lg:after:border-gray-300' : ''}`}
                >
                    <aside
                        className={`transform transition-all duration-300 ease-in-out lg:sticky lg:top-[calc(72px+1rem)] lg:z-10 lg:max-h-[calc(100dvh-72px-1rem)] lg:overflow-y-auto lg:self-start ${expanded ? 'translate-x-0 opacity-100' : '-translate-x-4 opacity-0 pointer-events-none'}`}
                        aria-hidden={!expanded}
                        inert={!expanded}
                    >
                        <div className="space-y-4 px-2 py-3">
                            <h2 className="text-center text-sm text-gray-500">Info</h2>
                            <dl className="space-y-2 text-xs text-gray-500">
                                <div className="grid grid-cols-[60px_minmax(0,1fr)] gap-x-1">
                                    <dt>Skapad:</dt>
                                    <dd className="flex min-w-0 items-center gap-1 whitespace-nowrap text-xs">
                                        <span className="shrink-0 w-26 text-gray-700 font-semibold">{formatDateTime(data?.createdAt) || '-'}</span>
                                        <span className="min-w-0 truncate" title={data?.createdByName || '-'}>
                                            {data?.createdByName || '-'}
                                        </span>
                                    </dd>
                                </div>
                                <div className="grid grid-cols-[60px_minmax(0,1fr)] gap-x-1">
                                    <dt>Redigerad:</dt>
                                    <dd className="flex min-w-0 items-center gap-1 whitespace-nowrap text-xs">
                                        <span className="shrink-0 w-26 text-gray-700 font-semibold">{formatDateTime(data?.editedAt) || '-'}</span>
                                        <span className="min-w-0 truncate" title={data?.editedByName || '-'}>
                                            {data?.editedByName || '-'}
                                        </span>
                                    </dd>
                                </div>
                            </dl>
                            <LabeledCheckbox
                                id="calculation-is-archived"
                                label="Arkiverad"
                                checked={Boolean(formDraft?.isArchived)}
                                onChange={updateField('isArchived')}
                                disabled={!formDraft || isSaving}
                                color="red"
                            />
                            <div className="border-b border-gray-300" />
                            <section aria-labelledby="calculation-messages-heading" className="space-y-3">
                                <h2 id="calculation-messages-heading" className="text-center text-sm text-gray-500">
                                    Meddelanden
                                </h2>
                                {panelMessages.length === 0 && !isCustomerSelectionPending ? (
                                    <p className="text-center text-xs font-light">Inga meddelanden</p>
                                ) : (
                                    <ul className="space-y-2">
                                        {panelMessages.map((message, index) => (
                                            <li
                                                key={`${message.type}-${index}`}
                                                role={message.type === 'error' || message.type === 'warning' ? 'alert' : 'status'}
                                                className={`border p-2 text-center text-xs ${message.type === 'error'
                                                    ? 'border-red-200 bg-red-100 text-red-700'
                                                    : message.type === 'warning'
                                                        ? 'border-amber-200 bg-amber-50 text-amber-800'
                                                        : 'border-gray-200 bg-green-100 text-green-700'
                                                    }`}
                                            >
                                                {message.text}
                                            </li>
                                        ))}
                                        {isCustomerSelectionPending ? (
                                            <li
                                                role="alert"
                                                className="border border-amber-200 bg-amber-50 p-2 text-center text-xs text-amber-800"
                                            >
                                                Välj en kund i listan eller rensa kundfältet innan du sparar.
                                            </li>
                                        ) : null}
                                    </ul>
                                )}
                            </section>
                            {data && <CalculationNavigation key={id} calculation={data} />}
                            {/* {data ? (
                                <section className="space-y-3" aria-labelledby="calculation-approvals-heading">
                                    <h2 id="calculation-approvals-heading" className="text-center text-sm text-gray-500">
                                        Godkännanden
                                    </h2>
                                    {data.approvals.map((approval) => (
                                        <div key={approval.component} className="text-xs text-gray-500">
                                            <div className="font-medium">
                                                {APPROVAL_LABELS[approval.component]}
                                            </div>
                                            <div>
                                                {approval.approvedAt
                                                    ? formatDateTime(approval.approvedAt)
                                                    : 'Ännu ej godkänd'}
                                            </div>
                                            {approval.approvedBy ? <div>{approval.approvedBy}</div> : null}
                                        </div>
                                    ))}
                                </section>
                            ) : null} */}
                        </div>
                    </aside>
                </div>

                <main className="@container min-w-0">
                    <div className="flex min-w-0 items-center gap-3 pb-1">
                        <button
                            type="button"
                            onClick={toggleInfoPanel}
                            className="inline-flex h-8 w-8 shrink-0 items-center justify-center text-gray-600 hover:bg-gray-50"
                            title={expanded ? 'Dölj informationspanel' : 'Visa informationspanel'}
                            aria-label={expanded ? 'Dölj informationspanel' : 'Visa informationspanel'}
                            aria-expanded={expanded}
                            aria-controls="calculation-info-panel"
                        >
                            {expanded ? <PanelLeftClose size={16} /> : <PanelLeftOpen size={16} />}
                        </button>
                        <h1 className="text-sm font-semibold uppercase text-gray-500">
                            {isNew ? 'Ny kalkyl' : `Kalkyl ${validId ? id : ''}`}
                        </h1>
                    </div>
                    <div className="mb-5 flex flex-wrap items-center gap-6">
                        <ActionButton label="Tillbaka" icon={ArrowLeft} onClick={handleBack} accent="slate" />
                        {/* <ActionButton
                            label="Läs in igen"
                            icon={RefreshCw}
                            onClick={() => setRetry((previous) => previous + 1)}
                            disabled={loading || !validId || isDirty || isSaving}
                        /> */}
                        <ActionButton
                            label="Spara"
                            icon={Save}
                            onClick={handleSave}
                            disabled={!data || isSaving || Boolean(sectionAction) || isRefreshingFreight || isLoadingFreightFactors || hasPendingPartySelection}
                            accent="lime"
                        />
                        <ActionButton
                            label={sectionAction === 'all' ? 'Uppdaterar och godkänner alla…' : 'Uppdatera och godkänn alla'}
                            icon={RefreshCw}
                            onClick={() => handleSectionApproval('all')}
                            disabled={!data || loading || isSaving || Boolean(sectionAction) || isRefreshingFreight || isLoadingFreightFactors || hasPendingPartySelection}
                        />
                        <ActionButton
                            label="Ny förfrågan"
                            icon={Send}
                            onClick={() => openCalculationDocument('/order/inquiries/new')}
                            disabled={isNew || !data || loading || isSaving || Boolean(sectionAction) || hasPendingPartySelection}
                            accent="teal"
                        />
                        <ActionButton
                            label="Nytt affärsförslag"
                            icon={FilePlus2}
                            onClick={() => openCalculationDocument('/order/quotations/new')}
                            disabled={isNew || !data || loading || isSaving || Boolean(sectionAction) || hasPendingPartySelection}
                            accent="teal"
                        />
                        <ActionButton
                            label="Ny beställning"
                            icon={ShoppingCart}
                            onClick={() => openCalculationDocument('/order/supplierorders/new')}
                            disabled={isNew || !data || loading || isSaving || Boolean(sectionAction) || hasPendingPartySelection}
                            accent="teal"
                        />
                    </div>
                    {loading ? (
                        <div aria-label="Laddar kalkyl" className="space-y-4">
                            <Skeleton height={110} />
                            <Skeleton height={180} />
                            <Skeleton height={160} />
                        </div>
                    ) : error ? (
                        <p role="alert" className="border-l-2 border-red-400 px-3 py-2 text-xs text-red-700">
                            {error}
                        </p>
                    ) : data ? (
                        <>
                            <div className="mb-5 w-280 rounded-sm border border-gray-200 bg-gradient-to-br from-slate-50 via-white to-slate-100 px-5 pt-3 pb-3 shadow-sm">
                                <dl className="min-w-0 flex flex-row gap-5">
                                    <div className="w-60 space-y-2">
                                        <ProductInfoRow label="Produkt" value={data.product?.name} />
                                        <ProductInfoRow label="Material" value={data.product?.material} />
                                    </div>
                                    <div className="w-60 space-y-2">
                                        <ProductInfoRow label="Konstruktion" value={data.product?.construction} />
                                        <ProductInfoRow label="Format" value={productDimensions || data.product?.format} />
                                    </div>
                                    <div className="w-30 space-y-2">
                                        <ProductInfoRow label="Färger" value={data.product?.colors} />
                                        <ProductInfoRow
                                            label="Lack"
                                            value={data.product?.varnishOther
                                                ? `${data.product.varnish || 'Övrig'}: ${data.product.varnishOther}`
                                                : data.product?.varnish}
                                        />
                                    </div>
                                    <div className="w-40 space-y-2">
                                        <ProductInfoRow
                                            label="Not. intern"
                                            value={data.product?.noteInternal || '-'}
                                        />
                                        <ProductInfoRow
                                            label="Not. extern"
                                            value={data.product?.noteExternal || '-'}
                                        />
                                    </div>
                                    <div>
                                        {data.product ? (
                                            <div className="mb-4 flex justify-end">
                                                <Link
                                                    to={`/order/products/${data.product.id}`}
                                                    target="_blank"
                                                    rel="noopener noreferrer"
                                                    className="inline-flex items-center gap-1 text-xs text-blue-800 hover:underline"
                                                >
                                                    Öppna produkt i ny flik
                                                    <ExternalLink size={14} aria-hidden="true" />
                                                </Link>
                                            </div>
                                        ) : null}

                                    </div>
                                </dl>
                            </div>

                            <CalculationSection title="Kalkylrader" initiallyOpen>
                                <div className="mb-5 flex flex-wrap gap-x-15 gap-y-10">
                                    <div className="w-80 min-w-0 space-y-1 text-xs">
                                        <div className="flex min-w-0 items-center gap-3">
                                            <span className="w-20 shrink-0 text-gray-500">Kund</span>
                                            <SearchCustomer
                                                key={`${formDraft?.customer?.id ?? ''}-${customerPickerReset}`}
                                                onCustomerSelect={handleCalculationCustomerSelect}
                                                onPendingSelectionChange={handleCustomerSelectionPending}
                                                selectedCustomerName={formDraft?.customer?.name ?? ''}
                                                selectedCustomerId={formDraft?.customer?.id}
                                                placeholder="Sök kund"
                                                width="w-full"
                                                className="min-w-0 flex-1"
                                            />
                                        </div>
                                        <div className="flex min-w-0 items-center gap-3">
                                            <span className="w-20 shrink-0 text-gray-500">Leverantör</span>
                                            <SearchSupplier
                                                key={`${formDraft?.supplier?.id ?? ''}-${customerPickerReset}`}
                                                onSupplierSelect={handleCalculationSupplierSelect}
                                                onPendingSelectionChange={handleSupplierSelectionPending}
                                                selectedSupplierName={formDraft?.supplier?.name ?? ''}
                                                selectedSupplierId={formDraft?.supplier?.id}
                                                placeholder="Sök leverantör"
                                                width="w-full"
                                                className="min-w-0 flex-1"
                                            />
                                        </div>
                                        <div className='mt-5'>
                                            {headerFlag('Justera påslag', 'lockOutPrice')}
                                        </div>

                                    </div>

                                    <div className="w-150 shrink-0">
                                        <div className="grid min-w-0 text-xs grid-cols-[90px_minmax(80px,1fr)_50px_70px_125px_minmax(180px,1.2fr)] space-x-1 items-center">
                                            <label htmlFor="calculation-unit" className="text-gray-500 @xl:col-start-1 @xl:row-start-1">Enhet</label>
                                            <CompactSelect
                                                ariaLabel="Enhet"
                                                value={formDraft?.unitId}
                                                items={unitOptions.map((unit) => ({
                                                    ...unit,
                                                    name: unit.name ? `${unit.name}` : unit.multiplicator,
                                                }))}
                                                onChange={(value) => updateDraft('unitId', value ? Number(value) : null)}
                                                className="col-start-2 col-span-2 row-start-1"
                                            />
                                            <div className="hidden text-center text-gray-500 @xl:col-start-4 @xl:row-start-1 @xl:block text-center">Decimaler</div>
                                            <label htmlFor="purchase-currency" className="text-gray-500 @xl:col-start-1 @xl:row-start-2">Inköpsvaluta</label>
                                            <CompactSelect
                                                ariaLabel="Inköpsvaluta"
                                                value={formDraft?.purchaseCurrencyId}
                                                items={currencyOptions}
                                                onChange={(value) => handleCurrencyChange('purchase', value)}
                                                className="col-start-2 row-start-2"
                                            />
                                            <CompactNumberInput
                                                ariaLabel="Inköpskurs"
                                                value={formDraft?.purchaseCurrencyRate}
                                                onChange={updateField('purchaseCurrencyRate')}
                                                className="col-start-3 row-start-2"
                                                inputWidth="w-full"
                                            />
                                            <CompactNumberInput
                                                ariaLabel="Decimaler inköp"
                                                value={formDraft?.purchaseDecimals}
                                                onChange={updateField('purchaseDecimals')}
                                                integerOnly
                                                centerText
                                                className="col-start-4 row-start-2 px-2"
                                                inputWidth="w-full"
                                            />
                                            <button
                                                type="button"
                                                onClick={() => handleCurrencyRefresh('purchase')}
                                                className="inline-flex h-7 items-center justify-center gap-1 rounded-sm px-2 text-tiny font-semibold text-gray-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-sky-700 col-start-5 row-start-2"
                                            >
                                                <RefreshCw size={14} aria-hidden="true" />
                                                UPPDATERA
                                            </button>
                                            <p className="min-w-0 text-xs text-red-600 @xl:col-start-6 @xl:row-start-2" role={purchaseCurrencyWarning ? 'status' : undefined}>
                                                {purchaseCurrencyWarning}
                                            </p>

                                            <label htmlFor="sales-currency" className="text-gray-500 col-start-1 row-start-3">Fsg.valuta</label>
                                            <CompactSelect
                                                ariaLabel="Försäljningsvaluta"
                                                value={formDraft?.salesCurrencyId}
                                                items={currencyOptions}
                                                onChange={(value) => handleCurrencyChange('sales', value)}
                                                className="col-start-2 row-start-3"
                                            />
                                            <CompactNumberInput
                                                ariaLabel="Försäljningskurs"
                                                value={formDraft?.salesCurrencyRate}
                                                onChange={updateField('salesCurrencyRate')}
                                                className="col-start-3 row-start-3"
                                                inputWidth="w-full"
                                            />
                                            <CompactNumberInput
                                                ariaLabel="Decimaler försäljning"
                                                value={formDraft?.salesDecimals}
                                                onChange={updateField('salesDecimals')}
                                                integerOnly
                                                centerText
                                                className="col-start-4 row-start-3 px-2"
                                                inputWidth="w-full"
                                            />
                                            <button
                                                type="button"
                                                onClick={() => handleCurrencyRefresh('sales')}
                                                className="inline-flex h-7 items-center justify-center gap-1 rounded-sm px-2 text-tiny font-semibold text-gray-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-sky-700 col-start-5 row-start-3"
                                            >
                                                <RefreshCw size={14} aria-hidden="true" />
                                                UPPDATERA
                                            </button>
                                            <p className="min-w-0 text-xs text-red-600 @xl:col-start-6 @xl:row-start-3" role={salesCurrencyWarning ? 'status' : undefined}>
                                                {salesCurrencyWarning}
                                            </p>

                                            <label htmlFor="calculation-currency-rate" className="text-gray-500 col-start-1 row-start-4 col-span-2">Omräkning fr. inköpsvaluta</label>
                                            <CompactNumberInput
                                                ariaLabel="Omräkning från inköpsvaluta"
                                                value={formDraft?.currencyRate}
                                                onChange={updateField('currencyRate')}
                                                className="@xl:col-start-3 @xl:row-start-4"
                                                inputWidth="w-full"
                                            />
                                        </div>

                                    </div>

                                    <div className="w-60">
                                        <label className="shrink-0 text-xs text-gray-500">Allmän info</label>
                                        <LabeledTextArea
                                            value={formDraft?.calculationFollowUp ?? ''}
                                            onChange={updateField('calculationFollowUp')}
                                            height="h-16"
                                            maxLength={255}
                                            margintop="1"
                                        />
                                        {/* <LabeledTextArea
                                            label="Övrig kostnadsnotering"
                                            labelWidth="w-20"
                                            value={formDraft?.otherCostNote ?? ''}
                                            onChange={updateField('otherCostNote')}
                                            height="h-20"
                                            margintop="0"
                                            maxLength={1000}
                                        /> */}
                                        {/* <div className='mt-5'>
                                            <SegmentedFilter
                                                value={rowView}
                                                onChange={setRowView}
                                                options={VIEW_OPTIONS}
                                                theme="sky"
                                            />
                                        </div> */}
                                    </div>
                                </div>

                                <div className="pb-5">
                                    <CalculationRowsTable
                                        rows={rows}
                                        columns={SALES_COLUMNS}
                                        purchaseDecimals={formDraft.purchaseDecimals}
                                        salesDecimals={formDraft.salesDecimals}
                                        lockOutPrice={formDraft.lockOutPrice}
                                        deactivateFreightCalculation={formDraft.deactivateFreightCalculation}
                                        autoFreightCalc={formDraft.autoFreightCalc}
                                        editionPerPallet={formDraft.editionPerPallet}
                                        onRowChange={handleCalculationRowChange}
                                        onFreightRowChange={handleFreightRowChange}
                                        onAdjustEdition={handleAdjustFreightEdition}
                                        onDeleteRow={handleDeleteCalculationRow}
                                    />
                                </div>
                            </CalculationSection>

                            <CalculationSection title="Packning" initiallyOpen {...sectionApprovalProps('packaging', 'approvePackaging')}>
                                <div className="min-w-0 space-y-4 pb-2">

                                    <div className="grid min-w-0 gap-12 @4xl:grid-cols-[minmax(0,1.15fr)_minmax(0,1fr)_minmax(260px,0.75fr)]">
                                        <div className="min-w-0">
                                            <LabeledSelect
                                                label="Pall/packning"
                                                labelWidth="w-25"
                                                inputWidth="w-75"
                                                margintop="0"
                                                value={formDraft?.palletFormatId ?? ''}
                                                items={palletFormatOptions}
                                                onChange={handlePalletFormatChange}
                                                name="palletFormatId"
                                            />
                                            <div className="min-w-0 w-120 grid grid-cols-[auto_minmax(0,1fr)] items-center mt-2">
                                                <div className="w-35 text-xs text-gray-700">Yttermått pall LxBxH mm</div>
                                                <div className="grid min-w-0 grid-cols-3 gap-1">
                                                    <CompactNumberInput
                                                        ariaLabel="Pall längd"
                                                        value={formDraft?.palletLength}
                                                        onChange={updateField('palletLength')}
                                                        integerOnly
                                                        suffix="mm"
                                                        inputWidth="w-full"
                                                        suffixPaddingWidth={40}
                                                    />
                                                    <CompactNumberInput
                                                        ariaLabel="Pall bredd"
                                                        value={formDraft?.palletWidth}
                                                        onChange={updateField('palletWidth')}
                                                        integerOnly
                                                        className=""
                                                        suffix="mm"
                                                        inputWidth="w-full"
                                                        suffixPaddingWidth={40}
                                                    />
                                                    <CompactNumberInput
                                                        ariaLabel="Pall höjd"
                                                        value={formDraft?.palletHeight}
                                                        onChange={updateField('palletHeight')}
                                                        integerOnly
                                                        className=""
                                                        suffix="mm"
                                                        inputWidth="w-full"
                                                        suffixPaddingWidth={40}
                                                    />
                                                </div>
                                            </div>

                                            <div className="min-w-0 w-120 grid grid-cols-[auto_minmax(0,1fr)] items-center">
                                                <div className="w-35 text-xs text-gray-700">Antal/pall</div>
                                                <div className="grid min-w-0 grid-cols-3 items-center gap-1">
                                                    <CompactNumberInput
                                                        ariaLabel="Antal per pall"
                                                        value={formDraft?.editionPerPallet}
                                                        onChange={updateField('editionPerPallet')}
                                                        integerOnly
                                                    />
                                                    <div className="col-span-2 pl-3">
                                                        <LabeledCheckbox
                                                            label="Staplingsbar"
                                                            checked={Boolean(formDraft?.palletIsStackable)}
                                                            onChange={updateField('palletIsStackable')}
                                                            color="cyan"
                                                        />
                                                    </div>
                                                </div>
                                            </div>

                                            <div className="min-w-0 w-120 grid grid-cols-[auto_minmax(0,1fr)] items-center">
                                                <LabeledCheckbox
                                                    label="Buntas"
                                                    checked={Boolean(formDraft?.doBuntas)}
                                                    onChange={updateField('doBuntas')}
                                                    color="cyan"
                                                    className="w-35"
                                                />
                                                <div className="grid min-w-0 grid-cols-[120px_auto_minmax(0,1fr)] items-center gap-2">
                                                    <CompactNumberInput
                                                        ariaLabel="Antal per bunt"
                                                        value={formDraft?.nrOfPerBundle}
                                                        onChange={updateField('nrOfPerBundle')}
                                                        integerOnly
                                                        suffix="ant/bunt"
                                                        inputWidth="w-30"
                                                        suffixPaddingWidth={64}
                                                        disabled={!Boolean(formDraft?.doBuntas)}
                                                    />
                                                    <span className="col-span-2 text-xs text-red-600">{bundlesPerPallet.count ? bundlesPerPallet.count + ' bunt/pall' : ''}</span>
                                                </div>
                                            </div>
                                            {/* <div className="min-w-0 w-120 grid grid-cols-[auto_minmax(0,1fr)] items-center">
                                                <div className="w-35" />
                                                <div className="grid min-w-0 grid-cols-3 items-center gap-1">
                                                    <CompactNumberInput
                                                        ariaLabel="Buntar per pall"
                                                        value={bundlesPerPallet.count}
                                                        integerOnly
                                                        disabled
                                                        suffix="bunt/pall"
                                                        style={{ color: bundlesPerPallet.uneven ? '#dc2626' : undefined }}
                                                    />
                                                </div>
                                            </div> */}

                                            <div className="min-w-0 w-120 grid grid-cols-[auto_minmax(0,1fr)] items-center">
                                                <LabeledCheckbox
                                                    label="Ytterförpackning"
                                                    checked={Boolean(formDraft?.isYtterforpackning)}
                                                    onChange={updateField('isYtterforpackning')}
                                                    color="cyan"
                                                    className="w-35"
                                                />
                                                <div className="grid min-w-0 grid-cols-[120px_auto_minmax(0,1fr)] items-center gap-2">
                                                    <CompactNumberInput
                                                        ariaLabel="Antal per ytterförpackning"
                                                        value={formDraft?.nrOfPerOuterPackaging}
                                                        onChange={updateField('nrOfPerOuterPackaging')}
                                                        integerOnly
                                                        suffix="ant/förp."
                                                        inputWidth="w-30"
                                                        suffixPaddingWidth={64}
                                                        disabled={!Boolean(formDraft?.isYtterforpackning)}
                                                    />
                                                    <span className="col-span-2 text-xs text-red-600">{outerPackagesPerPallet.count ? outerPackagesPerPallet.count + ' ytterförp/pall' : ''}</span>
                                                </div>
                                            </div>
                                            {/* <div className="min-w-0 w-120 grid grid-cols-[auto_minmax(0,1fr)] items-center">
                                                <div className="w-35" />
                                                <div className="grid min-w-0 grid-cols-3 items-center gap-1">
                                                    <CompactNumberInput
                                                        ariaLabel="Ytterförpackningar per pall"
                                                        value={outerPackagesPerPallet.count}
                                                        integerOnly
                                                        disabled
                                                        suffix="ytterförp/pall"
                                                        style={{ color: outerPackagesPerPallet.uneven ? '#dc2626' : undefined }}
                                                    />
                                                </div>
                                            </div> */}
                                        </div>

                                        <div className="min-w-0">
                                            <div className="flex items-center justify-between">
                                                <label className="text-xs text-gray-700 w-35 pl-1">Packinstruktion</label>
                                                <LabeledCheckbox
                                                    label="Automatisk packinstruktion"
                                                    checked={Boolean(formDraft?.automaticLoadingInstruction)}
                                                    onChange={updateField('automaticLoadingInstruction')}
                                                    color="cyan"
                                                />
                                            </div>
                                            <LabeledTextArea
                                                label=""
                                                value={formDraft?.loadingInstruction ?? ''}
                                                onChange={updateField('loadingInstruction')}
                                                height="h-16"
                                                margintop="1"
                                                maxLength={255}
                                                disabled={Boolean(formDraft?.automaticLoadingInstruction)}
                                            />
                                            <LabeledTextArea
                                                label=""
                                                labelWidth="w-0"
                                                value={formDraft?.freightNote ?? ''}
                                                onChange={updateField('freightNote')}
                                                height="h-10"
                                                margintop="1"
                                                maxLength={255}
                                            />
                                        </div>

                                        <div className="min-w-0 w-50">
                                            <div className="flex items-center justify-between">
                                                <label className="text-xs text-gray-700">Pallpriser</label>
                                                <div className="flex items-center gap-2 pt-1">
                                                    <span className="text-xs text-gray-700">Deb</span>
                                                    <LabeledCheckbox
                                                        label=""
                                                        checked={Boolean(formDraft?.doDebitPalletPrice)}
                                                        onChange={updateField('doDebitPalletPrice')}
                                                        ariaLabel="Debitera pall"
                                                        color="cyan"
                                                    />
                                                </div>
                                            </div>
                                            <div className="grid min-w-0 grid-cols-[auto_minmax(0,1fr)_80px] items-center gap-2">
                                                <span className="w-6 text-xs text-gray-700">In</span>
                                                <CompactNumberInput
                                                    ariaLabel="Pallpris in"
                                                    value={formDraft?.palletPriceIn}
                                                    onChange={updateField('palletPriceIn')}
                                                    suffix={purchaseCurrency?.name ?? ''}
                                                />
                                                {/* <span className="text-xs text-gray-600">{purchaseCurrency?.name ?? ''}</span> */}
                                                <span className="text-xs text-gray-600 text-right">{numberText(palletPriceInSek, 2)} SEK</span>
                                            </div>
                                            <div className="grid min-w-0 grid-cols-[auto_minmax(0,1fr)_80px] items-center gap-2">
                                                <span className="w-6 text-xs text-gray-700">Ut</span>
                                                <CompactNumberInput
                                                    ariaLabel="Pallpris ut"
                                                    value={formDraft?.palletPriceOut}
                                                    onChange={updateField('palletPriceOut')}
                                                    className=""
                                                    suffix={salesCurrency?.name ?? ''}
                                                />
                                                {/* <span className="text-xs text-gray-600">{salesCurrency?.name ?? ''}</span> */}
                                                <span className="text-xs text-gray-600 text-right">{numberText(palletPriceOutSek, 2)} SEK</span>
                                            </div>
                                            <div className="flex items-center gap-2 pt-1 justify-end">
                                                {/* <span className="text-xs text-gray-700">Deb</span>
                                                <LabeledCheckbox
                                                    label=""
                                                    checked={Boolean(formDraft?.doDebitPalletPrice)}
                                                    onChange={updateField('doDebitPalletPrice')}
                                                    ariaLabel="Debitera pall"
                                                    color="gray"
                                                /> */}
                                                <span className="text-xs text-gray-600 font-bold mt-1 text-right">= {numberText(palletDebitDifference, 0)} SEK/Pall</span>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </CalculationSection>

                            <CalculationSection title="Fraktberäkning" {...sectionApprovalProps('freight', 'approveFreight')}>
                                <div className="min-w-0 space-y-4 pb-2">
                                    <div className="mb-4 flex flex-wrap gap-5">
                                        <LabeledCheckbox
                                            label="Deaktivera frakt"
                                            checked={Boolean(formDraft?.deactivateFreightCalculation)}
                                            onChange={handleFreightModeChange('deactivateFreightCalculation')}
                                            disabled={Boolean(sectionAction) || isSaving}
                                            color="red"
                                        />
                                        <LabeledCheckbox
                                            label="Automatisk fraktberäkning"
                                            checked={Boolean(formDraft?.autoFreightCalc)}
                                            onChange={handleFreightModeChange('autoFreightCalc')}
                                            disabled={Boolean(formDraft?.deactivateFreightCalculation || sectionAction || isSaving)}
                                            color="red"
                                        />
                                        <LabeledCheckbox
                                            label="Automatiskt fraktpris"
                                            checked={Boolean(formDraft?.autoFreightPrice)}
                                            disabled={Boolean(formDraft?.deactivateFreightCalculation || sectionAction || isSaving)}
                                            onChange={handleFreightModeChange('autoFreightPrice')}
                                            color="red"
                                        />
                                    </div>
                                    {isLoadingFreightFactors ? <p role="status" className="text-xs text-gray-600">Beräknar pallfaktorer…</p> : null}
                                    {freightFactorError ? <p role="alert" className="text-xs text-red-600">{freightFactorError}</p> : null}

                                    <div className="mb-5 min-w-0 pb-1">
                                        <div className="flex flex-wrap gap-20">
                                            <div className="w-80">
                                                <label className="mb-1 block text-xs text-gray-700">Leveransinformation</label>
                                                <CompactSelect
                                                    ariaLabel="Fabrik för fraktberäkning"
                                                    value={formDraft?.supplierFactoryId ?? ''}
                                                    items={supplierFactoryItems}
                                                    onChange={handleSupplierFactoryChange}
                                                    isDisabled={!formDraft?.autoFreightPrice || formDraft?.deactivateFreightCalculation}
                                                />
                                                <CompactSelect
                                                    ariaLabel="Leveransadress för fraktberäkning"
                                                    value={formDraft?.customerDeliveryAddressId ?? ''}
                                                    items={deliveryAddressItems}
                                                    onChange={handleDeliveryAddressChange}
                                                    isDisabled={!formDraft?.autoFreightPrice || formDraft?.deactivateFreightCalculation}
                                                    openInNewTabUrl={formDraft?.customerDeliveryAddressId
                                                        ? `/order/customers/${formDraft.customer?.id}`
                                                        : undefined}
                                                    openInNewTabLabel="Öppna kundens leveransadress i ny flik"
                                                />
                                                <div className="grid min-w-0 grid-cols-[160px_minmax(0,1fr)] gap-1">
                                                    <LabeledInput
                                                        label=""
                                                        labelWidth="w-0"
                                                        margintop="0"
                                                        value={formDraft?.deliveryAddressCountryCode ?? ''}
                                                        onChange={updateField('deliveryAddressCountryCode')}
                                                        disabled={!formDraft?.autoFreightPrice || formDraft?.deactivateFreightCalculation}
                                                        aria-label="Leveranslandskod"
                                                    />
                                                    <CompactNumberInput
                                                        ariaLabel="Leveranspostnummer"
                                                        value={formDraft?.deliveryAddressPostalNr}
                                                        onChange={updateField('deliveryAddressPostalNr')}
                                                        integerOnly
                                                        disabled={!formDraft?.autoFreightPrice || formDraft?.deactivateFreightCalculation}
                                                    />
                                                </div>
                                            </div>
                                            <div className="max-w-120 flex flex-wrap gap-2">
                                                <FreightHeaderField
                                                    label="Kr/ppl"
                                                    value={formDraft?.freightPerPallet}
                                                    onChange={handleFreightDefaultChange('freightPerPallet', 'freightInternationalPerPallet')}
                                                    disabled={Boolean(formDraft?.autoFreightPrice || formDraft?.deactivateFreightCalculation)}
                                                />
                                                <FreightHeaderField
                                                    label="Lossn/lev"
                                                    value={formDraft?.loadingPerDelivery}
                                                    onChange={handleFreightDefaultChange('loadingPerDelivery', 'freightUnloading')}
                                                    disabled={Boolean(formDraft?.deactivateFreightCalculation)}
                                                />
                                                <FreightHeaderField
                                                    label="Kr/bil"
                                                    value={formDraft?.freightPerVehicle}
                                                    onChange={handleFreightDefaultChange('freightPerVehicle', 'freightPerVehicle')}
                                                    disabled={Boolean(formDraft?.autoFreightPrice || formDraft?.deactivateFreightCalculation)}
                                                />
                                                <FreightHeaderField
                                                    label="Pall/bil"
                                                    value={formDraft?.palletsPerTruck}
                                                    integerOnly
                                                    disabled
                                                />
                                                <FreightHeaderField
                                                    label="Pallfaktor"
                                                    value={formDraft?.palletCalcFactor}
                                                    onChange={handleFreightDefaultChange('palletCalcFactor', 'palletCalcFactor')}
                                                    disabled={Boolean(formDraft?.autoFreightPrice || formDraft?.deactivateFreightCalculation)}
                                                />
                                            </div>
                                        </div>
                                    </div>

                                    <FreightRowsTable
                                        rows={rows}
                                        calculation={formDraft}
                                        addressItems={rowAddressItems}
                                        onRowChange={handleFreightRowChange}
                                        onAdjustEdition={handleAdjustFreightEdition}
                                    />
                                    {rows
                                        .filter((row) => row.priceListSummary)
                                        .map((row) => (
                                            <details key={row.id} className="mt-3 text-xs text-gray-600">
                                                <summary className="cursor-pointer">
                                                    Prisunderlag · {numberText(row.edition, 0)}
                                                </summary>
                                                <pre className="mt-2 whitespace-pre-wrap break-words font-sans">
                                                    {row.priceListSummary}
                                                </pre>
                                            </details>
                                        ))}
                                </div>
                            </CalculationSection>

                            <CalculationSection title="Övriga kostnader" className="max-w-300" {...sectionApprovalProps('other', 'approveOtherCosts')}>
                                <div className="min-w-0 pb-2">
                                    {otherCostWarning ? (
                                        <p role="status" className="mb-4 text-xs text-red-600">{otherCostWarning}</p>
                                    ) : null}
                                    <CalculationCostsTable
                                        rows={rows}
                                        calculation={formDraft}
                                        unitMultiplier={otherCostUnitMultiplier}
                                        costOptions={costOptions}
                                        currencyOptions={currencyOptions}
                                        onCostChange={handleCostChange}
                                        onAddCost={handleAddCost}
                                        onRemoveCost={handleRemoveCost}
                                    />
                                </div>
                            </CalculationSection>

                            <CalculationSection title="Lagringskostnader" className="max-w-300" {...sectionApprovalProps('storage', 'approveStorage')}>
                                <div className="min-w-0 pb-2">
                                    <div className="mb-5 flex flex-wrap gap-15">
                                        <div className="min-w-0 w-60">
                                            {headerField('Lossning/pall', 'loadingPerPallet', true)}
                                            {headerField('Lagring/pall/mån', 'storagePerM2', true)}
                                        </div>
                                        <div className="min-w-0">
                                            <SavedField label="Årskalkylränta" value={formDraft?.interest} numeric />
                                            {headerField('Frakt till kund', 'freightFromStockPerPallet', true)}
                                        </div>
                                        {/* {headerFlag('Beräkna lagring', 'calcStorage')} */}
                                    </div>
                                    <StorageRowsTable
                                        rows={rows}
                                        calculation={formDraft}
                                        unitMultiplier={storageUnitMultiplier}
                                        decimals={storageUnitDecimals}
                                        onRowChange={handleStorageRowChange}
                                    />
                                </div>
                            </CalculationSection>

                            <CalculationSection title="Historik" initiallyOpen={true}>
                                <div className="pb-3">
                                    <CalculationHistoryTable
                                        rows={historyRows}
                                        purchaseDecimals={formDraft.purchaseDecimals}
                                        salesDecimals={formDraft.salesDecimals}
                                        isLoading={historyLoading}
                                        error={historyError}
                                        onEdit={(row) => {
                                            historyFocusRef.current = row.customerOrderId ? `customer-${row.customerOrderId}` : `supplier-${row.supplierOrderId}`;
                                            setEditingHistoryRow(row);
                                        }}
                                    />
                                </div>
                            </CalculationSection>
                        </>
                    ) : null}
                </main>
            </div>
            {editingHistoryRow && (
                <CalculationRowEditModal
                    key={editingHistoryRow.supplierOrderId}
                    supplierOrderId={editingHistoryRow.supplierOrderId}
                    supplierOrderNr={editingHistoryRow.supplierOrderNr}
                    onClose={closeHistoryEditor}
                />
            )}
        </div>
    );
};

const Calculation = () => {
    const { id } = useParams();
    const [searchParams] = useSearchParams();
    const productId = searchParams.get('productId');
    return <CalculationPage key={`${id}:${productId ?? ''}`} id={id} productId={productId} />;
};

export default Calculation;
