import React, { forwardRef, useCallback, useEffect, useImperativeHandle, useMemo, useRef, useState } from 'react';
import { Trash2 } from 'lucide-react';
import Select from 'react-select';

import ActionButton from '../../../components/ActionButton';
import LabeledCheckbox from '../../../components/LabeledCheckbox';
import LabeledReactSelect from '../../../components/LabeledReactSelect';
import apiClient from '../../../config/apiClient';
import {
    distributeResultInternationalCost,
    recalculateCostCalcRow,
} from './transportOrderCostCalcCalculations';

const numberOrEmpty = (value) => (value === null || value === undefined ? '' : String(value));
const numberOrEmptyRounded = (value) => (value === null || value === undefined || Number.isNaN(Number(value)) ? '' : String(Math.round(Number(value))));
const toNumberOrNull = (value) => (value === '' || value === null || value === undefined ? null : Number(value));
const sumRows = (rows, field) => rows.reduce((acc, row) => acc + (Number(row?.[field]) || 0), 0);

const buildUpdatePayload = (draft) => ({
    isManualCalc: draft.isManualCalc,
    isLTL: draft.isLTL,
    note: draft.note || null,
    calcCityFrom: draft.calcCityFrom || null,
    calcPostalNrTo: toNumberOrNull(draft.calcPostalNrTo),
    costCalcForcePriceListId: draft.costCalcForcePriceListId === '' ? null : Number(draft.costCalcForcePriceListId),
    toInternationalCost: draft.isManualCalc ? toNumberOrNull(draft.toInternationalCostHeader) : null,
    resultInternationalCost: toNumberOrNull(draft.resultInternationalCostHeader),
    supplierOrders: draft.rows.map((row) => ({
        id: row.id,
        toInternationalCost: draft.isManualCalc ? toNumberOrNull(row.toInternationalCost) : null,
        transportOrderDomesticCost: toNumberOrNull(row.transportOrderDomesticCost),
        transportOrderLoading: toNumberOrNull(row.transportOrderLoading),
        transportOrderUnloading: toNumberOrNull(row.transportOrderUnloading),
        transportOrderOther: toNumberOrNull(row.transportOrderOther),
        resultDomesticCost: toNumberOrNull(row.resultDomesticCost),
        resultOther: toNumberOrNull(row.resultOther),
        resultNote: row.resultNote || null,
    })),
});

const formatNumber = (value) => {
    if (value === null || value === undefined || Number.isNaN(Number(value))) {
        return '';
    }
    return Math.round(Number(value)).toLocaleString('sv-SE');
};

const formatDecimal = (value) => {
    if (value === null || value === undefined || Number.isNaN(Number(value))) {
        return '';
    }
    return Number(value).toLocaleString('sv-SE', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    });
};

const negativeClass = (value) => (Number(value) < 0 ? 'text-red-600' : '');

