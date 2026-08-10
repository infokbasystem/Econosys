import React, { useEffect, useRef, useState } from 'react';
import { ArrowLeftCircle, ArrowRightCircle, ChevronDown, ChevronUp, ChevronsUpDown } from 'lucide-react';
import ExcelJS from 'exceljs';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import DateRangePicker from '../../components/Daterangepicker';
import apiClient from '../../config/apiClient';
import { formatDateShort, formatDeliveryDate } from '../../helpers/dateUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';

const PAGE_SIZE = 100;

const columns = [
    { key: 'orderNumber', label: 'Ordernr', align: 'left', width: 'w-[14%]', sortable: true, sortField: 'orderNumber' },
    { key: 'productName', label: 'Produktnamn', align: 'left', width: 'w-[23%]', sortable: true, sortField: 'productName' },
    { key: 'customerName', label: 'Kund', align: 'left', width: 'w-[18%]', sortable: true, sortField: 'customerName' },
    { key: 'supplierName', label: 'Leverantör', align: 'left', width: 'w-[18%]', sortable: true, sortField: 'supplierName' },
    { key: 'supplierOrderCreatedAt', label: 'Lev.order skapad', align: 'left', width: 'w-[12%]', sortable: true, sortField: 'supplierOrderCreatedAt' },
    { key: 'supplierOrderDeliveryDate', label: 'Lev.datum', align: 'left', width: 'w-[9%]', sortable: true, sortField: 'supplierOrderDeliveryDate' },
    { key: 'edition', label: 'Upplaga', align: 'right', width: 'w-[6%]', sortable: true, sortField: 'edition' },
];

