import React, { useEffect, useRef, useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { ArrowLeftCircle, ArrowRightCircle, ChevronDown, ChevronUp, ChevronsUpDown } from 'lucide-react';
import ExcelJS from 'exceljs';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import DateRangePicker from '../../components/Daterangepicker';
import LabeledInput from '../../components/LabeledInput';
import apiClient from '../../config/apiClient';

const PAGE_SIZE = 100;
const INVOICE_SEARCH_CACHE_KEY = 'invoice-search-page-state';

const columns = [
    { key: 'invoiceNumber', label: 'Fakturanr', align: 'left', width: 'w-[5%]', sortable: true, sortField: 'invoiceNumber' },
    { key: 'customerName', label: 'Kund', align: 'left', width: 'w-[19%]', sortable: true, sortField: 'customerName' },
    { key: 'invoiceDate', label: 'Fakturadatum', align: 'left', width: 'w-[7%]', sortable: true, sortField: 'invoiceDate' },
    { key: 'dueDate', label: 'Forfallodatum', align: 'left', width: 'w-[7%]', sortable: true, sortField: 'dueDate' },
    { key: 'status', label: 'Status', align: 'center', width: 'w-[5%]', sortable: true, sortField: 'status' },
    { key: 'orderNumbers', label: 'Ordernummer', align: 'left', width: 'w-[28%]', sortable: false },
    { key: 'sumExVat', label: 'Summa exkl moms', align: 'right', width: 'w-[7%]', sortable: false },
    { key: 'sumInclVat', label: 'Summa inkl moms', align: 'right', width: 'w-[7%]', sortable: false },
    { key: 'currencyName', label: 'Valuta', align: 'left', width: 'w-[4%]', sortable: true, sortField: 'currencyName' },
    { key: 'sumInclVatSek', label: 'Summa inkl moms SEK', align: 'right', width: 'w-[7%]', sortable: false },
];

const toDateString = (date) =>
    `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;

const readCachedInvoiceSearchState = () => {
    if (typeof window === 'undefined') return null;

    try {
        const raw = window.sessionStorage.getItem(INVOICE_SEARCH_CACHE_KEY);
        if (!raw) return null;
        return JSON.parse(raw);
    } catch {
        return null;
    }
};

const InvoiceSearch = () => {
    const navigate = useNavigate();
    const now = new Date();
    now.setHours(0, 0, 0, 0);
    const yearStart = new Date(now.getFullYear(), 0, 1);

    const cachedState = readCachedInvoiceSearchState();
    const cachedFilters = cachedState?.filters
        ? {
            ...cachedState.filters,
            startDate: cachedState.filters.startDate ? new Date(cachedState.filters.startDate) : yearStart,
            endDate: cachedState.filters.endDate ? new Date(cachedState.filters.endDate) : now,
        }
        : null;

    const [rows, setRows] = useState(cachedState?.rows ?? []);
    const [loading, setLoading] = useState(!cachedState?.searchLoaded);
    const [showSkeleton, setShowSkeleton] = useState(false);
    const skeletonTimerRef = useRef(null);

    const [filters, setFilters] = useState({
        startDate: cachedFilters?.startDate ?? yearStart,
        endDate: cachedFilters?.endDate ?? now,
        invoiceNumber: cachedFilters?.invoiceNumber ?? '',
        customerName: cachedFilters?.customerName ?? '',
    });

    const [pagination, setPagination] = useState({
        pageNumber: cachedState?.pagination?.pageNumber ?? 1,
        pageSize: cachedState?.pagination?.pageSize ?? PAGE_SIZE,
        totalCount: cachedState?.pagination?.totalCount ?? 0,
        totalPages: cachedState?.pagination?.totalPages ?? 0,
        hasPreviousPage: Boolean(cachedState?.pagination?.hasPreviousPage),
        hasNextPage: Boolean(cachedState?.pagination?.hasNextPage),
    });

    const [totals, setTotals] = useState({
        totalInvoices: Number(cachedState?.totals?.totalInvoices) || 0,
        paidInvoices: Number(cachedState?.totals?.paidInvoices) || 0,
        unpaidInvoices: Number(cachedState?.totals?.unpaidInvoices) || 0,
        sumExVat: Number(cachedState?.totals?.sumExVat) || 0,
        sumInclVat: Number(cachedState?.totals?.sumInclVat) || 0,
        sumInclVatSek: Number(cachedState?.totals?.sumInclVatSek) || 0,
    });

    const [sortConfig, setSortConfig] = useState({
        key: cachedState?.sortConfig?.key ?? 'invoiceDate',
        direction: cachedState?.sortConfig?.direction ?? 'desc',
    });

    const [initialLoadCompleted, setInitialLoadCompleted] = useState(Boolean(cachedState?.searchLoaded));
    const [selectedRowId, setSelectedRowId] = useState(null);
    const [hasSearchSnapshot, setHasSearchSnapshot] = useState(Boolean(cachedState?.searchLoaded));
    const listRef = useRef(null);

    const didMountRef = useRef(false);

    useEffect(() => {
        const handlePointerDown = (event) => {
            if (listRef.current && !listRef.current.contains(event.target)) {
                setSelectedRowId(null);
            }
        };

        document.addEventListener('pointerdown', handlePointerDown);
        return () => document.removeEventListener('pointerdown', handlePointerDown);
    }, []);

    useEffect(() => {
        if (typeof window === 'undefined') return;

        window.sessionStorage.setItem(INVOICE_SEARCH_CACHE_KEY, JSON.stringify({
            rows,
            filters,
            pagination,
            totals,
            sortConfig,
            searchLoaded: hasSearchSnapshot,
        }));
    }, [rows, filters, pagination, totals, sortConfig, hasSearchSnapshot]);

    const buildRequestBody = (pageNumber, pageSize, activeFilters, activeSort = sortConfig) => {
        const parsedInvoiceNumber = activeFilters.invoiceNumber.trim() === ''
            ? null
            : Number(activeFilters.invoiceNumber);

        const sortColumn = columns.find((column) => column.key === activeSort.key);
        const sortField = sortColumn?.sortField;

        return {
            startDate: toDateString(activeFilters.startDate),
            endDate: toDateString(activeFilters.endDate),
            invoiceNumber: Number.isFinite(parsedInvoiceNumber) ? parsedInvoiceNumber : null,
            customerName: activeFilters.customerName.trim() || null,
            pagination: {
                pageNumber,
                pageSize,
            },
            orderBy: sortField
                ? [{
                    field: sortField,
                    direction: activeSort.direction,
                }]
                : [],
        };
    };

    const loadRows = async (pageNumber = 1, activeFilters = filters, signal = null, keepExistingRows = false) => {
        setLoading(true);

        if (!keepExistingRows) {
            setRows([]);
            skeletonTimerRef.current = setTimeout(() => setShowSkeleton(true), 200);
        }

        try {
            const requestBody = buildRequestBody(pageNumber, pagination.pageSize, activeFilters, sortConfig);

            const response = await apiClient.post(
                '/invoices/search',
                requestBody,
                signal ? { signal } : {}
            );

            const data = response?.data;
            setRows(data?.items ?? []);
            setPagination((prev) => ({
                ...prev,
                pageNumber: data?.pageNumber ?? pageNumber,
                pageSize: data?.pageSize ?? prev.pageSize,
                totalCount: data?.totalCount ?? 0,
                totalPages: data?.totalPages ?? 0,
                hasPreviousPage: Boolean(data?.hasPreviousPage),
                hasNextPage: Boolean(data?.hasNextPage),
            }));
            setTotals({
                totalInvoices: Number(data?.totals?.values?.totalInvoices) || 0,
                paidInvoices: Number(data?.totals?.values?.paidInvoices) || 0,
                unpaidInvoices: Number(data?.totals?.values?.unpaidInvoices) || 0,
                sumExVat: Number(data?.totals?.values?.sumExVat) || 0,
                sumInclVat: Number(data?.totals?.values?.sumInclVat) || 0,
                sumInclVatSek: Number(data?.totals?.values?.sumInclVatSek) || 0,
            });
        } catch (error) {
            if (error.code === 'ERR_CANCELED') return;
            console.error('Failed to load invoices:', error);
            if (!hasSearchSnapshot) {
                setRows([]);
                setPagination((prev) => ({
                    ...prev,
                    totalCount: 0,
                    totalPages: 0,
                    hasPreviousPage: false,
                    hasNextPage: false,
                }));
                setTotals({
                    totalInvoices: 0,
                    paidInvoices: 0,
                    unpaidInvoices: 0,
                    sumExVat: 0,
                    sumInclVat: 0,
                    sumInclVatSek: 0,
                });
            }
        } finally {
            clearTimeout(skeletonTimerRef.current);
            setHasSearchSnapshot(true);
            setLoading(false);
            setShowSkeleton(false);
        }
    };

    useEffect(() => {
        const controller = new AbortController();

        const timer = setTimeout(async () => {
            await loadRows(pagination.pageNumber, filters, controller.signal, hasSearchSnapshot);
            if (!didMountRef.current) {
                didMountRef.current = true;
                setInitialLoadCompleted(true);
            }
        }, didMountRef.current ? 250 : 0);

        return () => {
            controller.abort();
            clearTimeout(timer);
            clearTimeout(skeletonTimerRef.current);
        };
    }, [filters, pagination.pageNumber, sortConfig]);

    const onPageChange = (nextPage) => {
        if (nextPage < 1 || (pagination.totalPages > 0 && nextPage > pagination.totalPages)) {
            return;
        }

        setPagination((prev) => ({ ...prev, pageNumber: nextPage }));
    };

    const handleFilterChange = (updates) => {
        setFilters((prev) => ({ ...prev, ...updates }));
        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
    };

    const handleSort = (key) => {
        const column = columns.find((col) => col.key === key);
        if (!column?.sortable) return;

        setSortConfig((prev) => ({
            key,
            direction: prev.key === key && prev.direction === 'asc' ? 'desc' : 'asc',
        }));
        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
    };

    const renderSortIcon = (column) => {
        if (!column.sortable) return null;
        if (sortConfig.key !== column.key) return <ChevronsUpDown className="h-3.5 w-3.5 text-gray-300" />;
        return sortConfig.direction === 'asc'
            ? <ChevronUp className="h-3.5 w-3.5 text-gray-500" />
            : <ChevronDown className="h-3.5 w-3.5 text-gray-500" />;
    };

    const handleExport = async () => {
        if (loading) return;

        const workbook = new ExcelJS.Workbook();
        const sheet = workbook.addWorksheet('Fakturasok');

        sheet.addRow(columns.map((column) => column.label));
        sheet.getRow(1).font = { bold: true, size: 9 };

        try {
            let currentPage = 1;
            let totalPages = 1;

            while (currentPage <= totalPages) {
                const response = await apiClient.post('/invoices/search-list', buildRequestBody(currentPage, 200, filters));
                const data = response?.data ?? {};
                const items = data?.items ?? [];

                totalPages = Number(data?.totalPages) || 1;

                items.forEach((row) => {
                    sheet.addRow([
                        row.invoiceNumber ?? '',
                        row.customerName ?? '',
                        formatDate(row.invoiceDate),
                        formatDate(row.dueDate),
                        row.status ?? '',
                        row.orderNumbers ?? '',
                        row.sumExVat ?? 0,
                        row.sumInclVat ?? 0,
                        row.currencyName ?? '',
                        row.sumInclVatSek ?? 0,
                    ]);
                });

                currentPage += 1;
            }

            for (let col = 1; col <= columns.length; col++) {
                sheet.getColumn(col).width = 16;
            }

            const buffer = await workbook.xlsx.writeBuffer();
            const blob = new Blob([buffer], {
                type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
            });

            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `fakturasok-${toDateString(new Date())}.xlsx`;
            link.click();
            URL.revokeObjectURL(url);
        } catch (error) {
            console.error('Failed to export invoice search:', error);
        }
    };

    const formatAmount = (value) => {
        if (value == null) return '0';
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

    const getRowClass = (row) => {
        const isSelected = selectedRowId === row.id;
        if (isSelected) return 'cursor-pointer border-b border-amber-200 bg-amber-100';
        return 'cursor-pointer border-b border-gray-100 hover:bg-amber-50';
    };

    const handleOpenInvoice = (event, invoiceId) => {
        event.preventDefault();
        event.stopPropagation();
        navigate(`/finance/invoice/${invoiceId}`);
    };

    return (
        <div className="flex flex-col h-full py-2 px-7">
            <div className="ml-5 text-sm text-gray-500">Sök faktura</div>

            <div className={`flex flex-wrap items-center gap-8 mt-3 ml-5 ${loading && !hasSearchSnapshot ? 'opacity-70 pointer-events-none' : ''}`}>
                <div>
                    <DateRangePicker
                        placeholder="Valj period"
                        presets={['this-month', 'last-month', 'last-3-months', 'year-to-date']}
                        initialPresetKey="year-to-date"
                        onApply={({ startDate, endDate }) => {
                            handleFilterChange({ startDate, endDate });
                        }}
                    />
                </div>

                <div className="w-40">
                    <LabeledInput
                        label="Fakturanr"
                        margintop="0"
                        name="invoiceNumber"
                        value={filters.invoiceNumber}
                        onChange={(value) => handleFilterChange({ invoiceNumber: String(value ?? '') })}
                    />
                </div>

                <div className="w-72">
                    <LabeledInput
                        label="Kundnamn"
                        margintop="0"
                        name="customerName"
                        value={filters.customerName}
                        onChange={(value) => handleFilterChange({ customerName: value ?? '' })}
                    />
                </div>
                <div className="ml-10 text-xs text-center">
                    <a
                        href="#"
                        onClick={(event) => {
                            event.preventDefault();
                            if (loading || totals.totalInvoices === 0) return;
                            handleExport();
                        }}
                        aria-disabled={loading || totals.totalInvoices === 0}
                        className={`inline-flex items-center whitespace-nowrap font-medium transition-colors ${loading || totals.totalInvoices === 0
                            ? 'text-gray-300 cursor-not-allowed pointer-events-none'
                            : 'text-slate-500 hover:text-slate-700'
                            }`}
                    >
                        Exportera till EXCEL
                    </a>
                    {/* <button
                        type="button"
                        onClick={() => navigate('/finance/invoice/new')}
                        className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 px-4 py-[5px] ml-10"
                    >
                        Ny faktura
                    </button> */}
                </div>

                <div className="ml-auto flex items-center mr-4" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>

                    {initialLoadCompleted && (
                        <div className="ml-auto flex items-center mr-4">
                            <div className="ml-4 text-xs text-gray-500">
                                Fakturor: <strong>{totals.totalInvoices}</strong>
                            </div>
                            <div className="ml-4 text-xs text-gray-500">
                                Inkl moms SEK: <strong>{formatAmount(totals.sumInclVatSek)}</strong>
                            </div>
                        </div>
                    )}

                    {initialLoadCompleted && (
                        <div className="flex items-center ml-auto">
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
                    )}

                </div>
            </div>

            <div ref={listRef} className="border-t border-gray-300 py-1 mt-4 flex-1 overflow-auto">
                <table className="w-full border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <thead>
                        <tr>
                            {columns.map((col) => (
                                <th
                                    key={col.key}
                                    className={`${col.width} px-2 py-1.5 text-tiny font-medium text-gray-400 whitespace-nowrap ${col.align === 'right' ? 'text-right' : col.align === 'center' ? 'text-center' : 'text-left'
                                        }`}
                                >
                                    {col.sortable ? (
                                        <button
                                            type="button"
                                            onClick={() => handleSort(col.key)}
                                            className={`inline-flex items-center gap-1 ${col.align === 'right' ? 'ml-auto' : ''} hover:text-gray-600`}
                                        >
                                            <span>{col.label}</span>
                                            {renderSortIcon(col)}
                                        </button>
                                    ) : (
                                        col.label
                                    )}
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody className={`divide-y divide-gray-100 ${!loading && rows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {showSkeleton ? (
                            Array.from({ length: 12 }).map((_, i) => (
                                <tr key={i} className="border-b border-gray-100">
                                    {columns.map((col) => (
                                        <td key={col.key} className="px-2 py-1">
                                            <Skeleton height={14} />
                                        </td>
                                    ))}
                                </tr>
                            ))
                        ) : rows.length === 0 && !loading && initialLoadCompleted ? (
                            <tr>
                                <td colSpan={columns.length} className="px-4 py-6 text-center text-gray-400 text-xs">
                                    Inga fakturor hittades for valt urval
                                </td>
                            </tr>
                        ) : (
                            rows.map((row) => (
                                <tr
                                    key={row.id}
                                    className={getRowClass(row)}
                                    onClick={() => setSelectedRowId((prev) => (prev === row.id ? null : row.id))}
                                >
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800">
                                        <Link
                                            to={`/finance/invoice/${row.id}`}
                                            onClick={(event) => event.stopPropagation()}
                                            className="underline-offset-2 hover:underline"
                                        >
                                            {row.invoiceNumber ?? row.id}
                                        </Link>
                                    </td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800">{row.customerName}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800">{formatDate(row.invoiceDate)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800">{formatDate(row.dueDate)}</td>
                                    <td className="px-2 pt-[4px] pb-[4px] text-center text-xs text-gray-800">
                                        {String(row.status ?? '').toLowerCase() === 'credit' ? (
                                            <span className="inline-flex items-center rounded-full bg-red-400 text-white px-2 py-[1px] text-[10px] mr-2">
                                                Kredit
                                            </span>
                                        ) : ''}
                                    </td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800">{row.orderNumbers}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-right text-xs text-gray-800">{formatAmount(row.sumExVat)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-right text-xs text-gray-800">{formatAmount(row.sumInclVat)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800">{row.currencyName}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-right text-xs text-gray-800">{formatAmount(row.sumInclVatSek)}</td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>

        </div>
    );
};

export default InvoiceSearch;