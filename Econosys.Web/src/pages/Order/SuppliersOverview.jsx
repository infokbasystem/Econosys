import React, { useEffect, useMemo, useRef, useState } from 'react';
import ExcelJS from 'exceljs';
import { ArrowLeftCircle, ArrowRightCircle, Package, Percent, Plus, Search, TrendingUp, X } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import {
    CartesianGrid,
    ResponsiveContainer,
    Scatter,
    ScatterChart,
    XAxis,
    YAxis,
    ZAxis,
} from 'recharts';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import apiClient from '../../config/apiClient';
import ActionButton from '../../components/ActionButton';
import LabeledSwitch from '../../components/LabeledSwitch';
import { getSwedishTodayDateString } from '../../helpers/dateUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';
import bg from "../../assets/content.png";

const PAGE_SIZE = 25;
const TABLE_FONT_STYLE = { fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" };

const listColumns = [
    { key: 'id', label: 'Nr', width: '4%' },
    { key: 'name', label: 'Leverantör', width: '18%' },
    { key: 'address', label: 'Adress', width: '16%' },
    { key: 'postalNr', label: 'Postnr', width: '7%' },
    { key: 'postalAddress', label: 'Ort', width: '10%' },
    { key: 'country', label: 'Land', width: '8%' },
    { key: 'note', label: 'Notering' },
];

const metricGroups = [
    {
        key: 'currentYear',
        label: 'INNEVARANDE AR',
        className: 'bg-green-200',
        columnClassName: 'bg-green-100',
        excelFill: 'FFC6EFCE',
        columns: [
            { key: 'currentYear.purchaseValue', label: 'INK-VARDE', type: 'money', width: 55 },
            { key: 'currentYear.salesValue', label: 'FSG-VARDE', type: 'money', width: 55 },
            { key: 'currentYear.freight', label: 'FKT', type: 'money', width: 40 },
            { key: 'currentYear.orderCount', label: 'ANT ORDER', type: 'count', width: 55 },
            { key: 'currentYear.addition', label: 'PASLAG', type: 'percent', width: 62 },
        ],
    },
    {
        key: 'currentVsPrevious',
        label: 'INNEV. vs FOREG.',
        className: 'bg-yellow-100',
        columnClassName: 'bg-yellow-50',
        excelFill: 'FFFFF2CC',
        columns: [
            { key: 'currentVsPrevious.purchaseValue', label: 'INK-VARDE', type: 'money', width: 55 },
            { key: 'currentVsPrevious.salesValue', label: 'FSG-VARDE', type: 'money', width: 55 },
            { key: 'currentVsPrevious.orderCount', label: 'ANT ORDER', type: 'count', width: 55 },
        ],
    },
    {
        key: 'previousYtd',
        label: 'FOREGAENDE YTD',
        className: 'bg-sky-100',
        columnClassName: 'bg-sky-50',
        excelFill: 'FFDDEBF7',
        columns: [
            { key: 'previousYtd.purchaseValue', label: 'INK-VARDE', type: 'money', width: 55 },
            { key: 'previousYtd.salesValue', label: 'FSG-VARDE', type: 'money', width: 55 },
            { key: 'previousYtd.freight', label: 'FKT', type: 'money', width: 40 },
            { key: 'previousYtd.orderCount', label: 'ANT ORDER', type: 'count', width: 55 },
            { key: 'previousYtd.addition', label: 'PASLAG', type: 'percent', width: 62 },
        ],
    },
    {
        key: 'previousYear',
        label: 'FOREGAENDE HELAR',
        className: 'bg-blue-200',
        columnClassName: 'bg-blue-100',
        excelFill: 'FF9DC3E6',
        columns: [
            { key: 'previousYear.purchaseValue', label: 'INK-VARDE', type: 'money', width: 55 },
            { key: 'previousYear.salesValue', label: 'FSG-VARDE', type: 'money', width: 55 },
            { key: 'previousYear.freight', label: 'FKT', type: 'money', width: 40 },
            { key: 'previousYear.orderCount', label: 'ANT ORDER', type: 'count', width: 55 },
            { key: 'previousYear.addition', label: 'PASLAG', type: 'percent', width: 62 },
        ],
    },
];

const metricColumnCount = metricGroups.reduce((sum, group) => sum + group.columns.length, 0);
const metricColumns = metricGroups.flatMap((group) => group.columns.map((column) => ({
    ...column,
    className: group.columnClassName,
})));
const numberFormatter = new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 0 });
const percentFormatter = new Intl.NumberFormat('sv-SE', { style: 'percent', minimumFractionDigits: 1, maximumFractionDigits: 1 });
const compactCurrencyFormatter = new Intl.NumberFormat('sv-SE', { notation: 'compact', maximumFractionDigits: 1 });

