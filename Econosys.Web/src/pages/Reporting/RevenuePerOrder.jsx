import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Search, Calendar, Filter, Play, Loader2, CheckCircle, XCircle, ArrowLeftCircle, ArrowRightCircle } from 'lucide-react';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';
import ExcelJS from 'exceljs';

import LabeledReactSelect from '../../components/LabeledReactSelect';
import DateRangePicker from '../../components/Daterangepicker';
import apiClient from '../../config/apiClient';

const columns = [
    { key: 'orderNumber', label: 'Nr', align: 'left' },
    { key: 'createdAt', label: 'Skapad', align: 'left' },
    { key: 'customerName', label: 'Kund', align: 'left' },
    { key: 'productName', label: 'Produkt', align: 'left' },
    { key: 'construction', label: 'Konstruktion', align: 'left' },
    { key: 'materialType', label: 'Material', align: 'left' },
    { key: 'seller', label: 'Säljare', align: 'left' },
    { key: 'format', label: 'Format', align: 'left' },
    { key: 'supplierName', label: 'Levernatör', align: 'left' },
    { key: 'revenueSek', label: 'Intäkt', align: 'right' },
    { key: 'totalTbSek', label: 'Tot TB', align: 'right' },
    { key: 'markupPercent', label: 'Påslag', align: 'right' },
];

