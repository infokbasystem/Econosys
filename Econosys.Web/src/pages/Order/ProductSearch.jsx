import React, { useEffect, useMemo, useState } from 'react';
import { ArrowLeftCircle, ArrowRightCircle, ChevronDown, ChevronUp, Search } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import apiClient from '../../config/apiClient';
import LabeledSwitch from '../../components/LabeledSwitch';

const PAGE_SIZE = 25;

const columns = [
    { key: 'id', label: 'Produktnr', align: 'left', width: '10%' },
    { key: 'name', label: 'Namn', align: 'left', width: '28%' },
    { key: 'format', label: 'Format', align: 'left', width: '16%' },
    { key: 'constructionid', label: 'Konstruktion', align: 'left', width: '18%' },
    { key: 'active', label: 'Aktiv', align: 'left', width: '10%' },
    { key: 'lastsupplierordercreated', label: 'Senaste beställning', align: 'left' },
];

const initialPagination = {
    pageNumber: 1,
    pageSize: PAGE_SIZE,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
};

const formatDateTime = (value) => {
    if (!value) return '';

    return new Date(value).toLocaleString('sv-SE', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
    });
};

const formatProductDimensions = (row) => {
    const length = row?.lengthMm;
    const width = row?.widthMm;
    const height = row?.heightMm;

    if (length != null && width != null && height != null) {
        return `${length} x ${width} x ${height}`;
    }

    return row?.format ?? '';
};

