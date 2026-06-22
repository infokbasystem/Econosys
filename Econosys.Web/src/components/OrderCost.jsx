import React, { useEffect, useMemo, useState } from 'react';
import { createPortal } from 'react-dom';
import { ArrowLeftCircle, ArrowRightCircle, CircleHelp, CheckCircle, Trash2 } from 'lucide-react';
import apiClient from '../config/apiClient';
import { formatDateTime } from '../helpers/dateUtils';

import LabeledInput from './LabeledInput';
import LabeledSwitch from './LabeledSwitch';
import NumberInput from './NumberInput';

const toInputValue = (value) => (value == null ? '' : String(value));

const parseNullableInt = (value) => {
    if (value == null || value === '') return null;
    const parsed = Number.parseInt(String(value), 10);
    return Number.isFinite(parsed) ? parsed : null;
};

const parseNullableDecimal = (value) => {
    if (value == null || value === '') return null;
    const normalized = String(value).trim().replace(',', '.');
    if (!normalized) return null;
    const parsed = Number(normalized);
    return Number.isFinite(parsed) ? parsed : null;
};

const getPalletFormatCustomerPrice = (palletFormatOption) => parseNullableDecimal(
    palletFormatOption?.customerPrice ?? palletFormatOption?.CustomerPrice,
);

const NO_SUPPLIER_ORDER_KEY = '__no-supplier-order__';

const getSupplierOrderGroupKey = (supplierOrderId) => (
    supplierOrderId == null ? NO_SUPPLIER_ORDER_KEY : String(supplierOrderId)
);

const getSupplierOrderLabel = (supplierOrderId) => (
    supplierOrderId == null ? 'Ingen order' : `(${supplierOrderId})`
);

