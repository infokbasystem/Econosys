import React, { useEffect, useState } from 'react';
import { ArrowLeftCircle, ArrowRightCircle, ChevronDown, ChevronUp, Search } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import apiClient from '../../config/apiClient';
import { formatDateShort } from '../../helpers/dateUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';

const PAGE_SIZE = 25;

const columns = [
    { key: 'customerOrderNr', label: 'Nr', align: 'left', width: '9%' },
    { key: 'supplierOrderNr', label: 'Lev onr', align: 'left', width: '10%' },
    { key: 'product', label: 'Produkt', align: 'left', width: '14%' },
    { key: 'customerName', label: 'Kund', align: 'left', width: '14%' },
    { key: 'construction', label: 'Konstruktion', align: 'left', width: '12%' },
    { key: 'material', label: 'Material', align: 'left', width: '12%' },
    { key: 'format', label: 'Format', align: 'left', width: '10%' },
    { key: 'created', label: 'Skapad', align: 'left', width: '6%' },
    { key: 'edited', label: 'Ändrad', align: 'left', width: '7%' },
];

const initialPagination = {
    pageNumber: 1,
    pageSize: PAGE_SIZE,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
};

const CustomerOrderSearch = () => {
    const navigate = useNavigate();

    const [rows, setRows] = useState([]);
    const [loading, setLoading] = useState(true);
    const [searchLoaded, setSearchLoaded] = useState(false);
    const [searchInput, setSearchInput] = useState('');
    const [searchTerm, setSearchTerm] = useState('');
    const [pagination, setPagination] = useState(initialPagination);
    const [sortConfig, setSortConfig] = useState({ key: 'created', direction: 'desc' });
    const [selectedRowId, setSelectedRowId] = useState(null);

    useEffect(() => {
        const timer = setTimeout(() => {
            setSearchTerm(searchInput);
            setPagination((prev) => ({ ...prev, pageNumber: 1 }));
        }, 350);

        return () => clearTimeout(timer);
    }, [searchInput]);

    useEffect(() => {
        let isActive = true;

        const loadRows = async () => {
            setLoading(true);

            try {
                const trimmedSearchTerm = searchTerm.trim();

                const requestBody = {
                    searchTerm: trimmedSearchTerm || null,
                    pagination: {
                        pageNumber: pagination.pageNumber,
                        pageSize: pagination.pageSize,
                    },
                    orderBy: [{
                        field: sortConfig.key,
                        direction: sortConfig.direction,
                    }],
                };
                const requestKey = `customerorders:search:${JSON.stringify(requestBody)}`;
                const response = await getSharedRequest(requestKey, () => apiClient.post('/customerorders/search', requestBody));
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
                console.error('Failed to load customer order search:', error);
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
    }, [searchTerm, pagination.pageNumber, pagination.pageSize, sortConfig]);

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

    const handleOpenCustomerOrder = (event, customerOrderId) => {
        event.preventDefault();
        event.stopPropagation();
        navigate(`/order/customerorders/${customerOrderId}`);
    };

    return (
        <div className="flex h-full flex-col pt-1 pb-4 ps-5 pe-10">
            <div className="mt-2 flex items-center gap-4 overflow-x-auto whitespace-nowrap pb-2">
                <div className="relative ml-16 w-56">
                    <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
                    <input
                        type="text"
                        value={searchInput}
                        onChange={(event) => setSearchInput(event.target.value)}
                        placeholder="Sok"
                        className="w-full rounded-sm border border-gray-300 bg-white py-1 pl-7 pr-2 text-xs focus:outline-none"
                    />
                </div>

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

            <div className="mt-3 min-h-0 flex-1 overflow-auto border-t border-gray-300 py-1">
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
                                <td colSpan={columns.length} className="px-4 py-8 text-center text-gray-400">Inga ordererkännanden hittades.</td>
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
                                            onClick={(event) => handleOpenCustomerOrder(event, row.id)}
                                            className="underline-offset-2 hover:underline"
                                        >
                                            {row.customerOrderNr}
                                        </button>
                                    </td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.supplierOrderNr}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.product}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.customerName}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.construction}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.material}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.format}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{formatDateShort(row.created)}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{formatDateShort(row.edited)}</td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

export default CustomerOrderSearch;
