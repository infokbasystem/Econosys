import React, { useEffect, useRef, useState } from 'react';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';
import ExcelJS from 'exceljs';

import apiClient from '../../config/apiClient';
import LabeledReactSelect from '../../components/LabeledReactSelect';
import { getSwedishTodayDateString } from '../../helpers/dateUtils';

const SlowMoversReport = () => {
    const [loading, setLoading] = useState(false);
    const [showSkeleton, setShowSkeleton] = useState(false);
    const [initialLoadCompleted, setInitialLoadCompleted] = useState(false);
    const [rows, setRows] = useState([]);
    const [loadingInventories, setLoadingInventories] = useState(false);
    const [inventoryOptions, setInventoryOptions] = useState([
        { id: 0, name: 'Visa alla' },
    ]);
    const [filters, setFilters] = useState({
        inventory: 0,
    });
    const skeletonTimerRef = useRef(null);
    const activeRequestIdRef = useRef(0);

    useEffect(() => {
        const controller = new AbortController();
        const signal = controller.signal;

        const loadInventories = async () => {
            setLoadingInventories(true);

            try {
                const pageSize = 200;
                let pageNumber = 1;
                let hasNextPage = true;
                const fetchedInventories = [];

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

                    fetchedInventories.push(
                        ...items.map((item) => ({
                            id: item.id,
                            name: item.name,
                        }))
                    );

                    hasNextPage = Boolean(data?.hasNextPage);
                    pageNumber += 1;
                }

                const uniqueInventories = Array.from(
                    new Map(fetchedInventories.map((item) => [item.id, item])).values()
                ).sort((a, b) => a.name.localeCompare(b.name, 'sv-SE'));

                setInventoryOptions([
                    { id: 0, name: 'Visa alla' },
                    ...uniqueInventories,
                ]);
            } catch (error) {
                if (error.code === 'ERR_CANCELED') return;
                console.error('Failed to load inventories:', error);
            } finally {
                setLoadingInventories(false);
            }
        };

        loadInventories();
        return () => controller.abort();
    }, []);

    useEffect(() => {
        const controller = new AbortController();

        const run = async () => {
            await loadSlowMovers(controller.signal, filters.inventory);

            if (!controller.signal.aborted) {
                setInitialLoadCompleted(true);
            }
        };

        run();

        return () => {
            controller.abort();
        };
    }, [filters.inventory]);

    const loadSlowMovers = async (signal = null, selectedInventoryId = null) => {
        const requestId = activeRequestIdRef.current + 1;
        activeRequestIdRef.current = requestId;
        const inventoryId = selectedInventoryId ?? filters.inventory;

        setLoading(true);
        if (rows.length === 0) {
            skeletonTimerRef.current = setTimeout(() => setShowSkeleton(true), 200);
        }

        try {
            const requestConfig = {
                ...(signal ? { signal } : {}),
                params: {
                    ...(inventoryId > 0 ? { inventoryId } : {}),
                },
            };

            const response = await apiClient.get('/reporting/slowmovers', requestConfig);
            if (requestId !== activeRequestIdRef.current) return;

            const data = response?.data ?? {};
            setRows(data.rows ?? []);
        } catch (error) {
            if (error.code === 'ERR_CANCELED') return;
            if (requestId !== activeRequestIdRef.current) return;
            console.error('Failed to load slow movers report:', error);
        } finally {
            if (requestId !== activeRequestIdRef.current) return;
            clearTimeout(skeletonTimerRef.current);
            setLoading(false);
            setShowSkeleton(false);
        }
    };

    const handleFilterChange = (field, value) => {
        setFilters((prev) => ({
            ...prev,
            [field]: value,
        }));
    };

    const formatDate = (value) => {
        if (!value) {
            return '';
        }

        return new Date(value).toLocaleDateString('sv-SE');
    };

    const handleExport = async () => {
        if (!rows.length) return;

        const header = [
            'Customerorder nr',
            'Customer',
            'Product',
            'Inventory name',
            'Ordered delivery time',
            'Storage deadline',
            'Nr of days over storage deadline',
        ];

        const dataRows = rows.map((row) => ([
            row.customerOrderNr ?? '',
            row.customer ?? '',
            row.product ?? '',
            row.inventoryName ?? '',
            row.orderedDeliveryTime ? formatDate(row.orderedDeliveryTime) : '',
            row.storageDeadline ? formatDate(row.storageDeadline) : '',
            Number(row.daysOverStorageDeadline || 0),
        ]));

        const workbook = new ExcelJS.Workbook();
        const worksheet = workbook.addWorksheet('Hyllvarmare');
        worksheet.addRow(header);
        dataRows.forEach((dataRow) => worksheet.addRow(dataRow));

        const tableHeaderRow = 1;
        const tableLastRow = dataRows.length + 1;

        for (let col = 1; col <= header.length; col += 1) {
            let maxLength = String(header[col - 1] ?? '').length;

            for (let row = tableHeaderRow + 1; row <= tableLastRow; row += 1) {
                const cellValue = worksheet.getRow(row).getCell(col).value;
                const cellText = cellValue === null || cellValue === undefined ? '' : String(cellValue);
                if (cellText.length > maxLength) {
                    maxLength = cellText.length;
                }
            }

            worksheet.getColumn(col).width = Math.min(45, Math.max(8, maxLength + 1));
        }

        worksheet.eachRow((sheetRow) => {
            sheetRow.eachCell((cell) => {
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
                cell.alignment = {
                    ...(cell.alignment || {}),
                    horizontal: col === 8 ? 'right' : 'left',
                };
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
        link.download = `hyllvarmare-${dateSuffix}.xlsx`;
        link.click();
        URL.revokeObjectURL(url);
    };

    return (
        <div className="flex flex-col h-full p-2">
            <div className="ml-5 text-sm text-gray-500">Hyllvarmare</div>

            <div className={`flex justify-between items-center mt-3 ml-5 ${loading ? 'opacity-60 pointer-events-none' : ''}`}>
                <div className="flex items-center">
                    <div className="w-90 mr-6">
                        <LabeledReactSelect
                            label="Lager"
                            labelWidth="w-10"
                            name="inventory"
                            value={filters.inventory}
                            items={inventoryOptions}
                            onChange={(selected) => handleFilterChange('inventory', Number(selected) || 0)}
                            isDisabled={loadingInventories || loading}
                        />
                    </div>
                    <button
                        type="button"
                        onClick={() => loadSlowMovers(null, filters.inventory)}
                        disabled={loading}
                        className={`ml-10 shadow-md/30 w-30 text-center text-xs text-white p-[5px] ${loading ? 'bg-lime-900 cursor-not-allowed' : 'bg-lime-700 hover:bg-lime-900'}`}>
                        {loading ? 'Uppdaterar...' : 'Uppdatera'}
                    </button>
                    <button
                        type="button"
                        onClick={handleExport}
                        disabled={loading || rows.length === 0}
                        className="W-23 ml-20 shadow-md/30 text-xs text-gray bg-amber-200 hover:bg-amber-300 px-4 py-[5px]">
                        Excel
                    </button>
                </div>
                <div className="flex items-center mr-10">
                    {rows.length > 0 && (
                        <div className="ml-10 text-xs text-gray-500">
                            Antal rader: <strong>{rows.length}</strong>
                        </div>
                    )}
                </div>
            </div>

            <div className="border-t border-gray-300 rounded-sm py-1 mt-4 h-full overflow-y-auto">
                <table className="w-full table-fixed divide-y divide-gray-100">
                    <thead>
                        <tr>
                            <th className="w-[7%] px-2 py-1.5 text-left text-tiny font-medium text-gray-400 tracking-wider">Ordererkännande</th>
                            <th className="w-[14%] px-2 py-1.5 text-left text-tiny font-medium text-gray-400 tracking-wider">Kund</th>
                            <th className="w-[20%] px-2 py-1.5 text-left text-tiny font-medium text-gray-400 tracking-wider">Produkt</th>
                            <th className="w-[12%] px-2 py-1.5 text-left text-tiny font-medium text-gray-400 tracking-wider">Lager</th>
                            <th className="w-[12%] px-2 py-1.5 text-left text-tiny font-medium text-gray-400 tracking-wider">Lev.datum</th>
                            <th className="w-[12%] px-2 py-1.5 text-left text-tiny font-medium text-gray-400 tracking-wider">Kalk.datum</th>
                            <th className="w-[8%] px-2 py-1.5 text-right text-tiny font-medium text-gray-400 tracking-wider">Dagar över</th>
                        </tr>
                    </thead>
                    <tbody className={`divide-y divide-gray-100 ${!loading && rows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {showSkeleton ? (
                            Array.from({ length: 3 }).map((_, index) => (
                                <tr key={index}>
                                    <td colSpan="8" className="px-2 py-0">
                                        <Skeleton className="h-5 m-0" />
                                    </td>
                                </tr>
                            ))
                        ) : initialLoadCompleted && !loading && rows.length === 0 ? (
                            <tr>
                                <td colSpan="8" className="px-6 py-14 whitespace-nowrap text-sm text-gray-500 text-center">Inget att visa</td>
                            </tr>
                        ) : (
                            rows.map((row) => (
                                <tr key={`${row.deliveryNr}-${row.customerOrderNr}`} className="hover:bg-blue-50">
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{row.customerOrderNr}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{row.customer}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{row.product}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{row.inventoryName}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{formatDate(row.orderedDeliveryTime)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] truncate text-xs text-gray-800">{formatDate(row.storageDeadline)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] text-xs text-gray-800 text-right">{Number(row.daysOverStorageDeadline || 0).toLocaleString('sv-SE')}</td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

export default SlowMoversReport;
