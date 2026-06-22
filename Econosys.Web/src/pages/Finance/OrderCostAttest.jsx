import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ArrowLeftCircle, ArrowRightCircle, ChevronDown, ChevronUp, Search } from 'lucide-react';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import SwitchSelector from 'react-switch-selector';
import DateRangePicker from '../../components/Daterangepicker';
import apiClient from '../../config/apiClient';
import { useAuth } from '../../contexts/AuthContext';
import { toSwedishDateBoundaryIso } from '../../helpers/dateUtils';

const PAGE_SIZE = 100;

const modeOptions = [
    {
        label: <span className="py-[1px]">Ej attesterade</span>,
        value: 'notattested',
        id: 1,
        index: 0,
        selectedBackgroundColor: '#f59e0b',
        fontColor: '#f5f6fa',
    },
    {
        label: <span className="py-[1px]">Alla</span>,
        value: 'all',
        id: 2,
        index: 1,
        selectedBackgroundColor: '#f59e0b',
    },
];

const toUserDisplayName = (user) => {
    if (!user) return '';
    const firstName = user.firstName ?? user.FirstName ?? '';
    const lastName = user.lastName ?? user.LastName ?? '';
    return `${firstName} ${lastName}`.trim();
};

const formatAmount = (value) => {
    if (value == null || value === '') return '';
    return new Intl.NumberFormat('sv-SE', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    }).format(Number(value));
};

const formatDate = (value) => {
    if (!value) return '';
    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) return '';
    return parsed.toLocaleDateString('sv-SE');
};

const parseNullableDecimal = (value) => {
    if (value == null) return null;
    const normalized = String(value).trim().replace(',', '.');
    if (normalized === '') return null;
    const parsed = Number(normalized);
    return Number.isFinite(parsed) ? parsed : null;
};

