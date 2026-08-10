import React, { useEffect, useMemo, useRef, useState } from 'react';
import { ArrowLeftCircle, ArrowRightCircle, ChevronDown, ChevronUp, Search } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import apiClient from '../../config/apiClient';
import LabeledSwitch from '../../components/LabeledSwitch';
import { getSwedishTodayDateString } from '../../helpers/dateUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';

const PAGE_SIZE = 20;
const DEVIATIONS_OVERVIEW_CACHE_KEY = 'deviations-overview-page-state';
const MONTH_LABELS = ['Jan', 'Feb', 'Mar', 'Apr', 'Maj', 'Jun', 'Jul', 'Aug', 'Sep', 'Okt', 'Nov', 'Dec'];

const searchColumns = [
    { key: 'responsibleUserName', label: 'Ansvarig', align: 'left', width: '8%' },
    { key: 'deviationNr', label: 'Avvikelsenr', align: 'left', width: '6%' },
    { key: 'customerOrderNr', label: 'Ordernr', align: 'left', width: '5%' },
    { key: 'customerName', label: 'Kund', align: 'left', width: '14%' },
    { key: 'supplierName', label: 'Leverantör', align: 'left', width: '10%' },
    { key: 'openDays', label: 'Dagar öppen', align: 'right', width: '7%' },
    { key: 'status', label: 'Status', align: 'left', width: '6%' },
    { key: 'deviationProcessCode', label: 'Process', align: 'left', width: '7%' },
    { key: 'description', label: 'Beskrivning', align: 'left' },
];

const initialPagination = {
    pageNumber: 1,
    pageSize: PAGE_SIZE,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
};

const initialFilters = {
    searchTerm: '',
    status: '',
    responsibleUserId: '',
    customerId: '',
    supplierId: '',
    isInternal: '',
    includeOpen: false,
    includeInternal: false,
};

const defaultOverview = {
    year: new Date().getFullYear(),
    openItems: [],
    monthlyStats: [],
};

const metricDefinitions = [
    {
        key: 'openedCount',
        label: 'Nya reklamationer',
        format: (value) => formatNumber(value, 0),
        total: (rows) => rows.reduce((sum, row) => sum + (row.openedCount || 0), 0),
    },
    {
        key: 'closedCount',
        label: 'Avslutade reklamationer',
        format: (value) => formatNumber(value, 0),
        total: (rows) => rows.reduce((sum, row) => sum + (row.closedCount || 0), 0),
    },
    {
        key: 'openAtMonthEndCount',
        label: 'Pågående vid månadens slut',
        format: (value) => formatNumber(value, 0),
        total: (rows) => rows.at(-1)?.openAtMonthEndCount || 0,
    },
    {
        key: 'averageHandlingDays',
        label: 'Medel hanteringstid arbetsdagar',
        format: (value) => formatNumber(value, 1),
        total: (rows) => {
            const weightedDays = rows.reduce((sum, row) => sum + ((row.averageHandlingDays || 0) * (row.closedCount || 0)), 0);
            const closedCount = rows.reduce((sum, row) => sum + (row.closedCount || 0), 0);
            return closedCount === 0 ? 0 : weightedDays / closedCount;
        },
    },
    {
        key: 'handledWithin10DaysPercent',
        label: '% hanterade under 10 dagar',
        format: (value) => formatNumber(value, 1),
        total: (rows) => {
            const weightedPercent = rows.reduce((sum, row) => sum + ((row.handledWithin10DaysPercent || 0) * (row.closedCount || 0)), 0);
            const closedCount = rows.reduce((sum, row) => sum + (row.closedCount || 0), 0);
            return closedCount === 0 ? 0 : weightedPercent / closedCount;
        },
    },
    {
        key: 'actualInternalCostSek',
        label: 'Econopack kostnader',
        format: (value) => formatNumber(value, 0),
        total: (rows) => rows.reduce((sum, row) => sum + (row.actualInternalCostSek || 0), 0),
    },
];

