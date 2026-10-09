import React, { useEffect, useRef, useState } from 'react';
import { NavLink } from 'react-router-dom'
import Skeleton from 'react-loading-skeleton'
import 'react-loading-skeleton/dist/skeleton.css'
import ExcelJS from 'exceljs';

import LabeledReactSelect from '../../../components/LabeledReactSelect';
import LabeledDatePicker from '../../../components/LabeledDatePicker';
import apiClient from '../../../config/apiClient';
import { getSwedishTodayDateString } from '../../../helpers/dateUtils';
import { getSharedRequest } from '../../../helpers/sharedRequest';

const inventoryColumns = [
    { key: 'supplierOrderNr', label: 'Beställning', align: 'left', width: '7%' },
    { key: 'inventoryName', label: 'Lager', align: 'left', width: '10%' },
    { key: 'customerName', label: 'Kund', align: 'left', width: '11%' },
    { key: 'productName', label: 'Produkt', align: 'left', width: '15%' },
    { key: 'lastInventoryDate', label: 'Senast inventerad', align: 'left', width: '9%' },
    { key: 'producedNrOfItems', label: 'Prod. upplaga', align: 'right', width: '6%' },
    { key: 'currentInventoryNrOfItems', label: 'Lagernivå', align: 'right', width: '6%' },
    { key: 'currentInventoryNrOfPallets', label: 'Antal pall', align: 'right', width: '6%' },
    { key: 'totalStockValue', label: 'Ink.värde', align: 'right', width: '6%' },
    { key: 'totalSalesValue', label: 'Fsg.värde', align: 'right', width: '6%' },
    { key: 'totalPalletPurchaseValue', label: 'Pall ink.värde', align: 'right', width: '6%' },
    { key: 'totalPalletSalesValue', label: 'Pall fsg.värde', align: 'right', width: '6%' },
    { key: 'history', label: 'Historik', align: 'right', width: '6%' },
];

const INVENTORY_REPORT_CACHE_KEY = 'inventory-report-state';

const readInventoryReportCache = () => {
    try {
        const cachedState = JSON.parse(sessionStorage.getItem(INVENTORY_REPORT_CACHE_KEY));
        return Array.isArray(cachedState?.inventoryData) && cachedState?.filters
            ? cachedState
            : null;
    } catch {
        return null;
    }
};