const OrderCostAttest = () => {
    const navigate = useNavigate();
    const { user } = useAuth();

    const now = useMemo(() => {
        const value = new Date();
        value.setHours(0, 0, 0, 0);
        return value;
    }, []);

    const yearStart = useMemo(() => new Date(now.getFullYear(), 0, 1), [now]);

    const [filters, setFilters] = useState({
        mode: 'notattested',
        startDate: yearStart,
        endDate: now,
        searchText: '',
    });

    const [rows, setRows] = useState([]);
    const [attestInputs, setAttestInputs] = useState({});
    const [savingRowId, setSavingRowId] = useState(null);
    const [loading, setLoading] = useState(true);
    const [showSkeleton, setShowSkeleton] = useState(false);

    const [pagination, setPagination] = useState({
        pageNumber: 1,
        pageSize: PAGE_SIZE,
        totalCount: 0,
        totalPages: 0,
        hasPreviousPage: false,
        hasNextPage: false,
    });

    const skeletonTimerRef = useRef(null);

    const handleFilterChange = (field, value) => {
        setFilters((prev) => ({ ...prev, [field]: value }));
        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
    };

    const buildRequestBody = (pageNumber) => {
        const request = {
            mode: filters.mode,
            searchText: filters.searchText?.trim() || null,
            pagination: {
                pageNumber,
                pageSize: pagination.pageSize,
            },
            orderBy: [
                {
                    field: 'createdDateTime',
                    direction: 'desc',
                },
            ],
        };

        if (filters.mode === 'all' && filters.startDate && filters.endDate) {
            request.startDate = toSwedishDateBoundaryIso(filters.startDate, false);
            request.endDate = toSwedishDateBoundaryIso(filters.endDate, true);
        }

        return request;
    };

    const loadRows = async (pageNumber = 1, signal = null) => {
        setLoading(true);
        setRows([]);
        skeletonTimerRef.current = setTimeout(() => setShowSkeleton(true), 200);

        try {
            const response = await apiClient.post(
                '/ordercosts/search',
                buildRequestBody(pageNumber),
                signal ? { signal } : {},
            );

            const data = response?.data ?? {};
            const items = data?.items ?? [];

            setRows(items);
            setAttestInputs(items.reduce((acc, row) => {
                acc[row.id] = row.inPriceAttested ?? '';
                return acc;
            }, {}));

            setPagination((prev) => ({
                ...prev,
                pageNumber: data?.pageNumber ?? pageNumber,
                pageSize: data?.pageSize ?? prev.pageSize,
                totalCount: data?.totalCount ?? 0,
                totalPages: data?.totalPages ?? 0,
                hasPreviousPage: Boolean(data?.hasPreviousPage),
                hasNextPage: Boolean(data?.hasNextPage),
            }));
        } catch (error) {
            if (error.code === 'ERR_CANCELED') return;
            console.error('Failed to load order costs for attestation:', error);
            setRows([]);
            setAttestInputs({});
            setPagination((prev) => ({
                ...prev,
                totalCount: 0,
                totalPages: 0,
                hasPreviousPage: false,
                hasNextPage: false,
            }));
        } finally {
            clearTimeout(skeletonTimerRef.current);
            setLoading(false);
            setShowSkeleton(false);
        }
    };

    useEffect(() => {
        const controller = new AbortController();

        const timer = setTimeout(() => {
            loadRows(pagination.pageNumber, controller.signal);
        }, 200);

        return () => {
            controller.abort();
            clearTimeout(timer);
            clearTimeout(skeletonTimerRef.current);
        };
    }, [filters, pagination.pageNumber]);

    const onPageChange = (nextPage) => {
        if (nextPage < 1 || (pagination.totalPages > 0 && nextPage > pagination.totalPages)) {
            return;
        }

        setPagination((prev) => ({ ...prev, pageNumber: nextPage }));
    };

    const updateAttestInput = (rowId, value) => {
        setAttestInputs((prev) => ({
            ...prev,
            [rowId]: value,
        }));
    };

    const saveAttestPrice = async (row) => {
        if (!row?.id) return;

        const rawValue = attestInputs[row.id];
        const parsedValue = parseNullableDecimal(rawValue);
        const currentPersisted = parseNullableDecimal(row.inPriceAttested);

        if (parsedValue === currentPersisted) {
            return;
        }

        setSavingRowId(row.id);

        try {
            const attesterName = toUserDisplayName(user);

            const response = await apiClient.put(`/ordercosts/${row.id}`, {
                "updatedFields": ["inPriceAttested"],
                inPriceAttested: parsedValue,
            });

            const updated = response?.data;
            if (!updated) return;

            const merged = {
                ...row,
                ...updated,
                createdByName: updated.createdByName || row.createdByName,
                customerOrderNr: updated.customerOrderNr || row.customerOrderNr,
                orderSupplierName: updated.orderSupplierName || row.orderSupplierName,
                orderCustomerName: updated.orderCustomerName || row.orderCustomerName,
                costName: updated.costName || row.costName,
                inPriceCurrencyName: updated.inPriceCurrencyName || row.inPriceCurrencyName,
                outPriceCurrencyName: updated.outPriceCurrencyName || row.outPriceCurrencyName,
            };

            setRows((prev) => prev.map((item) => (item.id === row.id ? merged : item)));
            setAttestInputs((prev) => ({
                ...prev,
                [row.id]: merged.inPriceAttested ?? '',
            }));
        } catch (error) {
            console.error('Failed to save attested price:', error);
            setAttestInputs((prev) => ({
                ...prev,
                [row.id]: row.inPriceAttested ?? '',
            }));
        } finally {
            setSavingRowId(null);
        }
    };

    return (
        <div className="flex h-full flex-col px-7 py-2">
            <div className="ml-5 text-sm text-gray-500">Attestera kostnader</div>

            <div className="mt-3 ml-5 flex flex-wrap items-center gap-8">

                <div className="flex items-center text-xs">
                    <div className="w-55">
                        <SwitchSelector
                            options={modeOptions}
                            initialSelectedIndex={filters.mode === 'notattested' ? 0 : 1}
                            onChange={(value) => handleFilterChange('mode', value)}
                            backgroundColor="#353b48"
                            fontColor="#374151"
                        />
                    </div>
                </div>

                {filters.mode === 'all' && (
                    <DateRangePicker
                        placeholder="Valj period"
                        presets={['this-month', 'last-month', 'last-3-months', 'year-to-date']}
                        initialPresetKey="year-to-date"
                        onApply={({ startDate, endDate }) => {
                            setFilters((prev) => ({ ...prev, startDate, endDate }));
                            setPagination((prev) => ({ ...prev, pageNumber: 1 }));
                        }}
                    />
                )}

                <div className="relative ml-20 w-44">
                    <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
                    <input
                        type="text"
                        placeholder="Sök"
                        value={filters.searchText}
                        onChange={(event) => handleFilterChange('searchText', event.target.value)}
                        className="w-full text-xs border border-gray-300 rounded-sm pl-7 pr-2 py-1 focus:outline-none bg-white"
                    />
                </div>

                <div className="ml-auto mr-4 flex items-center" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <div className="ml-4 text-xs text-gray-500">
                        Rader: <strong>{pagination.totalCount}</strong>
                    </div>

                    <div className="ml-8 flex items-center">
                        <span className="mr-3 text-xs text-gray-700">
                            Sida {pagination.pageNumber} av {Math.max(1, pagination.totalPages)}
                        </span>
                        <div className="flex gap-1">
                            <button
                                type="button"
                                onClick={() => onPageChange(pagination.pageNumber - 1)}
                                disabled={loading || !pagination.hasPreviousPage}
                                className="disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                                <ArrowLeftCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                            </button>
                            <button
                                type="button"
                                onClick={() => onPageChange(pagination.pageNumber + 1)}
                                disabled={loading || !pagination.hasNextPage}
                                className="disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                                <ArrowRightCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                            </button>
                        </div>
                    </div>
                </div>
            </div>

            <div className="mt-4 flex-1 overflow-auto border-t border-gray-300 py-1">
                <table className="w-full border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <thead>
                        <tr>
                            <th className="w-[7%] px-2 py-1.5 text-left text-tiny font-medium text-gray-400">Skapad av</th>
                            <th className="w-[6%] px-2 py-1.5 text-left text-tiny font-medium text-gray-400">Datum</th>
                            <th className="w-[6%] px-2 py-1.5 text-left text-tiny font-medium text-gray-400">Ordernr</th>
                            <th className="w-[12%] px-2 py-1.5 text-left text-tiny font-medium text-gray-400">Leverantor</th>
                            <th className="w-[12%] px-2 py-1.5 text-left text-tiny font-medium text-gray-400">Kund</th>
                            <th className="w-[10%] px-2 py-1.5 text-left text-tiny font-medium text-gray-400">Kostnad</th>
                            <th className="w-[7%] px-2 py-1.5 text-right pr-9 text-tiny font-medium text-gray-400">Inpris</th>
                            <th className="w-[7%] px-2 py-1.5 text-right pr-9 text-tiny font-medium text-gray-400">Utpris</th>
                            <th className="w-[5%] px-2 py-1.5 text-right pr-3 text-tiny font-medium text-gray-400">Attestpris</th>
                            <th className="w-[5%] pl-4 py-1.5 text-left text-tiny font-medium text-gray-400">Att. av</th>
                            <th className="w-[6%] px-2 py-1.5 text-left text-tiny font-medium text-gray-400">Faktura</th>
                            <th className="px-2 py-1.5 text-left text-tiny font-medium text-gray-400">Intern not</th>
                        </tr>
                    </thead>
                    <tbody className={`divide-y divide-gray-100 ${!loading && rows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {showSkeleton ? (
                            Array.from({ length: 12 }).map((_, i) => (
                                <tr key={i} className="border-b border-gray-100">
                                    {Array.from({ length: 14 }).map((__, j) => (
                                        <td key={j} className="px-2 py-1">
                                            <Skeleton height={14} />
                                        </td>
                                    ))}
                                </tr>
                            ))
                        ) : rows.length === 0 && !loading ? (
                            <tr>
                                <td colSpan={14} className="px-4 py-6 text-center text-xs text-gray-400">
                                    Inga kostnadsrader hittades for valt urval
                                </td>
                            </tr>
                        ) : (
                            rows.map((row) => (
                                <tr key={row.id} className="border-b border-gray-100 hover:bg-amber-50">
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800">{row.createdByName || ''}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800">{formatDate(row.createdDateTime)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800">{row.customerOrderNr || ''}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800">{row.orderSupplierName || ''}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800">{row.orderCustomerName || ''}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800">{row.costName || ''}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-right text-xs text-gray-800">
                                        {formatAmount(row.inPrice)} <span className="ml-1 text-gray-400">{row.inPriceCurrencyName || ''}</span>
                                    </td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-right text-xs text-gray-800">
                                        {formatAmount(row.outPrice)} <span className="ml-1 text-gray-400">{row.outPriceCurrencyName || ''}</span>
                                    </td>
                                    <td className="p-0 relative">
                                        <div className="pl-3">
                                            <input
                                                type="text"
                                                value={attestInputs[row.id] ?? ''}
                                                onChange={(event) => updateAttestInput(row.id, event.target.value)}
                                                onBlur={() => saveAttestPrice(row)}
                                                onKeyDown={(event) => {
                                                    if (event.key === 'Enter') {
                                                        event.currentTarget.blur();
                                                    }
                                                }}
                                                disabled={savingRowId === row.id}
                                                className={`w-full h-full px-2 py-1 text-right text-xs border bg-white outline-none disabled:opacity-50 disabled:cursor-not-allowed 
                                                    ${row.inPriceAttested != null ? 'border-lime-600 focus:ring-1 focus:ring-lime-700' : 'border-red-300 focus:ring-1 focus:ring-red-400'}
                                                    `}
                                            />
                                        </div>
                                    </td>
                                    <td className="pl-4 pt-[6px] pb-[4px] text-xs text-gray-800">{row.attestedByName || ''}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800">
                                        {row.invoiceId ? (
                                            <Link
                                                to={`/finance/invoice/${row.invoiceId}`}
                                                className="underline-offset-2 hover:underline"
                                            >
                                                {row.invoiceNumber ?? row.invoiceId}
                                            </Link>
                                        ) : ''}
                                    </td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800">{row.note || ''}</td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

export default OrderCostAttest;
