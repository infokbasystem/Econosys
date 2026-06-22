import React, { useEffect, useMemo, useRef, useState } from 'react';
import { ArrowLeftCircle, ArrowRightCircle, ChevronUp, ChevronDown } from 'lucide-react';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import LabeledReactSelect from '../../components/LabeledReactSelect';
import apiClient from '../../config/apiClient';

const PAGE_SIZE = 100;

const columns = [
    { key: 'id',            label: 'Id',            align: 'left' },
    { key: 'sentAt',        label: 'Skickat',        align: 'left' },
    { key: 'fileSent',      label: 'Fil',            align: 'left' },
    { key: 'seller',        label: 'Ansv. Säljare',  align: 'left',  filterable: true },
    { key: 'ourReference',  label: 'Vår Referens',   align: 'left',  filterable: true },
    { key: 'customerName',  label: 'Kund',           align: 'left',  filterable: true },
    { key: 'supplierName',  label: 'Leverantör',     align: 'left',  filterable: true },
    { key: 'productName',   label: 'Produktnamn',    align: 'left' },
    { key: 'createdAt',     label: 'Skapad',         align: 'left' },
    { key: 'deliveryDate',  label: 'Lev. Datum',     align: 'left' },
    { key: 'edition',       label: 'Upplaga',        align: 'right' },
    { key: 'orderValueSek', label: 'Ordervärde',     align: 'right' },
    { key: 'markupPercent', label: 'Påslag',         align: 'right' },
    { key: 'totalTbSek',    label: 'TB',             align: 'right' },
];

