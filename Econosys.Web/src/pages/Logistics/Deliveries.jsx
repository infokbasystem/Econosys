import React, { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeftCircle, ArrowRightCircle, ChevronDown, ChevronUp } from 'lucide-react';
import ExcelJS from 'exceljs';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import DateRangePicker from '../../components/DaterangePicker';
import SegmentedFilter from '../../components/SegmentedFilter';
import Select from '../../components/Select';
import apiClient from '../../config/apiClient';
import { getSharedRequest } from '../../helpers/sharedRequest';
import DeliveryFromStock from '../../modals/DeliveryFromStock';

const PAGE_SIZE = 100;
const DELIVERIES_SEARCH_CACHE_KEY = 'deliveries-search-page-state';

const columns = [
    { key: 'productName', label: 'Produkt', align: 'left', width: 'w-[15%]', sortable: true, sortField: 'productName' },
    { key: 'deliveryDate', label: 'Lev.datum', align: 'left', width: '100px', sortable: true, sortField: 'deliveryDate' },
    { key: 'type', label: 'Typ', align: 'left', width: '120px', sortable: true, sortField: 'type' },
    { key: 'deliveryStatus', label: 'Status', align: 'center', width: '120px', sortable: true, sortField: 'deliveryStatus' },
    { key: 'customerName', label: 'Kund', align: 'left', width: 'w-[15%]', sortable: true, sortField: 'customerName' },
    { key: 'supplierOrderNr', label: 'Beställning', align: 'left', width: '100px', sortable: true, sortField: 'supplierOrderNr' },
    { key: 'customersOrderNr', label: 'Kundordernr', align: 'left', width: '15%', sortable: true, sortField: 'customersOrderNr' },
    { key: 'inventoryName', label: 'Lager', align: 'left', width: 'w-[12%]', sortable: true, sortField: 'inventoryName' },
    { key: 'nrOfItems', label: 'Antal ex', align: 'right', width: '100px', sortable: true, sortField: 'nrOfItems' },
    { key: 'nrOfPallets', label: 'Antal pall', align: 'right', width: '100px', sortable: true, sortField: 'nrOfPallets' },
    { key: 'transportOrderNr', label: 'Trp.ordernr', align: 'right', width: '100px', sortable: true, sortField: 'transportOrderNr' },
    { key: 'callOffNr', label: 'Avropenr', align: 'right', width: '100px', sortable: true, sortField: 'callOffNr' },
];

const deliveryTypeOptions = [
    { value: 'DeliveryToCustomer', label: 'Direktleverans' },
    { value: 'DeliveryToStock', label: 'Till lager' },
    { value: 'DeliveryFromStock', label: 'Från lager' },
];

const deliveryTypeLabels = {
    DeliveryToCustomer: 'Direktleverans',
    DeliveryToStock: 'Till lager',
    DeliveryFromStock: 'Från lager',
};

const deliveryStatusLabels = {
    1: 'Ej levererad',
    2: 'Levererad',
};

