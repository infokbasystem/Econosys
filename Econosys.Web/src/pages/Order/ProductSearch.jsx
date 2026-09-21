import React, { useEffect, useMemo, useState } from 'react';
import { ArrowLeftCircle, ArrowRightCircle, ChevronDown, ChevronUp, Plus, Search } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import apiClient from '../../config/apiClient';
import ActionButton from '../../components/ActionButton';
import LabeledSwitch from '../../components/LabeledSwitch';
import { getSharedRequest } from '../../helpers/sharedRequest';

const PAGE_SIZE = 25;
const TABLE_FONT_STYLE = { fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" };

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
        let isActive = true;

        const loadRows = async () => {
            setLoading(true);

            try {
                const requestKey = `products:search:${JSON.stringify(payload)}`;
                const response = await getSharedRequest(requestKey, () => apiClient.post('/products/search', payload));
                if (!isActive) return;
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
                console.error('Failed to load products search:', error);
                if (!isActive) return;
                setRows([]);
                setPagination((prev) => ({
                    ...prev,
                    totalCount: 0,
                    totalPages: 0,
                    hasPreviousPage: false,
                    hasNextPage: false,
                }));
            } finally {
                if (isActive) {
                    setSearchLoaded(true);
                    setLoading(false);
                }
            }
        };

        loadRows();

        return () => {
            isActive = false;
        };
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
        const isSelected = selectedRowId === rowId;
        return [
            'h-6 cursor-pointer border-b border-gray-100',
            isSelected ? 'bg-lime-100/80' : '',
            !isSelected ? 'hover:bg-lime-200/70' : 'hover:bg-lime-200/70',
        ].join(' ');
    };

    const handleOpenProduct = (event, rowId) => {
        event.preventDefault();
        event.stopPropagation();
        navigate(`/order/products/${rowId}`);
    };

    return (
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            <div className="flex items-center gap-4 overflow-x-auto whitespace-nowrap pb-2 mt-2">
                <ActionButton
                    label="Ny produkt"
                    icon={Plus}
                    onClick={() => navigate('/order/products/new')}
                    accent="lime"
                />

                <div className="relative ml-16 w-44">
                    <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
                    <input
                        type="text"
                        value={searchCode}
                        onChange={(event) => setSearchCode(event.target.value)}
                        placeholder="Sök produktkod"
                        className="h-7 w-full pl-8 rounded-full border border-lime-600 bg-white px-4 text-xs text-gray-700 outline-none transition placeholder:text-gray-500 focus:border-lime-700"
                    />
                </div>

                <div className="relative w-56 mr-10">
                    <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
                    <input
                        type="text"
                        value={searchName}
                        onChange={(event) => setSearchName(event.target.value)}
                        placeholder="Sök namn"
                        className="h-7 w-full pl-8 rounded-full border border-lime-600 bg-white px-4 text-xs text-gray-700 outline-none transition placeholder:text-gray-500 focus:border-lime-700"
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
                <table className="table-fixed w-full border-collapse text-xs" style={TABLE_FONT_STYLE}>
                    <colgroup>
                        {columns.map((column) => (
                            <col key={column.key} {...(column.width ? { style: { width: column.width } } : {})} />
                        ))}
                    </colgroup>
                    <thead>
                        <tr className="text-tiny text-gray-500">
                            {columns.map((column) => (
                                <th
                                    key={column.key}
                                    onClick={() => handleSort(column.key)}
                                    className={`cursor-pointer px-2 pt-1 pb-2 text-tiny font-medium text-gray-500 ${column.align === 'right' ? 'text-right' : 'text-left'}`}
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
                                            className="text-slate-700 hover:text-slate-900 hover:underline"
                                        >
                                            {row.id}
                                        </button>
                                    </td>
                                    <td className="truncate px-2 py-1 text-gray-800">
                                        <button
                                            type="button"
                                            onClick={(event) => handleOpenProduct(event, row.id)}
                                            className="text-slate-700 hover:text-slate-900 hover:underline"
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