const RevenuePerOrder = () => {
    const today = new Date();
    const firstDayOfYear = new Date(today.getFullYear(), 0, 1);

    const [loading, setLoading] = useState(false);
    const [showSkeleton, setShowSkeleton] = useState(false);
    const skeletonTimerRef = useRef(null);
    const [rows, setRows] = useState([]);
    const [sellers, setSellers] = useState([]);

    const [pagination, setPagination] = useState({
        pageNumber: 1,
        pageSize: 100,
        totalCount: 0,
        totalPages: 0,
        hasPreviousPage: false,
        hasNextPage: false,
    });

    const [sortConfig, setSortConfig] = useState({ key: 'createdAt', direction: 'desc' });

    const [filters, setFilters] = useState({
        startDate: firstDayOfYear,
        endDate: today,
        sellerId: null,
        orderNumber: '',
        productName: '',
        supplierName: '',
        customerName: '',
    });

    useEffect(() => {
        const controller = new AbortController();

        const initialize = async () => {
            await loadFilterOptions(controller.signal);
            await loadReport(controller.signal, 1, filters, sortConfig);
        };

        initialize();

        return () => {
            controller.abort();
        };
    }, []);

    const toDateString = (value) => {
        if (!value) return null;
        const d = value instanceof Date ? value : new Date(value);
        return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    };

    const buildRequest = (pageNumber, activeFilters, activeSort, pageSize = pagination.pageSize) => ({
        startDate: toDateString(activeFilters.startDate),
        endDate: toDateString(activeFilters.endDate),
        sellerId: activeFilters.sellerId,
        orderNumber: activeFilters.orderNumber || null,
        productName: activeFilters.productName || null,
        supplierName: activeFilters.supplierName || null,
        customerName: activeFilters.customerName || null,
        includeInactiveOrders: false,
        pagination: {
            pageNumber,
            pageSize,
        },
        orderBy: [{
            field: activeSort.key,
            direction: activeSort.direction,
        }],
    });

    const loadFilterOptions = async (signal = null) => {
        try {
            const response = await apiClient.get('/reporting/revenue-per-order/filter-options', signal ? { signal } : {});
            const options = response?.data?.sellers ?? [];
            setSellers(options);
        } catch (error) {
            if (error.code === 'ERR_CANCELED') return;
            console.error('Failed to load revenue per order filter options:', error);
            setSellers([]);
        }
    };

    const loadReport = async (signal = null, pageNumber = 1, overrideFilters = null, overrideSort = null) => {
        const requestFilters = overrideFilters ?? filters;
        const requestSort = overrideSort ?? sortConfig;

        setLoading(true);
        setRows([]);
        skeletonTimerRef.current = setTimeout(() => setShowSkeleton(true), 200);

        try {
            const response = await apiClient.post(
                '/reporting/revenue-per-order',
                buildRequest(pageNumber, requestFilters, requestSort),
                signal ? { signal } : {}
            );

            const data = response?.data ?? {};
            setRows(data.items ?? []);
            setPagination({
                pageNumber: data.pageNumber ?? pageNumber,
                pageSize: data.pageSize ?? pagination.pageSize,
                totalCount: data.totalCount ?? 0,
                totalPages: data.totalPages ?? 0,
                hasPreviousPage: Boolean(data.hasPreviousPage),
                hasNextPage: Boolean(data.hasNextPage),
            });
        } catch (error) {
            if (error.code === 'ERR_CANCELED') return;
            console.error('Failed to load revenue per order report:', error);
            setRows([]);
            setPagination((prev) => ({ ...prev, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false }));
        } finally {
            clearTimeout(skeletonTimerRef.current);
            setLoading(false);
            setShowSkeleton(false);
        }
    };

    const handleFilterChange = (updates) => {
        setFilters((prev) => {
            const next = { ...prev, ...updates };
            loadReport(null, 1, next, sortConfig);
            return next;
        });
    };

    const handleSort = async (key) => {
        const nextSort = {
            key,
            direction: sortConfig.key === key && sortConfig.direction === 'asc' ? 'desc' : 'asc',
        };
        setSortConfig(nextSort);
        await loadReport(null, 1, filters, nextSort);
    };

    const handlePageChange = async (targetPage) => {
        if (targetPage < 1) return;
        if (pagination.totalPages > 0 && targetPage > pagination.totalPages) return;
        await loadReport(null, targetPage, filters, sortConfig);
    };

    const getSortIndicator = (key) => {
        if (sortConfig.key !== key) return '↕';
        return sortConfig.direction === 'asc' ? '▲' : '▼';
    };

    const formatAmount = (value, digits = 0) => {
        if (value === null || value === undefined) return '';
        return new Intl.NumberFormat('sv-SE', {
            minimumFractionDigits: digits,
            maximumFractionDigits: digits,
        }).format(Number(value));
    };

    const formatPercent = (value) => {
        if (value === null || value === undefined) return '';
        return `${new Intl.NumberFormat('sv-SE', {
            minimumFractionDigits: 1,
            maximumFractionDigits: 1,
        }).format(Number(value))}`;
    };

    const formatDate = (value) => {
        if (!value) return '';
        return new Date(value).toLocaleString('sv-SE', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit',
        });
    };

    const totals = useMemo(() => {
        return rows.reduce((acc, row) => {
            acc.revenueSek += Number(row.revenueSek) || 0;
            acc.totalTbSek += Number(row.totalTbSek) || 0;
            return acc;
        }, { revenueSek: 0, totalTbSek: 0 });
    }, [rows]);

    const handleExportExcel = async () => {
        if (!rows.length) return;

        const exportRows = [];
        let exportPage = 1;
        let hasNext = true;

        while (hasNext) {
            const response = await apiClient.post('/reporting/revenue-per-order', buildRequest(exportPage, filters, sortConfig, 200));
            const data = response?.data ?? {};
            const pageItems = data.items ?? [];
            exportRows.push(...pageItems);
            hasNext = Boolean(data.hasNextPage);
            exportPage += 1;
        }

        const workbook = new ExcelJS.Workbook();
        const worksheet = workbook.addWorksheet('RevenuePerOrder');

        worksheet.addRow(['Revenue Per Order']);
        worksheet.addRow([`Period: ${toDateString(filters.startDate)} - ${toDateString(filters.endDate)}`]);
        worksheet.addRow([`Genererad: ${new Date().toLocaleString('sv-SE')}`]);
        worksheet.addRow([]);
        worksheet.addRow(columns.map((col) => col.label));

        exportRows.forEach((row) => {
            worksheet.addRow([
                row.orderNumber ?? '',
                formatDate(row.createdAt),
                row.customerName ?? '',
                row.productName ?? '',
                row.construction ?? '',
                row.materialType ?? '',
                row.seller ?? '',
                row.format ?? '',
                row.supplierName ?? '',
                row.revenueSek ?? 0,
                row.totalTbSek ?? 0,
                row.markupPercent ?? '',
            ]);
        });

        const headerRowIndex = 5;
        const lastRowIndex = exportRows.length + headerRowIndex;

        worksheet.getRow(headerRowIndex).eachCell((cell) => {
            cell.font = { bold: true, size: 9 };
        });

        worksheet.eachRow((row, rowNumber) => {
            if (rowNumber !== headerRowIndex) {
                row.eachCell((cell) => {
                    cell.font = { size: 8 };
                });
            }
        });

        const rightAlignedCols = new Set([10, 11, 12]);
        for (let row = headerRowIndex; row <= lastRowIndex; row += 1) {
            for (let col = 1; col <= columns.length; col += 1) {
                const cell = worksheet.getRow(row).getCell(col);
                if (rightAlignedCols.has(col)) {
                    cell.alignment = { horizontal: 'right' };
                }
            }
        }

        for (let col = 1; col <= columns.length; col += 1) {
            let maxLength = String(columns[col - 1].label).length;
            for (let row = headerRowIndex + 1; row <= lastRowIndex; row += 1) {
                const val = worksheet.getRow(row).getCell(col).value;
                const text = val == null ? '' : String(val);
                if (text.length > maxLength) maxLength = text.length;
            }
            worksheet.getColumn(col).width = Math.min(40, Math.max(7, maxLength + 2));
        }

        worksheet.autoFilter = {
            from: { row: headerRowIndex, column: 1 },
            to: { row: lastRowIndex, column: columns.length },
        };

        const buffer = await workbook.xlsx.writeBuffer();
        const blob = new Blob([buffer], {
            type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `revenue-per-order-${toDateString(filters.startDate)}.xlsx`;
        link.click();
        URL.revokeObjectURL(url);
    };

    return (
        <div className="flex flex-col h-full p-2">
            <div className="ml-5 text-sm text-gray-500">Intäkt per order</div>

            <div className={`flex flex-wrap items-center gap-3 mt-3 ml-5 ${loading ? 'opacity-70 pointer-events-none' : ''}`}>
                <div className="w-64">
                    <DateRangePicker
                        placeholder="Valj period"
                        presets={['this-month', 'last-month', 'last-3-months', 'year-to-date']}
                        initialPresetKey="year-to-date"
                        onApply={({ startDate, endDate }) => {
                            handleFilterChange({
                                startDate,
                                endDate,
                            });
                        }}
                    />
                </div>

                <div className="w-52">
                    <LabeledReactSelect
                        label="Säljare"
                        labelWidth="w-12"
                        name="sellerId"
                        value={filters.sellerId ?? ''}
                        items={[{ id: '', name: 'Alla' }, ...sellers.map((s) => ({ id: s.id, name: s.name }))]}
                        onChange={(selected) => handleFilterChange({ sellerId: selected ? Number(selected) : null })}
                        isDisabled={loading}
                    />
                </div>


                {/* <input
                    type="text"
                    value={filters.orderNumber}
                    onChange={(e) => handleFilterChange({ orderNumber: e.target.value })}
                    placeholder="Ordernr"
                    className="text-xs w-32 border border-gray-300 rounded-sm px-2 py-1 focus:outline-none bg-white"
                />

                <input
                    type="text"
                    value={filters.productName}
                    onChange={(e) => handleFilterChange({ productName: e.target.value })}
                    placeholder="Produktnamn"
                    className="text-xs w-44 border border-gray-300 rounded-sm px-2 py-1 focus:outline-none bg-white"
                />

                <input
                    type="text"
                    value={filters.supplierName}
                    onChange={(e) => handleFilterChange({ supplierName: e.target.value })}
                    placeholder="Leverantor"
                    className="text-xs w-44 border border-gray-300 rounded-sm px-2 py-1 focus:outline-none bg-white"
                />

                <input
                    type="text"
                    value={filters.customerName}
                    onChange={(e) => handleFilterChange({ customerName: e.target.value })}
                    placeholder="Kund"
                    className="text-xs w-40 border border-gray-300 rounded-sm px-2 py-1 focus:outline-none bg-white"
                /> */}

                <div className="ml-10 text-xs text-center">
                    <a
                        href="#"
                        onClick={(event) => {
                            event.preventDefault();
                            if (loading || totals.totalInvoices === 0) return;
                            handleExportExcel();
                        }}
                        aria-disabled={loading || totals.totalInvoices === 0}
                        className={`inline-flex items-center whitespace-nowrap font-medium transition-colors ${loading || totals.totalInvoices === 0
                            ? 'text-gray-300 cursor-not-allowed pointer-events-none'
                            : 'text-slate-500 hover:text-slate-700'
                            }`}
                    >
                        Exportera till EXCEL
                    </a>
                </div>

                <div className="ml-auto flex items-center mr-4" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <div className="ml-auto flex items-center mr-15">
                        <div className="ml-4 text-xs text-gray-500">Total intäkt: <strong>{formatAmount(totals.revenueSek, 0)}</strong></div>
                        <div className="ml-4 text-xs text-gray-500">Total TB: <strong>{formatAmount(totals.totalTbSek, 0)}</strong></div>
                    </div>

                    <span className="mr-3 text-xs text-gray-700">
                        Sida {pagination.pageNumber} av {Math.max(1, pagination.totalPages)}
                    </span>
                    <div className="flex gap-1 mr-10">
                        <button
                            type="button"
                            onClick={() => handlePageChange(pagination.pageNumber - 1)}
                            disabled={loading || !pagination.hasPreviousPage}
                            className="disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                            <ArrowLeftCircle className="h-5 w-5 text-red-400 hover:text-red-500 disabled:opacity-50 disabled:text-gray-500" />
                        </button>
                        <button
                            type="button"
                            onClick={() => handlePageChange(pagination.pageNumber + 1)}
                            disabled={loading || !pagination.hasNextPage}
                            className="disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                            <ArrowRightCircle className="h-5 w-5 text-red-400 hover:text-red-600 disabled:opacity-50 disabled:text-gray-500" />
                        </button>
                    </div>
                </div>
            </div>

            <div className="border-t border-gray-300 py-1 mt-2 flex-1 overflow-auto">
                <table className="w-full border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <thead>
                        <tr>
                            {columns.map((col) => (
                                <th
                                    key={col.key}
                                    className={`px-2 py-1.5 text-[10px] font-medium text-gray-500 cursor-pointer whitespace-nowrap ${col.align === 'right' ? 'text-right' : 'text-left'}`}
                                    onClick={() => handleSort(col.key)}
                                >
                                    {col.label}
                                    <span className="ml-1 text-[10px] text-gray-400">{getSortIndicator(col.key)}</span>
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody className={`divide-y divide-gray-100 ${!loading && rows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {showSkeleton ? (
                            Array.from({ length: 12 }).map((_, i) => (
                                <tr key={i}>
                                    {columns.map((col) => (
                                        <td key={col.key} className="px-2 py-1">
                                            <Skeleton height={14} />
                                        </td>
                                    ))}
                                </tr>
                            ))
                        ) : rows.length === 0 ? (
                            <tr>
                                <td colSpan={columns.length} className="px-4 py-6 text-center text-gray-400 text-xs">
                                    Inga orderrader hittades for valda filter.
                                </td>
                            </tr>
                        ) : (
                            rows.map((row) => (
                                <tr key={row.orderId} className="hover:bg-amber-50">
                                    <td className="px-2 py-1 text-gray-800">{row.orderNumber}</td>
                                    <td className="px-2 py-1 text-gray-800 whitespace-nowrap">{formatDate(row.createdAt)}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.customerName}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.productName}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.construction}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.materialType}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.seller}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.format}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.supplierName}</td>
                                    <td className="px-2 py-1 text-right text-gray-800">{formatAmount(row.revenueSek, 0)}</td>
                                    <td className="px-2 py-1 text-right text-gray-800">{formatAmount(row.totalTbSek, 0)}</td>
                                    <td className="px-2 py-1 text-right text-gray-800">{formatPercent(row.markupPercent)}</td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>


        </div>
    );
};

export default RevenuePerOrder;