const makeTempId = () => `temp-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;

const buildAttestRatesLabel = (purchaseRate, salesRate) => {
    const purchase = purchaseRate == null ? '' : String(purchaseRate);
    const sales = salesRate == null ? '' : String(salesRate);
    if (!purchase && !sales) return '';
    return `${purchase}${purchase && sales ? ' / ' : ''}${sales}`;
};

const createDraftRow = ({ quotationId, customerOrderId, supplierOrderId }) => ({
    localId: '__draft__',
    id: null,
    quotationId: quotationId ?? null,
    customerOrderId: customerOrderId ?? null,
    supplierOrderId: supplierOrderId ?? null,
    costId: null,
    doDebit: false,
    nrOf: '',
    inPrice: '',
    inPriceCurrencyId: null,
    inPriceAttested: '',
    outPrice: '',
    markup: null,
    note: '',
    supplierName: '',
    invoiceId: null,
    invoiceRowId: null,
    doPrintOnQuotation: true,
    doPrintOnCustomerOrder: false,
    doPrintOnSupplierOrder: false,
    doInvoiceSeparately: false,
    doInvoiceSeparatelyImmediately: false,
    isCostInvoicedSeparately: false,
});

const createDetailsDraft = (row) => ({
    id: row?.id ?? null,
    supplierName: row?.supplierName ?? '',
    note: row?.note ?? '',
    doPrintOnCustomerOrder: Boolean(row?.doPrintOnCustomerOrder),
    doPrintOnSupplierOrder: Boolean(row?.doPrintOnSupplierOrder),
    doPrintOnQuotation: Boolean(row?.doPrintOnQuotation),
    inPriceAttested: toInputValue(row?.inPriceAttested),
    doInvoiceSeparately: Boolean(row?.doInvoiceSeparately),
    doInvoiceSeparatelyImmediately: Boolean(row?.doInvoiceSeparatelyImmediately),
    createdByName: row?.createdByName ?? '',
    createdDateTime: row?.createdDateTime ?? null,
    editedByName: row?.editedByName ?? '',
    editedDateTime: row?.editedDateTime ?? null,
    attestedByName: row?.attestedByName ?? row?.atttestedBySignature ?? '',
    attestedDateTime: row?.attestedDateTime ?? null,
    attestedInPriceCurrencyRate: row?.attestedInPriceCurrencyRate ?? null,
    attestedOutPriceCurrencyRate: row?.attestedOutPriceCurrencyRate ?? null,
});

const OrderCost = ({
    quotationId,
    customerOrderId,
    supplierOrderId,
    rows,
    costOptions,
    currencyOptions,
    palletFormatId,
    eurPallet,
    nrOfEurPallet,
    eurPalletValue,
    palletFormatOptions,
    onPalletChange,
    onChange,
}) => {
    const [detailsLocalId, setDetailsLocalId] = useState(null);
    const [detailsDraft, setDetailsDraft] = useState(null);
    const [detailsLoading, setDetailsLoading] = useState(false);
    const [detailsSaving, setDetailsSaving] = useState(false);
    const [detailsAttesting, setDetailsAttesting] = useState(false);
    const [detailsError, setDetailsError] = useState('');
    const [activeGroupKey, setActiveGroupKey] = useState(null);

    const supplierOrderGroups = useMemo(() => {
        const groupsMap = new Map();

        rows.forEach((row) => {
            const groupSupplierOrderId = row.supplierOrderId ?? null;
            const key = getSupplierOrderGroupKey(groupSupplierOrderId);

            if (!groupsMap.has(key)) {
                groupsMap.set(key, {
                    key,
                    supplierOrderId: groupSupplierOrderId,
                    rows: [],
                });
            }

            groupsMap.get(key).rows.push(row);
        });

        if (groupsMap.size === 0) {
            const fallbackSupplierOrderId = supplierOrderId ?? null;
            const fallbackKey = getSupplierOrderGroupKey(fallbackSupplierOrderId);

            groupsMap.set(fallbackKey, {
                key: fallbackKey,
                supplierOrderId: fallbackSupplierOrderId,
                rows: [],
            });
        }

        return Array.from(groupsMap.values());
    }, [rows, supplierOrderId]);

    useEffect(() => {
        if (!supplierOrderGroups.length) {
            setActiveGroupKey(null);
            return;
        }

        if (!activeGroupKey || !supplierOrderGroups.some((group) => group.key === activeGroupKey)) {
            setActiveGroupKey(supplierOrderGroups[supplierOrderGroups.length - 1].key);
        }
    }, [supplierOrderGroups, activeGroupKey]);

    const activeGroupIndex = useMemo(() => {
        if (!supplierOrderGroups.length) return -1;
        const foundIndex = supplierOrderGroups.findIndex((group) => group.key === activeGroupKey);
        return foundIndex >= 0 ? foundIndex : supplierOrderGroups.length - 1;
    }, [supplierOrderGroups, activeGroupKey]);

    const activeGroup = activeGroupIndex >= 0
        ? supplierOrderGroups[activeGroupIndex]
        : null;

    const rowsWithDraft = useMemo(() => {
        if (!activeGroup) return [];

        return [
            ...activeGroup.rows,
            createDraftRow({
                quotationId,
                customerOrderId,
                supplierOrderId: activeGroup.supplierOrderId,
            }),
        ];
    }, [activeGroup, quotationId, customerOrderId]);

    const sortedCostOptions = useMemo(() => {
        if (!Array.isArray(costOptions)) return [];
        return [...costOptions].sort((a, b) => {
            if (a.isActive === b.isActive) return (a.name || '').localeCompare(b.name || '');
            return a.isActive ? -1 : 1;
        });
    }, [costOptions]);

    const activeCostOptions = useMemo(
        () => sortedCostOptions.filter((o) => o.isActive),
        [sortedCostOptions],
    );

    const activeCurrencyOptions = useMemo(() => {
        if (!Array.isArray(currencyOptions)) return [];
        return currencyOptions.filter((o) => o.active !== false);
    }, [currencyOptions]);

    const selectedPalletFormat = useMemo(
        () => (Array.isArray(palletFormatOptions) && palletFormatOptions.find((o) => o.id === palletFormatId)) || null,
        [palletFormatId, palletFormatOptions],
    );

    const detailsRow = useMemo(
        () => rows.find((row) => row.localId === detailsLocalId) ?? null,
        [detailsLocalId, rows],
    );

    const patchRow = (localId, patch) => {
        if (localId === '__draft__') {
            const newRow = {
                ...createDraftRow({ quotationId, customerOrderId, supplierOrderId: activeGroup?.supplierOrderId ?? null }),
                ...patch,
                localId: makeTempId(),
            };
            onChange([...rows, newRow]);
            return;
        }

        onChange(rows.map((row) => (row.localId === localId ? { ...row, ...patch } : row)));
    };

    const handleRemoveRow = (row) => {
        onChange(rows.filter((item) => item.localId !== row.localId));
        if (detailsLocalId === row.localId) {
            setDetailsLocalId(null);
            setDetailsDraft(null);
        }
    };

    const closeDetailsModal = () => {
        setDetailsLocalId(null);
        setDetailsDraft(null);
        setDetailsLoading(false);
        setDetailsSaving(false);
        setDetailsAttesting(false);
        setDetailsError('');
    };

    const openDetailsModal = async (row) => {
        if (!row?.id) {
            return;
        }

        setDetailsLocalId(row.localId);
        setDetailsDraft(null);
        setDetailsError('');
        setDetailsLoading(true);

        try {
            const response = await apiClient.get(`/ordercosts/${row.id}`);
            setDetailsDraft(createDetailsDraft(response?.data ?? row));
        } catch (error) {
            console.error('Failed to load order cost popup data:', error);
            setDetailsError('Kunde inte läsa orderkostnadens detaljer från backend.');
        } finally {
            setDetailsLoading(false);
        }
    };

    const patchDetailsDraft = (patch) => {
        setDetailsDraft((prev) => ({
            ...(prev ?? {}),
            ...patch,
        }));
    };

    const syncRowFromApi = (localId, payload) => {
        if (!payload) return;

        patchRow(localId, {
            supplierName: payload.supplierName ?? '',
            note: payload.note ?? '',
            doPrintOnCustomerOrder: Boolean(payload.doPrintOnCustomerOrder),
            doPrintOnSupplierOrder: Boolean(payload.doPrintOnSupplierOrder),
            doPrintOnQuotation: Boolean(payload.doPrintOnQuotation),
            inPriceAttested: toInputValue(payload.inPriceAttested),
            doInvoiceSeparately: Boolean(payload.doInvoiceSeparately),
            doInvoiceSeparatelyImmediately: Boolean(payload.doInvoiceSeparatelyImmediately),
            createdByName: payload.createdByName ?? '',
            createdDateTime: payload.createdDateTime ?? null,
            editedByName: payload.editedByName ?? '',
            editedDateTime: payload.editedDateTime ?? null,
            attestedByName: payload.attestedByName ?? '',
            attestedDateTime: payload.attestedDateTime ?? null,
            attestedInPriceCurrencyRate: payload.attestedInPriceCurrencyRate ?? null,
            attestedOutPriceCurrencyRate: payload.attestedOutPriceCurrencyRate ?? null,
        });
    };

    const saveDetails = async () => {
        if (!detailsRow?.id || !detailsDraft) return;

        setDetailsSaving(true);
        setDetailsError('');

        try {
            const response = await apiClient.put(`/ordercosts/${detailsRow.id}`, {
                updatedFields: [
                    'SupplierName',
                    'Note',
                    'DoPrintOnCustomerOrder',
                    'DoPrintOnSupplierOrder',
                    'DoPrintOnQuotation',
                    'InPriceAttested',
                    'DoInvoiceSeparately',
                    'DoInvoiceSeparatelyImmediately',
                ],
                supplierName: (detailsDraft.supplierName || '').trim() || null,
                note: (detailsDraft.note || '').trim() || null,
                doPrintOnCustomerOrder: Boolean(detailsDraft.doPrintOnCustomerOrder),
                doPrintOnSupplierOrder: Boolean(detailsDraft.doPrintOnSupplierOrder),
                doPrintOnQuotation: Boolean(detailsDraft.doPrintOnQuotation),
                inPriceAttested: parseNullableDecimal(detailsDraft.inPriceAttested),
                doInvoiceSeparately: Boolean(detailsDraft.doInvoiceSeparately),
                doInvoiceSeparatelyImmediately: Boolean(detailsDraft.doInvoiceSeparatelyImmediately),
            });

            const updated = response?.data;
            if (!updated) {
                throw new Error('No response payload returned from save endpoint.');
            }

            setDetailsDraft(createDetailsDraft(updated));
            syncRowFromApi(detailsRow.localId, updated);
            closeDetailsModal();
        } catch (error) {
            console.error('Failed to save order cost popup data:', error);
            setDetailsError('Kunde inte spara orderkostnadens popupfält.');
        } finally {
            setDetailsSaving(false);
        }
    };

    const attestDetails = async () => {
        if (!detailsRow?.id || !detailsDraft) return;

        setDetailsAttesting(true);
        setDetailsError('');

        try {
            const response = await apiClient.post(`/ordercosts/${detailsRow.id}/attest`);
            const updated = response?.data;

            if (!updated) {
                throw new Error('No response payload returned from attest endpoint.');
            }

            setDetailsDraft(createDetailsDraft(updated));
            syncRowFromApi(detailsRow.localId, updated);
        } catch (error) {
            console.error('Failed to attest order cost:', error);
            setDetailsError('Kunde inte attestera orderkostnaden.');
        } finally {
            setDetailsAttesting(false);
        }
    };

    const patchPallet = (patch) => {
        if (typeof onPalletChange === 'function') {
            onPalletChange(patch);
        }
    };

    if (!quotationId && !customerOrderId && !supplierOrderId) {
        return null;
    }

    const goToPrevGroup = () => {
        if (!supplierOrderGroups.length) return;
        const prevIndex = Math.max(activeGroupIndex - 1, 0);
        setActiveGroupKey(supplierOrderGroups[prevIndex].key);
    };

    const goToNextGroup = () => {
        if (!supplierOrderGroups.length) return;
        const nextIndex = Math.min(activeGroupIndex + 1, supplierOrderGroups.length - 1);
        setActiveGroupKey(supplierOrderGroups[nextIndex].key);
    };

    return (
        <div>
            <div className="relative mb-2">
                <div className="absolute left-0 top-1/2 flex -translate-y-1/2 items-center gap-1" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <button
                        type="button"
                        onClick={goToPrevGroup}
                        disabled={activeGroupIndex <= 0}
                        className=""
                        title="Föregående sida"
                        aria-label="Föregående sida"
                    >
                        <ArrowLeftCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                    </button>
                    <button
                        type="button"
                        onClick={goToNextGroup}
                        disabled={activeGroupIndex >= supplierOrderGroups.length - 1}
                        className=""
                        title="Nästa sida"
                        aria-label="Nästa sida"
                    >
                        <ArrowRightCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                    </button>
                    <span className="min-w-6 text-center text-xs text-gray-600">
                        {supplierOrderGroups.length > 0 ? `${activeGroupIndex + 1}/${supplierOrderGroups.length}` : '0/0'}
                    </span>
                    <span className='text-xs text-gray-600'>{getSupplierOrderLabel(activeGroup?.supplierOrderId ?? null)}</span>
                </div>
                <p className="text-center text-sm font-semibold text-gray-500">
                    Orderkostnader
                </p>
            </div>

            <div className="overflow-x-auto">
                <table className="table-fixed w-full border-collapse text-xs">
                    <thead>
                        <tr className="border-b border-gray-300 text-gray-700 text-xs text-gray-500 tracking-wide">
                            <th className="w-10 py-1 pl-[6px] text-left font-thin">Deb</th>
                            <th className="py-1 pl-[8px] text-left font-thin">Kostnad</th>
                            <th className="w-10 py-1 pr-[8px] text-right font-thin">Ant</th>
                            <th className="w-15 py-1 pr-[8px] text-right font-thin">Inpris</th>
                            <th className="w-15 py-1 pl-[8px] text-left font-thin">Valuta</th>
                            <th className="w-20 py-1 pl-[8px] text-left font-thin">Attest</th>
                            <th className="w-10 py-1 pl-[8px] text-left font-thin">Kalk</th>
                            <th className="w-24 py-1 pr-[8px] text-right font-thin">Utp. SEK</th>
                            <th className="w-13 py-1 pl-[8px] text-right font-medium" />
                            <th className="w-10 py-1 pl-[8px] text-right font-medium" />
                        </tr>
                    </thead>
                    <tbody className="[&>tr:first-child>td]:pt-2">
                        {palletFormatId != null && (
                            <tr className="">
                                <td className="py-[1px] px-[1px] align-middle">
                                    {/* <input
                                        type="checkbox"
                                        checked={Boolean(eurPallet)}
                                        onChange={(event) => patchPallet({ eurPallet: event.target.checked })}
                                        aria-label="EUR pall"
                                    /> */}
                                    <button
                                        type="button"
                                        role="switch"
                                        aria-checked={Boolean(eurPallet)}
                                        aria-label="EUR pall"
                                        onClick={() => patchPallet({ eurPallet: !eurPallet })}
                                        className={`mt-[2px] relative inline-flex h-4 w-7 items-center rounded-full transition-colors ${eurPallet ? 'bg-lime-600' : 'bg-gray-300'
                                            }`}
                                    >
                                        <span
                                            className={`inline-block h-3 w-3 transform rounded-full bg-white transition-transform ${eurPallet ? 'translate-x-3' : 'translate-x-1'
                                                }`}
                                        />
                                    </button>
                                </td>
                                <td className="py-[1px] px-[1px]">
                                    <select
                                        value={toInputValue(palletFormatId)}
                                        onChange={(event) => {
                                            const nextPalletFormatId = parseNullableInt(event.target.value);
                                            const nextPalletFormat = (Array.isArray(palletFormatOptions) ? palletFormatOptions : [])
                                                .find((option) => option.id === nextPalletFormatId);

                                            patchPallet({
                                                palletFormatId: nextPalletFormatId,
                                                eurPalletValue: getPalletFormatCustomerPrice(nextPalletFormat),
                                            });
                                        }}
                                        className="w-full rounded-sm border border-gray-300 bg-white px-2 pt-[3px] pb-[4px]"
                                    >
                                        <option value="">Välj packning</option>
                                        {selectedPalletFormat && selectedPalletFormat.isActive === false && (
                                            <option
                                                key={`selected-pallet-format-${selectedPalletFormat.id}`}
                                                value={selectedPalletFormat.id}
                                                style={{ color: '#6b7280' }}
                                            >
                                                {selectedPalletFormat.name + ' — inaktiv'}
                                            </option>
                                        )}
                                        {(Array.isArray(palletFormatOptions) ? palletFormatOptions : []).map((option) => (
                                            <option key={option.id} value={option.id}>{option.name}</option>
                                        ))}
                                    </select>
                                </td>
                                <td className="py-[1px] px-[1px]">
                                    <input
                                        type="text"
                                        value={toInputValue(nrOfEurPallet)}
                                        onChange={(event) => patchPallet({ nrOfEurPallet: parseNullableInt(event.target.value) })}
                                        className="w-full rounded-sm border border-gray-300 bg-white px-2 py-1 text-right"
                                    />
                                </td>
                                <td className="py-[1px] px-[1px]" />
                                <td className="py-[1px] px-[1px]" />
                                <td className="py-[1px] px-[1px]" />
                                <td className="py-[1px] px-[1px]" />
                                <td className="py-[1px] px-[1px]">
                                    <input
                                        type="text"
                                        value={toInputValue(eurPalletValue)}
                                        onChange={(event) => patchPallet({ eurPalletValue: parseNullableDecimal(event.target.value) })}
                                        className="w-full rounded-sm border border-gray-300 px-2 py-1 text-right bg-gray-100 text-gray-500 cursor-not-allowed"
                                        disabled
                                    />
                                </td>
                                <td className="py-[1px] px-[1px]" />
                                <td className="py-[1px] px-[1px]" />
                            </tr>
                        )}
                        {rowsWithDraft.map((row) => {
                            const selectedOption = (Array.isArray(costOptions) && costOptions.find((o) => o.id === row.costId)) || null;
                            const selectedCurrency = (Array.isArray(currencyOptions) && currencyOptions.find((o) => o.id === row.inPriceCurrencyId)) || null;
                            const nrOfDisabled = selectedOption?.isNrOf === false;
                            const isCurrencyEnabled = row.costId != null
                                && selectedOption?.isSupplier == false
                                && selectedOption?.dmtFixed == null
                                && selectedOption?.dmtPercent == null;

                            return (
                                <tr key={row.localId} className="border-b border-gray-100">
                                    <td className="py-0 px-[1px] align-middle">
                                        <button
                                            type="button"
                                            role="switch"
                                            aria-checked={Boolean(row.doDebit)}
                                            aria-label="Debitera"
                                            onClick={() => patchRow(row.localId, { doDebit: !row.doDebit })}
                                            className={`mt-[2px] relative inline-flex h-4 w-7 items-center rounded-full transition-colors ${row.doDebit ? 'bg-lime-600' : 'bg-gray-300'
                                                }`}
                                        >
                                            <span
                                                className={`inline-block h-3 w-3 transform rounded-full bg-white transition-transform ${row.doDebit ? 'translate-x-3' : 'translate-x-1'
                                                    }`}
                                            />
                                        </button>
                                    </td>
                                    <td className="py-0 px-[1px]">
                                        <select
                                            value={toInputValue(row.costId)}
                                            onChange={(event) => {
                                                const costId = parseNullableInt(event.target.value);
                                                patchRow(row.localId, {
                                                    costId,
                                                    ...(costId != null && { doDebit: true }),
                                                });
                                            }}
                                            className="w-full rounded-sm border border-gray-300 bg-white px-2 pt-[3px] pb-[4px]"
                                        >
                                            <option value="">Välj kostnad</option>
                                            {selectedOption && !selectedOption.isActive && (
                                                <option
                                                    key={`selected-${selectedOption.id}`}
                                                    value={selectedOption.id}
                                                    style={{ color: '#6b7280' }}
                                                >
                                                    {selectedOption.name + ' — inaktiv'}
                                                </option>
                                            )}
                                            {activeCostOptions.map((option) => (
                                                <option key={option.id} value={option.id}>{option.name}</option>
                                            ))}
                                        </select>
                                    </td>
                                    <td className="py-0 px-[1px]">
                                        <input
                                            type="text"
                                            value={toInputValue(row.nrOf)}
                                            onChange={(event) => patchRow(row.localId, { nrOf: event.target.value })}
                                            disabled={nrOfDisabled}
                                            className={`w-full rounded-sm border border-gray-300 px-2 py-1 text-right ${nrOfDisabled ? 'bg-gray-100 text-gray-500 cursor-not-allowed' : 'bg-white'}`}
                                        />
                                    </td>
                                    <td className="py-0 px-[1px]">
                                        <input
                                            type="text"
                                            value={toInputValue(row.inPrice)}
                                            onChange={(event) => patchRow(row.localId, { inPrice: event.target.value })}
                                            className="w-full rounded-sm border border-gray-300 bg-white px-2 py-1 text-right"
                                        />
                                    </td>
                                    <td className="py-0 px-[1px]">
                                        <select
                                            value={toInputValue(row.inPriceCurrencyId)}
                                            onChange={(event) => patchRow(row.localId, { inPriceCurrencyId: parseNullableInt(event.target.value) })}
                                            disabled={!isCurrencyEnabled}
                                            className={`rounded-sm border border-gray-300 px-2 pt-[3px] pb-[4px] ${isCurrencyEnabled ? 'bg-white' : 'bg-gray-100 text-gray-500 cursor-not-allowed'}`}
                                        >
                                            <option value=""></option>
                                            {selectedCurrency && selectedCurrency.active === false && (
                                                <option
                                                    key={`selected-currency-${selectedCurrency.id}`}
                                                    value={selectedCurrency.id}
                                                    style={{ color: '#6b7280' }}
                                                >
                                                    {selectedCurrency.name + ' — inaktiv'}
                                                </option>
                                            )}
                                            {activeCurrencyOptions.map((option) => (
                                                <option key={option.id} value={option.id}>{option.name}</option>
                                            ))}
                                        </select>
                                    </td>
                                    <td className="py-0 px-[1px]">
                                        <span className="block w-14 px-2 py-1 text-gray-600">
                                            {row.inPriceAttested}
                                        </span>
                                        {/*<input
                                            type="text"
                                             value={toInputValue(row.inPriceAttested)}
                                             onChange={(event) => patchRow(row.localId, { inPriceAttested: event.target.value })}
                                             className="w-24 rounded-sm border border-gray-300 bg-white px-2 py-1"
                                         /> */}
                                    </td>
                                    <td className="py-0 px-[1px]">
                                        <span className="block w-14 px-2 py-1 text-gray-600">
                                        </span>
                                        {/* <input
                                            type="text"
                                            value={toInputValue(row.outPrice)}
                                            onChange={(event) => patchRow(row.localId, { outPrice: event.target.value })}
                                            className="w-14 text-right rounded-sm border border-gray-300 bg-white px-2 py-1"
                                        /> */}
                                    </td>
                                    <td className="py-0 px-[1px]">
                                        <div className="flex items-center justify-end gap-1">
                                            {row.nrOf != null && row.nrOf !== '' && (
                                                <span className="text-gray-600 whitespace-nowrap">
                                                    {toInputValue(row.nrOf)} *
                                                </span>
                                            )}
                                            <input
                                                type="text"
                                                value={toInputValue(row.outPrice)}
                                                onChange={(event) => patchRow(row.localId, { outPrice: event.target.value })}
                                                className="w-full rounded-sm border border-gray-300 bg-white px-2 py-1 text-right"
                                            />
                                            {(row.invoiceRowId != null || row.invoiceId != null) && (
                                                <p title="Faktura skapad">
                                                    <CheckCircle size={14} className="text-emerald-600" />
                                                </p>

                                            )}
                                        </div>
                                    </td>
                                    <td className="py-0 px-[1px]">
                                        {row.localId !== '__draft__' && row.id != null && (
                                            <button
                                                type="button"
                                                onClick={() => openDetailsModal(row)}
                                                className="ml-8 p-1 text-gray-700 hover:bg-gray-100 text-right"
                                                title="Detaljer"
                                                aria-label="Detaljer"
                                            >
                                                <CircleHelp size={14} strokeWidth={1.75} />
                                            </button>
                                        )}
                                    </td>
                                    <td className="py-0 px-[1px]">
                                        {row.localId !== '__draft__' && (
                                            <button
                                                type="button"
                                                onClick={() => handleRemoveRow(row)}
                                                className="p-1 text-red-700 hover:bg-red-50 hover:text-red-700"
                                                title="Ta bort rad"
                                                aria-label="Ta bort rad"
                                            >
                                                <Trash2 size={14} strokeWidth={1.75} />
                                            </button>
                                        )}
                                    </td>
                                </tr>
                            );
                        })}
                    </tbody>
                </table>
            </div>

            {detailsRow && createPortal(
                <div className="fixed inset-0 z-50">
                    <div className="absolute inset-0 bg-black/50 z-40" />
                    <div className="relative z-50 flex min-h-screen items-start justify-center pt-20" onClick={closeDetailsModal}>
                        <div
                            className="relative bg-white rounded-sm shadow-xl max-w-lg w-full mx-4 p-6"
                            style={{ background: 'rgb(255, 255, 234)' }}
                            onClick={(event) => event.stopPropagation()}
                        >
                            <div className="relative flex items-center justify-center mb-4">
                                <h3 className="text-sm font-semibold text-center text-gray-800">Orderkostnad</h3>
                                <button
                                    type="button"
                                    onClick={closeDetailsModal}
                                    className="absolute right-0 text-gray-400 hover:text-gray-600 text-l leading-none mb-1"
                                    aria-label="Stäng"
                                >
                                    ×
                                </button>
                            </div>

                            <div className="mx-5 mt-7 text-xs text-gray-700">
                                {detailsError && (
                                    <div className="rounded-sm border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-700">
                                        {detailsError}
                                    </div>
                                )}

                                {detailsLoading && (
                                    <div className="py-4 text-sm text-gray-700">Läser data från backend...</div>
                                )}

                                {!detailsLoading && detailsDraft && (
                                    <>
                                        <LabeledInput
                                            label="Leverantör"
                                            labelWidth="w-20"
                                            margintop="0"
                                            value={detailsDraft.supplierName}
                                            onChange={(value) => patchDetailsDraft({ supplierName: value })}
                                            disabled={detailsSaving || detailsAttesting}
                                        />
                                        <p className="mt-2 text-xs text-gray-700">Notering, syns bara internt</p>
                                        <textarea
                                            value={detailsDraft.note}
                                            onChange={(event) => patchDetailsDraft({ note: event.target.value })}
                                            disabled={detailsSaving || detailsAttesting}
                                            className="mt-1 h-28 w-full border border-gray-300 rounded-sm px-2 py-1 focus:outline-none bg-white text-xs"
                                        />

                                        <div className="space-y-1 pt-2">
                                            <LabeledSwitch
                                                id="ordercost-popup-print-oe"
                                                name="ordercost-popup-print-oe"
                                                rowId={detailsDraft.id ?? detailsRow.localId}
                                                field="doPrintOnCustomerOrder"
                                                value={Boolean(detailsDraft.doPrintOnCustomerOrder)}
                                                onChange={(_rowId, _field, checked) => patchDetailsDraft({ doPrintOnCustomerOrder: checked })}
                                                label="Utskrift OE"
                                                labelWidth="w-[120px]"
                                                disabled={detailsSaving || detailsAttesting}
                                            />
                                            <LabeledSwitch
                                                id="ordercost-popup-print-best"
                                                name="ordercost-popup-print-best"
                                                rowId={detailsDraft.id ?? detailsRow.localId}
                                                field="doPrintOnSupplierOrder"
                                                value={Boolean(detailsDraft.doPrintOnSupplierOrder)}
                                                onChange={(_rowId, _field, checked) => patchDetailsDraft({ doPrintOnSupplierOrder: checked })}
                                                label="Utskrift Best."
                                                labelWidth="w-[120px]"
                                                disabled={detailsSaving || detailsAttesting}
                                            />
                                            <LabeledSwitch
                                                id="ordercost-popup-print-offert"
                                                name="ordercost-popup-print-offert"
                                                rowId={detailsDraft.id ?? detailsRow.localId}
                                                field="doPrintOnQuotation"
                                                value={Boolean(detailsDraft.doPrintOnQuotation)}
                                                onChange={(_rowId, _field, checked) => patchDetailsDraft({ doPrintOnQuotation: checked })}
                                                label="Utskrift Offert"
                                                labelWidth="w-[120px]"
                                                disabled={detailsSaving || detailsAttesting}
                                            />

                                            <div className="grid grid-cols-[120px_20px_120px] items-center gap-x-2 pt-1">
                                                <span>Attest. belopp</span>
                                                <span className="text-center">x</span>
                                                <NumberInput
                                                    type="text"
                                                    value={parseNullableDecimal(detailsDraft.inPriceAttested)}
                                                    onChange={(rowId, field, value) => patchDetailsDraft({ inPriceAttested: value })}
                                                    disabled={detailsSaving || detailsAttesting}
                                                    className="w-full rounded-sm border border-gray-300 bg-white px-2 py-1 text-right"
                                                />
                                            </div>

                                            <LabeledSwitch
                                                id="ordercost-popup-invoice-separate"
                                                name="ordercost-popup-invoice-separate"
                                                rowId={detailsDraft.id ?? detailsRow.localId}
                                                field="doInvoiceSeparately"
                                                value={Boolean(detailsDraft.doInvoiceSeparately)}
                                                onChange={(_rowId, _field, checked) => patchDetailsDraft({ doInvoiceSeparately: checked })}
                                                label="Fakturera separat"
                                                labelWidth="w-[120px]"
                                                disabled={detailsSaving || detailsAttesting}
                                            />
                                            <LabeledSwitch
                                                id="ordercost-popup-invoice-separate-immediately"
                                                name="ordercost-popup-invoice-separate-immediately"
                                                rowId={detailsDraft.id ?? detailsRow.localId}
                                                field="doInvoiceSeparatelyImmediately"
                                                value={Boolean(detailsDraft.doInvoiceSeparatelyImmediately)}
                                                onChange={(_rowId, _field, checked) => patchDetailsDraft({ doInvoiceSeparatelyImmediately: checked })}
                                                label="Fakt sep. direkt"
                                                labelWidth="w-[120px]"
                                                disabled={detailsSaving || detailsAttesting}
                                            />
                                        </div>

                                        <div className="grid grid-cols-[110px_minmax(0,1fr)_180px] gap-x-4 gap-y-1 pt-6 text-xs leading-tight">
                                            <span>Skapad av</span>
                                            <span className="truncate">{detailsDraft.createdByName ?? ''}</span>
                                            <span>{formatDateTime(detailsDraft.createdDateTime)}</span>

                                            <span>Ändrad av</span>
                                            <span className="truncate">{detailsDraft.editedByName ?? ''}</span>
                                            <span>{formatDateTime(detailsDraft.editedDateTime)}</span>

                                            <span>Attesterad av</span>
                                            <span className="truncate">{detailsDraft.attestedByName ?? ''}</span>
                                            <span>{formatDateTime(detailsDraft.attestedDateTime)}</span>

                                            <span>Attestkurser</span>
                                            <span className="truncate">{buildAttestRatesLabel(detailsDraft.attestedInPriceCurrencyRate, detailsDraft.attestedOutPriceCurrencyRate)}</span>
                                            <span></span>
                                        </div>
                                    </>
                                )}
                            </div>

                            <div className="flex mt-6 mb-3 pt-4 mx-5 items-center justify-between">
                                <button
                                    type="button"
                                    onClick={attestDetails}
                                    disabled={detailsLoading || !detailsDraft || detailsSaving || detailsAttesting}
                                    className="shadow-md/30 text-xs text-white bg-blue-800 hover:bg-blue-900 px-5 p-[5px] disabled:opacity-50 disabled:cursor-not-allowed"
                                >
                                    {detailsAttesting ? 'ATTESTERAR...' : 'ATTESTERA'}
                                </button>
                                <div className="flex gap-4">
                                    <button
                                        type="button"
                                        onClick={saveDetails}
                                        disabled={detailsLoading || !detailsDraft || detailsSaving || detailsAttesting}
                                        className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 px-5 p-[5px] disabled:opacity-50 disabled:cursor-not-allowed"
                                    >
                                        {detailsSaving ? 'SPARAR...' : 'SPARA'}
                                    </button>
                                    <button
                                        type="button"
                                        onClick={closeDetailsModal}
                                        className="shadow-md/30 text-xs text-white bg-orange-400 hover:bg-orange-600 px-5 p-[5px]"
                                    >
                                        STÄNG
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>,
                document.body,
            )}
        </div>
    );
};

export default OrderCost;