const toDateString = (date) =>
    `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;

const readCachedDeliverySearchState = () => {
    if (typeof window === 'undefined') return null;

    try {
        const raw = window.sessionStorage.getItem(DELIVERIES_SEARCH_CACHE_KEY);
        if (!raw) return null;
        return JSON.parse(raw);
    } catch {
        return null;
    }
};

const getRowId = (row) => `${row.type}-${row.id}`;

const Deliveries = () => {
    const now = new Date();
    now.setHours(0, 0, 0, 0);
    const yearStart = new Date(now.getFullYear(), 0, 1);

    const cachedState = readCachedDeliverySearchState();
    const cachedFilters = cachedState?.filters
        ? {
            ...cachedState.filters,
            startDate: cachedState.filters.startDate ? new Date(cachedState.filters.startDate) : yearStart,
            endDate: cachedState.filters.endDate ? new Date(cachedState.filters.endDate) : now,
        }
        : null;

    const [rows, setRows] = useState(cachedState?.rows ?? []);
    const [loading, setLoading] = useState(!cachedState?.searchLoaded);
    const [showSkeleton, setShowSkeleton] = useState(false);
    const skeletonTimerRef = useRef(null);

    const [filters, setFilters] = useState({
        startDate: cachedFilters?.startDate ?? yearStart,
        endDate: cachedFilters?.endDate ?? now,
        inventoryId: cachedFilters?.inventoryId ?? '',
        customerId: cachedFilters?.customerId ?? '',
        deliveryTypes: cachedFilters?.deliveryTypes ?? [],
    });

    const [inventoryOptions, setInventoryOptions] = useState([]);
    const [customerOptions, setCustomerOptions] = useState([]);

    const [pagination, setPagination] = useState({
        pageNumber: cachedState?.pagination?.pageNumber ?? 1,
        pageSize: cachedState?.pagination?.pageSize ?? PAGE_SIZE,
        totalCount: cachedState?.pagination?.totalCount ?? 0,
        totalPages: cachedState?.pagination?.totalPages ?? 0,
        hasPreviousPage: Boolean(cachedState?.pagination?.hasPreviousPage),
        hasNextPage: Boolean(cachedState?.pagination?.hasNextPage),
    });

    const [sortConfig, setSortConfig] = useState({
        key: cachedState?.sortConfig?.key ?? 'deliveryDate',
        direction: cachedState?.sortConfig?.direction ?? 'desc',
    });

    const [initialLoadCompleted, setInitialLoadCompleted] = useState(Boolean(cachedState?.searchLoaded));
    const [selectedRowId, setSelectedRowId] = useState(null);
    const [hasSearchSnapshot, setHasSearchSnapshot] = useState(Boolean(cachedState?.searchLoaded));
    const listRef = useRef(null);

    // View mode: shows the details of an already created delivery from stock (same endpoint as Calloff).
    const [viewDeliveryDetails, setViewDeliveryDetails] = useState(null);
    const [isLoadingViewDelivery, setIsLoadingViewDelivery] = useState(false);
    const [viewDeliveryError, setViewDeliveryError] = useState('');

    const didMountRef = useRef(false);

    useEffect(() => {
        let isActive = true;

        const loadFilterOptions = async () => {
            try {
                const response = await getSharedRequest('inventories:delivery-search-options', () => apiClient.post('/inventories/search', {
                    pagination: { pageNumber: 1, pageSize: 1000 },
                    orderBy: [{ field: 'name', direction: 'asc' }],
                }));

                if (!isActive) return;
                const inventories = Array.isArray(response?.data?.items) ? response.data.items : [];
                setInventoryOptions(inventories
                    .filter((item) => Boolean(item?.isInventory))
                    .map((item) => ({ id: item.id, name: item.name ?? String(item.id) })));
            } catch (error) {
                console.error('Failed to load inventory options:', error);
                if (isActive) setInventoryOptions([]);
            }

            try {
                const customersResponse = await getSharedRequest('customers:delivery-search-options', () => apiClient.post('/customers/search', {
                    active: true,
                    sortBy: 'Name',
                    sortDescending: false,
                }));

                if (!isActive) return;
                const customers = Array.isArray(customersResponse?.data?.items) ? customersResponse.data.items : [];
                setCustomerOptions(customers
                    .map((customer) => ({ id: customer.id, name: customer.name ?? String(customer.id) }))
                    .sort((a, b) => a.name.localeCompare(b.name, 'sv-SE')));
            } catch (error) {
                console.error('Failed to load customer options:', error);
                if (isActive) setCustomerOptions([]);
            }
        };

        loadFilterOptions();

        return () => {
            isActive = false;
        };
    }, []);

    useEffect(() => {
        const handlePointerDown = (event) => {
            if (listRef.current && !listRef.current.contains(event.target)) {
                setSelectedRowId(null);
            }
        };

        document.addEventListener('pointerdown', handlePointerDown);
        return () => document.removeEventListener('pointerdown', handlePointerDown);
    }, []);

    useEffect(() => {
        if (typeof window === 'undefined') return;

        window.sessionStorage.setItem(DELIVERIES_SEARCH_CACHE_KEY, JSON.stringify({
            rows,
            filters,
            pagination,
            sortConfig,
            searchLoaded: hasSearchSnapshot,
        }));
    }, [rows, filters, pagination, sortConfig, hasSearchSnapshot]);

    const buildRequestBody = (pageNumber, pageSize, activeFilters, activeSort = sortConfig) => {
        const conditions = [
            { field: 'deliveryDate', operator: 'gte', value: toDateString(activeFilters.startDate) },
            {
                field: 'deliveryDate',
                operator: 'lt',
                value: toDateString(new Date(
                    activeFilters.endDate.getFullYear(),
                    activeFilters.endDate.getMonth(),
                    activeFilters.endDate.getDate() + 1
                )),
            },
        ];

        if (activeFilters.inventoryId !== '' && activeFilters.inventoryId != null) {
            conditions.push({ field: 'inventoryId', operator: 'eq', value: Number(activeFilters.inventoryId) });
        }

        if (activeFilters.customerId !== '' && activeFilters.customerId != null) {
            conditions.push({ field: 'customerId', operator: 'eq', value: Number(activeFilters.customerId) });
        }

        if (Array.isArray(activeFilters.deliveryTypes) && activeFilters.deliveryTypes.length > 0
            && activeFilters.deliveryTypes.length < deliveryTypeOptions.length) {
            conditions.push({ field: 'type', operator: 'in', values: activeFilters.deliveryTypes });
        }

        const sortColumn = columns.find((column) => column.key === activeSort.key);
        const sortField = sortColumn?.sortField;

        return {
            filter: { conditions },
            pagination: { pageNumber, pageSize },
            orderBy: sortField ? [{ field: sortField, direction: activeSort.direction }] : [],
        };
    };

    const loadRows = async (pageNumber = 1, activeFilters = filters, isActive = true, keepExistingRows = false) => {
        setLoading(true);

        if (!keepExistingRows) {
            setRows([]);
            skeletonTimerRef.current = setTimeout(() => setShowSkeleton(true), 200);
        }

        try {
            const requestBody = buildRequestBody(pageNumber, pagination.pageSize, activeFilters, sortConfig);
            const requestKey = `deliveries:search:${JSON.stringify(requestBody)}`;
            const response = await getSharedRequest(requestKey, () => apiClient.post('/deliveries/search', requestBody));
            if (!isActive) return;

            const data = response?.data;
            setRows(data?.items ?? []);
            setPagination((prev) => ({
                ...prev,
                pageNumber: data?.pageNumber ?? pageNumber,
                pageSize: data?.pageSize ?? prev.pageSize,
                totalCount: data?.totalCount ?? 0,
                totalPages: data?.totalPages ?? 0,
                hasPreviousPage: Boolean(data?.hasPreviousPage),
                hasNextPage: Boolean(data?.hasNextPage),
            }));
        } catch (error) {
            console.error('Failed to load deliveries:', error);
            if (!isActive) return;
            if (!hasSearchSnapshot) {
                setRows([]);
                setPagination((prev) => ({
                    ...prev,
                    totalCount: 0,
                    totalPages: 0,
                    hasPreviousPage: false,
                    hasNextPage: false,
                }));
            }
        } finally {
            clearTimeout(skeletonTimerRef.current);
            if (isActive) {
                setHasSearchSnapshot(true);
                setLoading(false);
                setShowSkeleton(false);
            }
        }
    };

    useEffect(() => {
        let isActive = true;

        const timer = setTimeout(async () => {
            await loadRows(pagination.pageNumber, filters, isActive, hasSearchSnapshot);
            if (!didMountRef.current) {
                didMountRef.current = true;
                setInitialLoadCompleted(true);
            }
        }, didMountRef.current ? 250 : 0);

        return () => {
            isActive = false;
            clearTimeout(timer);
            clearTimeout(skeletonTimerRef.current);
        };
    }, [filters, pagination.pageNumber, sortConfig]);

    const onPageChange = (nextPage) => {
        if (nextPage < 1 || (pagination.totalPages > 0 && nextPage > pagination.totalPages)) {
            return;
        }

        setPagination((prev) => ({ ...prev, pageNumber: nextPage }));
    };

    const handleFilterChange = (updates) => {
        setFilters((prev) => ({ ...prev, ...updates }));
        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
    };

    const handleSort = (key) => {
        const column = columns.find((col) => col.key === key);
        if (!column?.sortable) return;

        setSortConfig((prev) => ({
            key,
            direction: prev.key === key && prev.direction === 'asc' ? 'desc' : 'asc',
        }));
        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
    };

    const handleCloseDeliveryDetailsModal = () => {
        if (isLoadingViewDelivery) {
            return;
        }

        setViewDeliveryDetails(null);
        setViewDeliveryError('');
    };

    const handleViewDelivery = async (row) => {
        if (isLoadingViewDelivery || row?.type !== 'DeliveryFromStock' || !row?.id) {
            return;
        }

        setIsLoadingViewDelivery(true);
        setViewDeliveryError('');

        try {
            const response = await apiClient.get(`/calloff/deliveries/fromstock/${row.id}`);
            setViewDeliveryDetails(response?.data ?? null);
        } catch (error) {
            console.error('Failed to load delivery details:', error);
            const apiMessage = error?.response?.data?.message;
            setViewDeliveryError(apiMessage || 'Kunde inte läsa in leveransen.');
        } finally {
            setIsLoadingViewDelivery(false);
        }
    };

    const handleExport = async () => {
        if (loading) return;

        const workbook = new ExcelJS.Workbook();
        const sheet = workbook.addWorksheet('Leveranser');

        sheet.addRow(columns.map((column) => column.label));
        sheet.getRow(1).font = { bold: true, size: 9 };

        try {
            let currentPage = 1;
            let totalPages = 1;

            while (currentPage <= totalPages) {
                const response = await apiClient.post('/deliveries/search', buildRequestBody(currentPage, 200, filters));
                const data = response?.data ?? {};
                const items = data?.items ?? [];

                totalPages = Number(data?.totalPages) || 1;

                items.forEach((row) => {
                    sheet.addRow([
                        row.productName ?? '',
                        formatDate(row.deliveryDate),
                        deliveryTypeLabels[row.type] ?? row.type ?? '',
                        deliveryStatusLabels[row.deliveryStatus] ?? '',
                        row.customerName ?? '',
                        row.supplierOrderNr ?? '',
                        row.customersOrderNr ?? '',
                        row.inventoryName ?? '',
                        row.nrOfItems ?? '',
                        row.nrOfPallets ?? '',
                        row.transportOrderNr ?? '',
                        row.callOffNr ?? '',
                    ]);
                });

                currentPage += 1;
            }

            for (let col = 1; col <= columns.length; col++) {
                sheet.getColumn(col).width = 16;
            }

            const buffer = await workbook.xlsx.writeBuffer();
            const blob = new Blob([buffer], {
                type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
            });

            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `leveranser-${toDateString(new Date())}.xlsx`;
            link.click();
            URL.revokeObjectURL(url);
        } catch (error) {
            console.error('Failed to export delivery search:', error);
        }
    };

    const formatAmount = (value) => {
        if (value == null) return '';
        return new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 2 }).format(Number(value));
    };

    const formatDate = (value) => {
        if (!value) return '';
        const parsed = new Date(value);
        if (Number.isNaN(parsed.getTime())) return '';
        return parsed.toLocaleDateString('sv-SE');
    };

    const getRowClass = (row) => {
        const rowId = getRowId(row);
        const isSelected = selectedRowId === rowId;
        return [
            'h-6 cursor-pointer border-b border-gray-100',
            isSelected ? 'bg-lime-100/80' : '',
            !isSelected ? 'hover:bg-lime-200/70' : 'hover:bg-lime-200/70',
        ].join(' ');
    };

    return (
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            <DeliveryFromStock
                isOpen={viewDeliveryDetails != null}
                onClose={handleCloseDeliveryDetailsModal}
                details={viewDeliveryDetails}
                isSubmitting={isLoadingViewDelivery}
                submitError={viewDeliveryError}
            />
            <div className={`relative z-20 flex items-center gap-4 overflow-visible whitespace-nowrap pb-2 mt-2 pl-16 ${loading && !hasSearchSnapshot ? 'opacity-70 pointer-events-none' : ''}`}>
                <div className="mr-10">
                  <SegmentedFilter
                      theme="lime"
                      isMulti
                      values={filters.deliveryTypes}
                      options={deliveryTypeOptions}
                      onChange={(values) => handleFilterChange({ deliveryTypes: values ?? [] })}
                  />
                </div>

                <DateRangePicker
                    placeholder="Välj period"
                    presets={['this-month', 'last-month', 'last-3-months', 'year-to-date']}
                    initialPresetKey="year-to-date"
                    onApply={({ startDate, endDate }) => {
                        handleFilterChange({ startDate, endDate });
                    }}
                    triggerRadius="full"
                    triggerClassName="h-7 w-[260px] border-lime-600 px-3 text-xs text-gray-700 focus:border-lime-700"
                    openTriggerClassName="border-lime-700 ring-1 ring-lime-200"
                    closedTriggerClassName="border-lime-600 hover:border-lime-700"
                    widthClassName="w-60"
                />

                <div className="w-48 shrink-0">
                    <Select
                        isSearchable
                        isClearable
                        items={inventoryOptions}
                        value={filters.inventoryId}
                        placeholder="Alla lager"
                        onChange={(value) => handleFilterChange({ inventoryId: value ?? '' })}
                    />
                </div>

                <div className="w-56 shrink-0">
                    <Select
                        isSearchable
                        isClearable
                        items={customerOptions}
                        value={filters.customerId}
                        placeholder="Alla kunder"
                        onChange={(value) => handleFilterChange({ customerId: value ?? '' })}
                    />
                </div>

                <a
                    href="#"
                    onClick={(event) => {
                        event.preventDefault();
                        if (loading || pagination.totalCount === 0) return;
                        handleExport();
                    }}
                    aria-disabled={loading || pagination.totalCount === 0}
                    className={`ml-20 text-xs inline-flex items-center whitespace-nowrap font-medium transition-colors ${loading || pagination.totalCount === 0
                        ? 'text-gray-300 cursor-not-allowed pointer-events-none'
                        : 'text-slate-500 hover:text-slate-700'
                    }`}
                >
                    Exportera till EXCEL
                </a>

                <div className="ml-auto flex items-center gap-4 text-xs text-gray-600" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    {initialLoadCompleted && (
                        <>
                            <span>Leveranser <strong>{pagination.totalCount}</strong></span>
                            <span>Sida {pagination.pageNumber} av {Math.max(1, pagination.totalPages)}</span>
                            <div className="flex gap-1">
                                <button
                                    type="button"
                                    onClick={() => onPageChange(pagination.pageNumber - 1)}
                                    disabled={loading || !pagination.hasPreviousPage}
                                    className="disabled:cursor-not-allowed disabled:opacity-40"
                                >
                                    <ArrowLeftCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                                </button>
                                <button
                                    type="button"
                                    onClick={() => onPageChange(pagination.pageNumber + 1)}
                                    disabled={loading || !pagination.hasNextPage}
                                    className="disabled:cursor-not-allowed disabled:opacity-40"
                                >
                                    <ArrowRightCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                                </button>
                            </div>
                        </>
                    )}
                </div>
            </div>

            <div ref={listRef} className="border-t border-gray-300 py-1 mt-3 min-h-0 flex-1 overflow-auto">
                <table className="table-fixed w-full border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <colgroup>
                        {columns.map((column) => (
                            <col key={column.key} {...(column.width ? { style: { width: column.width } } : {})} />
                        ))}
                    </colgroup>
                    <thead className="">
                        <tr className="text-tiny text-gray-500">
                            {columns.map((col) => (
                                <th
                                    key={col.key}
                                    onClick={() => (col.sortable ? handleSort(col.key) : undefined)}
                                    className={`${col.sortable ? 'cursor-pointer' : ''} px-2 pt-1 pb-2 text-tiny font-medium text-gray-500 ${col.align === 'right' ? 'text-right' : col.align === 'center' ? 'text-center' : 'text-left'}`}
                                >
                                    <span className="inline-flex items-center gap-1">
                                        {col.label}
                                        {col.sortable && (
                                            sortConfig.key === col.key ? (
                                                sortConfig.direction === 'asc' ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />
                                            ) : (
                                                <ChevronUp className="h-3 w-3 opacity-0" />
                                            )
                                        )}
                                    </span>
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody className={`${!loading && rows.length > 0 ? 'bg-white' : 'bg-transparent'}`}>
                        {showSkeleton ? (
                            Array.from({ length: 12 }).map((_, i) => (
                                <tr key={i}>
                                    {columns.map((col) => (
                                        <td key={col.key} className="px-2 py-1">
                                            <Skeleton height={16} />
                                        </td>
                                    ))}
                                </tr>
                            ))
                        ) : rows.length === 0 && !loading && initialLoadCompleted ? (
                            <tr>
                                <td colSpan={columns.length} className="px-4 py-8 text-center text-gray-400 text-xs">
                                    Inga leveranser hittades för valt urval
                                </td>
                            </tr>
                        ) : (
                            rows.map((row) => (
                                <tr
                                    key={getRowId(row)}
                                    className={getRowClass(row)}
                                    onClick={() => setSelectedRowId((prev) => (prev === getRowId(row) ? null : getRowId(row)))}
                                >
                                    <td className="truncate px-2 py-0 text-gray-800">{row.productName}</td>
                                    <td className="truncate px-2 py-0 text-gray-800">{formatDate(row.deliveryDate)}</td>
                                    <td className="truncate px-2 py-0 text-gray-800">
                                        {row.type === 'DeliveryFromStock' ? (
                                            <button
                                                type="button"
                                                onClick={(event) => {
                                                    event.stopPropagation();
                                                    handleViewDelivery(row);
                                                }}
                                                disabled={isLoadingViewDelivery}
                                                className="text-slate-700 hover:text-slate-900 hover:underline disabled:cursor-not-allowed disabled:opacity-50"
                                            >
                                                {`${deliveryTypeLabels[row.type] ?? row.type} (${row.id})`}
                                            </button>
                                        ) : (deliveryTypeLabels[row.type] ?? row.type)}
                                    </td>
                                    <td className="truncate px-2 py-0 text-center text-gray-800">
                                        {Number(row.deliveryStatus) === 2 ? (
                                            <span className="inline-flex items-center rounded-full bg-lime-600 text-white px-2 py-[3px] text-tiny mr-2">
                                                Levererad
                                            </span>
                                        ) : Number(row.deliveryStatus) === 1 ? (
                                            <span className="inline-flex items-center rounded-full bg-gray-200 text-gray-600 px-2 py-[3px] text-tiny mr-2">
                                                Ej levererad
                                            </span>
                                        ) : ''}
                                    </td>
                                    <td className="truncate px-2 py-0 text-gray-800">{row.customerName}</td>
                                    <td className="truncate px-2 py-0 text-gray-800">
                                        {row.supplierOrderId ? (
                                            <Link
                                                to={`/order/supplierorders/${row.supplierOrderId}`}
                                                onClick={(event) => event.stopPropagation()}
                                                className="text-slate-700 hover:text-slate-900 hover:underline"
                                            >
                                                {row.supplierOrderNr ?? row.supplierOrderId}
                                            </Link>
                                        ) : (row.supplierOrderNr ?? '')}
                                    </td>
                                    <td className="truncate px-2 py-0 text-gray-800">{row.customersOrderNr}</td>
                                    <td className="truncate px-2 py-0 text-gray-800">{row.inventoryName}</td>
                                    <td className="truncate pr-6 py-0 text-right text-gray-800">{formatAmount(row.nrOfItems)}</td>
                                    <td className="truncate pr-6 py-0 text-right text-gray-800">{formatAmount(row.nrOfPallets)}</td>
                                    <td className="truncate pr-6 py-0 text-right text-gray-800">
                                        {row.transportOrderId ? (
                                            <Link
                                                to={`/logistics/transportorder/${row.transportOrderId}`}
                                                onClick={(event) => event.stopPropagation()}
                                                className="text-slate-700 hover:text-slate-900 hover:underline"
                                            >
                                                {row.transportOrderNr ?? row.transportOrderId}
                                            </Link>
                                        ) : (row.transportOrderNr ?? '')}
                                    </td>
                                    <td className="truncate pr-6 py-0 text-right text-gray-800">
                                        {row.callOffNr ? (
                                            <Link
                                                to={`/logistics/calloff/${row.callOffNr}`}
                                                onClick={(event) => event.stopPropagation()}
                                                className="text-slate-700 hover:text-slate-900 hover:underline"
                                            >
                                                {row.callOffNr}
                                            </Link>
                                        ) : ''}
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

export default Deliveries;
