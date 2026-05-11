import React, { useEffect, useMemo, useRef, useState } from 'react';
import { ArrowLeftCircle, ArrowRightCircle, ChevronDown, ChevronUp } from 'lucide-react';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import LabeledReactSelect from '../../components/LabeledReactSelect';
import apiClient from '../../config/apiClient';
import { formatDeliveryDate } from '../../helpers/dateUtils';

const PAGE_SIZE = 100;

const columns = [
    { key: 'nr', label: 'Nr', align: 'left', width: '5%' },
    { key: 'productName', label: 'Produktnamn', align: 'left', width: '16%' },
    { key: 'customerName', label: 'Kund', align: 'left', width: '13%' },
    { key: 'supplierName', label: 'Leverantör', align: 'left', width: '11%' },
    { key: 'createdAt', label: 'Skapad', align: 'left', width: '10%' },
    { key: 'tyep', label: 'Typ', align: 'center', width: '6%' },
    { key: 'deliveryDate', label: 'Lev.datum', align: 'left', width: '9%' },
    { key: 'edition', label: 'Upplaga', align: 'right', width: '7%' },
    { key: 'editionleft', label: 'Kvar att fakt', align: 'right', width: '8%' },
    { key: 'orderValueLeftSek', label: 'Värde kvar', align: 'right', width: '8%' },
    { key: 'totalTbLeftSek', label: 'TB kvar', align: 'right', width: '7%' },
];

const initialPagination = {
    pageNumber: 1,
    pageSize: PAGE_SIZE,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
};

const initialFilters = {
    sellerId: '',
    ourReferenceId: '',
    customerId: '',
    supplierId: '',
};

const toSelectItems = (values) => [{ id: '', name: 'Alla' }, ...(values ?? [])];

