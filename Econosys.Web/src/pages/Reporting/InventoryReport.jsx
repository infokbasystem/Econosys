import React, { useEffect, useRef, useState } from 'react';
import { NavLink } from 'react-router-dom'
import Skeleton from 'react-loading-skeleton'
import 'react-loading-skeleton/dist/skeleton.css'
import ExcelJS from 'exceljs';

import LabeledReactSelect from '../../components/LabeledReactSelect';
import apiClient from '../../config/apiClient';

const InventoryReport = () => {
    const [loading, setLoading] = useState(false);
    const [showSkeleton, setShowSkeleton] = useState(false);
    const [initialLoadCompleted, setInitialLoadCompleted] = useState(false);
    const skeletonTimerRef = useRef(null);
    const [loadingWarehouses, setLoadingWarehouses] = useState(false);
    const [calcDate, setCalcDate] = useState('');
    const [reportTotalValue, setReportTotalValue] = useState(0);


    const handleCalcDateChange = (value) => {
        setCalcDate(value);
        // buildAndExecuteQuery(value, toDate, itemNumber);
    };


    const [filters, setFilters] = useState({
        warehouse: 0,
        date: ''
    });

    const [inventoryData, setInventoryData] = useState([]);

    const [warehouseOptions, setWarehouseOptions] = useState([
        { id: 0, name: 'Visa alla' },
    ]);

    useEffect(() => {
        const controller = new AbortController();
        const signal = controller.signal;

        const loadWarehouses = async () => {
            setLoadingWarehouses(true);

            try {
                const pageSize = 200;
                let pageNumber = 1;
                let hasNextPage = true;
                const fetchedWarehouses = [];

                while (hasNextPage) {
                    const response = await apiClient.post('/inventories/search', {
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
                    }, { signal });

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

                setWarehouseOptions([
                    { id: 0, name: 'Visa alla' },
                    ...uniqueWarehouses,
                ]);
            } catch (error) {
                if (error.code === 'ERR_CANCELED') return;
                console.error('Failed to load warehouses:', error);
            } finally {
                setLoadingWarehouses(false);
            }
        };

        loadWarehouses();
        return () => controller.abort();
    }, []);

    useEffect(() => {
        const controller = new AbortController();

        const run = async () => {
            await handleUpdate(controller.signal);

            if (!controller.signal.aborted) {
                setInitialLoadCompleted(true);
            }
        };

        run();
        return () => controller.abort();
    }, [filters.warehouse, calcDate]);

    const handleFilterChange = (field, value) => {
        setFilters(prev => ({
            ...prev,
            [field]: value
        }));
    };

    const handleUpdate = async (signalOrEvent = null) => {
        setLoading(true);
        setInventoryData([]);
        setReportTotalValue(0);
        skeletonTimerRef.current = setTimeout(() => setShowSkeleton(true), 200);

        const isAbortSignal =
            signalOrEvent &&
            typeof signalOrEvent === 'object' &&
            typeof signalOrEvent.addEventListener === 'function' &&
            typeof signalOrEvent.aborted === 'boolean';

        const signal = isAbortSignal ? signalOrEvent : null;

        try {
            const payload = {};

            if (filters.warehouse > 0) {
                payload.inventoryIds = [filters.warehouse];
            }

            if (calcDate) {
                payload.calculationDate = calcDate;
            }

            const response = await apiClient.post('/reporting/inventory', payload, signal ? { signal } : {});
            const data = response?.data ?? {};

            setInventoryData(data.rows ?? []);
            setReportTotalValue(Number(data.currentInventoryValue) || 0);
        } catch (error) {
            if (error.code === 'ERR_CANCELED') return;
            console.error('Failed to load inventory report:', error);
            setInventoryData([]);
            setReportTotalValue(0);
        } finally {
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
        ]));

        const workbook = new ExcelJS.Workbook();
        const worksheet = workbook.addWorksheet('Lagerrapport');
        worksheet.addRow(header);
        dataRows.forEach((row) => worksheet.addRow(row));

        const tableHeaderRow = 1;
        const tableLastRow = dataRows.length + 1;
        const rightAlignedColumns = new Set([6, 7, 8, 9, 10]);

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

        const dateSuffix = new Date().toISOString().slice(0, 10);
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

    const totalPallets = inventoryData.reduce((sum, item) => sum + (Number(item.currentNrOfPallets) || 0), 0);

    return (
        <div className="flex flex-col h-full p-2">
            <div className='ml-5 text-sm text-gray-500'>Largerrapport</div>
            <div className={`flex justify-between items-center mt-3 ml-5 ${loading ? 'opacity-60 pointer-events-none' : ''}`}>
                <div className='flex items-center'>
                    <div className="w-90">
                        <LabeledReactSelect
                            label="Lager"
                            labelWidth="w-10"
                            name="warehouse"
                            value={filters.warehouse}
                            items={warehouseOptions}
                            onChange={(selected) => handleFilterChange('warehouse', Number(selected) || 0)}
                            isDisabled={loadingWarehouses || loading}
                        />
                    </div>
                    <label className="ml-10 mr-5 text-xs text-gray-700">Beräkningsdatum</label>
                    <input
                        type="date"
                        value={calcDate}
                        onChange={(e) => handleCalcDateChange(e.target.value)}
                        disabled={loading}
                        className="w-30 text-xs px-2 py-1 border border-gray-300 bg-white rounded-sm focus:outline-none focus:ring-1 focus:ring-green-500"
                    />
                    <button
                        type='button'
                        onClick={() => handleUpdate()}
                        disabled={loading}
                        className={`shadow-md/30 ml-10 w-30 text-center text-xs text-white p-[5px] ${loading ? 'bg-lime-900 cursor-not-allowed' : 'bg-lime-700 hover:bg-lime-900'}`}>
                        {loading ? 'Uppdaterar...' : 'Uppdatera'}
                    </button>
                    {/* <button
                        type='button'
                        className="W-23 ml-20 shadow-md/30 text-xs text-gray bg-amber-200 hover:bg-amber-300 px-4 py-[5px]">
                        Skriv ut
                    </button> */}
                    <button
                        type='button'
                        onClick={handleExport}
                        disabled={loading || inventoryData.length === 0}
                        className="W-23 ml-20 shadow-md/30 text-xs text-gray bg-amber-200 hover:bg-amber-300 px-4 py-[5px]">
                        Excel
                    </button>
                </div>
                <div className='flex items-center mr-10'>
                    {reportTotalValue > 0 && <div className='ml-10 text-xs text-gray-500'>Totalt värde: <strong>{formatNumber(reportTotalValue)}</strong></div>}
                    {totalPallets > 0 && <div className='ml-10 text-xs text-gray-500'>Antal pall: <strong>{totalPallets}</strong></div>}
                </div>
            </div>

            <div className='border-t border-gray-300 py-1 mt-4 h-full overflow-y-auto'>
                <table className="w-full table-fixed divide-y divide-gray-100">
                    <thead>
                        <tr>
                            <th className="w-[8%] px-2 py-1.5 text-left text-tiny font-medium text-gray-400 tracking-wider">Beställning</th>
                            <th className="w-[13%] px-2 py-1.5 text-left text-tiny font-medium text-gray-400 tracking-wider">Lager</th>
                            <th className="w-[13%] px-2 py-1.5 text-left text-tiny font-medium text-gray-400 tracking-wider">Kund</th>
                            <th className="w-[18%] px-2 py-1.5 text-left text-tiny font-medium text-gray-400 tracking-wider">Produkt</th>
                            <th className="w-[8%] px-2 py-1.5 text-left text-tiny font-medium text-gray-400 tracking-wider">Senast inventerad</th>
                            <th className="w-[7%] px-2 py-1.5 text-right text-tiny font-medium text-gray-400 tracking-wider">Prod. upplaga</th>
                            <th className="w-[7%] px-2 py-1.5 text-right text-tiny font-medium text-gray-400 tracking-wider">Lagernivå</th>
                            <th className="w-[7%] px-2 py-1.5 text-right text-tiny font-medium text-gray-400 tracking-wider">Antal pall</th>
                            <th className="w-[7%] px-2 py-1.5 text-right text-tiny font-medium text-gray-400 tracking-wider">Ink.värde</th>
                            <th className="w-[7%] px-2 py-1.5 text-right text-tiny font-medium text-gray-400 tracking-wider">Fsg.värde</th>
                            <th className="w-[5%] px-2 py-1.5 text-right text-tiny font-medium text-gray-400 tracking-wider">Historik</th>
                        </tr>
                    </thead>
                    <tbody className={`divide-y divide-gray-100 ${!loading && inventoryData.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {showSkeleton ? (
                            Array.from({ length: 3 }).map((_, i) => (
                                <tr key={i}>
                                    <td colSpan="11" className="px-2 py-0">
                                        <Skeleton className="h-5 m-0" />
                                    </td>
                                </tr>
                            ))
                        ) : initialLoadCompleted && !loading && inventoryData.length === 0 ? (
                            <tr>
                                <td colSpan="11" className="px-6 py-14 whitespace-nowrap text-sm text-gray-500 text-center">Inget att visa</td>
                            </tr>
                        ) : (
                            inventoryData.map((item) => (
                                <tr key={`${item.supplierOrderId}-${item.inventoryId}`} className='hover:bg-blue-50'>
                                    <td className="pl-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">
                                        <NavLink to={`/order/supplierorder/${item.supplierOrderId}`} target="_blank" rel="noopener noreferrer" className="text-blue-600 hover:text-blue-800 hover:underline">{item.supplierOrderNr}</NavLink>
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
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 text-right">
                                        <button className="text-blue-600 hover:text-blue-800 text-xs">Historik</button>
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