const ProductSearch = () => {
    const navigate = useNavigate();

    const [rows, setRows] = useState([]);
    const [loading, setLoading] = useState(true);
    const [searchLoaded, setSearchLoaded] = useState(false);
    const [searchName, setSearchName] = useState('');
    const [searchCode, setSearchCode] = useState('');
    const [appliedName, setAppliedName] = useState('');
    const [appliedCode, setAppliedCode] = useState('');
    const [includeInactive, setIncludeInactive] = useState(false);
    const [pagination, setPagination] = useState(initialPagination);
    const [sortConfig, setSortConfig] = useState({ key: 'id', direction: 'desc' });
    const [selectedRowId, setSelectedRowId] = useState(null);

    useEffect(() => {
        const timer = setTimeout(() => {
            setAppliedName(searchName);
            setAppliedCode(searchCode);
            setPagination((prev) => ({ ...prev, pageNumber: 1 }));
        }, 300);

        return () => clearTimeout(timer);
    }, [searchName, searchCode]);

    const payload = useMemo(() => {
        const conditions = [];

        if (appliedName.trim()) {
            conditions.push({
                field: 'name',
                operator: 'contains',
                value: appliedName.trim(),
            });
        }

        if (appliedCode.trim()) {
            conditions.push({
                field: 'productcode',
                operator: 'contains',
                value: appliedCode.trim(),
            });
        }

        if (!includeInactive) {
            conditions.push({
                field: 'active',
                operator: 'eq',
                value: true,
            });
        }

        return {
            filter: {
                conditions,
            },
            pagination: {
                pageNumber: pagination.pageNumber,
                pageSize: pagination.pageSize,
            },
            orderBy: [{
                field: sortConfig.key,
                direction: sortConfig.direction,
            }],
        };
    }, [appliedCode, appliedName, includeInactive, pagination.pageNumber, pagination.pageSize, sortConfig.direction, sortConfig.key]);

    useEffect(() => {
        const controller = new AbortController();

        const loadRows = async () => {
            setLoading(true);

            try {
                const response = await apiClient.post('/products/search', payload, { signal: controller.signal });
                const data = response?.data ?? {};

                setRows(data.items ?? []);
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
                console.error('Failed to load products search:', error);
                setRows([]);
                setPagination((prev) => ({
                    ...prev,
                    totalCount: 0,
                    totalPages: 0,
                    hasPreviousPage: false,
                    hasNextPage: false,
                }));
            } finally {
                if (!controller.signal.aborted) {
                    setSearchLoaded(true);
                    setLoading(false);
                }
            }
        };

        loadRows();

        return () => controller.abort();
    }, [payload]);

    const showSkeleton = loading && !searchLoaded;

    const handleSort = (key) => {
        setSortConfig((prev) => ({
            key,
            direction: prev.key === key && prev.direction === 'asc' ? 'desc' : 'asc',
        }));
        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
    };

    const handlePageChange = (nextPage) => {
        if (nextPage < 1) return;
        if (pagination.totalPages > 0 && nextPage > pagination.totalPages) return;
        setPagination((prev) => ({ ...prev, pageNumber: nextPage }));
    };

    const getRowClass = (rowId) => {
        if (selectedRowId === rowId) {
            return 'cursor-pointer border-b border-amber-200 bg-amber-100';
        }

        return 'cursor-pointer border-b border-gray-100 hover:bg-amber-50';
    };

    const handleOpenProduct = (event, rowId) => {
        event.preventDefault();
        event.stopPropagation();
        navigate(`/order/products/${rowId}`);
    };

    return (
        <div className="flex h-full flex-col pt-1 pb-4 ps-5 pe-10">
            <div className="flex items-center gap-4 overflow-x-auto whitespace-nowrap pb-2 mt-2">
                <button
                    type="button"
                    onClick={() => navigate('/order/products/new')}
                    className="w-32 shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 px-4 py-[5px]"
                >
                    Ny produkt
                </button>

                <div className="relative ml-16 w-44">
                    <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
                    <input
                        type="text"
                        value={searchCode}
                        onChange={(event) => setSearchCode(event.target.value)}
                        placeholder="Sok produktkod"
                        className="w-full text-xs border border-gray-300 rounded-sm pl-7 pr-2 py-1 focus:outline-none bg-white"
                    />
                </div>

                <div className="relative w-56">
                    <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
                    <input
                        type="text"
                        value={searchName}
                        onChange={(event) => setSearchName(event.target.value)}
                        placeholder="Sok namn"
                        className="w-full text-xs border border-gray-300 rounded-sm pl-7 pr-2 py-1 focus:outline-none bg-white"
                    />
                </div>

                <LabeledSwitch
                    label="Inkludera inaktiva"
                    name="includeInactive"
                    value={includeInactive}
                    onChange={(_rowId, _field, checked) => {
                        setIncludeInactive(checked);
                        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
                    }}
                    disabled={loading}
                    containerClassName="shrink-0"
                />

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

            <div className="border-t border-gray-300 py-1 mt-3 min-h-0 flex-1 overflow-auto">
                <table className="table-fixed w-full border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <colgroup>
                        {columns.map((column) => (
                            <col key={column.key} {...(column.width ? { style: { width: column.width } } : {})} />
                        ))}
                    </colgroup>
                    <thead>
                        <tr>
                            {columns.map((column) => (
                                <th
                                    key={column.key}
                                    onClick={() => handleSort(column.key)}
                                    className={`cursor-pointer px-2 py-2 text-[10px] font-medium text-gray-500 ${column.align === 'right' ? 'text-right' : 'text-left'}`}
                                >
                                    <span className="inline-flex items-center gap-1">
                                        {column.label}
                                        {sortConfig.key === column.key ? (
                                            sortConfig.direction === 'asc' ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />
                                        ) : (
                                            <ChevronUp className="h-3 w-3 opacity-0" />
                                        )}
                                    </span>
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody className={`${!loading && rows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {showSkeleton ? (
                            Array.from({ length: 12 }).map((_, index) => (
                                <tr key={index}>
                                    <td colSpan={columns.length} className="px-2 py-1"><Skeleton height={16} /></td>
                                </tr>
                            ))
                        ) : rows.length === 0 ? (
                            <tr>
                                <td colSpan={columns.length} className="px-4 py-8 text-center text-gray-400">Inga produkter hittades.</td>
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
                                            onClick={(event) => handleOpenProduct(event, row.id)}
                                            className="underline-offset-2 hover:underline"
                                        >
                                            {row.id}
                                        </button>
                                    </td>
                                    <td className="truncate px-2 py-1 text-gray-800">
                                        <button
                                            type="button"
                                            onClick={(event) => handleOpenProduct(event, row.id)}
                                            className="underline-offset-2 hover:underline"
                                        >
                                            {row.name}
                                        </button>
                                    </td>
                                    <td className="truncate px-2 py-1 text-gray-800">{formatProductDimensions(row)}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.construction?.name ?? row.constructionId}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.active ? 'Ja' : 'Nej'}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{formatDateTime(row.lastSupplierOrderCreated)}</td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

export default ProductSearch;
