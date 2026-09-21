import React, { useEffect, useMemo, useRef, useState } from 'react';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';
import ExcelJS from 'exceljs';

import LabeledReactSelect from '../../../components/LabeledReactSelect';
import DateRangePicker from '../../../components/DaterangePicker';
import SegmentedFilter from '../../../components/SegmentedFilter';
import apiClient from '../../../config/apiClient';
import { getSharedRequest } from '../../../helpers/sharedRequest';

const columns = [
    { key: 'customerName', label: 'Kund', align: 'left', width: '20%' },
    { key: 'productName', label: 'Produkt', align: 'left', width: '28%' },
    { key: 'materialGroup', label: 'Materialgrupp', align: 'left', width: '16%' },
    { key: 'packagingFeeCategory', label: 'Emballagekategori', align: 'left', width: '20%' },
    { key: 'totalDeliveredCount', label: 'Levererat', align: 'right', width: '8%' },
    { key: 'totalWeight', label: 'Total vikt', align: 'right', width: '8%' },
];

const reportModeOptions = [
    {
        label: 'Kund',
        value: 'customer',
    },
    {
        label: 'Econopack',
        value: 'econopack',
    },
];

const toDateOnlyString = (value) => {
    const d = value instanceof Date ? value : new Date(value);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

const materialGroupLabels = {
    1: 'Well',
    2: 'Papper/kartong',
    3: 'Plast',
    4: 'Övrigt',
    Well: 'Well',
    PapperKartong: 'Papper/kartong',
    Plast: 'Plast',
    Ovrigt: 'Övrigt',
};

const toDateString = (value) => {
    if (!(value instanceof Date)) return '';
    return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`;
};

const PackagingReport = () => {
    const now = new Date();
    now.setHours(0, 0, 0, 0);
    const yearStart = new Date(now.getFullYear(), 0, 1);

    const [loading, setLoading] = useState(false);
    const [showSkeleton, setShowSkeleton] = useState(false);
    const [rows, setRows] = useState([]);
    const [initialLoadCompleted, setInitialLoadCompleted] = useState(false);
    const [showInfoPopup, setShowInfoPopup] = useState(false);
    const [customerOptions, setCustomerOptions] = useState([]);
    const [sortConfig, setSortConfig] = useState({ key: 'customerName', direction: 'asc' });

    const skeletonTimerRef = useRef(null);

    const [filters, setFilters] = useState({
        mode: 'customer',
        startDate: yearStart,
        endDate: now,
        customerId: null,
    });

    useEffect(() => {
        let isActive = true;

        const initialize = async () => {
            await loadCustomers(isActive);
            if (isActive) {
                setInitialLoadCompleted(true);
            }
        };

        void initialize();

        return () => {
            isActive = false;
            clearTimeout(skeletonTimerRef.current);
        };
    }, []);

    useEffect(() => {
        if (!initialLoadCompleted) {
            return;
        }

        let isActive = true;
        void loadReport(isActive);

        return () => {
            isActive = false;
        };
    }, [filters, initialLoadCompleted]);

    const loadCustomers = async (isActive = true) => {
        try {
            const requestBody = {
                active: true,
                sortBy: 'Name',
                sortDescending: false,
            };
            const requestKey = `customers:search:packaging-report:${JSON.stringify(requestBody)}`;
            const response = await getSharedRequest(requestKey, () => apiClient.post('/customers/search', requestBody));

            const data = response?.data;
            const fetched = (data?.items ?? []).map((customer) => ({
                id: customer.id,
                name: customer.name ?? String(customer.id),
            }));

            if (!isActive) return;
            setCustomerOptions(fetched.sort((a, b) => a.name.localeCompare(b.name, 'sv-SE')));
        } catch (error) {
            console.error('Failed to load customers:', error);
            if (!isActive) return;
            setCustomerOptions([]);
        }
    };

    const loadReport = async (isActive = true, overrideFilters = null) => {
        const activeFilters = overrideFilters ?? filters;

        if (activeFilters.mode !== 'customer') {
            setRows([]);
            setLoading(false);
            setShowSkeleton(false);
            clearTimeout(skeletonTimerRef.current);
            return;
        }

        if (!activeFilters.customerId) {
            setRows([]);
            setLoading(false);
            setShowSkeleton(false);
            clearTimeout(skeletonTimerRef.current);
            return;
        }

        setLoading(true);
        setRows([]);
        skeletonTimerRef.current = setTimeout(() => setShowSkeleton(true), 200);

        try {
            const requestBody = {
                startDate: toDateOnlyString(activeFilters.startDate),
                endDate: toDateOnlyString(activeFilters.endDate),
                customerId: activeFilters.customerId,
            };
            const requestKey = `reporting:packaging-report:${JSON.stringify(requestBody)}`;
            const response = await getSharedRequest(requestKey, () => apiClient.post('/reporting/packaging-report', requestBody));

            if (!isActive) return;
            setRows(response?.data?.rows ?? []);
        } catch (error) {
            console.error('Failed to load packaging report:', error);
            if (!isActive) return;
            setRows([]);
        } finally {
            if (!isActive) return;
            clearTimeout(skeletonTimerRef.current);
            setLoading(false);
            setShowSkeleton(false);
        }
    };

    const handleFilterChange = (field, value) => {
        setFilters((prev) => {
            const next = { ...prev, [field]: value };

            if (field === 'mode' && value !== 'customer') {
                next.customerId = null;
            }

            return next;
        });
    };

    const handleSort = (key) => {
        setSortConfig((prev) => ({
            key,
            direction: prev.key === key && prev.direction === 'asc' ? 'desc' : 'asc',
        }));
    };

    const sortedRows = useMemo(() => {
        const list = [...rows];
        const directionMultiplier = sortConfig.direction === 'asc' ? 1 : -1;

        list.sort((left, right) => {
            const leftValue = left?.[sortConfig.key];
            const rightValue = right?.[sortConfig.key];

            if (typeof leftValue === 'number' || typeof rightValue === 'number') {
                return ((Number(leftValue) || 0) - (Number(rightValue) || 0)) * directionMultiplier;
            }

            return String(leftValue ?? '').localeCompare(String(rightValue ?? ''), 'sv-SE') * directionMultiplier;
        });

        return list;
    }, [rows, sortConfig]);

    const getSortIndicator = (key) => {
        if (sortConfig.key !== key) return '↕';
        return sortConfig.direction === 'asc' ? '▲' : '▼';
    };

    const formatWeight = (value) => {
        if (value === null || value === undefined) return '';

        return new Intl.NumberFormat('sv-SE', {
            minimumFractionDigits: 0,
            maximumFractionDigits: 0,
        }).format(Number(value));
    };

    const formatMaterialGroup = (value) => {
        if (value === null || value === undefined || value === '') return '';
        return materialGroupLabels[value] ?? String(value);
    };

    const handleExportExcel = async () => {
        if (!sortedRows.length) return;

        const workbook = new ExcelJS.Workbook();
        const worksheet = workbook.addWorksheet('Forpackningsrapport');

        worksheet.addRow(['Förpackningsrapport']);
        worksheet.addRow([`Period: ${toDateOnlyString(filters.startDate)} - ${toDateOnlyString(filters.endDate)}`]);
        worksheet.addRow([`Genererad: ${new Date().toLocaleString('sv-SE')}`]);
        worksheet.addRow([]);
        worksheet.addRow(columns.map((column) => column.label));

        sortedRows.forEach((row) => {
            worksheet.addRow([
                row.customerName ?? '',
                row.productName ?? '',
                formatMaterialGroup(row.materialGroup),
                row.packagingFeeCategory ?? '',
                Number(row.totalDeliveredCount) || 0,
                Number(row.totalWeight) || 0,
            ]);
        });

        const headerRowIndex = 5;
        const lastRowIndex = sortedRows.length + headerRowIndex;

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

        const rightAlignedCols = new Set([5, 6]);
        for (let row = headerRowIndex; row <= lastRowIndex; row += 1) {
            for (let col = 1; col <= columns.length; col += 1) {
                if (rightAlignedCols.has(col)) {
                    const cell = worksheet.getRow(row).getCell(col);
                    cell.alignment = { horizontal: 'right' };
                }
            }
        }

        for (let col = 1; col <= columns.length; col += 1) {
            let maxLength = String(columns[col - 1].label ?? '').length;
            for (let row = headerRowIndex + 1; row <= lastRowIndex; row += 1) {
                const value = worksheet.getRow(row).getCell(col).value;
                const text = value == null ? '' : String(value);
                if (text.length > maxLength) maxLength = text.length;
            }
            worksheet.getColumn(col).width = Math.min(40, Math.max(8, maxLength + 2));
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
        link.download = `forpackningsrapport-${toDateString(filters.startDate)}.xlsx`;
        link.click();
        URL.revokeObjectURL(url);
    };

    return (
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            {/* <div className="mt-2 text-sm text-gray-500">Förpackningsrapport</div> */}

            <div className={`relative z-20 flex items-center gap-4 mt-2 pb-2 ${loading ? 'opacity-60 pointer-events-none' : ''}`}>
                <div className="flex items-center text-xs">
                    <div className="w-45">
                        <SegmentedFilter
                            value={filters.mode}
                            options={reportModeOptions}
                            onChange={(value) => handleFilterChange('mode', value)}
                            theme="lime"
                        />
                    </div>
                </div>

                <div>
                    <DateRangePicker
                        placeholder="Välj period"
                        presets={['this-month', 'last-month', 'last-3-months', 'year-to-date']}
                        initialPresetKey="year-to-date"
                        triggerRadius="full"
                        triggerClassName="h-7 w-[260px] border-lime-600 px-3 text-xs text-gray-700 focus:border-lime-700"
                        openTriggerClassName="border-lime-700 ring-1 ring-lime-200"
                        closedTriggerClassName="border-lime-600 hover:border-lime-700"
                        widthClassName="w-60"
                        onApply={({ startDate, endDate }) => {
                            setFilters((prev) => ({ ...prev, startDate, endDate }));
                        }}
                    />
                </div>

                {filters.mode === 'customer' && (
                    <div className="w-64">
                        <LabeledReactSelect
                            name="customerId"
                            filterStyle
                            value={filters.customerId ?? ''}
                            items={[{ id: '', name: 'Välj kund...' }, ...customerOptions]}
                            onChange={(selected) => handleFilterChange('customerId', selected ? Number(selected) : null)}
                            isDisabled={loading}
                        />
                    </div>
                )}

                {initialLoadCompleted && (
                    <div className="ml-10 text-xs text-center">
                        <a
                            href="#"
                            onClick={(event) => {
                                event.preventDefault();
                                if (loading || sortedRows.length === 0) return;
                                handleExportExcel();
                            }}
                            aria-disabled={loading || sortedRows.length === 0}
                            className={`inline-flex items-center whitespace-nowrap font-medium transition-colors ${loading || sortedRows.length === 0
                                ? 'text-gray-300 cursor-not-allowed pointer-events-none'
                                : 'text-slate-500 hover:text-slate-700'
                                }`}
                        >
                            Exportera till EXCEL
                        </a>
                    </div>
                )}

                <div className="relative ml-auto text-xs">
                    {showInfoPopup && (
                        <>
                            <div
                                className="fixed inset-0 z-30"
                                onClick={() => setShowInfoPopup(false)}
                            />
                            <div
                                className="absolute right-0 top-7 z-40 w-[560px] border border-gray-400 bg-yellow-50 px-8 py-6 text-center text-xs text-yellow-900 shadow-sm"
                                onClick={(event) => event.stopPropagation()}
                            >
                                <p className="mt-2">Baserad på antalet leverarad upplaga på de underliggande fakturorna inom perioden.</p>
                                <p className="mt-2">Levererad upplaga * produktens vikt per 1000 / 1000</p>                               
                            </div>
                        </>
                    )}
                    <a
                        href="#"
                        onClick={(event) => {
                            event.preventDefault();
                            setShowInfoPopup((prev) => !prev);
                        }}
                        className="mr-4 inline-flex items-center whitespace-nowrap font-medium text-slate-500 transition-colors hover:text-slate-700"
                    >
                        Visa info
                    </a>
                </div>
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
                                    className={`overflow-hidden text-ellipsis whitespace-nowrap px-2 py-1.5 text-tiny font-medium text-gray-400 cursor-pointer select-none ${column.align === 'right' ? 'text-right' : 'text-left'}`}
                                    onClick={() => handleSort(column.key)}
                                >
                                    {column.label}
                                    <span className="ml-1 text-gray-400 text-tiny">{getSortIndicator(column.key)}</span>
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody className={`divide-y divide-gray-100 ${!loading && sortedRows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {showSkeleton ? (
                            Array.from({ length: 10 }).map((_, rowIndex) => (
                                <tr key={rowIndex} className="border-b border-gray-100 bg-transparent hover:!bg-transparent">
                                    {columns.map((column) => (
                                        <td key={column.key} className="bg-transparent px-2 py-1">
                                            <Skeleton height={14} />
                                        </td>
                                    ))}
                                </tr>
                            ))
                        ) : sortedRows.length === 0 && initialLoadCompleted ? (
                            <tr>
                                <td colSpan={columns.length} className="px-4 py-6 text-center text-gray-400 text-xs">
                                    {filters.mode !== 'customer'
                                        ? 'Econopack-läge kommer i nästa steg'
                                        : !filters.customerId
                                            ? 'Välj kund för att hämta rapportdata'
                                            : 'Inga leveranser för vald kund och period'}
                                </td>
                            </tr>
                        ) : (
                            sortedRows.map((row) => (
                                <tr key={`${row.customerName}__${row.productName}__${row.materialGroup}__${row.packagingFeeCategory}`} className="border-b border-gray-100 hover:bg-blue-50">
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{row.customerName}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{row.productName}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{formatMaterialGroup(row.materialGroup)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{row.packagingFeeCategory ?? ''}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-right text-xs text-gray-800">
                                        {Number(row.totalDeliveredCount ?? 0).toLocaleString('sv-SE')}
                                    </td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-right text-xs font-semibold text-gray-800">
                                        {formatWeight(row.totalWeight)}<span className='ml-1 font-normal'>kg</span>
                                    </td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

export default PackagingReport;