const getMetricValue = (row, key) => key.split('.').reduce((value, part) => value?.[part], row) ?? 0;

const formatMetricValue = (value, type) => {
    if (value == null || value === 0) return '';
    if (type === 'percent') return percentFormatter.format(value ?? 0);
    if (type === 'money') return numberFormatter.format((value ?? 0) / 1000);
    return numberFormatter.format(value ?? 0);
};

const formatCompactCurrency = (value) => `${compactCurrencyFormatter.format(value ?? 0)}`;

const marginColorBands = [
    { max: 0, label: '< 0%', color: '#e11d48' },
    { max: 0.1, label: '-10%', color: '#f97316' },
    { max: 0.16, label: '–16%', color: '#eab308' },
    { max: 0.22, label: '–22%', color: '#16a34a' },
    { max: 0.28, label: '–28%', color: '#0891b2' },
    { max: 0.34, label: '34%', color: '#2563eb' },
    { max: Number.POSITIVE_INFINITY, label: '≥ 34 %', color: '#c026d3' },
];

const getMarginColor = (margin) => marginColorBands.find((band) => margin < band.max)?.color;

const SupplierScatterDot = ({ cx, cy, payload, selectedSupplierId, onSelect }) => {
    const isSelected = selectedSupplierId === payload.supplierId;
    const color = getMarginColor(payload.margin);
    const selectSupplier = () => onSelect(payload);

    return (
        <g
            role="button"
            tabIndex={0}
            aria-label={`Visa detaljer för ${payload.supplierName}`}
            className="cursor-pointer outline-none"
            onClick={selectSupplier}
            onKeyDown={(event) => {
                if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    selectSupplier();
                }
            }}
        >
            <circle cx={cx} cy={cy} r={12} fill="transparent" />
            {isSelected && <circle cx={cx} cy={cy} r={9} fill="none" stroke={color} strokeWidth={2} opacity={0.45} />}
            <circle cx={cx} cy={cy} r={isSelected ? 6.5 : 5} fill={color} fillOpacity={isSelected ? 1 : 0.9} stroke="#fff" strokeWidth={1.25} />
        </g>
    );
};

const DetailRow = ({ icon, label, value }) => (
    <div className="flex items-center justify-between gap-3 text-[11px]">
        <span className="flex items-center gap-1.5 text-slate-400">
            {icon}
            {label}
        </span>
        <span className="font-semibold tabular-nums text-slate-50">{value}</span>
    </div>
);

const initialPagination = {
    pageNumber: 1,
    pageSize: PAGE_SIZE,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
};