const NonInvoicedOrders = () => {
    const [loading, setLoading] = useState(false);
    const [showSkeleton, setShowSkeleton] = useState(false);
    const skeletonTimerRef = useRef(null);

    const [rows, setRows] = useState([]);
    const [selectedRowId, setSelectedRowId] = useState(null);
    const [totals, setTotals] = useState({ orderValueSek: 0, orderValueLeftSek: 0, totalTbSek: 0, totalTbLeftSek: 0 });
    const [filterOptions, setFilterOptions] = useState({
        sellers: [],
        ourReferences: [],
        customers: [],
        suppliers: [],
    });
    const [filters, setFilters] = useState(initialFilters);
    const [pagination, setPagination] = useState(initialPagination);
    const [sortConfig, setSortConfig] = useState({ key: 'id', direction: '' });

    const selectOptions = useMemo(() => ({
        sellerId: toSelectItems(filterOptions.sellers),
        ourReferenceId: toSelectItems(filterOptions.ourReferences),
        customerId: toSelectItems(filterOptions.customers),
        supplierId: toSelectItems(filterOptions.suppliers),
    }), [filterOptions]);

    useEffect(() => {
        const controller = new AbortController();

        const initialize = async () => {
            try {
                const response = await apiClient.get('/reporting/non-invoiced-orders/filter-options', { signal: controller.signal });
                setFilterOptions({
                    sellers: response?.data?.sellers ?? [],
                    ourReferences: response?.data?.ourReferences ?? [],
                    customers: response?.data?.customers ?? [],
                    suppliers: response?.data?.suppliers ?? [],
                });
            } catch (error) {
                if (error.code === 'ERR_CANCELED') return;
                console.error('Failed to load non-invoiced order filter options:', error);
                setFilterOptions({ sellers: [], ourReferences: [], customers: [], suppliers: [] });
            }
        };

        initialize();

        return () => controller.abort();
    }, []);

    useEffect(() => {
        const controller = new AbortController();

        const loadReport = async () => {
            setLoading(true);
            setRows([]);
            skeletonTimerRef.current = setTimeout(() => setShowSkeleton(true), 200);

            try {
                const response = await apiClient.post(
                    '/reporting/non-invoiced-orders',
                    {
                        sellerId: filters.sellerId || null,
                        ourReferenceId: filters.ourReferenceId || null,
                        customerId: filters.customerId || null,
                        supplierId: filters.supplierId || null,
                        pagination: {
                            pageNumber: pagination.pageNumber,
                            pageSize: pagination.pageSize,
                        },
                        orderBy: [{
                            field: sortConfig.key,
                            direction: sortConfig.direction,
                        }],
                    },
                    { signal: controller.signal }
                );

                const data = response?.data ?? {};
                setRows(data.items ?? []);
                setTotals({
                    orderValueSek: Number(data?.totals?.values?.orderValueSek) || 0,
                    orderValueLeftSek: Number(data?.totals?.values?.orderValueLeftSek) || 0,    
                    totalTbSek: Number(data?.totals?.values?.totalTbSek) || 0,
                    totalTbLeftSek: Number(data?.totals?.values?.totalTbLeftSek) || 0,
                });
                setPagination((prev) => ({
                    ...prev,
                    pageNumber: data.pageNumber ?? prev.pageNumber,
                    pageSize: data.pageSize ?? prev.pageSize,
                    totalCount: data.totalCount ?? 0,
                    totalPages: data.totalPages ?? 0,
                    hasPreviousPage: Boolean(data.hasPreviousPage),
                    hasNextPage: Boolean(data.hasNextPage),
                }));
            } catch (error) {
                if (error.code === 'ERR_CANCELED') return;
                console.error('Failed to load non-invoiced orders:', error);
                setRows([]);
                setTotals({ orderValueSek: 0, totalTbSek: 0 });
                setPagination((prev) => ({
                    ...prev,
                    totalCount: 0,
                    totalPages: 0,
                    hasPreviousPage: false,
                    hasNextPage: false,
                }));
            } finally {
                clearTimeout(skeletonTimerRef.current);
                setLoading(false);
                setShowSkeleton(false);
            }
        };

        loadReport();

        return () => {
            controller.abort();
            clearTimeout(skeletonTimerRef.current);
        };
    }, [filters, pagination.pageNumber, pagination.pageSize, sortConfig]);

    const handleFilterChange = (updates) => {
        setFilters((prev) => ({ ...prev, ...updates }));
        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
    };

    const handleSort = (key) => {
        setSortConfig((prev) => ({
            key,
            direction: prev.key === key && prev.direction === 'asc' ? 'desc' : 'asc',
        }));
        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
    };

    const handlePageChange = (targetPage) => {
        if (targetPage < 1) return;
        if (pagination.totalPages > 0 && targetPage > pagination.totalPages) return;
        setPagination((prev) => ({ ...prev, pageNumber: targetPage }));
    };

    const formatAmount = (value) => {
        if (value === null || value === undefined) return '';
        return new Intl.NumberFormat('sv-SE', {
            minimumFractionDigits: 0,
            maximumFractionDigits: 0,
        }).format(Number(value));
    };

    const formatPercent = (value) => {
        if (value === null || value === undefined) return '';
        return new Intl.NumberFormat('sv-SE', {
            minimumFractionDigits: 0,
            maximumFractionDigits: 0,
        }).format(Number(value));
    };

    const formatDate = (value) => {
        if (!value) return '';
        return new Date(value).toLocaleString('sv-SE', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit',
        });
    };

    const formatDateShort = (value) => {
        if (!value) return '';
        return new Date(value).toLocaleDateString('sv-SE', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
        });
    };

    return (
        <div className="flex flex-col h-full p-2">
            <div className="ml-5 text-sm text-gray-500">Ej fullt fakturerade order</div>

            <div className={`flex flex-wrap items-center gap-10 mt-3 ml-5 ${loading ? 'opacity-70 pointer-events-none' : ''}`}>
                <div className="w-52">
                    <LabeledReactSelect
                        label="Säljare"
                        labelWidth="w-10"
                        name="sellerId"
                        value={filters.sellerId}
                        items={selectOptions.sellerId}
                        onChange={(selected) => handleFilterChange({ sellerId: selected || '' })}
                        isDisabled={loading}
                    />
                </div>

                <div className="w-52">
                    <LabeledReactSelect
                        label="Vår ref."
                        labelWidth="w-10"
                        name="ourReferenceId"
                        value={filters.ourReferenceId}
                        items={selectOptions.ourReferenceId}
                        onChange={(selected) => handleFilterChange({ ourReferenceId: selected || '' })}
                        isDisabled={loading}
                    />
                </div>

                <div className="w-52">
                    <LabeledReactSelect
                        label="Kund"
                        labelWidth="w-8"
                        name="customerId"
                        value={filters.customerId}
                        items={selectOptions.customerId}
                        onChange={(selected) => handleFilterChange({ customerId: selected || '' })}
                        isDisabled={loading}
                    />
                </div>

                <div className="w-52">
                    <LabeledReactSelect
                        label="Leverantör"
                        labelWidth="w-14"
                        name="supplierId"
                        value={filters.supplierId}
                        items={selectOptions.supplierId}
                        onChange={(selected) => handleFilterChange({ supplierId: selected || '' })}
                        isDisabled={loading}
                    />
                </div>

                <div className="ml-auto flex items-center mr-4">
                    <div className="ml-auto flex items-center mr-15">
                        <div className="ml-4 text-xs text-red-900">
                            Värde kvar: <strong >{formatAmount(totals.orderValueLeftSek)}</strong>
                        </div>
                        <div className="ml-4 text-xs text-red-900">
                            TB kvar: <strong>{formatAmount(totals.totalTbLeftSek)}</strong>
                        </div>
                        <div className="ml-4 text-xs text-gray-500">
                            Rader: <strong>{pagination.totalCount}</strong>
                        </div>
                    </div>

                    <span className="mr-3 text-xs text-gray-700">
                        Sida {pagination.pageNumber} av {Math.max(1, pagination.totalPages)}
                    </span>
                    <div className="flex gap-1 mr-10">
                        <button
                            type="button"
                            onClick={() => handlePageChange(pagination.pageNumber - 1)}
                            disabled={loading || !pagination.hasPreviousPage}
                            className="disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                            <ArrowLeftCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                        </button>
                        <button
                            type="button"
                            onClick={() => handlePageChange(pagination.pageNumber + 1)}
                            disabled={loading || !pagination.hasNextPage}
                            className="disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                            <ArrowRightCircle className="h-5 w-5 text-red-400 hover:text-red-600" />
                        </button>
                    </div>
                </div>
            </div>

            <div className="border-t border-gray-300 py-1 mt-2 flex-1 overflow-auto">
                <table className="table-fixed w-full border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
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
                                    onClick={() => handleSort(col.key)}
                                    className={`px-2 py-1.5 text-[10px] font-medium text-gray-500 whitespace-nowrap cursor-pointer select-none hover:text-gray-700 ${col.align === 'right' ? 'text-right' : col.align === 'center' ? 'text-center' : 'text-left'}`}
                                >
                                    <span className="inline-flex items-center gap-0.5">
                                        {col.label}
                                        {sortConfig.key === col.key
                                            ? sortConfig.direction === 'asc'
                                                ? <ChevronUp className="h-3 w-3" />
                                                : <ChevronDown className="h-3 w-3" />
                                            : <ChevronUp className="h-3 w-3 opacity-0" />}
                                    </span>
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody className={`divide-y divide-gray-100 ${!loading && rows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {showSkeleton ? (
                            Array.from({ length: 12 }).map((_, index) => (
                                <tr key={index}>
                                    {columns.map((col) => (
                                        <td key={col.key} className="px-2 py-1">
                                            <Skeleton height={14} />
                                        </td>
                                    ))}
                                </tr>
                            ))
                        ) : rows.length === 0 ? (
                            <tr>
                                <td colSpan={columns.length} className="px-4 py-6 text-center text-gray-400 text-xs">
                                    Inga ej fakturerade ordererkännanden hittades.
                                </td>
                            </tr>
                        ) : (
                            rows.map((row) => (
                                <tr
                                    key={row.id}
                                    onClick={() => setSelectedRowId(row.id === selectedRowId ? null : row.id)}
                                    className={`cursor-pointer ${row.id === selectedRowId ? 'bg-amber-100' : 'hover:bg-amber-50'}`}
                                >
                                    <td className="px-2 py-1 text-gray-800">{row.customerOrderNr}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.productName}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.customerName}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.supplierName}</td>
                                    <td className="px-2 py-1 text-gray-800 whitespace-nowrap">{formatDate(row.createdAt)}</td>
                                    <td className="px-2 py-1 text-center text-gray-800">{row.isInventory ? 'Lager' : 'Direkt' }</td>
                                    <td className="px-2 py-1 text-gray-800 whitespace-nowrap">{formatDeliveryDate(row.deliveryDate, row.deliveryDateWeekMode)}</td>
                                    <td className="px-2 py-1 text-right text-gray-800">{formatAmount(row.edition)}</td>
                                    <td className="px-2 py-1 text-right text-gray-800">{formatAmount(row.editionLeft)}</td>
                                    <td className="px-2 py-1 text-right text-gray-800">{formatAmount(row.orderValueLeftSek)}</td>
                                    <td className="px-2 py-1 text-right text-gray-800">{formatPercent(row.totalTbLeftSek)}</td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

export default NonInvoicedOrders;