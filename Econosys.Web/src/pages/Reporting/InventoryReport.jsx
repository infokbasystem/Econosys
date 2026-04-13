import React, { useEffect, useState } from 'react';
import { NavLink } from 'react-router-dom'
import Skeleton from 'react-loading-skeleton'

import LabeledReactSelect from '../../components/LabeledReactSelect';
import apiClient from '../../config/apiClient';

const InventoryReport = () => {
    const [loading, setLoading] = useState(false);
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
                    });

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
                console.error('Failed to load warehouses:', error);
            } finally {
                setLoadingWarehouses(false);
            }
        };

        loadWarehouses();
    }, []);

    useEffect(() => {
        handleUpdate();
    }, [filters.warehouse, calcDate]);

    const handleFilterChange = (field, value) => {
        setFilters(prev => ({
            ...prev,
            [field]: value
        }));
    };

    const handleUpdate = async () => {
        setLoading(true);

        try {
            const payload = {};

            if (filters.warehouse > 0) {
                payload.inventoryIds = [filters.warehouse];
            }

            if (calcDate) {
                payload.calculationDate = calcDate;
            }

            const response = await apiClient.post('/reporting/inventory', payload);
            const data = response?.data ?? {};

            setInventoryData(data.rows ?? []);
            setReportTotalValue(Number(data.currentInventoryValue) || 0);
        } catch (error) {
            console.error('Failed to load inventory report:', error);
            setInventoryData([]);
            setReportTotalValue(0);
        } finally {
            setLoading(false);
        }
    };

    const handleExport = () => {
        console.log('Exporting to Excel');
    };

    const handlePrint = () => {
        // Handle print
        console.log('Printing report');
    };

    const formatNumber = (num) => {
        return new Intl.NumberFormat('sv-SE', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(num);
    };

    const totalPallets = inventoryData.reduce((sum, item) => sum + (Number(item.currentNrOfPallets) || 0), 0);

    return (
        <div className="flex flex-col h-full p-2">
            <div className='ml-5 text-sm text-gray-500'>Largerrapport</div>
            <div className='flex justify-between items-center mt-3 ml-5'>
                <div className='flex items-center'>
                    <div className="w-90">
                        <LabeledReactSelect
                            label="Lager"
                            labelWidth="w-10"
                            name="warehouse"
                            value={filters.warehouse}
                            items={warehouseOptions}
                            onChange={(selected) => handleFilterChange('warehouse', Number(selected) || 0)}
                            isDisabled={loadingWarehouses}
                        />
                    </div>
                    <label className="ml-10 mr-5 text-xs text-gray-700">Beräkningsdatum</label>
                    <input
                        type="date"
                        value={calcDate}
                        onChange={(e) => handleCalcDateChange(e.target.value)}
                        className="w-30 text-xs px-2 py-1 border border-gray-300 bg-white rounded-sm focus:outline-none focus:ring-1 focus:ring-green-500"
                    />
                    <button
                        type='button'
                        onClick={handleUpdate}
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
                        className="W-23 ml-20 shadow-md/30 text-xs text-gray bg-amber-200 hover:bg-amber-300 px-4 py-[5px]">
                        Excel
                    </button>
                </div>
                <div className='flex items-center mr-10'>
                    {reportTotalValue > 0 && <div className='ml-10 text-xs text-gray-500'>Totalt värde: <strong>{formatNumber(reportTotalValue)}</strong></div>}
                    {totalPallets > 0 && <div className='ml-10 text-xs text-gray-500'>Antal pall: <strong>{totalPallets}</strong></div>}
                </div>
            </div>

            <div className='border-t border-gray-300 rounded-sm py-1 mt-4 h-full overflow-y-auto'>
                <table className="min-w-full divide-y divide-gray-100">
                    <thead>
                        <tr>
                            <th className="px-2 py-1.5 text-left text-tiny font-medium text-gray-400 uppercase tracking-wider">Beställning</th>
                            <th className="px-2 py-1.5 text-left text-tiny font-medium text-gray-400 uppercase tracking-wider">Kund</th>
                            <th className="px-2 py-1.5 text-left text-tiny font-medium text-gray-400 uppercase tracking-wider">Produkt</th>
                            <th className="px-2 py-1.5 text-left text-tiny font-medium text-gray-400 uppercase tracking-wider">Senaste inventerad</th>
                            <th className="px-2 py-1.5 text-right text-tiny font-medium text-gray-400 uppercase tracking-wider">Upplaga</th>
                            <th className="px-2 py-1.5 text-right text-tiny font-medium text-gray-400 uppercase tracking-wider">Lagernivå</th>
                            <th className="px-2 py-1.5 text-right text-tiny font-medium text-gray-400 uppercase tracking-wider">Antal pall</th>
                            <th className="px-2 py-1.5 text-right text-tiny font-medium text-gray-400 uppercase tracking-wider">Ink.värde</th>
                            <th className="px-2 py-1.5 text-right text-tiny font-medium text-gray-400 uppercase tracking-wider">Fsg.värde</th>
                            <th className="px-2 py-1.5 text-right text-tiny font-medium text-gray-400 uppercase tracking-wider w-50">Historik</th>
                        </tr>
                    </thead>
                    <tbody className={`divide-y divide-gray-100 ${!loading && inventoryData.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {loading ? (
                            Array.from({ length: 3 }).map((_, i) => (
                                <tr key={i}>
                                    <td colSpan="10" className="px-2 py-1">
                                        <Skeleton className="h-5 m-0" />
                                    </td>
                                </tr>
                            ))
                        ) : inventoryData.length === 0 ? (
                            <tr>
                                <td colSpan="11" className="px-6 py-14 whitespace-nowrap text-sm text-gray-500 text-center">Inget att visa</td>
                            </tr>
                        ) : (
                            inventoryData.map((item) => (
                                <tr key={`${item.supplierOrderId}-${item.inventoryId}`} className='hover:bg-blue-50'>
                                    <td className="pl-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800">
                                        <NavLink to={`/order/supplierorder/${item.supplierOrderId}`} target="_blank" rel="noopener noreferrer" className="text-blue-600 hover:text-blue-800 hover:underline">{item.supplierOrderId}</NavLink>
                                    </td>
                                    <td className="px-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800">{item.customerName}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800">{item.productName}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800">{item.lastInventoryDate ? new Date(item.lastInventoryDate).toLocaleString('sv-SE') : '-'}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800 text-right">{Number(item.nrOfItems || 0).toLocaleString('sv-SE')}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800 text-right">{Number(item.currentInventoryLevel || 0).toLocaleString('sv-SE')}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800 text-right">{item.currentNrOfPallets ?? 0}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800 text-right">-</td>
                                    <td className="px-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800 text-right">{formatNumber(Number(item.totalSalesValue) || 0)}</td>
                                    <td className="px-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800 text-right">
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
