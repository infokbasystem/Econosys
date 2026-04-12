import React, { useEffect, useState } from 'react';
import { NavLink } from 'react-router-dom'
import Skeleton from 'react-loading-skeleton'

import LabeledReactSelect from '../../components/LabeledReactSelect';
import LabeledInput from '../../components/LabeledInput';
import apiClient from '../../config/apiClient';

const InventoryReport = () => {
    const [loading, setLoading] = useState(false);
    const [loadingWarehouses, setLoadingWarehouses] = useState(false);
    const [calcDate, setCalcDate] = useState('');


    const handleCalcDateChange = (value) => {
        setCalcDate(value);
        // buildAndExecuteQuery(value, toDate, itemNumber);
    };


    const [filters, setFilters] = useState({
        warehouse: 0,
        date: '',
        currency: 'SEK'
    });

    const [inventoryData, setInventoryData] = useState([
        {
            id: 39279,
            order: 'Salico AB',
            product: '40300, Lida Minsit Grön',
            edition: 20000,
            lastInventoried: '2026-01-31 23:59',
            stockLevel: 17280,
            pallets: 36,
            purchaseValue: 47430.49,
            fsgValue: 80144.64,
        },
        {
            id: 40679,
            order: 'Econopack AB',
            product: '41490, Alberber Tomat NY',
            edition: 864000,
            lastInventoried: '2026-02-28 23:59',
            stockLevel: 207037,
            pallets: 0,
            purchaseValue: 49688.88,
            fsgValue: 0.00,
        },
        {
            id: 41071,
            order: 'Jasberg Rapid AB',
            product: 'K1 KAPSEL RAPID',
            edition: 150000,
            lastInventoried: '2026-02-28 23:59',
            stockLevel: 45000,
            pallets: 8,
            purchaseValue: 34650.00,
            fsgValue: 43200.00,
        },
        {
            id: 41278,
            order: 'Foodhills AB',
            product: '12889 - 6 kg',
            edition: 28000,
            lastInventoried: '2026-01-31 23:59',
            stockLevel: 26880,
            pallets: 48,
            purchaseValue: 115127.04,
            fsgValue: 149748.48,
        },
        {
            id: 41290,
            order: 'Mizor AB',
            product: '13285 - AIN - SM - E/C',
            edition: 3120,
            lastInventoried: '2026-01-31 23:59',
            stockLevel: 520,
            pallets: 1,
            purchaseValue: 7540.88,
            fsgValue: 12860.12,
        },
    ]);

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

    const handleFilterChange = (field, value) => {
        setFilters(prev => ({
            ...prev,
            [field]: value
        }));
    };

    const handleUpdate = () => {
        console.log('Updating with filters:', filters);
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

    const totalStockValue = inventoryData.reduce((sum, item) => sum + item.purchaseValue, 0);
    const totalPallets = inventoryData.reduce((sum, item) => sum + item.pallets, 0);

    return (
        <div className="flex flex-col h-full p-2">
            <div className='ml-5 text-sm text-gray-500'>Largerrapport</div>
            <div className='flex justify-between items-center mt-3 ml-5'>
                <div className='flex items-center'>
                    <div className="w-70">
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
                        className={`shadow-md/30 ml-10 w-30 text-center text-xs text-white bg-lime-700 hover:bg-lime-900 p-[5px]`}>
                        Uppdatera
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
                    {totalStockValue > 0 && <div className='ml-10 text-xs text-gray-500'>Totalt värde: <strong>{formatNumber(totalStockValue)}</strong></div>}
                    {totalPallets > 0 && <div className='ml-10 text-xs text-gray-500'>Antal pall: <strong>{totalPallets}</strong></div>}
                </div>
            </div>

            <div className='border-t border-gray-300 rounded-sm py-1 mt-4 h-full overflow-y-auto'>
                {loading || !inventoryData ? (
                    <div className='skeleton-content'><Skeleton count={3} className="h-5 m-0" /></div>
                ) :
                    (
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
                            <tbody className="bg-white divide-y divide-gray-100">
                                {!inventoryData || inventoryData.length === 0 ? (
                                    <tr>
                                        <td colSpan="11" className="px-6 py-14 whitespace-nowrap text-sm text-gray-500 text-center">Inget att visa</td>
                                    </tr>
                                ) : (
                                    inventoryData.map((item) => {
                                        return (
                                            <tr key={item.id} className='hover:bg-blue-50'>
                                                <td className="pl-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800">
                                                    <NavLink to={`/order/supplierorder/${item.id}`} target="_blank" rel="noopener noreferrer" className="text-blue-600 hover:text-blue-800 hover:underline">{item.id}</NavLink>
                                                </td>
                                                <td className="px-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800">{item.customerName}</td>
                                                <td className="px-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800">{item.product}</td>
                                                <td className="px-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800">{item.lastInventoried}</td>
                                                <td className="px-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800 text-right">{item.edition.toLocaleString('sv-SE')}</td>
                                                <td className="px-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800 text-right">{item.stockLevel.toLocaleString('sv-SE')}</td>
                                                <td className="px-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800 text-right">{item.pallets}</td>
                                                <td className="px-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800 text-right">{formatNumber(item.purchaseValue)}</td>
                                                <td className="px-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800 text-right">{formatNumber(item.fsgValue)}</td>
                                                <td className="px-2 pt-[6px] pb-[4px] whitespace-nowrap text-xs text-gray-800 text-right">
                                                    <button className="text-blue-600 hover:text-blue-800 text-xs">Historik</button>
                                                </td>
                                            </tr>
                                        )
                                    }
                                    )
                                )}
                            </tbody>
                        </table>
                    )
                }
            </div>

        </div>
    );
};

export default InventoryReport;