const InventoryReport = () => {
    const cachedState = readInventoryReportCache();
    const [loading, setLoading] = useState(false);
    const [showSkeleton, setShowSkeleton] = useState(false);
    const [initialLoadCompleted, setInitialLoadCompleted] = useState(Boolean(cachedState));
    const skeletonTimerRef = useRef(null);
    const shouldPreserveCachedDataRef = useRef(Boolean(cachedState));
    const [loadingWarehouses, setLoadingWarehouses] = useState(false);
    const [calcDate, setCalcDate] = useState(cachedState?.calcDate ?? '');
    const [reportTotalValue, setReportTotalValue] = useState(cachedState?.reportTotalValue ?? 0);
    const [reportPalletPurchaseValue, setReportPalletPurchaseValue] = useState(cachedState?.reportPalletPurchaseValue ?? 0);
    const [reportPalletSalesValue, setReportPalletSalesValue] = useState(cachedState?.reportPalletSalesValue ?? 0);
    const [showReportInfo, setShowReportInfo] = useState(false);

    const [filters, setFilters] = useState({
        warehouse: cachedState?.filters?.warehouse ?? 0,
        date: cachedState?.filters?.date ?? '',
    });

    const [inventoryData, setInventoryData] = useState(cachedState?.inventoryData ?? []);

    const [warehouseOptions, setWarehouseOptions] = useState([
        { id: 0, name: 'Visa alla' },
    ]);

    useEffect(() => {
        let isActive = true;

        const loadWarehouses = async () => {
            setLoadingWarehouses(true);

            try {
                const pageSize = 200;
                let pageNumber = 1;
                let hasNextPage = true;
                const fetchedWarehouses = [];

                while (hasNextPage) {
                    const requestBody = {
                        filter: {
                            conditions: [
                                {
                                    field: 'isinventory',
                                    operator: 'eq',
                                    value: true,
                                },
                            ],
                        },
                        sort: {
                            field: 'name',
                            direction: 'asc',
                        },
                        pagination: {
                            pageNumber,
                            pageSize,
                        },
                    };
                    const requestKey = `inventories:search:inventory-report:${pageNumber}:${pageSize}`;
                    const response = await getSharedRequest(requestKey, () => apiClient.post('/inventories/search', requestBody));
                    if (!isActive) return;

                    const data = response?.data;
                    const items = data?.items ?? [];

                    fetchedWarehouses.push(
                        ...items.map((item) => ({
                            id: item.id,
                            name: item.name,
                        }))
                    );

                    hasNextPage = Boolean(data?.hasNextPage);
                    pageNumber += 1;
                }

                const uniqueWarehouses = Array.from(
                    new Map(fetchedWarehouses.map((item) => [item.id, item])).values()
                ).sort((a, b) => a.name.localeCompare(b.name, 'sv-SE'));

                if (!isActive) return;
                setWarehouseOptions([
                    { id: 0, name: 'Visa alla' },
                    ...uniqueWarehouses,
                ]);
            } catch (error) {
                console.error('Failed to load warehouses:', error);
            } finally {
                if (!isActive) return;
                setLoadingWarehouses(false);
            }
        };

        void loadWarehouses();
        return () => {
            isActive = false;
        };
    }, []);

    useEffect(() => {
        let isActive = true;

        const run = async () => {
            await handleUpdate(isActive, shouldPreserveCachedDataRef.current);

            if (isActive) {
                setInitialLoadCompleted(true);
            }
        };

        void run();
        return () => {
            isActive = false;
        };
    }, [filters.warehouse, calcDate]);

    const handleFilterChange = (field, value) => {
        shouldPreserveCachedDataRef.current = false;
        setFilters(prev => ({
            ...prev,
            [field]: value
        }));
    };

    const handleUpdate = async (isActiveOrEvent = true, preserveExistingData = false) => {
        setLoading(true);
        if (!preserveExistingData) {
            setInventoryData([]);
            setReportTotalValue(0);
            setReportPalletPurchaseValue(0);
            setReportPalletSalesValue(0);
            skeletonTimerRef.current = setTimeout(() => setShowSkeleton(true), 200);
        }

        const isActive = typeof isActiveOrEvent === 'boolean' ? isActiveOrEvent : true;

        try {
            const payload = {};

            if (filters.warehouse > 0) {
                payload.inventoryIds = [filters.warehouse];
            }

            if (calcDate) {
                payload.calculationDate = calcDate;
            }

            const requestKey = `reporting:inventory:${JSON.stringify(payload)}`;
            const response = await getSharedRequest(requestKey, () => apiClient.post('/reporting/inventory', payload));
            if (!isActive) return;
            const data = response?.data ?? {};
            const rows = data.rows ?? [];
            const currentInventoryValue = Number(data.currentInventoryValue) || 0;
            const currentPalletPurchaseValue = Number(data.currentPalletPurchaseValue) || 0;
            const currentPalletSalesValue = Number(data.currentPalletSalesValue) || 0;

            setInventoryData(rows);
            setReportTotalValue(currentInventoryValue);
            setReportPalletPurchaseValue(currentPalletPurchaseValue);
            setReportPalletSalesValue(currentPalletSalesValue);
            try {
                sessionStorage.setItem(INVENTORY_REPORT_CACHE_KEY, JSON.stringify({
                    filters,
                    calcDate,
                    inventoryData: rows,
                    reportTotalValue: currentInventoryValue,
                    reportPalletPurchaseValue: currentPalletPurchaseValue,
                    reportPalletSalesValue: currentPalletSalesValue,
                }));
            } catch (error) {
                console.error('Failed to cache inventory report:', error);
            }
        } catch (error) {
            console.error('Failed to load inventory report:', error);
            if (!isActive) return;
            if (!preserveExistingData) {
                setInventoryData([]);
                setReportTotalValue(0);
                setReportPalletPurchaseValue(0);
                setReportPalletSalesValue(0);
            }
        } finally {
            if (!isActive) return;
            clearTimeout(skeletonTimerRef.current);
            setLoading(false);
            setShowSkeleton(false);
        }
    };

    const handleExport = async () => {
        if (!inventoryData.length) return;

        const header = [
            'Bestallning',
            'Lager',
            'Kund',
            'Produkt',
            'SenastInventerad',
            'ProduceradUpplaga',
            'Lagerniva',
            'AntalPall',
            'Inkopsvarde',
            'Forsaljningsvarde',
            'PallInkopsvarde',
            'PallForsaljningsvarde',
        ];

        const dataRows = inventoryData.map((item) => ([
            item.supplierOrderNr ?? '',
            item.inventoryName ?? '',
            item.customerName ?? '',
            item.productName ?? '',
            item.lastInventoryDate ? new Date(item.lastInventoryDate).toLocaleString('sv-SE') : '',
            Number(item.producedNrOfItems || 0),
            Number(item.currentInventoryNrOfItems || 0),
            Number(item.currentInventoryNrOfPallets || 0),
            Number(item.totalStockValue || 0),
            Number(item.totalSalesValue || 0),
            Number(item.totalPalletPurchaseValue || 0),
            Number(item.totalPalletSalesValue || 0),
        ]));

        const workbook = new ExcelJS.Workbook();
        const worksheet = workbook.addWorksheet('Lagerrapport');
        worksheet.addRow(header);
        dataRows.forEach((row) => worksheet.addRow(row));

        const tableHeaderRow = 1;
        const tableLastRow = dataRows.length + 1;
        const rightAlignedColumns = new Set([6, 7, 8, 9, 10, 11, 12]);

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

        worksheet.getRow(tableHeaderRow).eachCell((cell) => {
            cell.font = {
                ...(cell.font || {}),
                size: 8,
                bold: true,
            };
        });

        for (let row = tableHeaderRow; row <= tableLastRow; row += 1) {
            for (let col = 1; col <= header.length; col += 1) {
                const cell = worksheet.getRow(row).getCell(col);
                if (rightAlignedColumns.has(col)) {
                    cell.alignment = { ...(cell.alignment || {}), horizontal: 'right' };
                } else {
                    cell.alignment = { ...(cell.alignment || {}), horizontal: 'left' };
                }
            }
        }

        worksheet.autoFilter = {
            from: { row: tableHeaderRow, column: 1 },
            to: { row: tableLastRow, column: header.length },
        };

        const dateSuffix = getSwedishTodayDateString();
        const buffer = await workbook.xlsx.writeBuffer();
        const blob = new Blob([buffer], {
            type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `lagerrapport-${dateSuffix}.xlsx`;
        link.click();
        URL.revokeObjectURL(url);
    };

    const handlePrint = () => {
        // Handle print
        console.log('Printing report');
    };

    const formatNumber = (num) => {
        return new Intl.NumberFormat('sv-SE', {
            minimumFractionDigits: 0,
            maximumFractionDigits: 0
        }).format(num);
    };

    const totalPallets = inventoryData.reduce((sum, item) => sum + (Number(item.currentInventoryNrOfPallets) || 0), 0);

    return (
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            {/* <div className="mt-2 text-sm text-gray-500">Largerrapport</div> */}
            <div className={`relative z-20 flex items-center justify-between gap-4 mt-2 pb-2 ${loading ? 'opacity-60 pointer-events-none' : ''}`}>
                <div className='flex items-center'>
                    <div className="w-70">
                        <LabeledReactSelect
                            label="Lager"
                            labelWidth="w-10"
                            name="warehouse"
                            filterStyle
                            value={filters.warehouse}
                            items={warehouseOptions}
                            onChange={(selected) => handleFilterChange('warehouse', Number(selected) || 0)}
                            isDisabled={loadingWarehouses || loading}
                        />
                    </div>
                    <div className="ml-10 w-64">
                        <LabeledDatePicker
                            label="Beräkningsdatum"
                            labelWidth="w-24"
                            inputWidth="w-40"
                            name="calcDate"
                            value={calcDate}
                            valueType="input"
                            onChange={(value) => {
                                shouldPreserveCachedDataRef.current = false;
                                setCalcDate(value || '');
                            }}
                            disabled={loading}
                            placeholder="Välj datum"
                            theme="filter"
                        />
                    </div>
                    {/* <button
                        type='button'
                        onClick={() => handleUpdate()}
                        disabled={loading}
                        className={`shadow-md/30 ml-10 w-30 text-center text-xs text-white p-[5px] ${loading ? 'bg-lime-900 cursor-not-allowed' : 'bg-lime-700 hover:bg-lime-900'}`}>
                        {loading ? 'Uppdaterar...' : 'Uppdatera'}
                    </button> */}
                    {/* <button
                        type='button'
                        className="W-23 ml-20 shadow-md/30 text-xs text-gray bg-amber-200 hover:bg-amber-300 px-4 py-[5px]">
                        Skriv ut
                    </button> */}
                    <a
                        href="#"
                        onClick={(event) => {
                            event.preventDefault();
                            if (loading || inventoryData.length === 0) return;
                            handleExport();
                        }}
                        aria-disabled={loading || inventoryData.length === 0}
                        className={`ml-20 inline-flex items-center whitespace-nowrap text-xs font-medium transition-colors ${loading || inventoryData.length === 0
                            ? 'text-gray-300 cursor-not-allowed pointer-events-none'
                            : 'text-slate-500 hover:text-slate-700'
                            }`}
                    >
                        Exportera till EXCEL
                    </a>
                    <div className="relative ml-6 inline-flex items-center">
                        {showReportInfo && (
                            <>
                                <div
                                    className="fixed inset-0 z-30"
                                    onClick={() => setShowReportInfo(false)}
                                />
                                <div
                                    className="absolute left-0 top-full z-40 mt-3 w-[480px] border border-gray-400 bg-yellow-50 px-8 py-6 text-xs text-yellow-900 shadow-sm"
                                    onClick={(event) => event.stopPropagation()}
                                >
                                    <p>Lagernivå och antal pall utgår från den senaste inventeringen före beräkningsdatumet och justeras med efterföljande leveranser till och från lager.</p>
                                    <p className="mt-2">Ink.värde beräknas som lagernivå × leverantörsorderns inköpspris × inköpsvalutakurs, justerat för enhetens multiplikator.</p>
                                    <p className="mt-2">Fsg.värde beräknas som lagernivå × försäljningspriset på den första kundordern × försäljningsvalutakurs, justerat för enhetens multiplikator.</p>
                                    <p className="mt-2">Pall ink.värde beräknas som antal pall × leverantörens pris för leverantörsorderns pallformat × inköpsvalutakurs.</p>
                                    <p className="mt-2">Pall fsg.värde beräknas som antal pall × pallens försäljningspris på den första kundordern × försäljningsvalutakurs.</p>
                                </div>
                            </>
                        )}
                        <a
                            href="#"
                            onClick={(event) => {
                                event.preventDefault();
                                setShowReportInfo((prev) => !prev);
                            }}
                            className="whitespace-nowrap text-xs font-medium text-slate-500 transition-colors hover:text-slate-700"
                        >
                            Visa info
                        </a>
                    </div>
                </div>
                <div className='flex items-center mr-10'>
                    {reportTotalValue > 0 && <div className='ml-10 text-xs text-gray-500'>Totalt värde: <strong>{formatNumber(reportTotalValue)}</strong></div>}
                    {reportPalletPurchaseValue > 0 && <div className='ml-10 text-xs text-gray-500'>Pall ink.värde: <strong>{formatNumber(reportPalletPurchaseValue)}</strong></div>}
                    {reportPalletSalesValue > 0 && <div className='ml-10 text-xs text-gray-500'>Pall fsg.värde: <strong>{formatNumber(reportPalletSalesValue)}</strong></div>}
                    {totalPallets > 0 && <div className='ml-10 text-xs text-gray-500'>Antal pall: <strong>{totalPallets}</strong></div>}
                </div>
            </div>

            <div className="border-t border-gray-300 py-1 mt-3 min-h-0 flex-1 overflow-auto">
                <table className={`table-fixed w-full border-collapse text-xs [&_thead_th]:px-2 [&_thead_th]:pt-1 [&_thead_th]:pb-2 [&_thead_th]:text-tiny [&_thead_th]:font-medium [&_thead_th]:text-gray-500 [&_tbody_>_tr]:h-6 [&_tbody_>_tr]:border-b [&_tbody_>_tr]:border-gray-100 [&_tbody_td]:overflow-hidden [&_tbody_td]:text-ellipsis [&_tbody_td]:whitespace-nowrap [&_tbody_td]:px-2 [&_tbody_td]:py-0 [&_tbody_td]:text-gray-800 ${showSkeleton ? '' : '[&_tbody_>_tr:hover]:!bg-lime-200/70'}`} style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <colgroup>
                        {inventoryColumns.map((column) => (
                            <col key={column.key} style={{ width: column.width }} />
                        ))}
                    </colgroup>
                    <thead>
                        <tr>
                            {inventoryColumns.map((column) => (
                                <th
                                    key={column.key}
                                    className={`overflow-hidden text-ellipsis whitespace-nowrap px-2 py-1.5 text-tiny font-medium text-gray-400 tracking-wider ${column.align === 'right' ? 'text-right' : 'text-left'}`}
                                >
                                    {column.label}
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody className={`divide-y divide-gray-100 ${!loading && inventoryData.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {showSkeleton ? (
                            Array.from({ length: 3 }).map((_, i) => (
                                <tr key={i} className="bg-transparent hover:!bg-transparent">
                                    <td colSpan={inventoryColumns.length} className="bg-transparent px-2 py-0">
                                        <Skeleton className="h-5 m-0" />
                                    </td>
                                </tr>
                            ))
                        ) : initialLoadCompleted && !loading && inventoryData.length === 0 ? (
                            <tr>
                                <td colSpan={inventoryColumns.length} className="px-6 py-14 whitespace-nowrap text-sm text-gray-500 text-center">Inget att visa</td>
                            </tr>
                        ) : (
                            inventoryData.map((item) => (
                                <tr key={`${item.supplierOrderId}-${item.inventoryId}`} className='hover:bg-blue-50'>
                                    <td className="pl-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">
                                        <NavLink to={`/order/supplierorders/${item.supplierOrderId}`} className="text-slate-700 hover:text-slate-900 hover:underline">{item.supplierOrderNr}</NavLink>
                                    </td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{item.inventoryName}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{item.customerName}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{item.productName}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{item.lastInventoryDate ? new Date(item.lastInventoryDate).toLocaleString('sv-SE') : '-'}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 text-right">{Number(item.producedNrOfItems || 0).toLocaleString('sv-SE')}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 text-right">{Number(item.currentInventoryNrOfItems || 0).toLocaleString('sv-SE')}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 text-right">{item.currentInventoryNrOfPallets ?? 0}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 text-right">{formatNumber(Number(item.totalStockValue) || 0)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 text-right">{formatNumber(Number(item.totalSalesValue) || 0)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 text-right">{formatNumber(Number(item.totalPalletPurchaseValue) || 0)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 text-right">{formatNumber(Number(item.totalPalletSalesValue) || 0)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 text-right">
                                        <button type="button" className="text-xs text-slate-700 hover:text-slate-900 hover:underline">Historik</button>
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

export default InventoryReport;