const formatDateTime = (value) => {
    if (!value) {
        return '';
    }

    // Legacy timestamps are Swedish wall-clock values without a timezone offset.
    const swedishDateTime = String(value).match(/^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/);
    if (swedishDateTime) {
        const [, year, month, day, hour, minute] = swedishDateTime;
        return `${year}-${month}-${day} ${hour}:${minute}`;
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return '';
    }
    return date.toLocaleString('sv-SE', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' });
};

const compareVersionsOldestFirst = (left, right) =>
    new Date(left.createdDateTime) - new Date(right.createdDateTime) || left.id - right.id;

// Mirrors legacy TransportOrderCostCalcViewModel.SetVersionStatusName().
const buildStatusNames = (versions) => {
    if (versions.length === 0) {
        return new Map();
    }
    const lastItem = [...versions].sort(compareVersionsOldestFirst).at(-1);
    const names = new Map();
    versions.forEach((version) => {
        const isLast = version.id === lastItem.id;
        if (version.createdAtStatus === 0) {
            names.set(version.id, isLast ? 'Planering' : 'Planerad');
        } else if (version.createdAtStatus === 1) {
            names.set(version.id, isLast ? 'Aktiv' : 'Lossad');
        } else if (version.createdAtStatus === 2) {
            names.set(version.id, 'Återrapportering');
        } else if (version.createdAtStatus === 3) {
            names.set(version.id, 'Återrapporterad');
        } else {
            names.set(version.id, '');
        }
    });
    return names;
};

const buildDraftFromVersion = (version) => {
    if (!version) {
        return null;
    }
    return {
        isManualCalc: Boolean(version.isManualCalc),
        isLTL: Boolean(version.isLTL),
        note: version.note ?? '',
        calcCityFrom: version.calcCityFrom ?? '',
        calcPostalNrTo: numberOrEmpty(version.calcPostalNrTo),
        costCalcForcePriceListId: version.costCalcForcePriceListId ?? '',
        toInternationalCostHeader: numberOrEmpty(Math.round(sumRows(version.supplierOrders, 'toInternationalCost'))),
        resultInternationalCostHeader: numberOrEmpty(Math.round(sumRows(version.supplierOrders, 'resultInternationalCost'))),
        rows: version.supplierOrders.map((row) => ({
            id: row.id,
            supplierOrderId: row.supplierOrderId,
            totalNrOfItems: row.totalNrOfItems,
            totalNrOfPalletPlaces: row.totalNrOfPalletPlaces,
            totalPalletArea: row.totalPalletArea,
            palletFactor: row.palletFactor,
            caclulationInternationalCost: row.caclulationInternationalCost,
            caclulationDomesticCost: row.caclulationDomesticCost,
            caclulationFtl: row.caclulationFtl,
            caclulationUnloading: row.caclulationUnloading,
            caclulationTotal: row.caclulationTotal,
            caclulationUsedTotal: row.caclulationUsedTotal,
            calculationCalcInfo: row.calculationCalcInfo,
            toInternationalCost: numberOrEmptyRounded(row.toInternationalCost),
            transportOrderDomesticCost: numberOrEmptyRounded(row.transportOrderDomesticCost),
            transportOrderLoading: numberOrEmptyRounded(row.transportOrderLoading),
            transportOrderUnloading: numberOrEmptyRounded(row.transportOrderUnloading),
            transportOrderOther: numberOrEmptyRounded(row.transportOrderOther),
            transportOrderTotal: row.transportOrderTotal,
            transportOrderCalcInfo: row.transportOrderCalcInfo,
            diffTransportOrderCaclulation: row.diffTransportOrderCaclulation,
            resultInternationalCost: row.resultInternationalCost,
            resultDomesticCost: numberOrEmptyRounded(row.resultDomesticCost),
            resultOther: numberOrEmptyRounded(row.resultOther),
            resultNote: row.resultNote ?? '',
            resultTotal: row.resultTotal,
            diffResultCaclulation: row.diffResultCaclulation,
            diffResultTransportOrder: row.diffResultTransportOrder,
        })),
    };
};

const TABLE_FONT_STYLE = { fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" };

const cellInputClass = 'w-full border border-gray-300 bg-white px-1 py-0.5 text-center text-xs text-gray-800 focus:outline-none focus:ring-1 focus:ring-blue-500 disabled:bg-gray-100 disabled:text-gray-500';
const headClassLeft = 'pr-1 text-left text-tiny font-normal tracking-[0.12em] text-gray-500 uppercase';
const headClassCenter = 'px-1 text-center text-tiny font-normal tracking-[0.12em] text-gray-500 uppercase';
const headClassRight = 'pl-2 pr-1 text-right text-tiny font-normal tracking-[0.12em] text-gray-500 uppercase';
const cellClassLeft = 'pr-1 text-left text-xs text-gray-700';
const cellClassCenter = 'px-1 text-center text-xs text-gray-700';
const cellClassRight = 'pl-2 pr-1 text-right text-xs text-gray-700';

const TransportOrderCostCalcArea = forwardRef(function TransportOrderCostCalcArea({ transportOrderId, onMessage }, ref) {
    const [versions, setVersions] = useState([]);
    const [priceListOptions, setPriceListOptions] = useState([]);
    const [selectedId, setSelectedId] = useState(null);
    const [draft, setDraft] = useState(null);
    const [deleteSelection, setDeleteSelection] = useState(() => new Set());
    const [loading, setLoading] = useState(false);
    const [saving, setSaving] = useState(false);
    const saveInProgressRef = useRef(false);

    const loadVersions = useCallback(async (preferredId) => {
        if (!transportOrderId) {
            return;
        }
        setLoading(true);
        try {
            const response = await apiClient.get(`/transportordercostcalc/by-transport-order/${transportOrderId}`);
            const list = Array.isArray(response?.data) ? response.data : [];
            setVersions(list);
            setDeleteSelection(new Set());

            const nextSelectedId = list.some((v) => v.id === preferredId)
                ? preferredId
                : [...list].sort(compareVersionsOldestFirst).at(-1)?.id ?? null;
            setSelectedId(nextSelectedId);
        } catch (error) {
            console.error('Failed to load transport order cost calc versions:', error);
            onMessage?.('error', 'Kunde inte hämta kostnadskalkyl.');
        } finally {
            setLoading(false);
        }
    }, [transportOrderId, onMessage]);

    useEffect(() => {
        let isActive = true;

        const load = async () => {
            await loadVersions();
            if (!isActive) {
                return;
            }
            try {
                const response = await apiClient.get('/transportcostpricelist');
                if (isActive) {
                    setPriceListOptions(Array.isArray(response?.data) ? response.data : []);
                }
            } catch (error) {
                console.error('Failed to load transport cost price lists:', error);
            }
        };

        load();

        return () => {
            isActive = false;
        };
    }, [loadVersions]);

    const selectedVersion = useMemo(() => versions.find((v) => v.id === selectedId) ?? null, [versions, selectedId]);
    const statusNames = useMemo(() => buildStatusNames(versions), [versions]);
    // Mirrors legacy: IsEnabled is only ever set on the freshly loaded latest version, so older versions are always read-only.
    const latestVersionId = useMemo(() => {
        if (versions.length === 0) {
            return null;
        }
        return [...versions].sort(compareVersionsOldestFirst).at(-1)?.id ?? null;
    }, [versions]);

    useEffect(() => {
        setDraft(buildDraftFromVersion(selectedVersion));
    }, [selectedVersion]);

    const versionOptions = useMemo(() => [...versions]
        .sort(compareVersionsOldestFirst)
        .map((v) => ({
            value: v.id,
            label: `${statusNames.get(v.id) ?? ''} · ${formatDateTime(v.createdDateTime)}`,
        })), [versions, statusNames]);

    // Renders a real checkbox per option row, independent of which option is the selected value.
    const VersionOption = (optionProps) => {
        const { data, innerRef, innerProps, isSelected, isFocused } = optionProps;
        return (
            <div
                ref={innerRef}
                {...innerProps}
                className={`flex cursor-pointer items-center gap-2 px-2 py-1 text-xs ${isSelected ? 'bg-lime-500 text-white' : isFocused ? 'bg-gray-100 text-gray-800' : 'bg-white text-gray-800'}`}
            >
                <span onClick={(event) => event.stopPropagation()}>
                    <LabeledCheckbox
                        ariaLabel={`Markera version ${data.label} för radering`}
                        checked={deleteSelection.has(data.value)}
                        disabled={data.value === latestVersionId}
                        onChange={() => toggleDeleteSelection(data.value)}
                        checkboxClassName="h-4 w-4 p-px"
                    />
                </span>
                <span>{data.label}</span>
            </div>
        );
    };

    const versionSelectStyles = {
        control: (provided) => ({ ...provided, minHeight: 25, height: 25 }),
        valueContainer: (provided) => ({ ...provided, minHeight: 25, padding: '0px 4px 1px 6px' }),
        input: (provided) => ({ ...provided, margin: 0, padding: 0 }),
        indicatorsContainer: (provided) => ({ ...provided, height: 25 }),
        indicatorSeparator: () => ({ display: 'none' }),
        menu: (provided) => ({ ...provided, zIndex: 20 }),
    };

    const priceListItems = useMemo(() => [{ id: '', name: 'Ej vald' }, ...priceListOptions], [priceListOptions]);

    const isEditable = Boolean(selectedVersion) && selectedVersion.id === latestVersionId && (selectedVersion.createdAtStatus ?? 0) < 3;

    const updateRow = (rowId, field, value) => {
        setDraft((prev) => {
            if (!prev) return prev;
            const rows = prev.rows.map((row) => (
                row.id === rowId ? recalculateCostCalcRow({ ...row, [field]: value }) : row
            ));
            return {
                ...prev,
                rows,
                ...(field === 'toInternationalCost'
                    ? { toInternationalCostHeader: numberOrEmpty(sumRows(rows, 'toInternationalCost')) }
                    : {}),
            };
        });
    };

    const updateResultInternationalCost = (value) => {
        setDraft((prev) => {
            if (!prev) return prev;
            return {
                ...prev,
                resultInternationalCostHeader: value,
                rows: distributeResultInternationalCost(prev.rows, value),
            };
        });
    };

    const persistAndRecalculate = async (nextDraft, errorMessage) => {
        if (!selectedVersion || saveInProgressRef.current) {
            return;
        }

        setDraft(nextDraft);
        saveInProgressRef.current = true;
        setSaving(true);
        try {
            await apiClient.put(`/transportordercostcalc/${selectedVersion.id}`, buildUpdatePayload(nextDraft));
            const response = await apiClient.post(`/transportordercostcalculation/${transportOrderId}/recalculate`, {
                deliveryStatus: selectedVersion.createdAtStatus ?? 0,
            });
            const recalculated = response?.data;
            setVersions((prev) => prev.map((version) => (
                version.id === recalculated.id ? recalculated : version
            )));
            setSelectedId(recalculated.id);
        } catch (error) {
            console.error('Failed to recalculate transport order costs:', error);
            onMessage?.('error', errorMessage);
            await loadVersions(selectedVersion.id);
        } finally {
            saveInProgressRef.current = false;
            setSaving(false);
        }
    };

    const handleForcePriceListChange = (value) => {
        if (!draft) {
            return;
        }
        persistAndRecalculate(
            { ...draft, costCalcForcePriceListId: value },
            'Kunde inte beräkna kalkyl med vald prislista.',
        );
    };

    const handleLtlChange = (isLTL) => {
        if (!draft) {
            return;
        }
        persistAndRecalculate(
            { ...draft, isLTL },
            'Kunde inte beräkna kalkyl efter ändring av LTL.',
        );
    };

    const toggleDeleteSelection = (versionId) => {
        setDeleteSelection((prev) => {
            const next = new Set(prev);
            if (next.has(versionId)) {
                next.delete(versionId);
            } else {
                next.add(versionId);
            }
            return next;
        });
    };

    const handleDeleteSelected = async () => {
        const deletableVersionIds = [...deleteSelection].filter((versionId) => versionId !== latestVersionId);
        if (deletableVersionIds.length === 0) {
            return;
        }
        try {
            await Promise.all(deletableVersionIds.map((versionId) => apiClient.delete(`/transportordercostcalc/${versionId}`)));
            await loadVersions();
            onMessage?.('info', 'Valda versioner raderades.');
        } catch (error) {
            console.error('Failed to delete transport order cost calc versions:', error);
            onMessage?.('error', 'Kunde inte radera valda versioner.');
        }
    };

    // Exposed via ref so the parent TransportOrder page can save the cost calc as part of its own Spara action.
    const handleSave = useCallback(async () => {
        if (saveInProgressRef.current) {
            return false;
        }
        if (!draft || !selectedVersion) {
            return true;
        }
        saveInProgressRef.current = true;
        setSaving(true);
        try {
            const response = await apiClient.put(`/transportordercostcalc/${selectedVersion.id}`, buildUpdatePayload(draft));
            const updated = response?.data;
            setVersions((prev) => prev.map((v) => (v.id === updated.id ? updated : v)));
            return true;
        } catch (error) {
            console.error('Failed to save transport order cost calc:', error);
            onMessage?.('error', 'Kunde inte spara kostnadskalkylen.');
            return false;
        } finally {
            saveInProgressRef.current = false;
            setSaving(false);
        }
    }, [draft, selectedVersion, onMessage]);

    useImperativeHandle(ref, () => ({ save: handleSave, reload: () => loadVersions() }), [handleSave, loadVersions]);

    if (loading && versions.length === 0) {
        return (
            <div className="flex h-[580px] w-full items-center justify-center border border-gray-300 bg-slate-50 text-xs text-gray-500">
                Läser in kostnadskalkyl...
            </div>
        );
    }

    if (!selectedVersion || !draft) {
        return (
            <div className="flex h-[580px] w-full items-center justify-center border border-gray-300 bg-slate-50 text-xs text-gray-500">
                Ingen kostnadskalkyl finns ännu för denna transportorder.
            </div>
        );
    }

    const calcTotals = {
        caclulationInternationalCost: sumRows(draft.rows, 'caclulationInternationalCost'),
        caclulationDomesticCost: sumRows(draft.rows, 'caclulationDomesticCost'),
        caclulationFtl: sumRows(draft.rows, 'caclulationFtl'),
        caclulationUnloading: sumRows(draft.rows, 'caclulationUnloading'),
        caclulationTotal: sumRows(draft.rows, 'caclulationTotal'),
        caclulationUsedTotal: sumRows(draft.rows, 'caclulationUsedTotal'),
    };

    const transportOrderTotals = {
        toInternationalCost: sumRows(draft.rows, 'toInternationalCost'),
        transportOrderDomesticCost: sumRows(draft.rows, 'transportOrderDomesticCost'),
        transportOrderUnloading: sumRows(draft.rows, 'transportOrderUnloading'),
        transportOrderLoading: sumRows(draft.rows, 'transportOrderLoading'),
        transportOrderOther: sumRows(draft.rows, 'transportOrderOther'),
        transportOrderTotal: sumRows(draft.rows, 'transportOrderTotal'),
        diffTransportOrderCaclulation: sumRows(draft.rows, 'diffTransportOrderCaclulation'),
    };

    const resultTotals = {
        resultInternationalCost: sumRows(draft.rows, 'resultInternationalCost'),
        resultDomesticCost: sumRows(draft.rows, 'resultDomesticCost'),
        resultOther: sumRows(draft.rows, 'resultOther'),
        resultTotal: sumRows(draft.rows, 'resultTotal'),
        diffResultCaclulation: sumRows(draft.rows, 'diffResultCaclulation'),
        diffResultTransportOrder: sumRows(draft.rows, 'diffResultTransportOrder'),
    };

    return (
        <div className="w-full overflow-y-auto text-xs">
            <div className="mb-2 flex flex-wrap items-center gap-3 pb-2">
                <span className="text-xs font-bold text-gray-700">KALKYL</span>
                <div className="flex w-53 items-center gap-2">
                    <Select
                        options={versionOptions}
                        value={versionOptions.find((option) => option.value === selectedId) ?? null}
                        onChange={(option) => setSelectedId(option?.value ?? null)}
                        components={{ Option: VersionOption }}
                        isSearchable={false}
                        styles={versionSelectStyles}
                        className="flex-1 text-xs"
                    />
                </div>
                <ActionButton label="" icon={Trash2} onClick={handleDeleteSelected} disabled={deleteSelection.size === 0} accent="rose" />
                <div className="ml-0 w-50">
                    <LabeledReactSelect
                        label="Tvinga prisl."
                        labelWidth="w-18"
                        items={priceListItems}
                        value={draft.costCalcForcePriceListId}
                        onChange={handleForcePriceListChange}
                        margintop="0"
                        isDisabled={!isEditable || saving}
                    />
                </div>
            </div>

            {saving ? <div className="mb-2 text-[11px] text-gray-500"></div> : null}

            <div className="mb-3 grid grid-cols-2 gap-3">
                <label className="flex items-center gap-2">
                    <span className="w-28 shrink-0 text-gray-600">Fraktgrundande ort</span>
                    <input
                        className={cellInputClass}
                        value={draft.calcCityFrom}
                        disabled={!isEditable}
                        onChange={(event) => setDraft((prev) => ({ ...prev, calcCityFrom: event.target.value }))}
                    />
                </label>
                <label className="flex items-center gap-2">
                    <span className="w-16 shrink-0 text-gray-600">Postnr till</span>
                    <input
                        className={cellInputClass}
                        value={draft.calcPostalNrTo}
                        disabled={!isEditable}
                        onChange={(event) => setDraft((prev) => ({ ...prev, calcPostalNrTo: event.target.value }))}
                    />
                </label>
            </div>

            <div className="mt-5 mb-1 font-bold text-gray-700">Kostnader kalkylrad</div>
            <table className="mb-3 w-full" style={TABLE_FONT_STYLE}>
                <thead>
                    <tr className="h-5.5">
                        <th className={headClassLeft}>Order</th>
                        <th className={headClassCenter}>Antal</th>
                        <th className={headClassCenter}>PPL</th>
                        <th className={headClassRight}>UTR</th>
                        <th className={headClassRight}>INR</th>
                        <th className={headClassRight}>FTL</th>
                        <th className={headClassRight}>LOSS</th>
                        <th className={headClassRight}>Totalt</th>
                        <th className={headClassRight}>Kalkylrad</th>
                    </tr>
                </thead>
                <tbody>
                    {draft.rows.map((row) => (
                        <tr key={row.id} className="h-5.5 border-t border-gray-200" title={row.calculationCalcInfo || ''}>
                            <td className={cellClassLeft}>{row.supplierOrderId}</td>
                            <td className={cellClassCenter}>{formatNumber(row.totalNrOfItems)}</td>
                            <td className={cellClassCenter}>{formatNumber(row.totalNrOfPalletPlaces)}</td>
                            <td className={cellClassRight}>{formatNumber(row.caclulationInternationalCost)}</td>
                            <td className={cellClassRight}>{formatNumber(row.caclulationDomesticCost)}</td>
                            <td className={cellClassRight}>{formatNumber(row.caclulationFtl)}</td>
                            <td className={cellClassRight}>{formatNumber(row.caclulationUnloading)}</td>
                            <td className={cellClassRight}>{formatNumber(row.caclulationTotal)}</td>
                            <td className={cellClassRight}>{formatNumber(row.caclulationUsedTotal)}</td>
                        </tr>
                    ))}
                    <tr className="h-6 border-t border-gray-300 font-semibold">
                        <td className={cellClassLeft}>Totalt</td>
                        <td className={cellClassCenter}></td>
                        <td className={cellClassRight}></td>
                        <td className={cellClassRight}>{formatNumber(calcTotals.caclulationInternationalCost)}</td>
                        <td className={cellClassRight}>{formatNumber(calcTotals.caclulationDomesticCost)}</td>
                        <td className={cellClassRight}>{formatNumber(calcTotals.caclulationFtl)}</td>
                        <td className={cellClassRight}>{formatNumber(calcTotals.caclulationUnloading)}</td>
                        <td className={cellClassRight}>{formatNumber(calcTotals.caclulationTotal)}</td>
                        <td className={cellClassRight}>{formatNumber(calcTotals.caclulationUsedTotal)}</td>
                    </tr>
                </tbody>
            </table>

            <div className="mt-8 mb-1 flex items-center gap-4">
                <span className="font-bold text-gray-700">Kostnader transportorder</span>
                <LabeledCheckbox
                    label="Manuell inmatning"
                    checked={draft.isManualCalc}
                    disabled={!isEditable}
                    onChange={(checked) => setDraft((prev) => ({ ...prev, isManualCalc: checked }))}
                    className="gap-1 text-gray-600"
                    checkboxClassName="h-5 w-5 p-px"
                />
                <LabeledCheckbox
                    label="LTL"
                    checked={draft.isLTL}
                    disabled={!isEditable || saving}
                    onChange={handleLtlChange}
                    className="gap-1 text-gray-600"
                    checkboxClassName="h-5 w-5 p-px"
                />
            </div>
            <table className="mb-1 w-full" style={TABLE_FONT_STYLE}>
                <thead>
                    <tr className="h-5.5">
                        <th className={headClassLeft}>Order</th>
                        <th className={headClassCenter}>Antal</th>
                        <th className={headClassCenter}>PPL</th>
                        {draft.isLTL ? <th className={headClassCenter}>A</th> : null}
                        <th className={headClassCenter}>UTR</th>
                        <th className={headClassCenter}>INR</th>
                        <th className={headClassCenter}>Loss</th>
                        <th className={headClassCenter}>Last</th>
                        <th className={headClassCenter}>Övrigt</th>
                        <th className={headClassRight}>Totalt</th>
                        <th className={headClassRight}>Diff</th>
                    </tr>
                </thead>
                <tbody>
                    {draft.rows.map((row) => (
                        <tr key={row.id} className="h-5.5 border-t border-gray-200" title={row.transportOrderCalcInfo || ''}>
                            <td className={cellClassLeft}>{row.supplierOrderId}</td>
                            <td className={cellClassCenter}>{formatNumber(row.totalNrOfItems)}</td>
                            <td className={cellClassCenter}>{formatNumber(row.totalNrOfPalletPlaces)}</td>
                            {draft.isLTL ? <td className={cellClassCenter}>{formatDecimal(row.totalPalletArea)}</td> : null}
                            <td className={cellClassRight}>
                                <input className={cellInputClass} value={row.toInternationalCost} disabled={!isEditable || !draft.isManualCalc} onChange={(event) => updateRow(row.id, 'toInternationalCost', event.target.value)} />
                            </td>
                            <td className={cellClassRight}>
                                <input className={cellInputClass} value={row.transportOrderDomesticCost} disabled={!isEditable || !draft.isManualCalc} onChange={(event) => updateRow(row.id, 'transportOrderDomesticCost', event.target.value)} />
                            </td>
                            <td className={cellClassRight}>
                                <input className={cellInputClass} value={row.transportOrderUnloading} disabled={!isEditable || !draft.isManualCalc} onChange={(event) => updateRow(row.id, 'transportOrderUnloading', event.target.value)} />
                            </td>
                            <td className={cellClassRight}>
                                <input className={cellInputClass} value={row.transportOrderLoading} disabled={!isEditable || !draft.isManualCalc} onChange={(event) => updateRow(row.id, 'transportOrderLoading', event.target.value)} />
                            </td>
                            <td className={cellClassRight}>
                                <input className={cellInputClass} value={row.transportOrderOther} disabled={!isEditable} onChange={(event) => updateRow(row.id, 'transportOrderOther', event.target.value)} />
                            </td>
                            <td className={cellClassRight}>{formatNumber(row.transportOrderTotal)}</td>
                            <td className={`${cellClassRight} ${negativeClass(row.diffTransportOrderCaclulation)}`}>{formatNumber(row.diffTransportOrderCaclulation)}</td>
                        </tr>
                    ))}
                    <tr className="h-6 border-t border-gray-300 font-semibold">
                        <td className={cellClassLeft}>Totalt</td>
                        <td className={cellClassCenter}></td>
                        <td className={cellClassRight}></td>
                        {draft.isLTL ? <td className={cellClassCenter}></td> : null}
                        <td className={cellClassCenter}>{formatNumber(draft.toInternationalCostHeader)}</td>
                        {/* <td className={cellClassRight}>
                            <input
                                className={cellInputClass}
                                value={draft.toInternationalCostHeader}
                                disabled={!isEditable || !draft.isManualCalc}
                                onChange={(event) => setDraft((prev) => ({ ...prev, toInternationalCostHeader: event.target.value }))}
                            />
                        </td> */}
                        <td className={cellClassCenter}>{formatNumber(transportOrderTotals.transportOrderDomesticCost)}</td>
                        <td className={cellClassCenter}>{formatNumber(transportOrderTotals.transportOrderUnloading)}</td>
                        <td className={cellClassCenter}>{formatNumber(transportOrderTotals.transportOrderLoading)}</td>
                        <td className={cellClassCenter}>{formatNumber(transportOrderTotals.transportOrderOther)}</td>
                        <td className={cellClassRight}>{formatNumber(transportOrderTotals.transportOrderTotal)}</td>
                        <td className={`${cellClassRight} ${negativeClass(transportOrderTotals.diffTransportOrderCaclulation)}`}>{formatNumber(transportOrderTotals.diffTransportOrderCaclulation)}</td>
                    </tr>
                </tbody>
            </table>

            <div className="mt-8 mb-1 font-bold text-gray-700">Utfall</div>
            <table className="w-full" style={TABLE_FONT_STYLE}>
                <thead>
                    <tr>
                        <th className={headClassLeft}>Order</th>
                        <th className={headClassCenter}>Antal</th>
                        <th className={headClassCenter}>PPL</th>
                        <th className={headClassCenter}>UTR</th>
                        <th className={headClassCenter}>INR</th>
                        <th className={headClassCenter}>Övrigt</th>
                        <th className={headClassCenter}>Total</th>
                        <th className={headClassCenter}>Diff kalkyl</th>
                        <th className={headClassCenter}>Diff TO</th>
                        <th className={headClassCenter}>Notering</th>
                    </tr>
                </thead>
                <tbody>
                    {draft.rows.map((row) => (
                        <tr key={row.id} className="h-5.5 border-t border-gray-200">
                            <td className={cellClassLeft}>{row.supplierOrderId}</td>
                            <td className={cellClassCenter}>{formatNumber(row.totalNrOfItems)}</td>
                            <td className={cellClassCenter}>{formatNumber(row.totalNrOfPalletPlaces)}</td>
                            <td className={cellClassCenter}>{formatNumber(row.resultInternationalCost)}</td>
                            <td className={cellClassCenter}>
                                <input className={cellInputClass} value={row.resultDomesticCost} disabled={!isEditable} onChange={(event) => updateRow(row.id, 'resultDomesticCost', event.target.value)} />
                            </td>
                            <td className={cellClassCenter}>
                                <input className={cellInputClass} value={row.resultOther} disabled={!isEditable} onChange={(event) => updateRow(row.id, 'resultOther', event.target.value)} />
                            </td>
                            <td className={cellClassCenter}>{formatNumber(row.resultTotal)}</td>
                            <td className={`${cellClassCenter} ${negativeClass(row.diffResultCaclulation)}`}>{formatNumber(row.diffResultCaclulation)}</td>
                            <td className={`${cellClassCenter} ${negativeClass(row.diffResultTransportOrder)}`}>{formatNumber(row.diffResultTransportOrder)}</td>
                            <td className={cellClassCenter}>
                                <input className={cellInputClass} value={row.resultNote} disabled={!isEditable} onChange={(event) => updateRow(row.id, 'resultNote', event.target.value)} />
                            </td>
                        </tr>
                    ))}
                    <tr className="h-6 border-t border-gray-300 font-semibold">
                        <td className={cellClassLeft}>Totalt</td>
                        <td className={cellClassCenter}></td>
                        <td className={cellClassRight}></td>
                        <td className={cellClassCenter}>
                            <input
                                className={cellInputClass}
                                value={draft.resultInternationalCostHeader}
                                disabled={!isEditable}
                                onChange={(event) => updateResultInternationalCost(event.target.value)}
                            />
                        </td>
                        <td className={cellClassCenter}>{formatNumber(resultTotals.resultDomesticCost)}</td>
                        <td className={cellClassCenter}>{formatNumber(resultTotals.resultOther)}</td>
                        <td className={cellClassCenter}>{formatNumber(resultTotals.resultTotal)}</td>
                        <td className={`${cellClassCenter} ${negativeClass(resultTotals.diffResultCaclulation)}`}>{formatNumber(resultTotals.diffResultCaclulation)}</td>
                        <td className={`${cellClassCenter} ${negativeClass(resultTotals.diffResultTransportOrder)}`}>{formatNumber(resultTotals.diffResultTransportOrder)}</td>
                        <td className={cellClassCenter}></td>
                    </tr>
                </tbody>
            </table>
        </div>
    );
});

export default TransportOrderCostCalcArea;
