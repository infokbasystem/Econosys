import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ArrowLeftCircle, ArrowRightCircle, ChevronDown, ChevronUp, Search } from 'lucide-react';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import SegmentedFilter from '../../components/SegmentedFilter';
import DateRangePicker from '../../components/DaterangePicker';
import apiClient from '../../config/apiClient';
import { useAuth } from '../../contexts/AuthContext';
import { toSwedishDateBoundaryIso } from '../../helpers/dateUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';

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
        label: <span className="py-[1px]">Alla kostnader</span>,
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

    const loadRows = async (pageNumber = 1, isActive = true) => {
        setLoading(true);
        setRows([]);
        skeletonTimerRef.current = setTimeout(() => setShowSkeleton(true), 200);

        try {
            const requestBody = buildRequestBody(pageNumber);
            const requestKey = `ordercosts:search:attest:${JSON.stringify(requestBody)}`;
            const response = await getSharedRequest(requestKey, () => apiClient.post('/ordercosts/search', requestBody));
            if (!isActive) return;

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
            console.error('Failed to load order costs for attestation:', error);
            if (!isActive) return;
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
            if (!isActive) return;
            clearTimeout(skeletonTimerRef.current);
            setLoading(false);
            setShowSkeleton(false);
        }
    };

    useEffect(() => {
        let isActive = true;

        const timer = setTimeout(() => {
            void loadRows(pagination.pageNumber, isActive);
        }, 200);

        return () => {
            isActive = false;
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
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            <div className="relative z-20 flex items-center gap-4 overflow-visible whitespace-nowrap pb-2 mt-2">
                <div className="flex items-center text-xs shrink-0 mr-20">
                    <SegmentedFilter
                        value={filters.mode}
                        onChange={(value) => handleFilterChange('mode', value)}
                        options={modeOptions}
                        theme="lime"
                    />
                </div>

                {filters.mode === 'all' && (
                    <div className="shrink-0">
                        <DateRangePicker
                            placeholder="Valj period"
                            presets={['this-month', 'last-month', 'last-3-months', 'year-to-date']}
                            initialPresetKey="year-to-date"
                            onApply={({ startDate, endDate }) => {
                                setFilters((prev) => ({ ...prev, startDate, endDate }));
                                setPagination((prev) => ({ ...prev, pageNumber: 1 }));
                            }}
                            triggerRadius="full"
                            triggerClassName="h-7 w-[260px] border-lime-600 px-3 text-xs text-gray-700 focus:border-lime-700"
                            openTriggerClassName="border-lime-700 ring-1 ring-lime-200"
                            closedTriggerClassName="border-lime-600 hover:border-lime-700"
                            widthClassName="w-60"
                        />
                    </div>
                )}

                <div className="relative w-56 shrink-0">
                    <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
                    <input
                        type="text"
                        placeholder="Sök"
                        value={filters.searchText}
                        onChange={(event) => handleFilterChange('searchText', event.target.value)}
                        className="h-7 w-full rounded-full border border-lime-600 bg-white pl-8 pr-4 text-xs text-gray-700 outline-none transition placeholder:text-gray-500 focus:border-lime-700"
                    />
                </div>

                <div className="ml-auto flex items-center gap-4 text-xs text-gray-600" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <span>Rader <strong>{pagination.totalCount}</strong></span>
                    <span>Sida {pagination.pageNumber} av {Math.max(1, pagination.totalPages)}</span>
                    <div className="flex gap-1">
                        <button
                            type="button"
                            onClick={() => onPageChange(pagination.pageNumber - 1)}
                            disabled={loading || !pagination.hasPreviousPage}
                            className="disabled:cursor-not-allowed disabled:opacity-40"
                        >
                            <ArrowLeftCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                        </button>
                        <button
                            type="button"
                            onClick={() => onPageChange(pagination.pageNumber + 1)}
                            disabled={loading || !pagination.hasNextPage}
                            className="disabled:cursor-not-allowed disabled:opacity-40"
                        >
                            <ArrowRightCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                        </button>
                    </div>
                </div>
            </div>

            <div className="border-t border-gray-300 py-1 mt-3 min-h-0 flex-1 overflow-auto">
                <table className="table-fixed w-full border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <colgroup>
                        <col style={{ width: '7%' }} />
                        <col style={{ width: '6%' }} />
                        <col style={{ width: '6%' }} />
                        <col style={{ width: '12%' }} />
                        <col style={{ width: '12%' }} />
                        <col style={{ width: '10%' }} />
                        <col style={{ width: '7%' }} />
                        <col style={{ width: '7%' }} />
                        <col style={{ width: '5%' }} />
                        <col style={{ width: '5%' }} />
                        <col style={{ width: '6%' }} />
                        <col style={{ width: 'auto' }} />
                    </colgroup>
                    <thead>
                        <tr className="text-tiny text-gray-500">
                            <th className="px-2 pt-1 pb-2 text-left text-tiny font-medium text-gray-500">Skapad av</th>
                            <th className="px-2 pt-1 pb-2 text-left text-tiny font-medium text-gray-500">Datum</th>
                            <th className="px-2 pt-1 pb-2 text-left text-tiny font-medium text-gray-500">Ordernr</th>
                            <th className="px-2 pt-1 pb-2 text-left text-tiny font-medium text-gray-500">Leverantor</th>
                            <th className="px-2 pt-1 pb-2 text-left text-tiny font-medium text-gray-500">Kund</th>
                            <th className="px-2 pt-1 pb-2 text-left text-tiny font-medium text-gray-500">Kostnad</th>
                            <th className="px-2 pt-1 pb-2 text-right pr-9 text-tiny font-medium text-gray-500">Inpris</th>
                            <th className="px-2 pt-1 pb-2 text-right pr-9 text-tiny font-medium text-gray-500">Utpris</th>
                            <th className="px-2 pt-1 pb-2 text-right pr-3 text-tiny font-medium text-gray-500">Attestpris</th>
                            <th className="pl-4 pt-1 pb-2 text-left text-tiny font-medium text-gray-500">Att. av</th>
                            <th className="px-2 pt-1 pb-2 text-left text-tiny font-medium text-gray-500">Faktura</th>
                            <th className="px-2 pt-1 pb-2 text-left text-tiny font-medium text-gray-500">Intern not</th>
                        </tr>
                    </thead>
                    <tbody className={`${!loading && rows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {showSkeleton ? (
                            Array.from({ length: 12 }).map((_, i) => (
                                <tr key={i} className="border-b border-gray-100">
                                    {Array.from({ length: 12 }).map((__, j) => (
                                        <td key={j} className="px-2 py-1">
                                            <Skeleton height={16} />
                                        </td>
                                    ))}
                                </tr>
                            ))
                        ) : rows.length === 0 && !loading ? (
                            <tr>
                                <td colSpan={12} className="px-4 py-8 text-center text-xs text-gray-400">
                                    Inga kostnadsrader hittades för valt urval
                                </td>
                            </tr>
                        ) : (
                            rows.map((row) => (
                                <tr key={row.id} className="h-6 border-b border-gray-100 hover:bg-lime-200/70">
                                    <td className="truncate px-2 py-0 text-gray-800">{row.createdByName || ''}</td>
                                    <td className="truncate px-2 py-0 text-gray-800">{formatDate(row.createdDateTime)}</td>
                                    <td className="truncate px-2 py-0 text-gray-800">{row.customerOrderNr || ''}</td>
                                    <td className="truncate px-2 py-0 text-gray-800">{row.orderSupplierName || ''}</td>
                                    <td className="truncate px-2 py-0 text-gray-800">{row.orderCustomerName || ''}</td>
                                    <td className="truncate px-2 py-0 text-gray-800">{row.costName || ''}</td>
                                    <td className="truncate px-2 py-0 text-right text-gray-800">
                                        {formatAmount(row.inPrice)} <span className="ml-1 text-gray-400">{row.inPriceCurrencyName || ''}</span>
                                    </td>
                                    <td className="truncate px-2 py-0 text-right text-gray-800">
                                        {formatAmount(row.outPrice)} <span className="ml-1 text-gray-400">{row.outPriceCurrencyName || ''}</span>
                                    </td>
                                    <td className="relative p-0">
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
                                                className={`w-full px-2 py-[3px] text-right text-xs border outline-none disabled:opacity-50 disabled:cursor-not-allowed 
                                                    ${row.inPriceAttested != null ? 'bg-lime-50 border-lime-300 focus:ring-1 focus:ring-lime-500' : 'bg-red-50/50 border-red-200 focus:ring-1 focus:ring-red-300'}
                                                    `}
                                            />
                                        </div>
                                    </td>
                                    <td className="truncate pl-4 py-0 text-gray-800">{row.attestedByName || ''}</td>
                                    <td className="truncate px-2 py-0 text-gray-800">
                                        {row.invoiceId ? (
                                            <Link
                                                to={`/finance/invoice/${row.invoiceId}`}
                                                className="text-slate-700 hover:text-slate-900 hover:underline"
                                            >
                                                {row.invoiceNumber ?? row.invoiceId}
                                            </Link>
                                        ) : ''}
                                    </td>
                                    <td className="truncate px-2 py-0 text-gray-800">{row.note || ''}</td>
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
