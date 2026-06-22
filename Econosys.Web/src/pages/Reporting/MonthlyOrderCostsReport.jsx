import React, { useEffect, useMemo, useRef, useState } from 'react';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';
import ExcelJS from 'exceljs';

import LabeledReactSelect from '../../components/LabeledReactSelect';
import apiClient from '../../config/apiClient';

const monthOptions = [
    { id: 1, name: 'Januari' },
    { id: 2, name: 'Februari' },
    { id: 3, name: 'Mars' },
    { id: 4, name: 'April' },
    { id: 5, name: 'Maj' },
    { id: 6, name: 'Juni' },
    { id: 7, name: 'Juli' },
    { id: 8, name: 'Augusti' },
    { id: 9, name: 'September' },
    { id: 10, name: 'Oktober' },
    { id: 11, name: 'November' },
    { id: 12, name: 'December' },
];

const columns = [
    { key: 'cost', label: 'Kostnad', align: 'left', width: 'w-[8%]' },
    { key: 'customer', label: 'Kund', align: 'left', width: 'w-[7%]' },
    { key: 'product', label: 'Produkt', align: 'left', width: 'w-[9%]' },
    { key: 'purchasePrice', label: 'Inpris', align: 'right', width: 'w-[4%]' },
    { key: 'purchasePriceSEK', label: 'Inpris', label2: 'SEK', align: 'right', width: 'w-[4%]' },
    { key: 'purchasePriceAttested', label: 'Att inpris', align: 'right', width: 'w-[4%]' },
    { key: 'purchasePriceAttestedSEK', label: 'Att inpris', label2: 'SEK', align: 'right', width: 'w-[4%]' },
    { key: 'salesPriceSEK', label: 'Utpris', label2: 'SEK', align: 'right', width: 'w-[4%]' },
    { key: 'markup', label: '% Påslag', align: 'right', width: 'w-[4%]' },
    { key: 'shouldInvoice', label: 'Debiteras', align: 'center', width: 'w-[4%]' },
    { key: 'invoiceNumber', label: 'Fakturanr', align: 'right', width: 'w-[4%]' },
    { key: 'supplier', label: 'Leverantör', align: 'left', width: 'w-[8%]' },
    { key: 'orderCreatedAt', label: 'Betälln. Skapad', align: 'left', width: 'w-[8%]' },
    { key: 'responsible', label: 'Säljare', align: 'left', width: 'w-[7%]' },
    { key: 'orderNumber', label: 'Ordernr.', align: 'right', width: 'w-[6%]' },
];