function formatNumber(value, digits = 0) {
    if (value === null || value === undefined) return '';
    return new Intl.NumberFormat('sv-SE', {
        minimumFractionDigits: digits,
        maximumFractionDigits: digits,
    }).format(Number(value));
}

const getProcessLabel = (code) => {
    switch (code) {
        case 'ORDER':
            return 'Beställning';
        case 'DELIVERY':
            return 'Leverans';
        case 'INVOICE':
            return 'Fakturering';
        case 'SERVICE':
            return 'Tjänst';
        case 'OTHER':
            return 'Övrigt';
        default:
            return '???';
    }
};

const getStatusLabel = (status, deviationClosed) => {
    return status === 'CLOSED' && deviationClosed != null ? 'Stängd' : 'Öppen';
};

const readCachedOverviewPageState = () => {
    if (typeof window === 'undefined') return null;

    try {
        const raw = window.sessionStorage.getItem(DEVIATIONS_OVERVIEW_CACHE_KEY);
        if (!raw) return null;
        return JSON.parse(raw);
    } catch {
        return null;
    }
};

const DeviationsOverview = () => {
    const cachedState = readCachedOverviewPageState();
    const navigation = useNavigate();
    const currentMonth = new Date().getMonth() + 1;
    const [overviewLoading, setOverviewLoading] = useState(!cachedState?.overviewLoaded);
    const [searchLoading, setSearchLoading] = useState(!cachedState?.searchLoaded);
    const [hasOverviewSnapshot, setHasOverviewSnapshot] = useState(Boolean(cachedState?.overviewLoaded));
    const [hasSearchSnapshot, setHasSearchSnapshot] = useState(Boolean(cachedState?.searchLoaded));

    const [overview, setOverview] = useState(cachedState?.overview ?? defaultOverview);
    const [rows, setRows] = useState(cachedState?.rows ?? []);
    const [selectedOpenItemId, setSelectedOpenItemId] = useState(null);
    const [selectedSearchRowId, setSelectedSearchRowId] = useState(null);
    const openListRef = useRef(null);
    const searchListRef = useRef(null);
    const [showStatisticsInfo, setShowStatisticsInfo] = useState(false);
    const [searchInput, setSearchInput] = useState(cachedState?.searchInput ?? '');
    const [filters, setFilters] = useState(cachedState?.filters ?? initialFilters);
    const [pagination, setPagination] = useState(cachedState?.pagination ?? initialPagination);
    const [sortConfig, setSortConfig] = useState(cachedState?.sortConfig ?? { key: 'openDays', direction: 'desc' });

    useEffect(() => {
        const timer = setTimeout(() => {
            handleFilterChange({ searchTerm: searchInput });
        }, 400);
        return () => clearTimeout(timer);
    }, [searchInput]);

    useEffect(() => {
        const handlePointerDown = (event) => {
            if (openListRef.current && !openListRef.current.contains(event.target)) {
                setSelectedOpenItemId(null);
            }

            if (searchListRef.current && !searchListRef.current.contains(event.target)) {
                setSelectedSearchRowId(null);
            }
        };

        document.addEventListener('pointerdown', handlePointerDown);

        return () => document.removeEventListener('pointerdown', handlePointerDown);
    }, []);

    useEffect(() => {
        if (typeof window === 'undefined') return;

        window.sessionStorage.setItem(DEVIATIONS_OVERVIEW_CACHE_KEY, JSON.stringify({
            overview,
            rows,
            searchInput,
            filters,
            pagination,
            sortConfig,
            overviewLoaded: hasOverviewSnapshot,
            searchLoaded: hasSearchSnapshot,
        }));
    }, [overview, rows, searchInput, filters, pagination, sortConfig, hasOverviewSnapshot, hasSearchSnapshot]);

    const monthlyStatsByMonth = useMemo(
        () => new Map((overview.monthlyStats ?? []).map((month) => [month.monthNumber, month])),
        [overview.monthlyStats]
    );

    const visibleMonthlyStats = useMemo(
        () => (overview.monthlyStats ?? []).filter((month) => month.monthNumber <= currentMonth),
        [overview.monthlyStats, currentMonth]
    );

    const buildSearchPayload = (pageNumber = pagination.pageNumber, pageSize = pagination.pageSize) => ({
        searchTerm: filters.searchTerm || null,
        status: filters.includeOpen ? null : 'CLOSED',
        responsibleUserId: filters.responsibleUserId ? Number(filters.responsibleUserId) : null,
        customerId: filters.customerId ? Number(filters.customerId) : null,
        supplierId: filters.supplierId ? Number(filters.supplierId) : null,
        isInternal: filters.includeInternal ? null : false,
        pagination: {
            pageNumber,
            pageSize,
        },
        orderBy: [{
            field: sortConfig.key,
            direction: sortConfig.direction,
        }],
    });

    useEffect(() => {
        let isActive = true;

        const loadInitial = async () => {
            setOverviewLoading(true);

            try {
                const response = await getSharedRequest('deviations:overview', () => apiClient.get('/deviations/overview'));
                if (!isActive) return;

                setOverview({
                    year: response?.data?.year ?? new Date().getFullYear(),
                    openItems: response?.data?.openItems ?? [],
                    monthlyStats: response?.data?.monthlyStats ?? [],
                });
            } catch (error) {
                console.error('Failed to load deviation overview data:', error);
                if (!isActive) return;
                if (!hasOverviewSnapshot) {
                    setOverview(defaultOverview);
                }
            } finally {
                if (isActive) {
                    setHasOverviewSnapshot(true);
                    setOverviewLoading(false);
                }
            }
        };

        loadInitial();

        return () => {
            isActive = false;
        };
    }, []);

    useEffect(() => {
        let isActive = true;

        const loadRows = async () => {
            setSearchLoading(true);
            const requestPayload = buildSearchPayload();
            const requestKey = `deviations:search:${JSON.stringify(requestPayload)}`;

            try {
                const response = await getSharedRequest(requestKey, () => apiClient.post('/deviations/search', requestPayload));
                if (!isActive) return;

                const data = response?.data ?? {};
                setRows(data.items ?? []);
                setPagination((prev) => ({
                    ...prev,
                    pageNumber: data.pageNumber ?? prev.pageNumber,
                    pageSize: data.pageSize ?? prev.pageSize,
                    totalCount: data.totalCount ?? 0,
                    totalPages: data.totalPages ?? 0,
                    hasPreviousPage: Boolean(data.hasPreviousPage),
                    hasNextPage: Boolean(data.hasNextPage),
                }));
            } catch (error) {
                console.error('Failed to load deviations search:', error);
                if (!isActive) return;
                if (!hasSearchSnapshot) {
                    setRows([]);
                    setPagination((prev) => ({
                        ...prev,
                        totalCount: 0,
                        totalPages: 0,
                        hasPreviousPage: false,
                        hasNextPage: false,
                    }));
                }
            } finally {
                if (isActive) {
                    setHasSearchSnapshot(true);
                    setSearchLoading(false);
                }
            }
        };

        loadRows();

        return () => {
            isActive = false;
        };
    }, [filters, pagination.pageNumber, pagination.pageSize, sortConfig]);

    const handleFilterChange = (updates) => {
        setFilters((prev) => ({ ...prev, ...updates }));
        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
    };

    const handleSort = (key) => {
        setSortConfig((prev) => ({
            key,
            direction: prev.key === key && prev.direction === 'asc' ? 'desc' : 'asc',
        }));
        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
    };

    const handlePageChange = (targetPage) => {
        if (targetPage < 1) return;
        if (pagination.totalPages > 0 && targetPage > pagination.totalPages) return;
        setPagination((prev) => ({ ...prev, pageNumber: targetPage }));
    };

    const handleExportSearchToExcel = () => {
        const headers = ['Ansvarig', 'Avvikelsenr', 'Ordernr', 'Kund', 'Leverantör', 'Dagar öppen', 'Status', 'Process', 'Beskrivning'];
        const escapeCsv = (value) => `"${String(value ?? '').replace(/"/g, '""')}"`;
        const lines = [
            headers.map(escapeCsv).join(';'),
            ...rows.map((row) => [
                row.responsibleUserName,
                row.deviationNr,
                row.customerOrderNr,
                row.customerName,
                row.supplierName,
                row.openDays,
                getStatusLabel(row.status, row.deviationClosed),
                getProcessLabel(row.deviationProcessCode),
                row.description,
            ].map(escapeCsv).join(';')),
        ];

        const blob = new Blob([`\ufeff${lines.join('\n')}`], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        const datePart = getSwedishTodayDateString();

        link.href = url;
        link.download = `avvikelser-soklista-${datePart}.csv`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    };

    const handleExportStatisticsToExcel = () => {
        const headers = ['Nyckeltal', ...MONTH_LABELS, 'TOT'];
        const escapeCsv = (value) => `"${String(value ?? '').replace(/"/g, '""')}"`;

        const lines = [
            headers.map(escapeCsv).join(';'),
            ...metricDefinitions.map((metric) => {
                const monthValues = MONTH_LABELS.map((_, index) => {
                    const monthNumber = index + 1;
                    const month = monthlyStatsByMonth.get(monthNumber);
                    return monthNumber <= currentMonth && month ? metric.format(month[metric.key]) : '';
                });

                return [
                    metric.label,
                    ...monthValues,
                    metric.format(metric.total(visibleMonthlyStats)),
                ].map(escapeCsv).join(';');
            }),
        ];

        const blob = new Blob([`\ufeff${lines.join('\n')}`], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');

        link.href = url;
        link.download = `avvikelser-statistik-${overview.year}.csv`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    };

    const getOpenItemRowClass = (item) => {
        const isSelected = selectedOpenItemId === item.id;
        if (isSelected) return 'cursor-pointer bg-red-800 text-white';
        if (item.openDays > 10) return 'cursor-pointer bg-red-400 text-white hover:bg-red-500';
        return 'cursor-pointer hover:bg-gray-100';
    };

    const getSearchRowClass = (row) => {
        const isSelected = selectedSearchRowId === row.id;
        if (isSelected) return 'cursor-pointer border-b border-amber-200 bg-amber-100';
        return 'cursor-pointer border-b border-gray-100 hover:bg-amber-50';
    };

    const handleOpenDeviation = (event, id) => {
        event.preventDefault();
        event.stopPropagation();
        // window.open(`/management/deviations/${id}`, '_blank', 'noopener,noreferrer');
        navigation(`/management/deviations/${id}`); // För att även navigera i den nuvarande fliken
    };

    const handleCreateDeviation = () => {
        navigation('/management/deviations/new');
    };

    const showOverviewSkeleton = overviewLoading && !hasOverviewSnapshot;
    const showSearchSkeleton = searchLoading && !hasSearchSnapshot;

    return (
        <div className="flex h-full flex-col gap-5 pt-1 pb-4 ps-5 pe-10">
            <div className="grid grid-cols-[1.1fr_1.5fr] gap-20 px-2 mt-2">
                <section className="overflow-hidden">
                    <div className="px-4 text-xs text-gray-500 text-center">Öppna avvikelser</div>
                    <div className="px-4 mt-1 text-xs text-gray-500 text-center">
                        <span className="inline-block h-2 w-2 bg-red-700 mr-2 align-middle"></span>
                        över 10 dagar
                    </div>
                    <div ref={openListRef} className="max-h-56 overflow-auto mt-2">
                        <table className="table-fixed w-full border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                            <colgroup>
                                <col style={{ width: '20%' }} />
                                <col style={{ width: '14%' }} />
                                <col style={{ width: '14%' }} />
                                <col style={{ width: '22%' }} />
                                <col style={{ width: '22%' }} />
                                <col style={{ width: '8%' }} />
                            </colgroup>
                            <thead>
                                <tr className="border-b border-gray-200 text-tiny text-gray-500">
                                    <th className="px-2 py-1 text-left font-medium">Ansvarig</th>
                                    <th className="px-2 py-1 text-left font-medium">Avvikelsenr</th>
                                    <th className="px-2 py-1 text-left font-medium">Ordernr</th>
                                    <th className="px-2 py-1 text-left font-medium">Kund</th>
                                    <th className="px-2 py-1 text-left font-medium">Leverantör</th>
                                    <th className="px-2 py-1 text-right font-medium">Dagar</th>
                                </tr>
                            </thead>
                            <tbody>
                                {showOverviewSkeleton ? (
                                    Array.from({ length: 6 }).map((_, index) => (
                                        <tr key={index}>
                                            <td className="px-2 py-1" colSpan={6}><Skeleton height={16} /></td>
                                        </tr>
                                    ))
                                ) : overview.openItems.length === 0 ? (
                                    <tr>
                                        <td colSpan={6} className="px-4 py-6 text-center text-gray-400">Inga oppna avvikelser.</td>
                                    </tr>
                                ) : (
                                    overview.openItems.map((item) => (
                                        <tr
                                            key={item.id}
                                            onClick={() => setSelectedOpenItemId((prev) => (prev === item.id ? null : item.id))}
                                            className={getOpenItemRowClass(item)}
                                        >
                                            <td className="truncate px-2 py-1">{item.responsibleUserName}</td>
                                            <td className="truncate px-2 py-1">
                                                <button
                                                    type="button"
                                                    onClick={(event) => handleOpenDeviation(event, item.id)}
                                                    className="underline-offset-2 hover:underline"
                                                >
                                                    {item.deviationNr ?? item.id}
                                                </button>
                                            </td>
                                            <td className="truncate px-2 py-1">{item.customerOrderNr}</td>
                                            <td className="truncate px-2 py-1">{item.customerName}</td>
                                            <td className="truncate px-2 py-1">{item.supplierName}</td>
                                            <td className="px-2 py-1 text-right">{formatNumber(item.openDays, 0)}</td>
                                        </tr>
                                    ))
                                )}
                            </tbody>
                        </table>
                    </div>
                </section>

                <section className="relative overflow-hidden">
                    <div className="relative px-4 text-xs text-center">
                        <div className="flex justify-center">
                            <a
                                href="#"
                                onClick={(event) => {
                                    event.preventDefault();
                                    if (showOverviewSkeleton) return;
                                    handleExportStatisticsToExcel();
                                }}
                                aria-disabled={showOverviewSkeleton}
                                className={`inline-flex items-center whitespace-nowrap font-medium transition-colors ${showOverviewSkeleton
                                    ? 'text-gray-300 cursor-not-allowed pointer-events-none'
                                    : 'text-slate-500 hover:text-slate-700'
                                    }`}
                            >
                                Exportera till EXCEL
                            </a>
                        </div>
                        <div className="absolute right-4 top-0 inline-flex items-center">
                            {showStatisticsInfo && (
                                <>
                                    <div
                                        className="fixed inset-0 z-30"
                                        onClick={() => setShowStatisticsInfo(false)}
                                    />
                                    <div
                                        className="absolute right-full mr-3 top-0 z-40 w-[420px] border border-gray-400 bg-yellow-50 px-8 py-6 text-xs text-yellow-900 shadow-sm"
                                        onClick={(event) => event.stopPropagation()}
                                    >
                                        <p>Baseras på det som ordererkänts under respektive år</p>
                                        <p className="mt-2">Ink.värde beräknas utifrån producerat antal och inköpspriset med dåvarande valutakurs</p>
                                        <p className="mt-2">Fsg.värde beräknas utifrån producerat antal och försäljningpriset med dåvarande valutakurs</p>
                                        <p className="mt-2">Om econopack är trp.ansvariga så visas fraktkostnaden utifrån der arkiverade kalkylens fraktkolumn</p>
                                    </div>
                                </>
                            )}
                            <a
                                href="#"
                                onClick={(event) => {
                                    event.preventDefault();
                                    setShowStatisticsInfo((prev) => !prev);
                                }}
                                className="whitespace-nowrap font-medium text-slate-500 hover:text-slate-700 transition-colors"
                            >
                                Visa info
                            </a>
                        </div>
                    </div>
                    <div className="px-4 mt-1 text-xs text-gray-500 text-center font-semibold">{overview.year}</div>
                    <div className="overflow-auto mt-2">
                        <table className="min-w-full border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                            <thead>
                                <tr className="border-b border-gray-200 text-tiny text-gray-500">
                                    <th className="sticky left-0 text-left font-medium">&nbsp;</th>
                                    {MONTH_LABELS.map((month) => (
                                        <th key={month} className="px-2 py-1 text-right font-medium">{month}</th>
                                    ))}
                                    <th className="px-2 py-1 text-right font-medium">TOT</th>
                                </tr>
                            </thead>
                            <tbody>
                                {showOverviewSkeleton ? (
                                    Array.from({ length: 6 }).map((_, index) => (
                                        <tr key={index}>
                                            <td className="px-3 py-1" colSpan={14}><Skeleton height={16} /></td>
                                        </tr>
                                    ))
                                ) : (
                                    metricDefinitions.map((metric) => (
                                        <tr key={metric.key} className="border-b border-gray-200 last:border-b-0">
                                            <td className="sticky left-0 px-3 py-2 text-left text-gray-700">{metric.label}</td>
                                            {MONTH_LABELS.map((_, index) => {
                                                const monthNumber = index + 1;
                                                const month = monthlyStatsByMonth.get(monthNumber);
                                                return (
                                                    <td key={`${metric.key}-${monthNumber}`} className="px-2 py-2 text-right text-gray-800">
                                                        {monthNumber <= currentMonth && month ? metric.format(month[metric.key]) : ''}
                                                    </td>
                                                );
                                            })}
                                            <td className="px-2 py-2 text-right text-gray-900">{metric.format(metric.total(visibleMonthlyStats))}</td>
                                        </tr>
                                    ))
                                )}
                            </tbody>
                        </table>
                    </div>
                </section>
            </div>

            <section className="flex min-h-0 flex-1 flex-col mt-10">
                <div className="flex items-center gap-4 overflow-x-auto whitespace-nowrap pb-1">
                    <button
                        type="button"
                        onClick={handleCreateDeviation}
                        className="w-40 shadow-md/30 text-xs text-white bg-lime-600 hover:bg-lime-700 px-4 py-[5px]"
                    >
                        Skapa ny avvikelse
                    </button>

                    <div className="relative ml-20 w-44">
                        <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
                        <input
                            type="text"
                            value={searchInput}
                            onChange={(event) => setSearchInput(event.target.value)}
                            placeholder="Sök"
                            className="w-full text-xs border border-gray-300 rounded-sm pl-7 pr-2 py-1 focus:outline-none bg-white"
                        />
                    </div>

                    <LabeledSwitch
                        label="Inkludera öppna"
                        name="includeOpen"
                        value={filters.includeOpen}
                        onChange={(_rowId, _field, checked) => handleFilterChange({ includeOpen: checked })}
                        disabled={searchLoading}
                        marginLeft={16}
                        containerClassName="shrink-0"
                    />

                    <LabeledSwitch
                        label="Inkludera interna"
                        name="includeInternal"
                        value={filters.includeInternal}
                        onChange={(_rowId, _field, checked) => handleFilterChange({ includeInternal: checked })}
                        disabled={searchLoading}
                        containerClassName="shrink-0"
                    />

                    <a
                        href="#"
                        onClick={(event) => {
                            event.preventDefault();
                            if (rows.length === 0) return;
                            handleExportSearchToExcel();
                        }}
                        aria-disabled={rows.length === 0}
                        className={`ml-20 inline-flex items-center whitespace-nowrap text-xs font-medium transition-colors ${rows.length === 0
                            ? 'text-gray-300 cursor-not-allowed pointer-events-none'
                            : 'text-slate-500 hover:text-slate-700'
                            }`}
                    >
                        Exportera till Excel
                    </a>

                    <div className="ml-auto flex items-center gap-4 text-xs text-gray-600">
                        <span>Rader <strong>{pagination.totalCount}</strong></span>
                        <span>Sida {pagination.pageNumber} av {Math.max(1, pagination.totalPages)}</span>
                        <div className="flex gap-1">
                            <button
                                type="button"
                                onClick={() => handlePageChange(pagination.pageNumber - 1)}
                                disabled={searchLoading || !pagination.hasPreviousPage}
                                className="disabled:cursor-not-allowed disabled:opacity-40"
                            >
                                <ArrowLeftCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                            </button>
                            <button
                                type="button"
                                onClick={() => handlePageChange(pagination.pageNumber + 1)}
                                disabled={searchLoading || !pagination.hasNextPage}
                                className="disabled:cursor-not-allowed disabled:opacity-40"
                            >
                                <ArrowRightCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                            </button>
                        </div>
                    </div>
                </div>

                <div ref={searchListRef} className="border-t border-gray-300 py-1 mt-4 min-h-0 flex-1 overflow-auto">
                    <table className="table-fixed w-full border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                        <colgroup>
                            {searchColumns.map((column) => (
                                <col key={column.key} {...(column.width ? { style: { width: column.width } } : {})} />
                            ))}
                        </colgroup>
                        <thead>
                            <tr>
                                {searchColumns.map((column) => (
                                    <th
                                        key={column.key}
                                        onClick={() => handleSort(column.key)}
                                        className={`cursor-pointer px-2 py-2 text-[10px] font-medium text-gray-500 ${column.align === 'right' ? 'text-right' : 'text-left'}`}
                                    >
                                        <span className="inline-flex items-center gap-1">
                                            {column.label}
                                            {sortConfig.key === column.key ? (
                                                sortConfig.direction === 'asc' ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />
                                            ) : (
                                                <ChevronUp className="h-3 w-3 opacity-0" />
                                            )}
                                        </span>
                                    </th>
                                ))}
                            </tr>
                        </thead>
                        <tbody className={`${!searchLoading && rows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                            {showSearchSkeleton ? (
                                Array.from({ length: 12 }).map((_, index) => (
                                    <tr key={index}>
                                        <td colSpan={searchColumns.length} className="px-2 py-1"><Skeleton height={16} /></td>
                                    </tr>
                                ))
                            ) : rows.length === 0 ? (
                                <tr>
                                    <td colSpan={searchColumns.length} className="px-4 py-8 text-center text-gray-400">Inga avvikelser hittades.</td>
                                </tr>
                            ) : (
                                rows.map((row) => (
                                    <tr
                                        key={row.id}
                                        onClick={() => setSelectedSearchRowId((prev) => (prev === row.id ? null : row.id))}
                                        className={getSearchRowClass(row)}
                                    >
                                        <td className="truncate px-2 py-1 text-gray-800">{row.responsibleUserName}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">
                                            <button
                                                type="button"
                                                onClick={(event) => handleOpenDeviation(event, row.id)}
                                                className="underline-offset-2 hover:underline"
                                            >
                                                {row.deviationNr ?? row.id}
                                            </button>
                                        </td>
                                        <td className="truncate px-2 py-1 text-gray-800">{row.customerOrderNr}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{row.customerName}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{row.supplierName}</td>
                                        <td className="pr-6 py-1 text-right text-gray-800">{formatNumber(row.openDays, 0)}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{getStatusLabel(row.status, row.deviationClosed)}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{getProcessLabel(row.deviationProcessCode)}</td>
                                        <td className="truncate px-2 py-1 text-gray-800" title={row.description}>{row.description}</td>
                                    </tr>
                                ))
                            )}
                        </tbody>
                    </table>
                </div>
            </section>
        </div>
    );
};

export default DeviationsOverview;