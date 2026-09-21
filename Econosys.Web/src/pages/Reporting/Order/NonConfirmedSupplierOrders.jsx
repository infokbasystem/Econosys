import React, { useEffect, useMemo, useRef, useState } from 'react';
import { ArrowLeftCircle, ArrowRightCircle, ChevronUp, ChevronDown } from 'lucide-react';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import LabeledReactSelect from '../../../components/LabeledReactSelect';
import apiClient from '../../../config/apiClient';
import { getSharedRequest } from '../../../helpers/sharedRequest';

const PAGE_SIZE = 100;

const columns = [
    { key: 'id',            label: 'Id',            align: 'left',  width: '4%' },
    { key: 'sentAt',        label: 'Skickat',       align: 'left',  width: '6%' },
    { key: 'fileSent',      label: 'Fil',           align: 'left',  width: '3%' },
    { key: 'seller',        label: 'Ansv. Säljare', align: 'left',  width: '8%', filterable: true },
    { key: 'ourReference',  label: 'Vår Referens',  align: 'left',  width: '8%', filterable: true },
    { key: 'customerName',  label: 'Kund',          align: 'left',  width: '10%', filterable: true },
    { key: 'supplierName',  label: 'Leverantör',    align: 'left',  width: '10%', filterable: true },
    { key: 'productName',   label: 'Produktnamn',   align: 'left',  width: '10%' },
    { key: 'createdAt',     label: 'Skapad',        align: 'left',  width: '7%' },
    { key: 'deliveryDate',  label: 'Lev. Datum',    align: 'left',  width: '7%' },
    { key: 'edition',       label: 'Upplaga',       align: 'right', width: '5%' },
    { key: 'orderValueSek', label: 'Ordervärde',    align: 'right', width: '5%' },
    { key: 'markupPercent', label: 'Påslag',        align: 'right', width: '3%' },
    { key: 'totalTbSek',    label: 'TB',            align: 'right', width: '4%' },
];

const NonConfirmedSupplierOrders = () => {
    const [loading, setLoading] = useState(false);
    const [showSkeleton, setShowSkeleton] = useState(false);
    const skeletonTimerRef = useRef(null);
    const reportRequestRef = useRef(0);

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
        let isActive = true;
        void loadReport(isActive);

        return () => {
            isActive = false;
            clearTimeout(skeletonTimerRef.current);
            reportRequestRef.current += 1;
        };
    }, []);

    const loadReport = async (isActive = true) => {
        const requestId = reportRequestRef.current + 1;
        reportRequestRef.current = requestId;

        clearTimeout(skeletonTimerRef.current);
        setLoading(true);
        setAllRows([]);
        setShowSkeleton(false);
        const skeletonTimer = setTimeout(() => {
            if (isActive && reportRequestRef.current === requestId) {
                setShowSkeleton(true);
            }
        }, 200);
        skeletonTimerRef.current = skeletonTimer;

        try {
            const response = await getSharedRequest(
                'reporting:non-confirmed-supplier-orders',
                () => apiClient.get('/reporting/non-confirmed-supplier-orders'),
            );
            if (!isActive || reportRequestRef.current !== requestId) return;
            setAllRows(response?.data ?? []);
            setPageNumber(1);
        } catch (error) {
            console.error('Failed to load non-confirmed supplier orders:', error);
            if (!isActive || reportRequestRef.current !== requestId) return;
            setAllRows([]);
        } finally {
            clearTimeout(skeletonTimer);
            if (skeletonTimerRef.current === skeletonTimer) {
                skeletonTimerRef.current = null;
            }
            if (!isActive || reportRequestRef.current !== requestId) return;
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
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            {/* <div className="mt-2 text-sm text-gray-500">Ej ordererkända beställningar</div> */}

            <div className={`relative z-20 flex items-center gap-6 mt-2 pb-2 ${loading ? 'opacity-70 pointer-events-none' : ''}`}>
                <div className="w-52">
                    <LabeledReactSelect
                        label="Säljare"
                        labelWidth="w-14"
                        name="seller"
                        filterStyle
                        value={filters.seller}
                        items={filterOptions.seller}
                        onChange={(selected) => handleFilterChange({ seller: selected || '' })}
                        isDisabled={loading}
                    />
                </div>

                <div className="w-52">
                    <LabeledReactSelect
                        label="Vår ref."
                        labelWidth="w-12"
                        name="ourReference"
                        filterStyle
                        value={filters.ourReference}
                        items={filterOptions.ourReference}
                        onChange={(selected) => handleFilterChange({ ourReference: selected || '' })}
                        isDisabled={loading}
                    />
                </div>

                <div className="w-52">
                    <LabeledReactSelect
                        label="Kund"
                        labelWidth="w-10"
                        name="customerName"
                        filterStyle
                        value={filters.customerName}
                        items={filterOptions.customerName}
                        onChange={(selected) => handleFilterChange({ customerName: selected || '' })}
                        isDisabled={loading}
                    />
                </div>

                <div className="w-52">
                    <LabeledReactSelect
                        label="Leverantör"
                        labelWidth="w-16"
                        name="supplierName"
                        filterStyle
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
                                    onClick={() => handleSort(col.key)}
                                    className={`overflow-hidden text-ellipsis whitespace-nowrap px-2 py-1.5 text-tiny font-medium text-gray-500 cursor-pointer select-none hover:text-gray-700 ${col.align === 'right' ? 'text-right' : 'text-left'}`}
                                >
                                    <span className="inline-flex max-w-full items-center gap-0.5 overflow-hidden">
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
                                <tr key={i} className="bg-transparent hover:!bg-transparent">
                                    {columns.map((col) => (
                                        <td key={col.key} className="bg-transparent px-2 py-1">
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
