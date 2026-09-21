import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Search, Calendar, Filter, Play, Loader2, CheckCircle, XCircle, ArrowLeftCircle, ArrowRightCircle } from 'lucide-react';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';
import ExcelJS from 'exceljs';

import LabeledReactSelect from '../../../components/LabeledReactSelect';
import DateRangePicker from '../../../components/DaterangePicker';
import apiClient from '../../../config/apiClient';
import { getSharedRequest } from '../../../helpers/sharedRequest';

const columns = [
    { key: 'orderNumber', label: 'Nr', align: 'left', width: '4%' },
    { key: 'createdAt', label: 'Skapad', align: 'left', width: '8%' },
    { key: 'customerName', label: 'Kund', align: 'left', width: '12%' },
    { key: 'productName', label: 'Produkt', align: 'left', width: '12%' },
    { key: 'construction', label: 'Konstruktion', align: 'left', width: '10%' },
    { key: 'materialType', label: 'Material', align: 'left', width: '8%' },
    { key: 'seller', label: 'Säljare', align: 'left', width: '8%' },
    { key: 'format', label: 'Format', align: 'left', width: '8%' },
    { key: 'supplierName', label: 'Levernatör', align: 'left', width: '12%' },
    { key: 'revenueSek', label: 'Intäkt', align: 'right', width: '5%' },
    { key: 'totalTbSek', label: 'Tot TB', align: 'right', width: '5%' },
    { key: 'markupPercent', label: 'Påslag', align: 'right', width: '5%' },
];

const RevenuePerOrder = () => {
    const today = new Date();
    const firstDayOfYear = new Date(today.getFullYear(), 0, 1);

    const [loading, setLoading] = useState(false);
    const [showSkeleton, setShowSkeleton] = useState(false);
    const skeletonTimerRef = useRef(null);
    const reportRequestRef = useRef(0);
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
        let isActive = true;

        const initialize = async () => {
            await loadFilterOptions(isActive);
            await loadReport(isActive, 1, filters, sortConfig);
        };

        void initialize();

        return () => {
            isActive = false;
            clearTimeout(skeletonTimerRef.current);
            reportRequestRef.current += 1;
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

    const loadFilterOptions = async (isActive = true) => {
        try {
            const response = await getSharedRequest(
                'reporting:revenue-per-order:filter-options',
                () => apiClient.get('/reporting/revenue-per-order/filter-options'),
            );
            if (!isActive) return;
            const options = response?.data?.sellers ?? [];
            setSellers(options);
        } catch (error) {
            console.error('Failed to load revenue per order filter options:', error);
            if (!isActive) return;
            setSellers([]);
        }
    };

    const loadReport = async (isActive = true, pageNumber = 1, overrideFilters = null, overrideSort = null) => {
        const requestFilters = overrideFilters ?? filters;
        const requestSort = overrideSort ?? sortConfig;
        const requestId = reportRequestRef.current + 1;
        reportRequestRef.current = requestId;

        clearTimeout(skeletonTimerRef.current);
        setLoading(true);
        setRows([]);
        setShowSkeleton(false);
        const skeletonTimer = setTimeout(() => {
            if (isActive && reportRequestRef.current === requestId) {
                setShowSkeleton(true);
            }
        }, 200);
        skeletonTimerRef.current = skeletonTimer;

        try {
            const requestBody = buildRequest(pageNumber, requestFilters, requestSort);
            const requestKey = `reporting:revenue-per-order:${JSON.stringify(requestBody)}`;
            const response = await getSharedRequest(
                requestKey,
                () => apiClient.post('/reporting/revenue-per-order', requestBody),
            );
            if (!isActive || reportRequestRef.current !== requestId) return;

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
            console.error('Failed to load revenue per order report:', error);
            if (!isActive || reportRequestRef.current !== requestId) return;
            setRows([]);
            setPagination((prev) => ({ ...prev, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false }));
        } finally {
            clearTimeout(skeletonTimer);
            if (skeletonTimerRef.current === skeletonTimer) {
                skeletonTimerRef.current = null;
            }
            if (!isActive || reportRequestRef.current !== requestId) return;
            setLoading(false);
            setShowSkeleton(false);
        }
    };

    const handleFilterChange = (updates) => {
        setFilters((prev) => {
            const next = { ...prev, ...updates };
            void loadReport(true, 1, next, sortConfig);
            return next;
        });
    };

    const handleSort = async (key) => {
        const nextSort = {
            key,
            direction: sortConfig.key === key && sortConfig.direction === 'asc' ? 'desc' : 'asc',
        };
        setSortConfig(nextSort);
        await loadReport(true, 1, filters, nextSort);
    };

    const handlePageChange = async (targetPage) => {
        if (targetPage < 1) return;
        if (pagination.totalPages > 0 && targetPage > pagination.totalPages) return;
        await loadReport(true, targetPage, filters, sortConfig);
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
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            {/* <div className="mt-2 text-sm text-gray-500">Intäkt per order</div> */}

            <div className={`relative z-20 flex items-center gap-4 mt-2 pb-2 ${loading ? 'opacity-70 pointer-events-none' : ''}`}>
                <div className="w-64">
                    <DateRangePicker
                        placeholder="Valj period"
                        presets={['this-month', 'last-month', 'last-3-months', 'year-to-date']}
                        initialPresetKey="year-to-date"
                        triggerRadius="full"
                        triggerClassName="h-7 w-[260px] border-lime-600 px-3 text-xs text-gray-700 focus:border-lime-700"
                        openTriggerClassName="border-lime-700 ring-1 ring-lime-200"
                        closedTriggerClassName="border-lime-600 hover:border-lime-700"
                        widthClassName="w-60"
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
                        filterStyle
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
                    <div className="flex gap-1 mr-0">
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

            <div className="border-t border-gray-300 py-1 mt-3 min-h-0 flex-1 overflow-auto">
                <table className={`table-fixed w-full border-collapse text-xs [&_thead_th]:px-2 [&_thead_th]:pt-1 [&_thead_th]:pb-2 [&_thead_th]:text-tiny [&_thead_th]:font-medium [&_thead_th]:text-gray-500 [&_tbody_>_tr]:h-6 [&_tbody_>_tr]:border-b [&_tbody_>_tr]:border-gray-100 [&_tbody_td]:overflow-hidden [&_tbody_td]:text-ellipsis [&_tbody_td]:whitespace-nowrap [&_tbody_td]:px-2 [&_tbody_td]:py-0 [&_tbody_td]:text-gray-800 ${showSkeleton ? '' : '[&_tbody_>_tr:hover]:!bg-lime-200/70'}`} style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <colgroup>
                        {columns.map((col) => (
                            <col key={col.key} style={{ width: col.width }} />
                        ))}
                    </colgroup>
                    <thead>
                        <tr>
                            {columns.map((col) => (
                                <th
                                    key={col.key}
                                    className={`overflow-hidden text-ellipsis whitespace-nowrap px-2 py-1.5 text-tiny font-medium text-gray-400 cursor-pointer ${col.align === 'right' ? 'text-right' : 'text-left'}`}
                                    onClick={() => handleSort(col.key)}
                                >
                                    {col.label}
                                    <span className="ml-1 text-tiny text-gray-400">{getSortIndicator(col.key)}</span>
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody className={`divide-y divide-gray-100 ${!loading && rows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {showSkeleton ? (
                            Array.from({ length: 12 }).map((_, i) => (
                                <tr key={i} className="bg-transparent hover:!bg-transparent">
                                    {columns.map((col) => (
                                        <td key={col.key} className="bg-transparent px-2 py-1">
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
