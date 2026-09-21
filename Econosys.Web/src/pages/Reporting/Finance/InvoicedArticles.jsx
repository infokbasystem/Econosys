import React, { useEffect, useMemo, useRef, useState } from 'react';
import { CircleDollarSign } from 'lucide-react';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';
import ExcelJS from 'exceljs';

import DateRangePicker from '../../../components/DaterangePicker';
import apiClient from '../../../config/apiClient';
import { getSharedRequest } from '../../../helpers/sharedRequest';
import { formatDateShort } from '../../../helpers/dateUtils';

const columns = [
    { key: 'invoiceDate', label: 'Fakturadatum', align: 'left', width: '10%' },
    { key: 'invoiceNumber', label: 'Fakturanr', align: 'right', width: '8%' },
    { key: 'customer', label: 'Kund', align: 'left', width: '14%' },
    { key: 'productCode', label: 'Produktkod', align: 'left', width: '10%' },
    { key: 'product', label: 'Produkt', align: 'left', width: '14%' },
    { key: 'quantity', label: 'Antal', align: 'right', width: '8%' },
    { key: 'unitPrice', label: 'Enhetspris', align: 'right', width: '10%' },
    { key: 'unit', label: 'Enhet', align: 'left', width: '8%' },
    { key: 'sum', label: 'Summa', align: 'right', width: '10%' },
];

const toDateString = (value) => {
    if (!value) return null;
    const date = value instanceof Date ? value : new Date(value);
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
};