const MonthlyOrderCostsReport = () => {
    const now = new Date();
    const [loading, setLoading] = useState(false);
    const [showSkeleton, setShowSkeleton] = useState(false);
    const skeletonTimerRef = useRef(null);
    const [rows, setRows] = useState([]);
    const [initialLoadCompleted, setInitialLoadCompleted] = useState(false);
    const [yearOptions, setYearOptions] = useState([]);
    const [costOptions, setCostOptions] = useState([]);
    const [filters, setFilters] = useState({
        year: now.getFullYear(),
        month: now.getMonth() + 1,
        costIds: [],
    });

    const [sortConfig, setSortConfig] = useState({ key: 'orderCreatedAt', direction: 'desc' });

    useEffect(() => {
        const controller = new AbortController();
        let disposed = false;

        const initialize = async () => {
            const selectedYear = await loadSelectableYears(controller.signal);
            await loadCosts(controller.signal);
            const currentMonth = new Date().getMonth() + 1;
            const initialFilters = {
                ...filters,
                year: selectedYear || filters.year,
                month: currentMonth,
            };

            setFilters(initialFilters);
            await loadReport(controller.signal, initialFilters);

            if (!disposed) {
                setInitialLoadCompleted(true);
            }
        };

        initialize();
        return () => {
            disposed = true;
            controller.abort();
        };
    }, []);

    const loadSelectableYears = async (signal = null) => {
        try {
            const response = await apiClient.get('/reporting/selectable-years', {
                params: {
                    itemType: 'supplier-order',
                    dateField: 'created',
                },
                ...(signal ? { signal } : {}),
            });

            const years = response?.data?.years ?? [];
            const options = years.map((year) => ({ id: year, name: String(year) }));
            setYearOptions(options);

            let selectedYear = filters.year;
            if (!years.includes(selectedYear) && years.length > 0) {
                selectedYear = years[0];
            }

            setFilters((prev) => ({
                ...prev,
                year: selectedYear,
            }));

            return selectedYear;
        } catch (error) {
            if (error.code === 'ERR_CANCELED') return null;
            console.error('Failed to load selectable years:', error);
            setYearOptions([]);
            return null;
        }
    };

    const handleFilterChange = (field, value) => {
        setFilters((prev) => {
            const nextFilters = {
                ...prev,
                [field]: field === 'costIds'
                    ? (Array.isArray(value) ? value.map((id) => Number(id)).filter((id) => id > 0) : [])
                    : (Number(value) || prev[field]),
            };

            loadReport(null, nextFilters);
            return nextFilters;
        });
    };

    const loadCosts = async (signal = null) => {
        try {
            const response = await apiClient.post('/costs/search', {}, signal ? { signal } : {});
            const costs = Array.isArray(response?.data) ? response.data : [];
            setCostOptions(costs.filter((cost) => cost.isActive).map((cost) => ({ id: cost.id, name: cost.name })));
        } catch (error) {
            if (error.code === 'ERR_CANCELED') return;
            console.error('Failed to load costs:', error);
            setCostOptions([]);
        }
    };

    const loadReport = async (signal = null, overrideFilters = null) => {
        const requestFilters = overrideFilters ?? filters;

        setLoading(true);
        setRows([]);
        skeletonTimerRef.current = setTimeout(() => setShowSkeleton(true), 200);

        try {
            const response = await apiClient.post('/reporting/order-costs/monthly', {
                year: requestFilters.year,
                month: requestFilters.month,
                costIds: requestFilters.costIds ?? [],
            }, signal ? { signal } : {});

            const data = response?.data ?? {};
            setRows(data.rows ?? []);
        } catch (error) {
            if (error.code === 'ERR_CANCELED') return;
            console.error('Failed to load monthly order costs report:', error);
            setRows([]);
        } finally {
            clearTimeout(skeletonTimerRef.current);
            setLoading(false);
            setShowSkeleton(false);
        }
    };

    const handleSort = (key) => {
        setSortConfig((prev) => {
            if (prev.key === key) {
                return { key, direction: prev.direction === 'asc' ? 'desc' : 'asc' };
            }

            return { key, direction: 'asc' };
        });
    };

    const sortedRows = useMemo(() => {
        const list = [...rows];
        const directionMultiplier = sortConfig.direction === 'asc' ? 1 : -1;

        list.sort((a, b) => {
            const aValue = a?.[sortConfig.key];
            const bValue = b?.[sortConfig.key];

            if (sortConfig.key === 'orderCreatedAt') {
                const aTime = aValue ? new Date(aValue).getTime() : 0;
                const bTime = bValue ? new Date(bValue).getTime() : 0;
                return (aTime - bTime) * directionMultiplier;
            }

            if (typeof aValue === 'number' || typeof bValue === 'number') {
                return ((Number(aValue) || 0) - (Number(bValue) || 0)) * directionMultiplier;
            }

            if (typeof aValue === 'boolean' || typeof bValue === 'boolean') {
                return (Number(Boolean(aValue)) - Number(Boolean(bValue))) * directionMultiplier;
            }

            return String(aValue ?? '').localeCompare(String(bValue ?? ''), 'sv-SE') * directionMultiplier;
        });

        return list;
    }, [rows, sortConfig]);

    const formatAmount = (value) => {
        if (value === null || value === undefined) {
            return '';
        }

        return new Intl.NumberFormat('sv-SE', {
            minimumFractionDigits: 0,
            maximumFractionDigits: 0,
        }).format(Number(value));
    };

    const formatMarkup = (value) => {
        if (value === null || value === undefined) return '';
        return new Intl.NumberFormat('sv-SE', {
            style: 'percent',
            minimumFractionDigits: 1,
            maximumFractionDigits: 1,
        }).format(Number(value));
    };

    const formatDateTime = (value) => {
        if (!value) {
            return '';
        }

        return new Date(value).toLocaleString('sv-SE');
    };

    const totalPurchaseAmountSEK = useMemo(() => {
        return sortedRows.reduce((sum, row) => sum + (Number(row.purchasePriceSEK) || 0), 0);
    }, [sortedRows]);

    const totalSalesAmountSEK = useMemo(() => {
        return sortedRows.reduce((sum, row) => sum + (Number(row.salesPriceSEK) || 0), 0);
    }, [sortedRows]);

    const handleExport = async () => {
        if (!sortedRows.length) return;

        const reportMonth = monthOptions.find((item) => item.id === filters.month)?.name ?? String(filters.month);
        const generatedAt = new Date().toLocaleString('sv-SE');

        const header = [
            'Kostnad',
            'Kund',
            'Produkt',
            'Inpris',
            'Inpris SEK',
            'Att inpris',
            'Att inpris SEK',
            'Utpris SEK',
            '% Påslag',
            'Debiteras',
            'Fakturanr',
            'Leverantör',
            'Betälln. Skapad',
            'Säljare',
            'Ordernr.',
        ];

        const dataRows = sortedRows.map((row) => ([
            row.cost ?? '',
            row.customer ?? '',
            row.product ?? '',
            row.purchasePrice ?? '',
            row.purchasePriceSEK ?? '',
            row.purchasePriceAttested ?? '',
            row.purchasePriceAttestedSEK ?? '',
            row.salesPriceSEK ?? '',
            row.markup != null ? row.markup : '',
            row.shouldInvoice ? 'Ja' : 'Nej',
            row.invoiceNumber ?? '',
            row.supplier ?? '',
            row.orderCreatedAt ? formatDateTime(row.orderCreatedAt) : '',
            row.responsible ?? '',
            row.orderNumber ?? '',
        ]));

        const aoa = [
            ['Orderkostnader per månad'],
            [`Period: ${filters.year}-${String(filters.month).padStart(2, '0')} (${reportMonth})`],
            [`Genererad: ${generatedAt}`],
            [],
            header,
            ...dataRows,
        ];
        const workbook = new ExcelJS.Workbook();
        const worksheet = workbook.addWorksheet('Orderkostnader');

        aoa.forEach((row) => worksheet.addRow(row));

        const tableHeaderRow = 5;
        const tableLastRow = sortedRows.length + 5;
        const rightAlignedColumns = new Set([4, 5, 6, 7, 8, 9]);
        const centeredColumns = new Set([10]);
        const leftAlignedColumns = new Set([11, 15]);

        for (let col = 1; col <= header.length; col += 1) {
            let maxLength = String(header[col - 1] ?? '').length;

            for (let row = tableHeaderRow + 1; row <= tableLastRow; row += 1) {
                const cellValue = worksheet.getRow(row).getCell(col).value;
                const cellText = cellValue === null || cellValue === undefined ? '' : String(cellValue);
                if (cellText.length > maxLength) {
                    maxLength = cellText.length;
                }
            }

            worksheet.getColumn(col).width = Math.min(35, Math.max(6, maxLength + 1));
        }

        worksheet.eachRow((row) => {
            row.eachCell((cell) => {
                cell.font = {
                    ...(cell.font || {}),
                    size: 8,
                };
            });
        });

        worksheet.getRow(5).eachCell((cell) => {
            cell.font = {
                ...(cell.font || {}),
                size: 8,
                bold: true,
            };
        });

        for (let row = tableHeaderRow; row <= tableLastRow; row += 1) {
            for (let col = 1; col <= header.length; col += 1) {
                const cell = worksheet.getRow(row).getCell(col);

                if (centeredColumns.has(col)) {
                    cell.alignment = { ...(cell.alignment || {}), horizontal: 'center' };
                } else if (leftAlignedColumns.has(col)) {
                    cell.alignment = { ...(cell.alignment || {}), horizontal: 'left' };
                } else if (rightAlignedColumns.has(col)) {
                    cell.alignment = { ...(cell.alignment || {}), horizontal: 'right' };
                }
            }
        }

        worksheet.autoFilter = {
            from: { row: tableHeaderRow, column: 1 },
            to: { row: tableLastRow, column: 15 },
        };

        const dateSuffix = `${filters.year}-${String(filters.month).padStart(2, '0')}`;
        const buffer = await workbook.xlsx.writeBuffer();
        const blob = new Blob([buffer], {
            type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `orderkostnader-${dateSuffix}.xlsx`;
        link.click();
        URL.revokeObjectURL(url);
    };

    const getSortIndicator = (key) => {
        if (sortConfig.key !== key) return '↕';
        return sortConfig.direction === 'asc' ? '▲' : '▼';
    };

    return (
        <div className="flex flex-col h-full p-2">
            <div className="ml-5 text-sm text-gray-500">Orderkostnader per månad</div>

            <div className={`flex justify-between items-center mt-3 ml-5 ${loading ? 'opacity-60 pointer-events-none' : ''}`}>
                <div className="flex items-center">
                    <div className="w-50">
                        <LabeledReactSelect
                            label="År"
                            labelWidth="w-5"
                            name="year"
                            value={filters.year}
                            items={yearOptions}
                            onChange={(selected) => handleFilterChange('year', selected)}
                            isDisabled={loading || yearOptions.length === 0}
                        />
                    </div>

                    <div className="w-56 ml-8">
                        <LabeledReactSelect
                            label="Månad"
                            labelWidth="w-10"
                            name="month"
                            value={filters.month}
                            items={monthOptions}
                            onChange={(selected) => handleFilterChange('month', selected)}
                            isDisabled={loading}
                        />
                    </div>

                    <div className="w-96 ml-8">
                        <LabeledReactSelect
                            label="Kostnader"
                            labelWidth="w-16"
                            name="costIds"
                            value={filters.costIds}
                            items={costOptions}
                            onChange={(selected) => handleFilterChange('costIds', selected)}
                            isDisabled={loading || costOptions.length === 0}
                            isMulti
                        />
                    </div>

                    <button
                        type="button"
                        onClick={() => loadReport()}
                        disabled={loading}
                        className={`shadow-md/30 ml-10 w-30 text-center text-xs text-white p-[5px] ${loading ? 'bg-lime-900 cursor-not-allowed' : 'bg-lime-700 hover:bg-lime-900'}`}
                    >
                        {loading ? 'Uppdaterar...' : 'Uppdatera'}
                    </button>

                    <button
                        type="button"
                        onClick={handleExport}
                        disabled={loading || sortedRows.length === 0}
                        className="W-23 ml-20 shadow-md/30 text-xs text-gray bg-amber-200 hover:bg-amber-300 px-4 py-[5px]"
                    >
                        Excel
                    </button>
                </div>

                <div className="flex items-center mr-10">
                    <div className="ml-10 text-xs text-gray-500">Rader: <strong>{sortedRows.length}</strong></div>
                    <div className="ml-10 text-xs text-gray-500">Totalt inpris SEK: <strong>{formatAmount(totalPurchaseAmountSEK)}</strong></div>
                    <div className="ml-10 text-xs text-gray-500">Totalt utpris SEK: <strong>{formatAmount(totalSalesAmountSEK)}</strong></div>
                </div>
            </div>

            <div className="border-t border-gray-300 rounded-sm py-1 mt-4 h-full overflow-y-auto">
                <table className="w-full table-fixed divide-y divide-gray-100">
                    <thead>
                        <tr>
                            {columns.map((column) => (
                                <th
                                    key={column.key}
                                    className={`${column.width} px-2 py-1.5 ${column.align === 'right' ? 'text-right' : 'text-left'} text-tiny font-medium text-gray-400 uppercase tracking-wider`}
                                >
                                    <button
                                        type="button"
                                        className={`w-full flex items-center ${column.align === 'right' ? 'justify-end' : 'justify-start'} gap-1 hover:text-gray-600`}
                                        onClick={() => handleSort(column.key)}
                                    >
                                        <span className="flex flex-col leading-tight">
                                            <span>{column.label}</span>
                                            {column.label2 && <span>{column.label2}</span>}
                                        </span>
                                        <span className="text-[9px]">{getSortIndicator(column.key)}</span>
                                    </button>
                                </th>
                            ))}
                        </tr>
                    </thead>

                    <tbody className={`divide-y divide-gray-100 ${!loading && sortedRows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {showSkeleton ? (
                            Array.from({ length: 4 }).map((_, i) => (
                                <tr key={i}>
                                    {columns.map((column) => (
                                        <td key={column.key} className="px-2 py-0">
                                            <Skeleton className="h-5 m-0" />
                                        </td>
                                    ))}
                                </tr>
                            ))
                        ) : initialLoadCompleted && !loading && sortedRows.length === 0 ? (
                            <tr>
                                <td colSpan="15" className="px-6 py-14 whitespace-nowrap text-sm text-gray-400 text-center">Inget att visa</td>
                            </tr>
                        ) : (
                            sortedRows.map((row, index) => (
                                <tr key={`${row.orderNumber}-${index}`} className="hover:bg-blue-50">
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{row.cost}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{row.customer}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{row.product}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 text-right">{formatAmount(row.purchasePrice)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 text-right">{formatAmount(row.purchasePriceSEK)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 text-right">{formatAmount(row.purchasePriceAttested)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 text-right">{formatAmount(row.purchasePriceAttestedSEK)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 text-right">{formatAmount(row.salesPriceSEK)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 text-right">{formatMarkup(row.markup)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800 text-center">{row.shouldInvoice ? 'Ja' : 'Nej'}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 text-right">{row.invoiceNumber ?? ''}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{row.supplier}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{formatDateTime(row.orderCreatedAt)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{row.responsible}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 text-right">{row.orderNumber}</td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

export default MonthlyOrderCostsReport;