const toDateString = (date) =>
    `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;

const formatEdition = (value) => {
    if (value == null) return '';
    return new Intl.NumberFormat('sv-SE', {
        minimumFractionDigits: 0,
        maximumFractionDigits: 0,
    }).format(Number(value));
};

const NonDeliveredWarehouseOrders = () => {
    const now = new Date();
    now.setHours(0, 0, 0, 0);
    const yearStart = new Date(now.getFullYear(), 0, 1);

    const [rows, setRows] = useState([]);
    const [loading, setLoading] = useState(false);
    const [showSkeleton, setShowSkeleton] = useState(false);
    const skeletonTimerRef = useRef(null);
    const didMountRef = useRef(false);

    const [filters, setFilters] = useState({
        startDate: yearStart,
        endDate: now,
    });

    const [pagination, setPagination] = useState({
        pageNumber: 1,
        pageSize: PAGE_SIZE,
        totalCount: 0,
        totalPages: 0,
        hasPreviousPage: false,
        hasNextPage: false,
    });

    const [sortConfig, setSortConfig] = useState({
        key: 'supplierOrderCreatedAt',
        direction: 'desc',
    });

    const [initialLoadCompleted, setInitialLoadCompleted] = useState(false);

    const buildRequestBody = (pageNumber, pageSize, activeFilters, activeSort = sortConfig) => {
        const sortColumn = columns.find((column) => column.key === activeSort.key);
        const sortField = sortColumn?.sortField;

        return {
            startDate: toDateString(activeFilters.startDate),
            endDate: toDateString(activeFilters.endDate),
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

    const loadRows = async (pageNumber = 1, activeFilters = filters, isActive = true) => {
        setLoading(true);
        setRows([]);
        skeletonTimerRef.current = setTimeout(() => setShowSkeleton(true), 200);

        try {
            const requestBody = buildRequestBody(pageNumber, pagination.pageSize, activeFilters, sortConfig);
            const requestKey = `reporting:non-delivered-warehouse-orders:${JSON.stringify(requestBody)}`;
            const response = await getSharedRequest(requestKey, () => apiClient.post('/reporting/non-delivered-warehouse-orders', requestBody));
            if (!isActive) return;

            const data = response?.data ?? {};
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
        } catch (error) {
            console.error('Failed to load non-delivered warehouse orders:', error);
            if (!isActive) return;
            setRows([]);
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

        const timer = setTimeout(async () => {
            await loadRows(pagination.pageNumber, filters, isActive);
            if (!didMountRef.current) {
                didMountRef.current = true;
                setInitialLoadCompleted(true);
            }
        }, didMountRef.current ? 250 : 0);

        return () => {
            isActive = false;
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
        const sheet = workbook.addWorksheet('EjInlevereradeLagerorder');

        sheet.addRow(columns.map((column) => column.label));
        sheet.getRow(1).font = { bold: true, size: 9 };

        try {
            let currentPage = 1;
            let totalPages = 1;

            while (currentPage <= totalPages) {
                const response = await apiClient.post(
                    '/reporting/non-delivered-warehouse-orders',
                    buildRequestBody(currentPage, 200, filters, sortConfig)
                );
                const data = response?.data ?? {};
                const items = data?.items ?? [];

                totalPages = Number(data?.totalPages) || 1;

                items.forEach((row) => {
                    sheet.addRow([
                        row.orderNumber ?? '',
                        row.productName ?? '',
                        row.customerName ?? '',
                        row.supplierName ?? '',
                        formatDateShort(row.supplierOrderCreatedAt),
                        formatDeliveryDate(row.supplierOrderDeliveryDate, row.supplierOrderDeliveryDateWeekMode),
                        row.edition ?? 0,
                    ]);
                });

                currentPage += 1;
            }

            for (let col = 1; col <= columns.length; col++) {
                sheet.getColumn(col).width = 20;
            }

            const buffer = await workbook.xlsx.writeBuffer();
            const blob = new Blob([buffer], {
                type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
            });

            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `ej-inlevererade-lagerorder-${toDateString(new Date())}.xlsx`;
            link.click();
            URL.revokeObjectURL(url);
        } catch (error) {
            console.error('Failed to export non-delivered warehouse orders:', error);
        }
    };

    return (
        <div className="flex flex-col h-full p-2">
            <div className="ml-5 text-sm text-gray-500">Ej inlevererade lagerorder</div>

            <div className={`flex flex-wrap items-center gap-8 mt-3 ml-5 ${loading ? 'opacity-70 pointer-events-none' : ''}`}>
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

                <div className="ml-10 text-xs text-center">
                    <a
                        href="#"
                        onClick={(event) => {
                            event.preventDefault();
                            if (loading || pagination.totalCount === 0) return;
                            handleExport();
                        }}
                        aria-disabled={loading || pagination.totalCount === 0}
                        className={`inline-flex items-center whitespace-nowrap font-medium transition-colors ${loading || pagination.totalCount === 0
                            ? 'text-gray-300 cursor-not-allowed pointer-events-none'
                            : 'text-slate-500 hover:text-slate-700'
                            }`}
                    >
                        Exportera till EXCEL
                    </a>
                </div>

                <div className="ml-auto flex items-center mr-4" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    {initialLoadCompleted && (
                        <div className="ml-auto flex items-center mr-4">
                            <div className="ml-4 text-xs text-gray-500">
                                Rader: <strong>{pagination.totalCount}</strong>
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

            <div className="border-t border-gray-300 py-1 mt-4 flex-1 overflow-auto">
                <table className="w-full border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <thead>
                        <tr>
                            {columns.map((col) => (
                                <th
                                    key={col.key}
                                    className={`${col.width} px-2 py-1.5 text-tiny font-medium text-gray-400 whitespace-nowrap ${col.align === 'right' ? 'text-right' : col.align === 'center' ? 'text-center' : 'text-left'}`}
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
                            Array.from({ length: 12 }).map((_, index) => (
                                <tr key={index} className="border-b border-gray-100">
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
                                    Inga ej inlevererade lagerorder hittades for valt urval.
                                </td>
                            </tr>
                        ) : (
                            rows.map((row) => (
                                <tr key={row.id} className="border-b border-gray-100 hover:bg-blue-50">
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800">{row.orderNumber ?? ''}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800">{row.productName ?? ''}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800">{row.customerName ?? ''}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800">{row.supplierName ?? ''}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 whitespace-nowrap">{formatDateShort(row.supplierOrderCreatedAt)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 whitespace-nowrap">{formatDeliveryDate(row.supplierOrderDeliveryDate, row.supplierOrderDeliveryDateWeekMode)}</td>
                                    <td className="pl-2 pr-6 pt-[6px] pb-[4px] text-right text-xs text-gray-800">{formatEdition(row.edition)}</td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

export default NonDeliveredWarehouseOrders;