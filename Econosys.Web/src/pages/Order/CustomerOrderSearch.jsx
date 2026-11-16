import React, { useEffect, useState } from 'react';
import { ArrowLeftCircle, ArrowRightCircle, ChevronDown, ChevronUp } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import apiClient from '../../config/apiClient';
import MultiSelectCheckboxFilter from '../../components/MultiSelectCheckboxFilter';
import OrderSearchInputs from '../../components/OrderSearchInputs';
import { buildOrderTextFilters } from '../../helpers/orderSearchFilters';
import { formatDateShort } from '../../helpers/dateUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';
import useOrderSearchPeople from '../../hooks/useOrderSearchPeople';
import useColumnResize from '../../hooks/useColumnResize';

const PAGE_SIZE = 25;
const TABLE_FONT_STYLE = { fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" };
const extraSearchFields = [{ key: 'yourOrderNr', label: 'Kunds onr' }];

const columns = [
    { key: 'customerOrderNr', label: 'Nr', align: 'left', width: '80px' },
    { key: 'yourOrderNr', label: 'Kunds onr', align: 'left', width: '80px' },
    { key: 'product', label: 'Produkt', align: 'left', width: '14%' },
    { key: 'customerName', label: 'Kund', align: 'left', width: '14%' },
    { key: 'supplierName', label: 'Leverantör', align: 'left', width: '10%' },
    { key: 'construction', label: 'Konstruktion', align: 'left', width: '12%' },
    { key: 'material', label: 'Material', align: 'left', width: '12%' },
    { key: 'format', label: 'Format', align: 'left', width: '10%' },
    { key: 'sellerName', label: 'Säljare', align: 'left', width: '12%', filter: 'seller' },
    { key: 'createdByUserName', label: 'Skapad av', align: 'left', width: '12%', filter: 'creator' },
    { key: 'created', label: 'Skapad', align: 'left', width: '100px' },
    { key: 'edited', label: 'Ändrad', align: 'left', width: '100px' },
];

// Pixel widths stay fixed; percentage widths share the remaining space proportionally.
const tableStyle = {
    ...TABLE_FONT_STYLE,
    gridTemplateColumns: columns.map(({ width }) => (
        width.endsWith('%') ? `minmax(0, ${parseFloat(width)}fr)` : width
    )).join(' '),
};

const initialPagination = {
    pageNumber: 1,
    pageSize: PAGE_SIZE,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
};

const CustomerOrderSearch = () => {
    const resize = useColumnResize();
    const navigate = useNavigate();
    const people = useOrderSearchPeople();
    const { personFilters } = people;

    const [rows, setRows] = useState([]);
    const [loading, setLoading] = useState(true);
    const [searchLoaded, setSearchLoaded] = useState(false);
    const [searchInput, setSearchInput] = useState({ searchTerm: '', product: '', supplierName: '', customerName: '', yourOrderNr: '' });
    const [searchFilters, setSearchFilters] = useState(() => buildOrderTextFilters(searchInput));
    const [pagination, setPagination] = useState(initialPagination);
    const [sortConfig, setSortConfig] = useState({ key: 'created', direction: 'desc' });
    const [selectedRowId, setSelectedRowId] = useState(null);

    useEffect(() => {
        const timer = setTimeout(() => {
            setSearchFilters(buildOrderTextFilters(searchInput));
            setPagination((prev) => ({ ...prev, pageNumber: 1 }));
        }, 350);

        return () => clearTimeout(timer);
    }, [searchInput]);

    useEffect(() => {
        let isActive = true;

        const loadRows = async () => {
            setLoading(true);

            try {
                const requestBody = {
                    ...searchFilters,
                    ...personFilters,
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
    }, [searchFilters, pagination.pageNumber, pagination.pageSize, sortConfig, personFilters]);

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

    const handlePersonFilterChange = (setFilter, values) => {
        setFilter(values);
        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
    };

    const getRowClass = (rowId) => {
        const isSelected = selectedRowId === rowId;
        return [
            'col-span-full grid grid-cols-subgrid h-6 cursor-pointer border-b border-gray-100',
            isSelected ? 'odd:bg-lime-100/80 even:bg-lime-100/80' : 'odd:bg-white even:bg-gray-50',
            'hover:bg-lime-200/70',
        ].join(' ');
    };

    const handleOpenCustomerOrder = (event, customerOrderId) => {
        event.preventDefault();
        event.stopPropagation();
        navigate(`/order/customerorders/${customerOrderId}`);
    };

    return (
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            <div className="mt-2 flex items-center gap-4 overflow-x-auto whitespace-nowrap pb-2">
                <OrderSearchInputs values={searchInput} onChange={setSearchInput} extraFields={extraSearchFields} />

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

            {people.error && <p role="alert" className="mt-1 text-xs text-red-600">{people.error}</p>}

            <div className="mt-3 min-h-0 flex-1 overflow-auto border-t border-gray-300 py-1">
                <table
                    ref={resize.tableRef}
                    className={`grid text-xs ${resize.widths ? '' : 'w-full min-w-[1400px]'}`}
                    style={{ ...tableStyle, width: resize.tableWidth, gridTemplateColumns: resize.widths ? resize.widths.map((width) => `${width}px`).join(' ') : tableStyle.gridTemplateColumns }}
                >
                    <thead className="contents">
                        <tr className="col-span-full grid grid-cols-subgrid text-tiny text-gray-500">
                            {columns.map((column, index) => (
                                <th
                                    key={column.key}
                                    onClick={column.filter ? undefined : () => handleSort(column.key)}
                                    className={`relative ${column.filter ? 'cursor-default' : 'overflow-hidden cursor-pointer'} px-2 pt-1 pb-2 text-tiny font-medium text-gray-500 ${column.align === 'right' ? 'text-right' : 'text-left'}`}
                                >
                                    <span className="inline-flex items-center gap-1">
                                        {column.label}
                                        {column.filter ? null : sortConfig.key === column.key ? (
                                            sortConfig.direction === 'asc' ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />
                                        ) : (
                                            <ChevronUp className="h-3 w-3 opacity-0" />
                                        )}
                                    </span>
                                    {column.filter && (
                                        <MultiSelectCheckboxFilter
                                            label={`Filtrera ${column.label.toLowerCase()}`}
                                            options={people.options}
                                            selectedValues={column.filter === 'seller' ? people.sellerIds : people.creatorIds}
                                            disabled={people.options.length === 0}
                                            menuAlign="right"
                                            onChange={(values) => handlePersonFilterChange(
                                                column.filter === 'seller' ? people.setSellerIds : people.setCreatorIds,
                                                values,
                                            )}
                                        />
                                    )}
                                    {resize.handle(index, column.label)}
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody className="contents">
                        {showSkeleton ? (
                            Array.from({ length: 12 }).map((_, index) => (
                                <tr key={index} className="col-span-full grid grid-cols-subgrid">
                                    <td colSpan={columns.length} className="col-span-full px-2 py-1"><Skeleton height={16} /></td>
                                </tr>
                            ))
                        ) : rows.length === 0 ? (
                            <tr className="col-span-full grid grid-cols-subgrid">
                                <td colSpan={columns.length} className="col-span-full px-4 py-8 text-center text-gray-400">Inga ordererkännanden hittades.</td>
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
                                            className="text-sky-700 decoration-sky-300 underline-offset-2 hover:underline hover:text-sky-800"
                                        >
                                            {row.customerOrderNr}
                                        </button>
                                    </td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.yourOrderNr}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.product}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.customerName}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.supplierName}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.construction}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.material}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.format}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.sellerName}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.createdByUserName}</td>
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