const NonConfirmedSupplierOrders = () => {
    const [loading, setLoading] = useState(false);
    const [showSkeleton, setShowSkeleton] = useState(false);
    const skeletonTimerRef = useRef(null);

    const [allRows, setAllRows] = useState([]);
    const [pageNumber, setPageNumber] = useState(1);
    const [sortKey, setSortKey] = useState(null);
    const [sortDir, setSortDir] = useState('asc');

    const [filters, setFilters] = useState({
        seller: '',
        ourReference: '',
        customerName: '',
        supplierName: '',
    });

    useEffect(() => {
        const controller = new AbortController();
        loadReport(controller.signal);
        return () => controller.abort();
    }, []);

    const loadReport = async (signal = null) => {
        setLoading(true);
        setAllRows([]);
        skeletonTimerRef.current = setTimeout(() => setShowSkeleton(true), 200);

        try {
            const response = await apiClient.get(
                '/reporting/non-confirmed-supplier-orders',
                signal ? { signal } : {}
            );
            setAllRows(response?.data ?? []);
            setPageNumber(1);
        } catch (error) {
            if (error.code === 'ERR_CANCELED') return;
            console.error('Failed to load non-confirmed supplier orders:', error);
            setAllRows([]);
        } finally {
            clearTimeout(skeletonTimerRef.current);
            setLoading(false);
            setShowSkeleton(false);
        }
    };

    // Derive unique filter options from all loaded rows
    const filterOptions = useMemo(() => {
        const unique = (key) => {
            const values = [...new Set(allRows.map((r) => r[key]).filter(Boolean))].sort();
            return [{ id: '', name: 'Alla' }, ...values.map((v) => ({ id: v, name: v }))];
        };
        return {
            seller: unique('seller'),
            ourReference: unique('ourReference'),
            customerName: unique('customerName'),
            supplierName: unique('supplierName'),
        };
    }, [allRows]);

    const handleSort = (key) => {
        if (sortKey === key) {
            setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'));
        } else {
            setSortKey(key);
            setSortDir('asc');
        }
        setPageNumber(1);
    };

    // Apply in-memory filters
    const filteredRows = useMemo(() => {
        return allRows.filter((row) => {
            if (filters.seller && row.seller !== filters.seller) return false;
            if (filters.ourReference && row.ourReference !== filters.ourReference) return false;
            if (filters.customerName && row.customerName !== filters.customerName) return false;
            if (filters.supplierName && row.supplierName !== filters.supplierName) return false;
            return true;
        });
    }, [allRows, filters]);

    const sortedRows = useMemo(() => {
        if (!sortKey) return filteredRows;
        return [...filteredRows].sort((a, b) => {
            let av = a[sortKey];
            let bv = b[sortKey];
            if (av === null || av === undefined) av = '';
            if (bv === null || bv === undefined) bv = '';
            const cmp = typeof av === 'number' && typeof bv === 'number'
                ? av - bv
                : String(av).localeCompare(String(bv), 'sv-SE', { numeric: true });
            return sortDir === 'asc' ? cmp : -cmp;
        });
    }, [filteredRows, sortKey, sortDir]);

    const totalPages = sortedRows.length === 0 ? 0 : Math.ceil(sortedRows.length / PAGE_SIZE);
    const pagedRows = useMemo(() => {
        const start = (pageNumber - 1) * PAGE_SIZE;
        return sortedRows.slice(start, start + PAGE_SIZE);
    }, [sortedRows, pageNumber]);

    const totals = useMemo(() => {
        return sortedRows.reduce(
            (acc, row) => {
                acc.orderValueSek += Number(row.orderValueSek) || 0;
                acc.totalTbSek += Number(row.totalTbSek) || 0;
                return acc;
            },
            { orderValueSek: 0, totalTbSek: 0 }
        );
    }, [filteredRows]);

    const handleFilterChange = (updates) => {
        setFilters((prev) => ({ ...prev, ...updates }));
        setPageNumber(1);
    };

    const handlePageChange = (target) => {
        if (target < 1 || (totalPages > 0 && target > totalPages)) return;
        setPageNumber(target);
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
            minimumFractionDigits: 1,
            maximumFractionDigits: 1,
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

    const hasPreviousPage = pageNumber > 1;
    const hasNextPage = pageNumber < totalPages;

    return (
        <div className="flex flex-col h-full p-2">
            <div className="ml-5 text-sm text-gray-500">Ej ordererkända beställningar</div>

            <div className={`flex flex-wrap items-center gap-3 mt-3 ml-5 ${loading ? 'opacity-70 pointer-events-none' : ''}`}>
                <div className="w-52">
                    <LabeledReactSelect
                        label="Säljare"
                        labelWidth="w-14"
                        name="seller"
                        value={filters.seller}
                        items={filterOptions.seller}
                        onChange={(selected) => handleFilterChange({ seller: selected || '' })}
                        isDisabled={loading}
                    />
                </div>

                <div className="w-52">
                    <LabeledReactSelect
                        label="Vår ref."
                        labelWidth="w-14"
                        name="ourReference"
                        value={filters.ourReference}
                        items={filterOptions.ourReference}
                        onChange={(selected) => handleFilterChange({ ourReference: selected || '' })}
                        isDisabled={loading}
                    />
                </div>

                <div className="w-52">
                    <LabeledReactSelect
                        label="Kund"
                        labelWidth="w-14"
                        name="customerName"
                        value={filters.customerName}
                        items={filterOptions.customerName}
                        onChange={(selected) => handleFilterChange({ customerName: selected || '' })}
                        isDisabled={loading}
                    />
                </div>

                <div className="w-52">
                    <LabeledReactSelect
                        label="Leverantör"
                        labelWidth="w-14"
                        name="supplierName"
                        value={filters.supplierName}
                        items={filterOptions.supplierName}
                        onChange={(selected) => handleFilterChange({ supplierName: selected || '' })}
                        isDisabled={loading}
                    />
                </div>

                <div className="ml-auto flex items-center mr-4">
                    <div className="ml-auto flex items-center mr-15">
                        <div className="ml-4 text-xs text-gray-500">
                            Total ordervärde: <strong>{formatAmount(totals.orderValueSek)}</strong>
                        </div>
                        <div className="ml-4 text-xs text-gray-500">
                            Total TB: <strong>{formatAmount(totals.totalTbSek)}</strong>
                        </div>
                        <div className="ml-4 text-xs text-gray-500">
                            Rader: <strong>{sortedRows.length}</strong>
                        </div>
                    </div>

                    <span className="mr-3 text-xs text-gray-700">
                        Sida {pageNumber} av {Math.max(1, totalPages)}
                    </span>
                    <div className="flex gap-1 mr-0">
                        <button
                            type="button"
                            onClick={() => handlePageChange(pageNumber - 1)}
                            disabled={loading || !hasPreviousPage}
                            className="disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                            <ArrowLeftCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                        </button>
                        <button
                            type="button"
                            onClick={() => handlePageChange(pageNumber + 1)}
                            disabled={loading || !hasNextPage}
                            className="disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                            <ArrowRightCircle className="h-5 w-5 text-red-400 hover:text-red-600" />
                        </button>
                    </div>
                </div>
            </div>

            <div className="border-t border-gray-300 py-1 mt-4 flex-1 overflow-auto">
                <table className="w-full border-collapse text-xs">
                    <thead>
                        <tr>
                            {columns.map((col) => (
                                <th
                                    key={col.key}
                                    onClick={() => handleSort(col.key)}
                                    className={`px-2 py-1.5 text-[10px] font-medium text-gray-500 whitespace-nowrap cursor-pointer select-none hover:text-gray-700 ${col.align === 'right' ? 'text-right' : 'text-left'}`}
                                >
                                    <span className="inline-flex items-center gap-0.5">
                                        {col.label}
                                        {sortKey === col.key
                                            ? sortDir === 'asc'
                                                ? <ChevronUp className="h-3 w-3" />
                                                : <ChevronDown className="h-3 w-3" />
                                            : <ChevronUp className="h-3 w-3 opacity-0 group-hover:opacity-30" />}
                                    </span>
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody className={`divide-y divide-gray-100 ${!loading && pagedRows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {showSkeleton ? (
                            Array.from({ length: 12 }).map((_, i) => (
                                <tr key={i}>
                                    {columns.map((col) => (
                                        <td key={col.key} className="px-2 py-1">
                                            <Skeleton height={14} />
                                        </td>
                                    ))}
                                </tr>
                            ))
                        ) : pagedRows.length === 0 ? (
                            <tr>
                                <td colSpan={columns.length} className="px-4 py-6 text-center text-gray-400 text-xs">
                                    Inga ej ordererkända beställningar hittades.
                                </td>
                            </tr>
                        ) : (
                            pagedRows.map((row) => {
                                const hasCustomerOrderedEdition = Number(row.customerOrderedEdition ?? row.CustomerOrderedEdition ?? 0) > 0;
                                return (
                                <tr
                                    key={row.id}
                                    className={hasCustomerOrderedEdition ? 'bg-lime-50 hover:bg-lime-100' : 'hover:bg-amber-50'}
                                >
                                    <td className="px-2 py-1 text-gray-800">{row.id}</td>
                                    <td className="px-2 py-1 text-gray-800 whitespace-nowrap">
                                        {row.sentAt
                                            ? formatDateShort(row.sentAt)
                                            : <span className="text-red-500 font-medium">EJ SKICKAT!</span>}
                                    </td>
                                    <td className="px-2 py-1 text-gray-500">{row.fileSent ?? ''}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.seller}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.ourReference}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.customerName}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.supplierName}</td>
                                    <td className="px-2 py-1 text-gray-800">{row.productName}</td>
                                    <td className="px-2 py-1 text-gray-800 whitespace-nowrap">{formatDate(row.createdAt)}</td>
                                    <td className="px-2 py-1 text-gray-800 whitespace-nowrap">{formatDateShort(row.deliveryDate)}</td>
                                    <td className="px-2 py-1 text-right text-gray-800">{row.editiopnLeft?.toLocaleString('sv-SE') ?? ''}</td>
                                    <td className="px-2 py-1 text-right text-gray-800">{formatAmount(row.orderValueLeftSek)}</td>
                                    <td className="px-2 py-1 text-right text-gray-800">{formatPercent(row.markupPercent)}</td>
                                    <td className="px-2 py-1 text-right text-gray-800">{formatAmount(row.totalTbLeftSek)}</td>
                                </tr>
                                );
                            })
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

export default NonConfirmedSupplierOrders;
