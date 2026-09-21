import { Fragment, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import ExcelJS from 'exceljs';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';
import {
    CartesianGrid,
    ResponsiveContainer,
    Scatter,
    ScatterChart,
    XAxis,
    YAxis,
    ZAxis,
} from 'recharts';

import apiClient from '../../config/apiClient';
import ActionButton from '../../components/ActionButton';
import DateRangePicker from '../../components/DaterangePicker';
import { formatDateShort, getSwedishTodayDateString, toSwedishDateInputValue } from '../../helpers/dateUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';

const PROCESS_LABELS = {
    ORDER: 'Beställning',
    DELIVERY: 'Leverans',
    INVOICE: 'Fakturering',
    SERVICE: 'Tjänst',
    OTHER: 'Övrigt',
};

const TYPE_LABELS = {
    PRODUCTIONERROR: 'Produktionsfel',
    MATERIALERROR: 'Materialfel',
    PACKAGINGERROR: 'Packningsfel',
    PRICEERROR: 'Prisfel',
    DELIVERYDELAY: 'Leveransförsening',
    DELIVERYDAMAGE: 'Leveransskada',
    TRANSPORTORLOADING: 'Transport/Lastning',
    OTHER: 'Övrigt',
};

const DOT_COLORS = ['#65a30d', '#0284c7', '#c026d3', '#ea580c', '#0d9488', '#4f46e5', '#dc2626', '#ca8a04'];

const detailColumns = [
    { key: 'deviationNr', label: 'Avvikelsenr', width: '6%' },
    { key: 'customerOrderNr', label: 'Ordernr', width: '6%' },
    { key: 'customerName', label: 'Kund', width: '20%' },
    { key: 'deviationOpened', label: 'Öppnad', width: '10%' },
    { key: 'deviationClosedToSupplier', label: 'Stängd mot lev.', width: '10%' },
    { key: 'deviationClosed', label: 'Stängd mot kund', width: '10%' },
    { key: 'daysToCloseToSupplier', label: 'Dagar', width: '6%', align: 'right' },
    { key: 'deviationTypeCode', label: 'Typ', width: '12%' },
    { key: 'deviationProcessCode', label: 'Process', width: '10%' },
    { key: 'description', label: 'Beskrivning', width: '22%' },
];

const formatNumber = (value, digits = 0) => {
    if (value === null || value === undefined) return '';
    return new Intl.NumberFormat('sv-SE', {
        minimumFractionDigits: digits,
        maximumFractionDigits: digits,
    }).format(Number(value));
};

const getProcessLabel = (code) => PROCESS_LABELS[code] ?? '';
const getTypeLabel = (code) => TYPE_LABELS[code] ?? '';

const SupplierScatterDot = ({ cx, cy, payload, hoveredSupplierId, onHover }) => {
    const isHovered = hoveredSupplierId === payload.supplierId;
    const isDimmed = hoveredSupplierId !== null && !isHovered;

    return (
        <g
            className="cursor-pointer"
            opacity={isDimmed ? 0.25 : 1}
            onMouseEnter={() => onHover(payload.supplierId)}
            onMouseLeave={() => onHover(null)}
        >
            <circle cx={cx} cy={cy} r={12} fill="transparent" />
            {isHovered && <circle cx={cx} cy={cy} r={12} fill="none" stroke={payload.color} strokeWidth={2} opacity={0.45} />}
            <circle cx={cx} cy={cy} r={isHovered ? 10 : 8} fill={payload.color} fillOpacity={0.9} stroke="#fff" strokeWidth={1.25} />
            <text x={cx} y={cy + 3} textAnchor="middle" fontSize={9} fill="#ffffff" fontWeight={600} pointerEvents="none">
                {payload.index}
            </text>
        </g>
    );
};

const getYearToDateRange = () => {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return { startDate: new Date(today.getFullYear(), 0, 1), endDate: today };
};

const SupplierReport = () => {
    const navigate = useNavigate();
    const [range, setRange] = useState(getYearToDateRange);
    const [suppliers, setSuppliers] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [exporting, setExporting] = useState(false);
    const [hoveredSupplierId, setHoveredSupplierId] = useState(null);
    const [selectedDeviationId, setSelectedDeviationId] = useState(null);

    useEffect(() => {
        if (!range.startDate || !range.endDate) return undefined;

        let isActive = true;
        const payload = {
            startDate: toSwedishDateInputValue(range.startDate),
            endDate: toSwedishDateInputValue(range.endDate),
        };

        const load = async () => {
            setLoading(true);
            setError('');

            try {
                const response = await getSharedRequest(
                    `deviations:supplier-report:${payload.startDate}:${payload.endDate}`,
                    () => apiClient.post('/deviations/supplier-report', payload)
                );
                if (!isActive) return;

                setSuppliers(response.data?.suppliers ?? []);
            } catch {
                if (!isActive) return;
                setSuppliers([]);
                setError('Rapporten kunde inte hämtas.');
            } finally {
                if (isActive) setLoading(false);
            }
        };

        load();

        return () => {
            isActive = false;
        };
    }, [range.startDate, range.endDate]);

    const chartData = useMemo(
        () => suppliers
            .filter((supplier) => supplier.averageDaysToCloseToSupplier !== null && supplier.averageDaysToCloseToSupplier !== undefined)
            .map((supplier, index) => ({
                index: index + 1,
                supplierId: supplier.supplierId,
                supplierName: supplier.supplierName,
                averageDays: Number(supplier.averageDaysToCloseToSupplier),
                deviationCount: supplier.deviationCount,
                color: DOT_COLORS[index % DOT_COLORS.length],
            })),
        [suppliers]
    );

    const dayTicks = useMemo(() => {
        const maxDays = chartData.reduce((max, item) => Math.max(max, item.averageDays), 0);
        const lastTick = Math.ceil(maxDays / 5) * 5;
        return Array.from({ length: lastTick / 5 + 1 }, (_, index) => index * 5);
    }, [chartData]);

    const totalDeviations = useMemo(
        () => suppliers.reduce((sum, supplier) => sum + (supplier.deviationCount || 0), 0),
        [suppliers]
    );

    const handleExport = async () => {
        if (suppliers.length === 0) return;

        setExporting(true);

        try {
            const workbook = new ExcelJS.Workbook();
            const sheet = workbook.addWorksheet('Leverantorsrapport');
            const headers = ['Leverantör', 'Antal avvikelser', 'Snitt dagar till stängd mot leverantör', ...detailColumns.map((column) => column.label)];

            sheet.addRow(headers);
            sheet.getRow(1).font = { bold: true, size: 9 };

            suppliers.forEach((supplier) => {
                supplier.deviations.forEach((row) => {
                    sheet.addRow([
                        supplier.supplierName ?? '',
                        supplier.deviationCount,
                        supplier.averageDaysToCloseToSupplier ?? '',
                        row.deviationNr ?? '',
                        row.customerOrderNr ?? '',
                        row.customerName ?? '',
                        formatDateShort(row.deviationOpened),
                        formatDateShort(row.deviationClosedToSupplier),
                        formatDateShort(row.deviationClosed ?? row.DeviationClosed),
                        row.daysToCloseToSupplier ?? '',
                        getTypeLabel(row.deviationTypeCode),
                        getProcessLabel(row.deviationProcessCode),
                        row.description ?? '',
                    ]);
                });
            });

            for (let col = 1; col <= headers.length; col += 1) {
                sheet.getColumn(col).width = 18;
            }

            const buffer = await workbook.xlsx.writeBuffer();
            const blob = new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `leverantorsrapport-${getSwedishTodayDateString()}.xlsx`;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
        } finally {
            setExporting(false);
        }
    };

    return (
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            <div className="flex items-center gap-5 overflow-visible whitespace-nowrap pb-2 mt-2 pl-5">
                <DateRangePicker
                    placeholder="Välj period"
                    presets={['this-month', 'last-month', 'last-3-months', 'last-12-months', 'last-year', 'year-to-date']}
                    initialPresetKey="year-to-date"
                    onApply={({ startDate, endDate }) => setRange({ startDate, endDate })}
                    triggerRadius="full"
                    triggerClassName="h-7 w-[260px] border-lime-600 px-3 text-xs text-gray-700 focus:border-lime-700"
                    openTriggerClassName="border-lime-700 ring-1 ring-lime-200"
                    closedTriggerClassName="border-lime-600 hover:border-lime-700"
                    widthClassName="w-60"
                />

                {/* <span className="text-xs text-gray-500">
                    {formatNumber(totalDeviations)} avvikelser mot leverantör hos {formatNumber(suppliers.length)} leverantörer
                </span> */}

            </div>

            {error && (
                <div className="mt-4 w-80 bg-rose-50 pl-4 py-2 text-xs text-rose-700">{error}</div>
            )}

            <div className="mt-5 flex gap-30 ps-0 pe-50">
                <div className="h-[320px] flex-1 min-w-0">
                    {loading ? (
                        <Skeleton height={300} />
                    ) : chartData.length === 0 ? (
                        <div className="flex h-full items-center justify-center text-xs text-gray-400">
                            Ingen stängd avvikelse mot leverantör i vald period.
                        </div>
                    ) : (
                        <ResponsiveContainer width="100%" height="100%">
                            <ScatterChart accessibilityLayer={false} margin={{ top: 12, right: 16, bottom: 18, left: -8 }}>
                                <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                                <XAxis
                                    type="number"
                                    dataKey="averageDays"
                                    name="Snitt dagar"
                                    domain={[0, dayTicks.at(-1) ?? 5]}
                                    ticks={dayTicks}
                                    interval={0}
                                    tick={{ fontSize: 10, fill: '#999999' }}
                                    axisLine={{ stroke: '#cccccc' }}
                                    tickLine={false}
                                    label={{ value: 'SNITT DAGAR TILL STÄNGD MOT LEVERANTÖR', position: 'insideBottom', offset: -12, fontSize: 11, fill: '#666666' }}
                                />
                                <YAxis
                                    type="number"
                                    dataKey="deviationCount"
                                    name="Antal avvikelser"
                                    allowDecimals={false}
                                    tick={{ fontSize: 10, fill: '#999999' }}
                                    axisLine={{ stroke: '#cccccc' }}
                                    tickLine={false}
                                    width={50}
                                    label={{ value: 'ANTAL', angle: -90, position: 'insideLeft', offset: 60, fontSize: 11, fill: '#666666' }}
                                />
                                <ZAxis range={[60, 60]} />
                                <Scatter
                                    data={chartData}
                                    isAnimationActive={false}
                                    shape={(props) => (
                                        <SupplierScatterDot
                                            {...props}
                                            hoveredSupplierId={hoveredSupplierId}
                                            onHover={setHoveredSupplierId}
                                        />
                                    )}
                                />
                            </ScatterChart>
                        </ResponsiveContainer>
                    )}
                </div>

                <div className="w-100 shrink-0 max-h-[320px] overflow-auto">
                    <div className="text-tiny text-gray-500 pb-1">Leverantörer</div>
                    {chartData.map((item) => (
                        <div
                            key={item.supplierId}
                            onMouseEnter={() => setHoveredSupplierId(item.supplierId)}
                            onMouseLeave={() => setHoveredSupplierId(null)}
                            className={`flex cursor-default items-center gap-2 border-b border-gray-100 py-1 text-xs transition-colors ${hoveredSupplierId === item.supplierId
                                ? 'bg-lime-50 text-gray-900'
                                : hoveredSupplierId !== null
                                    ? 'text-gray-400'
                                    : 'text-gray-700'
                                }`}
                        >
                            <span
                                className="inline-flex h-4 w-4 shrink-0 items-center justify-center rounded-full text-[9px] font-semibold text-white"
                                style={{ backgroundColor: item.color }}
                            >
                                {item.index}
                            </span>
                            <span className="truncate">{item.supplierName}</span>
                            <span className="ml-auto shrink-0 tabular-nums text-gray-500">
                                {formatNumber(item.deviationCount)} st / {formatNumber(item.averageDays, 1)} d
                            </span>
                        </div>
                    ))}
                </div>
            </div>

                <div className="mt-5 mb-3 ml-2">
                    <button
                        type="button"
                        onClick={handleExport}
                        disabled={exporting || loading || suppliers.length === 0}
                        className="text-xs font-medium text-slate-500 transition-colors hover:text-slate-700 disabled:cursor-not-allowed disabled:text-gray-300"
                    >
                        Exportera till EXCEL
                    </button>
                </div>

            <div className="min-h-0 flex-1 overflow-auto border-t border-gray-300 pe-10">
                <table className="table-fixed w-full border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <colgroup>
                        {detailColumns.map((column) => (
                            <col key={column.key} {...(column.width ? { style: { width: column.width } } : {})} />
                        ))}
                    </colgroup>
                    <thead>
                        <tr>
                            {detailColumns.map((column) => (
                                <th
                                    key={column.key}
                                    className={`px-2 py-2 text-tiny font-medium text-gray-500 ${column.align === 'right' ? 'text-right' : 'text-left'}`}
                                >
                                    {column.label}
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody>
                        {loading ? (
                            Array.from({ length: 10 }).map((_, index) => (
                                <tr key={index}>
                                    <td colSpan={detailColumns.length} className="px-2 py-2"><Skeleton height={16} /></td>
                                </tr>
                            ))
                        ) : suppliers.length === 0 ? (
                            <tr>
                                <td colSpan={detailColumns.length} className="px-4 py-8 text-center text-gray-400">
                                    Inga avvikelser mot leverantör i vald period.
                                </td>
                            </tr>
                        ) : (
                            suppliers.map((supplier) => (
                                <Fragment key={supplier.supplierId}>
                                    <tr className="bg-purple-100/50">
                                        <td colSpan={detailColumns.length - 3} className="px-2 py-2 font-semibold text-gray-800">
                                            {supplier.supplierName}
                                        </td>
                                        <td colSpan={3} className="px-2 py-1 text-right text-gray-600">
                                            {formatNumber(supplier.deviationCount)} avvikelser
                                            {supplier.averageDaysToCloseToSupplier !== null && supplier.averageDaysToCloseToSupplier !== undefined
                                                ? ` · snitt ${formatNumber(supplier.averageDaysToCloseToSupplier, 1)} dagar`
                                                : ' · ingen stängd mot leverantör'}
                                        </td>
                                    </tr>
                                    {supplier.deviations.map((row) => {
                                        const isSelected = selectedDeviationId === row.deviationId;

                                        return (
                                            <tr
                                                key={row.deviationId}
                                                onClick={() => setSelectedDeviationId((prev) => (prev === row.deviationId ? null : row.deviationId))}
                                                className={`cursor-pointer border-b border-gray-100 ${isSelected ? 'bg-lime-200/80 hover:bg-lime-200/80' : 'bg-white hover:bg-lime-200/70'}`}
                                            >
                                                <td className="truncate px-2 py-1 text-gray-800">
                                                    <button
                                                        type="button"
                                                        onClick={(event) => {
                                                            event.stopPropagation();
                                                            navigate(`/management/deviations/${row.deviationId}`);
                                                        }}
                                                        className="text-slate-700 hover:text-slate-900 hover:underline"
                                                    >
                                                        {row.deviationNr ?? row.deviationId}
                                                    </button>
                                                </td>
                                                <td className="truncate px-2 py-1 text-gray-800">{row.customerOrderNr}</td>
                                                <td className="truncate px-2 py-1 text-gray-800">{row.customerName}</td>
                                                <td className="truncate px-2 py-1 text-gray-800">{formatDateShort(row.deviationOpened)}</td>
                                                <td className="truncate px-2 py-1 text-gray-800">{formatDateShort(row.deviationClosedToSupplier)}</td>
                                                <td className="truncate px-2 py-1 text-gray-800">{formatDateShort(row.deviationClosed ?? row.DeviationClosed)}</td>
                                                <td className="px-2 py-1 text-right text-gray-800">{formatNumber(row.daysToCloseToSupplier)}</td>
                                                <td className="truncate px-2 py-1 text-gray-800">{getTypeLabel(row.deviationTypeCode)}</td>
                                                <td className="truncate px-2 py-1 text-gray-800">{getProcessLabel(row.deviationProcessCode)}</td>
                                                <td className="truncate px-2 py-1 text-gray-800" title={row.description}>{row.description}</td>
                                            </tr>
                                        );
                                    })}
                                </Fragment>
                            ))
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

export default SupplierReport;
