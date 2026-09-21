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
    { key: 'supplier', label: 'Leverantör', align: 'left', width: '24%' },
    { key: 'palletType', label: 'Palltyp', align: 'left', width: '20%' },
    { key: 'nrOfPallets', label: 'Ant. pallar', align: 'right', width: '10%' },
    { key: 'sekPerPalletIn', label: 'SEK/pall in', align: 'right', width: '11%' },
    { key: 'sekPerPalletOut', label: 'SEK/pall ut', align: 'right', width: '11%' },
    { key: 'sekPerPalletDiff', label: 'SEK/pall diff', align: 'right', width: '10%' },
    { key: 'sumDiff', label: 'Summa diff', align: 'right', width: '7%' },
    { key: 'positiveSumDiff', label: 'Positiv diff', align: 'right', width: '7%' },
];

const customerModeOptions = [
    {
        label: 'Alla kunder',
        value: false,
    },
    {
        label: 'Per kund',
        value: true,
    },
];

const PalletFollowupReport = () => {
    const now = new Date();
    now.setHours(0, 0, 0, 0);
    const yearStart = new Date(now.getFullYear(), 0, 1);

    const [loading, setLoading] = useState(false);
    const [showSkeleton, setShowSkeleton] = useState(false);
    const skeletonTimerRef = useRef(null);
    const [rows, setRows] = useState([]);
    const [initialLoadCompleted, setInitialLoadCompleted] = useState(false);
    const [customerOptions, setCustomerOptions] = useState([]);
    const [filters, setFilters] = useState({
        startDate: yearStart,
        endDate: now,
        perCustomer: false,
        customerId: null,
    });

    const [sortConfig, setSortConfig] = useState({ key: 'supplier', direction: 'asc' });

    useEffect(() => {
        let isActive = true;

        const initialize = async () => {
            await loadCustomers(isActive);
            await loadReport(isActive, filters);

            if (isActive) {
                setInitialLoadCompleted(true);
            }
        };

        void initialize();
        return () => {
            isActive = false;
        };
    }, []);

    const loadCustomers = async (isActive = true) => {
        try {
            // API validates PageSize with an upper bound of 200.
            const pageSize = 200;
            let pageNumber = 1;
            let hasNextPage = true;
            const fetched = [];

            while (hasNextPage) {
                const response = await apiClient.post(
                    '/customers/search',
                    {
                        active: true,
                        sortBy: 'Name',
                        sortDescending: false,
                        pagination: { pageNumber, pageSize },
                    },
                    {}
                );
                if (!isActive) return;

                const data = response?.data;
                const items = data?.items ?? [];
                fetched.push(...items.map((c) => ({ id: c.id, name: c.name ?? c.id })));
                hasNextPage = Boolean(data?.hasNextPage);
                pageNumber += 1;
            }

            if (!isActive) return;
            setCustomerOptions(fetched.sort((a, b) => a.name.localeCompare(b.name, 'sv-SE')));
        } catch (error) {
            console.error('Failed to load customers:', error);
            if (!isActive) return;
            setCustomerOptions([]);
        }
    };

    const loadReport = async (isActive = true, overrideFilters = null) => {
        const f = overrideFilters ?? filters;

        if (f.perCustomer && !f.customerId) {
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
            const toDateStr = (d) =>
                `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

            const body = {
                startDate: toDateStr(f.startDate),
                endDate: toDateStr(f.endDate),
                ...(f.perCustomer && f.customerId ? { customerId: f.customerId } : {}),
            };
            const requestKey = `reporting:palletfollowup:${JSON.stringify(body)}`;

            const response = await getSharedRequest(requestKey, () => apiClient.post('/reporting/palletfollowup', body));
            if (!isActive) return;

            setRows(
                (response?.data?.rows ?? []).map((r) => ({
                    ...r,
                    positiveSumDiff:
                        r.sumDiff != null && Number(r.sumDiff) > 0 ? r.sumDiff : null,
                }))
            );
        } catch (error) {
            console.error('Failed to load pallet followup report:', error);
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

            if (field === 'perCustomer' && !value) {
                next.customerId = null;
            }

            void loadReport(true, next);
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
        const dir = sortConfig.direction === 'asc' ? 1 : -1;

        list.sort((a, b) => {
            const aVal = a?.[sortConfig.key];
            const bVal = b?.[sortConfig.key];

            if (typeof aVal === 'number' || typeof bVal === 'number') {
                return ((Number(aVal) || 0) - (Number(bVal) || 0)) * dir;
            }

            return String(aVal ?? '').localeCompare(String(bVal ?? ''), 'sv-SE') * dir;
        });

        return list;
    }, [rows, sortConfig]);

    const totals = useMemo(() => {
        const totalSumDiff = rows.reduce((acc, r) => acc + (r.sumDiff != null ? Number(r.sumDiff) : 0), 0);
        const totalPositiveSumDiff = rows.reduce(
            (acc, r) => acc + (r.positiveSumDiff != null ? Number(r.positiveSumDiff) : 0),
            0
        );
        return { totalSumDiff, totalPositiveSumDiff };
    }, [rows]);

    const formatAmount = (value, decimals = 0) => {
        if (value === null || value === undefined) return '';
        return new Intl.NumberFormat('sv-SE', {
            minimumFractionDigits: decimals,
            maximumFractionDigits: decimals,
        }).format(Number(value));
    };

    const getSortIndicator = (key) => {
        if (sortConfig.key !== key) return '↕';
        return sortConfig.direction === 'asc' ? '▲' : '▼';
    };

    const handleExport = async () => {
        if (!sortedRows.length) return;

        const generatedAt = new Date().toLocaleString('sv-SE');
        const startStr = filters.startDate.toLocaleDateString('sv-SE');
        const endStr = filters.endDate.toLocaleDateString('sv-SE');

        const header = columns.map((c) => c.label);

        const dataRows = sortedRows.map((row) => [
            row.supplier ?? '',
            row.palletType ?? '',
            row.nrOfPallets ?? '',
            row.sekPerPalletIn ?? '',
            row.sekPerPalletOut ?? '',
            row.sekPerPalletDiff ?? '',
            row.sumDiff ?? '',
            row.positiveSumDiff ?? '',
        ]);

        const aoa = [
            ['Palluppföljning'],
            [`Period: ${startStr} - ${endStr}`],
            [`Genererad: ${generatedAt}`],
            [],
            header,
            ...dataRows,
        ];

        const workbook = new ExcelJS.Workbook();
        const worksheet = workbook.addWorksheet('Palluppföljning');
        aoa.forEach((row) => worksheet.addRow(row));

        const tableHeaderRow = 5;
        const tableLastRow = sortedRows.length + 5;
        const rightAligned = new Set([3, 4, 5, 6, 7]);

        for (let col = 1; col <= header.length; col++) {
            let maxLen = String(header[col - 1] ?? '').length;
            for (let row = tableHeaderRow + 1; row <= tableLastRow; row++) {
                const val = worksheet.getRow(row).getCell(col).value;
                const text = val == null ? '' : String(val);
                if (text.length > maxLen) maxLen = text.length;
            }
            worksheet.getColumn(col).width = Math.min(35, Math.max(6, maxLen + 1));
        }

        worksheet.eachRow((row) => {
            row.eachCell((cell) => { cell.font = { ...(cell.font || {}), size: 8 }; });
        });
        worksheet.getRow(tableHeaderRow).eachCell((cell) => {
            cell.font = { ...(cell.font || {}), size: 8, bold: true };
        });

        for (let row = tableHeaderRow; row <= tableLastRow; row++) {
            for (let col = 1; col <= header.length; col++) {
                if (rightAligned.has(col)) {
                    const cell = worksheet.getRow(row).getCell(col);
                    cell.alignment = { ...(cell.alignment || {}), horizontal: 'right' };
                }
            }
        }

        worksheet.autoFilter = {
            from: { row: tableHeaderRow, column: 1 },
            to: { row: tableLastRow, column: header.length },
        };

        const dateSuffix = `${filters.startDate.getFullYear()}-${String(filters.startDate.getMonth() + 1).padStart(2, '0')}`;
        const buffer = await workbook.xlsx.writeBuffer();
        const blob = new Blob([buffer], {
            type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `palluppfoljning-${dateSuffix}.xlsx`;
        link.click();
        URL.revokeObjectURL(url);
    };

    return (
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            {/* <div className="mt-2 text-sm text-gray-500">Palluppföljning</div> */}

            <div className={`relative z-20 flex items-center gap-6 mt-2 pb-2 ${loading ? 'opacity-60 pointer-events-none' : ''}`}>

                <div className="flex items-center text-xs">
                    <div className="w-45">
                        <SegmentedFilter
                            value={filters.perCustomer}
                            options={customerModeOptions}
                            onChange={(value) => handleFilterChange('perCustomer', value)}
                            theme="lime"
                        />
                    </div>
                </div>

                <div className=''>
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
                            setFilters((prev) => {
                                const next = { ...prev, startDate, endDate };
                                loadReport(null, next);
                                return next;
                            });
                        }}
                    />
                </div>

                {filters.perCustomer && (
                    <div className="w-64">
                        <LabeledReactSelect
                            name="customerId"
                            filterStyle
                            value={filters.customerId ?? ''}
                            items={[{ id: '', name: 'Välj kund...' }, ...customerOptions]}
                            onChange={(val) => handleFilterChange('customerId', val ? Number(val) : null)}
                            isDisabled={loading}
                        />
                    </div>
                )}

                {initialLoadCompleted && !loading && (
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
                    </div>
                )}

                <div className='ml-auto flex items-center mr-4' style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                {initialLoadCompleted && rows.length > 0 && (
                    <div className="flex items-center">
                        <div className="ml-10 text-xs text-gray-500">
                            Summa diff: <strong className={totals.totalSumDiff >= 0 ? 'text-green-700' : 'text-red-600'}>{formatAmount(totals.totalSumDiff, 0)}</strong>
                        </div>
                        <div className="ml-10 text-xs text-gray-500">
                            Positiv diff: <strong className="text-green-700">{formatAmount(totals.totalPositiveSumDiff, 0)}</strong>
                        </div>
                    </div>
                )}

                </div>

                {/* <button
                    type="button"
                    onClick={handleExport}
                    disabled={loading || sortedRows.length === 0}
                    className="W-23 ml-20 shadow-md/30 text-xs text-gray bg-amber-200 hover:bg-amber-300 px-4 py-[5px]"
                >
                    Excel
                </button> */}
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
                                    className={`overflow-hidden text-ellipsis whitespace-nowrap px-2 py-1.5 text-tiny font-medium text-gray-400 cursor-pointer select-none ${col.align === 'right' ? 'text-right' : 'text-left'}`}
                                    onClick={() => handleSort(col.key)}
                                >
                                    {col.label}
                                    <span className="ml-1 text-gray-400 text-tiny">{getSortIndicator(col.key)}</span>
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody className={`divide-y divide-gray-100 ${!loading && sortedRows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {showSkeleton ? (
                            Array.from({ length: 10 }).map((_, i) => (
                                <tr key={i} className="border-b border-gray-100 bg-transparent hover:!bg-transparent">
                                    {columns.map((col) => (
                                        <td key={col.key} className="bg-transparent px-2 py-1">
                                            <Skeleton height={14} />
                                        </td>
                                    ))}
                                </tr>
                            ))
                        ) : sortedRows.length === 0 && initialLoadCompleted ? (
                            <tr>
                                <td colSpan={columns.length} className="px-4 py-6 text-center text-gray-400 text-xs">
                                    {filters.perCustomer && !filters.customerId
                                        ? 'Välj kund för att hämta rapportdata i läget Per kund'
                                        : 'Inga leveranser med palldata för vald period'}
                                </td>
                            </tr>
                        ) : (
                            sortedRows.map((row) => (
                                <tr key={`${row.supplier ?? ''}__${row.palletType ?? ''}`} className="border-b border-gray-100 hover:bg-blue-50">
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{row.supplier}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{row.palletType}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-right text-xs text-gray-800">{row.nrOfPallets}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-right text-xs text-gray-800">
                                        {row.sekPerPalletIn != null ? formatAmount(row.sekPerPalletIn, 2) : '–'}
                                    </td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-right text-xs text-gray-800">
                                        {row.sekPerPalletOut != null ? formatAmount(row.sekPerPalletOut, 2) : '–'}
                                    </td>
                                    <td className={`px-2 pt-[6px] pb-[4px] text-right text-xs font-medium ${row.sekPerPalletDiff != null ? (Number(row.sekPerPalletDiff) >= 0 ? 'text-green-700' : 'text-red-600') : 'text-gray-800'}`}>
                                        {row.sekPerPalletDiff != null ? formatAmount(row.sekPerPalletDiff, 2) : '–'}
                                    </td>
                                    <td className={`px-2 pt-[6px] pb-[4px] text-right text-xs font-semibold ${row.sumDiff != null ? (Number(row.sumDiff) >= 0 ? 'text-green-700' : 'text-red-600') : 'text-gray-800'}`}>
                                        {row.sumDiff != null ? formatAmount(row.sumDiff, 0) : '–'}
                                    </td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-right text-xs font-semibold text-green-700">
                                        {row.positiveSumDiff != null ? formatAmount(row.positiveSumDiff, 0) : '–'}
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

export default PalletFollowupReport;