const SuppliersOverview = () => {
    const navigate = useNavigate();
    const chartPopupRef = useRef(null);
    const [statisticsRows, setStatisticsRows] = useState([]);
    const [statisticsLoading, setStatisticsLoading] = useState(true);
    const [statisticsLoaded, setStatisticsLoaded] = useState(false);
    const [statisticsError, setStatisticsError] = useState('');
    const [statisticsSort, setStatisticsSort] = useState({ key: 'currentYear.purchaseValue', direction: 'desc' });
    const [showStatisticsInfo, setShowStatisticsInfo] = useState(false);
    const [selectedChartSupplier, setSelectedChartSupplier] = useState(null);
    const [rows, setRows] = useState([]);
    const [loading, setLoading] = useState(true);
    const [searchLoaded, setSearchLoaded] = useState(false);
    const [searchInput, setSearchInput] = useState('');
    const [searchTerm, setSearchTerm] = useState('');
    const [includeInactive, setIncludeInactive] = useState(true);
    const [pagination, setPagination] = useState(initialPagination);
    const [selectedRowId, setSelectedRowId] = useState(null);

    useEffect(() => {
        if (!selectedChartSupplier) return undefined;

        const closePopupOnOutsidePointerDown = (event) => {
            if (!chartPopupRef.current?.contains(event.target)) {
                setSelectedChartSupplier(null);
            }
        };

        document.addEventListener('pointerdown', closePopupOnOutsidePointerDown);
        return () => document.removeEventListener('pointerdown', closePopupOnOutsidePointerDown);
    }, [selectedChartSupplier]);

    useEffect(() => {
        let isActive = true;

        const loadStatistics = async () => {
            setStatisticsLoading(true);
            setStatisticsError('');

            try {
                const response = await getSharedRequest(
                    'reporting:supplier-overview',
                    () => apiClient.post('/reporting/supplier-overview')
                );
                if (!isActive) return;
                setStatisticsRows(response?.data?.rows ?? []);
            } catch (error) {
                console.error('Failed to load supplier statistics:', error);
                if (!isActive) return;
                setStatisticsRows([]);
                setStatisticsError('Leverantorsstatistiken kunde inte laddas.');
            } finally {
                if (isActive) {
                    setStatisticsLoaded(true);
                    setStatisticsLoading(false);
                }
            }
        };

        loadStatistics();

        return () => {
            isActive = false;
        };
    }, []);

    useEffect(() => {
        const timer = setTimeout(() => {
            setSearchTerm(searchInput);
            setPagination((prev) => ({ ...prev, pageNumber: 1 }));
        }, 350);

        return () => clearTimeout(timer);
    }, [searchInput]);

    const requestBody = useMemo(() => ({
        searchTerm: searchTerm.trim() || null,
        active: includeInactive ? null : true,
        sortBy: 'name',
        sortDescending: false,
        pagination: {
            pageNumber: pagination.pageNumber,
            pageSize: pagination.pageSize,
        },
    }), [includeInactive, pagination.pageNumber, pagination.pageSize, searchTerm]);

    useEffect(() => {
        let isActive = true;

        const loadRows = async () => {
            setLoading(true);

            try {
                const requestKey = `suppliers:search:${JSON.stringify(requestBody)}`;
                const response = await getSharedRequest(requestKey, () => apiClient.post('/suppliers/search', requestBody));
                if (!isActive) return;

                const data = response?.data ?? {};
                const totalCount = data.totalCount ?? 0;
                const totalPages = data.totalPages ?? 0;
                const pageNumber = data.pageNumber ?? pagination.pageNumber;

                setRows(data.items ?? []);
                setPagination((prev) => ({
                    ...prev,
                    pageNumber,
                    pageSize: data.pageSize ?? prev.pageSize,
                    totalCount,
                    totalPages,
                    hasPreviousPage: pageNumber > 1,
                    hasNextPage: totalPages > 0 && pageNumber < totalPages,
                }));
            } catch (error) {
                console.error('Failed to load suppliers overview:', error);
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
                if (isActive) {
                    setSearchLoaded(true);
                    setLoading(false);
                }
            }
        };

        loadRows();

        return () => {
            isActive = false;
        };
    }, [pagination.pageNumber, pagination.pageSize, requestBody]);

    const sortedStatisticsRows = useMemo(() => [...statisticsRows].sort((left, right) => {
        const leftValue = getMetricValue(left, statisticsSort.key);
        const rightValue = getMetricValue(right, statisticsSort.key);
        const valueDifference = leftValue - rightValue;

        if (valueDifference !== 0) {
            return statisticsSort.direction === 'asc' ? valueDifference : -valueDifference;
        }

        return (left.supplierName ?? '').localeCompare(right.supplierName ?? '', 'sv');
    }), [statisticsRows, statisticsSort]);

    const supplierChartData = useMemo(() => statisticsRows
        .filter((row) => (row.currentYear?.orderCount ?? 0) > 0)
        .map((row) => {
            const revenue = row.currentYear?.salesValue ?? 0;
            const contributionMargin = row.currentYear?.tb ?? 0;

            return {
                supplierId: row.supplierId,
                supplierName: row.supplierName,
                revenue,
                contributionMargin,
                orderCount: row.currentYear?.orderCount ?? 0,
                margin: revenue === 0 ? 0 : contributionMargin / revenue,
            };
        }), [statisticsRows]);

    const showSkeleton = loading && !searchLoaded;
    const showStatisticsSkeleton = statisticsLoading && !statisticsLoaded;

    const handleStatisticsSort = (key) => {
        setStatisticsSort((previous) => ({
            key,
            direction: previous.key === key && previous.direction === 'desc' ? 'asc' : 'desc',
        }));
    };

    const handlePageChange = (nextPage) => {
        if (nextPage < 1) return;
        if (pagination.totalPages > 0 && nextPage > pagination.totalPages) return;
        setPagination((prev) => ({ ...prev, pageNumber: nextPage }));
    };

    const escapeCsv = (value) => `"${String(value ?? '').replace(/"/g, '""')}"`;

    const toCsvLine = (values) => values.map(escapeCsv).join(';');

    const exportVisibleRows = () => {
        if (!rows.length) return;

        const headers = ['NR', 'LEVERANTOR', 'ADRESS', 'POSTNR', 'ORT', 'LAND', 'NOTERING', 'AKTIV'];
        const lines = [
            toCsvLine(headers),
            ...rows.map((row) => toCsvLine([
                row.id,
                row.name,
                row.address,
                row.postalNr,
                row.postalAddress,
                row.country,
                row.note,
                row.active ? 'Ja' : 'Nej',
            ])),
        ];

        const blob = new Blob([`\ufeff${lines.join('\n')}`], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `leverantorer-visade-${getSwedishTodayDateString()}.csv`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    };

    const exportAllRows = async () => {
        try {
            const requestBodyAll = {
                ...requestBody,
                pagination: {
                    pageNumber: 1,
                    pageSize: 10000,
                },
            };
            const requestKey = `suppliers:search:all:${JSON.stringify(requestBodyAll)}`;
            const response = await getSharedRequest(requestKey, () => apiClient.post('/suppliers/search', requestBodyAll));
            const allRows = response?.data?.items ?? [];
            if (!allRows.length) return;

            const headers = ['NR', 'LEVERANTOR', 'ADRESS', 'POSTNR', 'ORT', 'LAND', 'NOTERING', 'AKTIV'];
            const lines = [
                toCsvLine(headers),
                ...allRows.map((row) => toCsvLine([
                    row.id,
                    row.name,
                    row.address,
                    row.postalNr,
                    row.postalAddress,
                    row.country,
                    row.note,
                    row.active ? 'Ja' : 'Nej',
                ])),
            ];

            const blob = new Blob([`\ufeff${lines.join('\n')}`], { type: 'text/csv;charset=utf-8;' });
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `leverantorer-alla-${getSwedishTodayDateString()}.csv`;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
        } catch (error) {
            console.error('Failed to export all suppliers:', error);
        }
    };

    const exportStatistics = async () => {
        if (!sortedStatisticsRows.length) return;

        const workbook = new ExcelJS.Workbook();
        const worksheet = workbook.addWorksheet('Leverantorsoversikt', {
            views: [{ state: 'frozen', xSplit: 1, ySplit: 2 }],
        });

        worksheet.getColumn(1).width = 30;
        metricColumns.forEach((column, index) => {
            worksheet.getColumn(index + 2).width = column.type === 'percent' ? 12 : 14;
        });

        worksheet.mergeCells(1, 1, 2, 1);
        worksheet.getCell(1, 1).value = 'LEVERANTOR';

        let startColumn = 2;
        metricGroups.forEach((group) => {
            const endColumn = startColumn + group.columns.length - 1;
            worksheet.mergeCells(1, startColumn, 1, endColumn);
            worksheet.getCell(1, startColumn).value = group.label;

            group.columns.forEach((column, index) => {
                const cell = worksheet.getCell(2, startColumn + index);
                cell.value = column.label;
                cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: group.excelFill } };
            });

            for (let columnIndex = startColumn; columnIndex <= endColumn; columnIndex += 1) {
                worksheet.getCell(1, columnIndex).fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: group.excelFill } };
            }
            startColumn = endColumn + 1;
        });

        sortedStatisticsRows.forEach((row) => {
            const values = [
                row.supplierName,
                ...metricColumns.map((column) => {
                    const value = getMetricValue(row, column.key);
                    return column.type === 'money' ? value / 1000 : value;
                }),
            ];
            const excelRow = worksheet.addRow(values);
            metricColumns.forEach((column, index) => {
                excelRow.getCell(index + 2).numFmt = column.type === 'percent' ? '0.0%' : '#,##0';
            });
        });

        worksheet.getRows(1, 2).forEach((row) => {
            row.font = { bold: true, size: 10 };
            row.alignment = { vertical: 'middle', horizontal: 'center', wrapText: true };
        });
        worksheet.getCell(1, 1).alignment = { vertical: 'middle', horizontal: 'left' };
        worksheet.autoFilter = { from: { row: 2, column: 1 }, to: { row: 2, column: metricColumnCount + 1 } };

        const buffer = await workbook.xlsx.writeBuffer();
        const blob = new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `leverantorsoversikt-${getSwedishTodayDateString()}.xlsx`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    };

    const getRowClass = (rowId) => {
        const isSelected = selectedRowId === rowId;
        return [
            'h-6 cursor-pointer border-b border-gray-100',
            isSelected ? 'bg-lime-100/80' : '',
            'hover:bg-lime-200/70',
        ].join(' ');
    };

    const handleOpenSupplier = (event, supplierId) => {
        event.preventDefault();
        event.stopPropagation();
        navigate(`/order/suppliers/${supplierId}`);
    };

    return (
        <div className="flex h-full flex-col gap-5 pt-1 pb-4 ps-10 pe-0">
            <div className="grid grid-cols-1 gap-10 mt-2 lg:grid-cols-[1fr_auto]">

                <section className="relative min-w-0 px-0 pb-3 pt-5">
                    <div className="flex items-baseline justify-center gap-3">
                        <h2 className="text-xs font-semibold text-slate-500 uppercase">Täckningsbidrag vs. omsättning</h2>
                        {/* <span className="shrink-0 text-tiny text-slate-400">{supplierChartData.length} leverantörer · YTD</span> */}
                    </div>
                    <div className="mt-3 flex flex-wrap items-center justify-center gap-x-3 gap-y-1 text-[9px] text-slate-500" aria-label="Färgförklaring för TB-marginal">
                        {marginColorBands.map((band) => (
                            <span key={band.label} className="flex items-center gap-1 whitespace-nowrap">
                                <span className="h-2 w-2 rounded-full" style={{ backgroundColor: band.color }} />
                                {band.label}
                            </span>
                        ))}
                    </div>
                    {/* <p className="mb-1 mt-0.5 text-[11px] text-slate-500">Klicka på en punkt för detaljer</p> */}

                    <div className="h-[300px] w-full mt-5">
                        {showStatisticsSkeleton ? (
                            <Skeleton height={235} />
                        ) : statisticsError ? (
                            <div className="flex h-full items-center justify-center text-center text-xs text-red-500">Diagrammet kunde inte laddas.</div>
                        ) : supplierChartData.length === 0 ? (
                            <div className="flex h-full items-center justify-center text-xs text-gray-400">Ingen leverantörsdata för innevarande år.</div>
                        ) : (
                            <ResponsiveContainer width="100%" height="100%">
                                <ScatterChart accessibilityLayer={false} margin={{ top: 8, right: 8, bottom: 14, left: -12 }}>
                                    <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                                    <XAxis
                                        type="number"
                                        dataKey="revenue"
                                        name="Omsättning"
                                        tickFormatter={formatCompactCurrency}
                                        tick={{ fontSize: 10, fill: '#999999' }}
                                        axisLine={{ stroke: '#cccccc' }}
                                        tickLine={false}
                                        label={{ value: 'OMSÄTTNING', position: 'insideBottom', offset: -9, fontSize: 11, fill: '#666666' }}
                                    />
                                    <YAxis
                                        type="number"
                                        dataKey="contributionMargin"
                                        name="Täckningsbidrag"
                                        tickFormatter={formatCompactCurrency}
                                        tick={{ fontSize: 10, fill: '#999999' }}
                                        axisLine={{ stroke: '#cccccc' }}
                                        tickLine={false}
                                        width={50}
                                        label={{ value: 'TB', angle: -90, position: 'insideLeft', offset: 65, fontSize: 11, fill: '#666666' }}
                                    />
                                    <ZAxis range={[50, 50]} />
                                    <Scatter
                                        data={supplierChartData}
                                        isAnimationActive={false}
                                        shape={(props) => (
                                            <SupplierScatterDot
                                                {...props}
                                                selectedSupplierId={selectedChartSupplier?.supplierId}
                                                onSelect={setSelectedChartSupplier}
                                            />
                                        )}
                                    />
                                </ScatterChart>
                            </ResponsiveContainer>
                        )}
                    </div>

                    {selectedChartSupplier && (
                        <div ref={chartPopupRef} className="absolute right-4 top-11 z-20 w-52 bg-slate-900 px-3.5 py-3 text-white shadow-xl">
                            <div className="mb-2 flex items-start justify-between gap-2">
                                <div className="text-xs font-semibold leading-tight">{selectedChartSupplier.supplierName}</div>
                                <button
                                    type="button"
                                    onClick={() => setSelectedChartSupplier(null)}
                                    className="shrink-0 p-0.5 text-slate-400 hover:text-white"
                                    aria-label="Stäng"
                                >
                                    <X size={14} />
                                </button>
                            </div>
                            <div className="flex flex-col gap-1.5">
                                <DetailRow icon={<Package size={12} />} label="Antal order" value={numberFormatter.format(selectedChartSupplier.orderCount)} />
                                <DetailRow icon={<TrendingUp size={12} />} label="Omsättning" value={formatCompactCurrency(selectedChartSupplier.revenue)} />
                                <DetailRow icon={<TrendingUp size={12} />} label="TB" value={formatCompactCurrency(selectedChartSupplier.contributionMargin)} />
                                <DetailRow icon={<Percent size={12} />} label="TB-marginal" value={percentFormatter.format(selectedChartSupplier.margin)} />
                            </div>
                        </div>
                    )}
                </section>

                <section className="relative min-w-0 overflow-hidden">
                    <div className="relative mb-2 flex min-h-5 items-center justify-end gap-8 px-2 text-xs">
                        <button
                            type="button"
                            onClick={exportStatistics}
                            disabled={statisticsLoading || !sortedStatisticsRows.length}
                            className="font-medium text-slate-500 transition-colors hover:text-slate-700 disabled:cursor-not-allowed disabled:text-gray-300"
                        >
                            Exportera till EXCEL
                        </button>
                        <div className="relative">
                            {showStatisticsInfo && (
                                <>
                                    <div className="fixed inset-0 z-30" onClick={() => setShowStatisticsInfo(false)} />
                                    <div className="absolute right-0 top-7 z-40 w-[min(420px,80vw)] border border-gray-400 bg-yellow-50 px-8 py-6 text-xs text-yellow-900 shadow-sm">
                                        <p>Baseras pa det som ordererkants under respektive ar.</p>
                                        <p className="mt-2">Ink.varde beraknas utifran producerat antal och inkopspriset med davarande valutakurs.</p>
                                        <p className="mt-2">Fsg.varde beraknas utifran producerat antal och forsaljningspriset med davarande valutakurs.</p>
                                        <p className="mt-2">Om Econopack ar transportansvariga visas fktkostnaden utifran den arkiverade kalkylens fktkolumn.</p>
                                    </div>
                                </>
                            )}
                            <button
                                type="button"
                                onClick={() => setShowStatisticsInfo((previous) => !previous)}
                                className="font-medium text-slate-500 transition-colors hover:text-slate-700"
                            >
                                Visa info
                            </button>
                        </div>
                    </div>

                    <div className="overflow-hidden">
                        <div className="isolate overflow-auto max-h-[350px]">
                            <table className="border-separate border-spacing-0 text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                            <colgroup>
                                <col style={{ width: 176, minWidth: 176 }} />
                                {metricColumns.map((column) => (
                                    <col key={column.key} style={{ width: column.width, minWidth: column.width }} />
                                ))}
                            </colgroup>
                            <thead>
                                <tr className="text-tiny text-gray-600">
                                    <th className="sticky top-0 left-0 z-30 w-44 min-w-44 px-2 py-1 text-left font-medium" style={{ backgroundImage: `url(${bg})` }}></th>
                                    {metricGroups.map((group) => (
                                        <th key={group.key} colSpan={group.columns.length} className={`sticky top-0 h-[22px] z-20 px-2 text-center font-medium ${group.className}`}>
                                            {group.label}
                                        </th>
                                    ))}
                                </tr>
                                <tr className="text-tiny text-gray-500">
                                    <th className="sticky top-[22px] left-0 z-30 h-[30px] w-44 min-w-44 px-2 border-b border-stone-400 text-left font-normal" style={{ backgroundImage: `url(${bg})` }}>LEVERANTÖR</th>
                                    {metricGroups.map((group) => (
                                        group.columns.map((column) => (
                                            <th
                                                key={column.key}
                                                onClick={() => handleStatisticsSort(column.key)}
                                                className={`sticky top-[22px] z-20 h-[30px] cursor-pointer border-b border-stone-400 pr-2 text-right font-normal ${group.columnClassName}`}
                                                style={{ width: column.width, minWidth: column.width }}
                                            >
                                                {column.label}
                                            </th>
                                        ))
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {showStatisticsSkeleton ? (
                                    Array.from({ length: 10 }).map((_, index) => (
                                        <tr key={index}>
                                            <td className="px-2 py-1" colSpan={metricColumnCount + 1}><Skeleton height={14} /></td>
                                        </tr>
                                    ))
                                ) : statisticsError ? (
                                    <tr>
                                        <td colSpan={metricColumnCount + 1} className="px-4 py-4 text-center text-red-500">{statisticsError}</td>
                                    </tr>
                                ) : sortedStatisticsRows.length === 0 ? (
                                    <tr>
                                        <td colSpan={metricColumnCount + 1} className="px-4 py-4 text-center text-gray-400">Inga leverantorer hittades.</td>
                                    </tr>
                                ) : (
                                    sortedStatisticsRows.map((row) => (
                                        <tr key={row.supplierId}>
                                            <td className="sticky left-0 z-10 max-w-44 truncate px-2 py-1 text-left text-gray-700" title={row.supplierName}>{row.supplierName}</td>
                                            {metricColumns.map((column) => (
                                                <td key={`${row.supplierId}-${column.key}`} className={`px-2 py-[5px] text-right tabular-nums text-gray-600 ${column.className}`}>
                                                    {formatMetricValue(getMetricValue(row, column.key), column.type)}
                                                </td>
                                            ))}
                                        </tr>
                                    ))
                                )}
                            </tbody>
                            </table>
                        </div>
                    </div>
                </section>
            </div>

            <section className="flex min-h-0 flex-1 flex-col mt-8">
                <div className="flex items-center gap-4 overflow-x-auto whitespace-nowrap mx-3 pb-0">
                    <ActionButton
                        label="Skapa ny leverantör"
                        icon={Plus}
                        onClick={() => navigate('/order/suppliers/new')}
                        accent="lime"
                    />

                    <div className="relative ml-16 w-44">
                        <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
                        <input
                            type="text"
                            value={searchInput}
                            onChange={(event) => setSearchInput(event.target.value)}
                            placeholder="Leverantorsnamn"
                            className="h-7 w-full rounded-full border border-lime-600 bg-white pl-8 pr-4 text-xs text-gray-700 outline-none transition placeholder:text-gray-500 focus:border-lime-700"
                        />
                    </div>

                    <LabeledSwitch
                        label="Visa aktiva/ej aktiva"
                        name="includeInactive"
                        value={includeInactive}
                        onChange={(_rowId, _field, checked) => {
                            setIncludeInactive(checked);
                            setPagination((prev) => ({ ...prev, pageNumber: 1 }));
                        }}
                        disabled={loading}
                        containerClassName="shrink-0"
                    />

                    <button
                        type="button"
                        onClick={exportVisibleRows}
                        disabled={!rows.length}
                        className="ml-8 text-xs font-medium text-slate-500 transition-colors hover:text-slate-700 disabled:cursor-not-allowed disabled:text-gray-300"
                    >
                        EXCEL, visade
                    </button>

                    <button
                        type="button"
                        onClick={exportAllRows}
                        disabled={loading}
                        className="text-xs font-medium text-slate-500 transition-colors hover:text-slate-700 disabled:cursor-not-allowed disabled:text-gray-300"
                    >
                        EXCEL, alla
                    </button>

                    <div className="ml-auto flex items-center gap-4 text-xs text-gray-600" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                        <span>Rader <strong>{pagination.totalCount}</strong></span>
                        <span>Sida {pagination.pageNumber} av {Math.max(1, pagination.totalPages)}</span>
                        <div className="flex gap-1">
                            <button
                                type="button"
                                onClick={() => handlePageChange(pagination.pageNumber - 1)}
                                disabled={loading || !pagination.hasPreviousPage}
                                className="disabled:cursor-not-allowed disabled:opacity-40"
                            >
                                <ArrowLeftCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                            </button>
                            <button
                                type="button"
                                onClick={() => handlePageChange(pagination.pageNumber + 1)}
                                disabled={loading || !pagination.hasNextPage}
                                className="disabled:cursor-not-allowed disabled:opacity-40"
                            >
                                <ArrowRightCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                            </button>
                        </div>
                    </div>
                </div>

                <div className="border-t border-gray-300 py-1 mt-2 min-h-0 flex-1 overflow-auto">
                    <table className="table-fixed w-full border-collapse text-xs" style={TABLE_FONT_STYLE}>
                        <colgroup>
                            {listColumns.map((column) => (
                                <col key={column.key} {...(column.width ? { style: { width: column.width } } : {})} />
                            ))}
                        </colgroup>
                        <thead>
                            <tr className="text-tiny text-gray-500">
                                {listColumns.map((column) => (
                                    <th key={column.key} className="px-2 pt-1 pb-2 text-left text-tiny font-medium text-gray-500">
                                        {column.label}
                                    </th>
                                ))}
                            </tr>
                        </thead>
                        <tbody className={`${!loading && rows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                            {showSkeleton ? (
                                Array.from({ length: 12 }).map((_, index) => (
                                    <tr key={index}>
                                        <td colSpan={listColumns.length} className="px-2 py-1"><Skeleton height={16} /></td>
                                    </tr>
                                ))
                            ) : rows.length === 0 ? (
                                <tr>
                                    <td colSpan={listColumns.length} className="px-4 py-8 text-center text-gray-400">Inga leverantorer hittades.</td>
                                </tr>
                            ) : (
                                rows.map((row) => (
                                    <tr
                                        key={row.id}
                                        className={getRowClass(row.id)}
                                        onClick={() => setSelectedRowId((prev) => (prev === row.id ? null : row.id))}
                                    >
                                        <td className="truncate px-2 py-1 text-gray-800">
                                            <button
                                                type="button"
                                                onClick={(event) => handleOpenSupplier(event, row.id)}
                                                className="text-slate-700 hover:text-slate-900 hover:underline"
                                            >
                                                {row.id}
                                            </button>
                                        </td>
                                        <td className="truncate px-2 py-1 text-gray-800">
                                            <button
                                                type="button"
                                                onClick={(event) => handleOpenSupplier(event, row.id)}
                                                className="text-slate-700 hover:text-slate-900 hover:underline"
                                            >
                                                {row.name}
                                            </button>
                                        </td>
                                        <td className="truncate px-2 py-1 text-gray-800">{[row.address, row.address2].filter(Boolean).join(' ')}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{row.postalNr}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{row.postalAddress}</td>
                                        <td className="truncate px-2 py-1 text-gray-800">{row.country}</td>
                                        <td className="truncate px-2 py-1 text-gray-800" title={row.note}>{row.note}</td>
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

export default SuppliersOverview;