const InvoicedArticles = () => {
    const today = new Date();
    const firstDayOfYear = new Date(today.getFullYear(), 0, 1);
    const [filters, setFilters] = useState({ startDate: firstDayOfYear, endDate: today });
    const [rows, setRows] = useState([]);
    const [loading, setLoading] = useState(false);
    const [showSkeleton, setShowSkeleton] = useState(false);
    const [initialLoadCompleted, setInitialLoadCompleted] = useState(false);
    const [sortConfig, setSortConfig] = useState({ key: 'invoiceDate', direction: 'desc' });
    const skeletonTimerRef = useRef(null);
    const requestRef = useRef(0);

    const loadReport = async (activeFilters, isActive = true) => {
        const requestId = requestRef.current + 1;
        requestRef.current = requestId;
        clearTimeout(skeletonTimerRef.current);
        setLoading(true);
        setRows([]);
        setShowSkeleton(false);
        const skeletonTimer = setTimeout(() => {
            if (isActive && requestRef.current === requestId) setShowSkeleton(true);
        }, 200);
        skeletonTimerRef.current = skeletonTimer;

        try {
            const requestBody = {
                startDate: toDateString(activeFilters.startDate),
                endDate: toDateString(activeFilters.endDate),
            };
            const response = await getSharedRequest(
                `reporting:invoiced-articles:${JSON.stringify(requestBody)}`,
                () => apiClient.post('/reporting/invoiced-articles', requestBody),
            );
            if (!isActive || requestRef.current !== requestId) return;
            setRows(response?.data?.rows ?? []);
        } catch (error) {
            console.error('Failed to load invoiced articles report:', error);
            if (!isActive || requestRef.current !== requestId) return;
            setRows([]);
        } finally {
            clearTimeout(skeletonTimer);
            if (skeletonTimerRef.current === skeletonTimer) skeletonTimerRef.current = null;
            if (!isActive || requestRef.current !== requestId) return;
            setLoading(false);
            setShowSkeleton(false);
            setInitialLoadCompleted(true);
        }
    };

    useEffect(() => {
        let isActive = true;
        void loadReport(filters, isActive);
        return () => {
            isActive = false;
            clearTimeout(skeletonTimerRef.current);
            requestRef.current += 1;
        };
    }, []);

    const sortedRows = useMemo(() => {
        const direction = sortConfig.direction === 'asc' ? 1 : -1;
        return [...rows].sort((left, right) => {
            const leftValue = left?.[sortConfig.key];
            const rightValue = right?.[sortConfig.key];
            if (sortConfig.key === 'invoiceDate') {
                return ((leftValue ? new Date(leftValue).getTime() : 0) - (rightValue ? new Date(rightValue).getTime() : 0)) * direction;
            }
            if (typeof leftValue === 'number' || typeof rightValue === 'number') {
                return ((Number(leftValue) || 0) - (Number(rightValue) || 0)) * direction;
            }
            return String(leftValue ?? '').localeCompare(String(rightValue ?? ''), 'sv-SE') * direction;
        });
    }, [rows, sortConfig]);

    const handleDateChange = ({ startDate, endDate }) => {
        const nextFilters = { startDate, endDate };
        setFilters(nextFilters);
        void loadReport(nextFilters);
    };

    const handleSort = (key) => {
        setSortConfig((previous) => ({
            key,
            direction: previous.key === key && previous.direction === 'asc' ? 'desc' : 'asc',
        }));
    };

    const formatNumber = (value) => value == null ? '' : new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 2 }).format(Number(value));

    const handleExport = async () => {
        if (!sortedRows.length) return;
        const header = columns.map((column) => column.label);
        const dataRows = sortedRows.map((row) => [
            formatDateShort(row.invoiceDate),
            row.invoiceNumber ?? '',
            row.customer ?? '',
            row.productCode ?? '',
            row.product ?? '',
            row.quantity ?? '',
            row.unitPrice ?? '',
            row.unit ?? '',
            row.sum ?? '',
        ]);
        const workbook = new ExcelJS.Workbook();
        const worksheet = workbook.addWorksheet('Fakturerade artiklar');
        worksheet.addRow(['Fakturerade artiklar']);
        worksheet.addRow([`Period: ${toDateString(filters.startDate)} - ${toDateString(filters.endDate)}`]);
        worksheet.addRow([]);
        worksheet.addRow(header);
        dataRows.forEach((row) => worksheet.addRow(row));
        worksheet.autoFilter = { from: { row: 4, column: 1 }, to: { row: dataRows.length + 4, column: header.length } };
        worksheet.columns.forEach((column) => { column.width = 18; });
        const buffer = await workbook.xlsx.writeBuffer();
        const url = URL.createObjectURL(new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }));
        const link = document.createElement('a');
        link.href = url;
        link.download = `invoiced-articles-${toDateString(filters.startDate)}.xlsx`;
        link.click();
        URL.revokeObjectURL(url);
    };

    const getSortIndicator = (key) => sortConfig.key === key ? (sortConfig.direction === 'asc' ? '▲' : '▼') : '↕';

    return (
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            <div className={`relative z-20 flex items-center gap-4 mt-2 pb-2 ${loading ? 'opacity-70 pointer-events-none' : ''}`}>
                <DateRangePicker
                    placeholder="Välj period"
                    presets={['this-month', 'last-month', 'last-3-months', 'year-to-date']}
                    initialPresetKey="last-month"
                    triggerRadius="full"
                    triggerClassName="h-7 w-[260px] border-lime-600 px-3 text-xs text-gray-700 focus:border-lime-700"
                    openTriggerClassName="border-lime-700 ring-1 ring-lime-200"
                    closedTriggerClassName="border-lime-600 hover:border-lime-700"
                    widthClassName="w-60"
                    onApply={handleDateChange}
                />
                <button type="button" onClick={handleExport} disabled={loading || sortedRows.length === 0} className="ml-20 text-xs font-medium text-slate-500 hover:text-slate-700 disabled:cursor-not-allowed disabled:text-gray-300">
                    Exportera till EXCEL
                </button>
                <div className="ml-6 text-xs text-gray-500">Rader: <strong>{sortedRows.length}</strong></div>
            </div>
            <div className="border-t border-gray-300 py-1 mt-3 min-h-0 flex-1 overflow-auto">
                <table className={`table-fixed w-full border-collapse text-xs [&_thead_th]:px-2 [&_thead_th]:pt-1 [&_thead_th]:pb-2 [&_thead_th]:text-tiny [&_thead_th]:font-medium [&_thead_th]:text-gray-500 [&_tbody_>_tr]:h-6 [&_tbody_>_tr]:border-b [&_tbody_>_tr]:border-gray-100 [&_tbody_td]:overflow-hidden [&_tbody_td]:text-ellipsis [&_tbody_td]:whitespace-nowrap [&_tbody_td]:px-2 [&_tbody_td]:py-0 [&_tbody_td]:text-gray-800 ${showSkeleton ? '' : '[&_tbody_>_tr:hover]:!bg-lime-200/70'}`} style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <colgroup>
                        {columns.map((column) => (
                            <col key={column.key} style={{ width: column.width }} />
                        ))}
                    </colgroup>
                    <thead>
                        <tr>
                            {columns.map((column) => (
                                <th
                                    key={column.key}
                                    className={`overflow-hidden text-ellipsis whitespace-nowrap px-2 py-1.5 text-tiny font-medium text-gray-400 cursor-pointer ${column.align === 'right' ? 'text-right' : 'text-left'}`}
                                    onClick={() => handleSort(column.key)}
                                >
                                    {column.label}
                                    <span className="ml-1 text-tiny text-gray-400">{getSortIndicator(column.key)}</span>
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody className={`divide-y divide-gray-100 ${!loading && sortedRows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {showSkeleton ? (
                            Array.from({ length: 12 }).map((_, index) => (
                                <tr key={index} className="bg-transparent hover:!bg-transparent">
                                    {columns.map((column) => (
                                        <td key={column.key} className="bg-transparent px-2 py-1">
                                            <Skeleton height={14} />
                                        </td>
                                    ))}
                                </tr>
                            ))
                        ) : initialLoadCompleted && !loading && sortedRows.length === 0 ? (
                            <tr>
                                <td colSpan={columns.length} className="px-4 py-6 text-center text-gray-400 text-xs">
                                    Inga fakturarader hittades för vald period.
                                </td>
                            </tr>
                        ) : (
                            sortedRows.map((row, index) => (
                                <tr key={`${row.invoiceNumber}-${index}`} className="hover:bg-amber-50">
                                    <td className="px-2 py-1 text-gray-800 whitespace-nowrap">{formatDateShort(row.invoiceDate)}</td>
                                    <td className="px-2 py-1 text-right text-gray-800">{row.invoiceNumber ?? ''}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.customer}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.productCode}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.product}</td>
                                    <td className="px-2 py-1 text-right text-gray-800">{formatNumber(row.quantity)}</td>
                                    <td className="px-2 py-1 text-right text-gray-800">{formatNumber(row.unitPrice)}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.unit}</td>
                                    <td className="px-2 py-1 text-right text-gray-800">{formatNumber(row.sum)}</td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

export default InvoicedArticles;