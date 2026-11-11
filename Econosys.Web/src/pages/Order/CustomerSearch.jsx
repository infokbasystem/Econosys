import React, { useEffect, useMemo, useRef, useState } from 'react';
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
    { key: 'name', label: 'Kundnamn', align: 'left', width: '20%', sortBy: 'name' },
    { key: 'seller', label: 'Säljare', align: 'left', width: '14%', sortable: false, filter: 'seller' },
    { key: 'support', label: 'Support', align: 'left', width: '14%', sortable: false, filter: 'support' },
    { key: 'category', label: 'Kategori', align: 'left', width: '5%', sortable: false, filter: 'category' },
    { key: 'prospect', label: 'Prospekt', align: 'left', width: '5%', sortable: false, filter: 'prospect' },
    { key: 'cardStatus', label: 'Status', align: 'left', width: '8%', sortable: false, filter: 'complete' },
    { key: 'lastActivity', label: 'Senaste aktivitet', align: 'left', width: '14%', sortBy: 'lastactivity' },
];

const SmallStyledCheckbox = ({ checked }) => {
    const indicatorStyle = checked
        ? { backgroundColor: '#14b8a6', borderColor: '#14b8a6', color: '#ffffff' }
        : { backgroundColor: '#ffffff', borderColor: '#aaaaaa', color: '#4b5563' };

    return (
        <span className="inline-flex h-4 w-4 shrink-0 items-center justify-center rounded-full border border-[#e5e7eb] bg-white p-[1px]">
            <span className="inline-flex h-full w-full items-center justify-center rounded-full border transition-all" style={indicatorStyle}>
                <svg viewBox="0 0 16 16" aria-hidden="true" className={`h-2.5 w-2.5 transition-opacity ${checked ? 'opacity-100' : 'opacity-0'}`}>
                    <path d="M3 8.5 6.3 11.6 13 4.9" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
            </span>
        </span>
    );
};

const MultiSelectCheckboxFilter = ({ options, selectedValues, onChange }) => {
    const [open, setOpen] = useState(false);
    const rootRef = useRef(null);
    const allSelected = options.length > 0 && selectedValues.length === options.length;
    const label = allSelected
        ? 'Välj alla'
        : selectedValues.length > 0
            ? `${selectedValues.length} valda`
            : '';

    useEffect(() => {
        const handleClickOutside = (event) => {
            if (rootRef.current && !rootRef.current.contains(event.target)) {
                setOpen(false);
            }
        };

        document.addEventListener('mousedown', handleClickOutside);
        return () => document.removeEventListener('mousedown', handleClickOutside);
    }, []);

    const toggleOption = (value) => {
        onChange(
            selectedValues.includes(value)
                ? selectedValues.filter((selectedValue) => selectedValue !== value)
                : [...selectedValues, value],
        );
    };

    return (
        <div className="relative mt-1" ref={rootRef}>
            <button
                type="button"
                onClick={() => setOpen((previous) => !previous)}
                className="flex h-5 w-full items-center justify-between rounded-sm border border-gray-300 bg-white px-1.5 text-left text-tiny font-normal text-gray-700 focus:border-slate-400 focus:outline-none"
                title={label || 'Välj'}
            >
                <span className={`truncate ${label ? 'text-gray-700' : 'text-gray-400'}`}>{label || 'Välj'}</span>
                <span className="text-tiny text-gray-500">▾</span>
            </button>

            {open ? (
                <div className="absolute left-0 top-[26px] z-50 max-h-72 w-[420px] overflow-y-auto rounded-sm border border-gray-300 bg-white p-1.5 shadow-lg">
                    <label
                        className="mb-0.5 flex cursor-pointer items-center gap-2 rounded-sm px-2 py-1 text-tiny font-normal text-gray-700 hover:bg-gray-50"
                        onClick={() => onChange(allSelected ? [] : options.map((option) => option.value))}
                    >
                        <SmallStyledCheckbox checked={allSelected} />
                        <span>Välj alla</span>
                    </label>

                    {options.map((option) => (
                        <label
                            key={String(option.value)}
                            className="mb-0.5 flex cursor-pointer items-center gap-2 rounded-sm px-2 py-0.5 text-tiny font-normal text-gray-700 hover:bg-gray-50"
                            onClick={() => toggleOption(option.value)}
                        >
                            <SmallStyledCheckbox checked={selectedValues.includes(option.value)} />
                            <span className="truncate">{option.label}</span>
                        </label>
                    ))}
                </div>
            ) : null}
        </div>
    );
};

const initialPagination = {
    pageNumber: 1,
    pageSize: PAGE_SIZE,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
};

const CustomerSearch = () => {
    const navigate = useNavigate();

    const [rows, setRows] = useState([]);
    const [loading, setLoading] = useState(true);
    const [searchLoaded, setSearchLoaded] = useState(false);
    const [searchInput, setSearchInput] = useState('');
    const [searchTerm, setSearchTerm] = useState('');
    const [includeInactive, setIncludeInactive] = useState(false);
    const [pagination, setPagination] = useState(initialPagination);
    const [sortConfig, setSortConfig] = useState({ key: 'name', direction: 'asc' });
    const [selectedRowId, setSelectedRowId] = useState(null);
    const [userOptions, setUserOptions] = useState([]);
    const [categoryOptions, setCategoryOptions] = useState([]);
    const [sellerFilter, setSellerFilter] = useState([]);
    const [supportFilter, setSupportFilter] = useState([]);
    const [categoryFilter, setCategoryFilter] = useState([]);
    const [prospectFilter, setProspectFilter] = useState([]);
    const [completeFilter, setCompleteFilter] = useState([]);
    const [exporting, setExporting] = useState(false);
    const [exportError, setExportError] = useState('');

    useEffect(() => {
        let isActive = true;

        const loadUserOptions = async () => {
            try {
                const response = await getSharedRequest(
                    'customers:form-options',
                    () => apiClient.get('/customers/form-options'),
                );
                if (!isActive) return;
                setUserOptions(response?.data?.users ?? []);
                setCategoryOptions(response?.data?.categories ?? []);
            } catch (error) {
                console.error('Failed to load customer search user options:', error);
                if (isActive) {
                    setUserOptions([]);
                    setCategoryOptions([]);
                }
            }
        };

        loadUserOptions();
        return () => {
            isActive = false;
        };
    }, []);

    const userNames = useMemo(
        () => new Map(userOptions.map((user) => [String(user.id), user.name])),
        [userOptions],
    );
    const getUserName = (userId) => userNames.get(String(userId)) ?? (userId != null ? `Anv. ${userId}` : '');

    useEffect(() => {
        const timer = setTimeout(() => {
            setSearchTerm(searchInput);
            setPagination((prev) => ({ ...prev, pageNumber: 1 }));
        }, 300);

        return () => clearTimeout(timer);
    }, [searchInput]);

    const requestBody = useMemo(() => ({
        searchTerm: searchTerm.trim() || null,
        active: includeInactive ? null : true,
        responsibleUserIds: sellerFilter,
        supportEmployeeIds: supportFilter,
        categories: categoryFilter,
        isProspectValues: prospectFilter,
        isCompleteValues: completeFilter,
        sortBy: sortConfig.key,
        sortDescending: sortConfig.direction === 'desc',
        pagination: {
            pageNumber: pagination.pageNumber,
            pageSize: pagination.pageSize,
        },
    }), [completeFilter, categoryFilter, includeInactive, pagination.pageNumber, pagination.pageSize, prospectFilter, searchTerm, sellerFilter, sortConfig.direction, sortConfig.key, supportFilter]);

    useEffect(() => {
        let isActive = true;

        const loadRows = async () => {
            setLoading(true);

            try {
                const requestKey = `customers:search:${JSON.stringify(requestBody)}`;
                const response = await getSharedRequest(requestKey, () => apiClient.post('/customers/search', requestBody));
                if (!isActive) return;

                const data = response?.data ?? {};
                const totalCount = data.totalCount ?? 0;
                const totalPages = data.totalPages ?? 0;
                const pageNumber = data.pageNumber ?? pagination.pageNumber;

                setRows(data.items ?? []);
                setPagination((prev) => ({
                    ...prev,
                    pageNumber,
                    pageSize: data.pageSize ?? prev.pageSize,
                    totalCount,
                    totalPages,
                    hasPreviousPage: pageNumber > 1,
                    hasNextPage: totalPages > 0 && pageNumber < totalPages,
                }));
            } catch (error) {
                console.error('Failed to load customer search:', error);
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
    }, [pagination.pageNumber, pagination.pageSize, requestBody]);

    const showSkeleton = loading && !searchLoaded;

    const handleSort = (column) => {
        const nextKey = column.sortBy;

        if (!nextKey) {
            return;
        }

        setSortConfig((prev) => ({
            key: nextKey,
            direction: prev.key === nextKey && prev.direction === 'asc' ? 'desc' : 'asc',
        }));
        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
    };

    const handlePageChange = (nextPage) => {
        if (nextPage < 1) return;
        if (pagination.totalPages > 0 && nextPage > pagination.totalPages) return;
        setPagination((prev) => ({ ...prev, pageNumber: nextPage }));
    };

    const handleFilterChange = (setFilter, values) => {
        setFilter(values);
        setPagination((prev) => ({ ...prev, pageNumber: 1 }));
    };

    const getRowClass = (rowId) => {
        const isSelected = selectedRowId === rowId;
        return [
            'h-6 cursor-pointer border-b border-gray-100',
            isSelected ? 'odd:bg-lime-100/80 even:bg-lime-100/80' : 'odd:bg-white even:bg-gray-50',
            'hover:bg-lime-200/70',
        ].join(' ');
    };

    const handleOpenCustomer = (event, rowId) => {
        event.preventDefault();
        event.stopPropagation();
        navigate(`/order/customers/${rowId}`);
    };

    const handleExportExcel = async (exportAll) => {
        if (exporting) return;

        setExporting(true);
        setExportError('');

        try {
            let exportRows = rows;
            if (exportAll) {
                const response = await apiClient.post('/customers/search', {
                    ...requestBody,
                    pagination: null,
                });
                exportRows = response?.data?.items ?? [];
            }

            if (exportRows.length === 0) {
                setExportError('Det finns inga kunder att exportera.');
                return;
            }

            const { default: ExcelJS } = await import('exceljs');
            const workbook = new ExcelJS.Workbook();
            const worksheet = workbook.addWorksheet('Kunder');
            worksheet.addRow(columns.map((column) => column.label));

            exportRows.forEach((row) => {
                worksheet.addRow([
                    row.name ?? '',
                    getUserName(row.responsibleUserId),
                    getUserName(row.supportEmployeeId),
                    row.category ?? '',
                    row.isProspect ? 'Ja' : 'Nej',
                    row.isComplete ? 'Komplett' : 'Ej komplett',
                    row.lastActivity ?? '',
                ]);
            });

            worksheet.getRow(1).font = { bold: true };
            worksheet.autoFilter = {
                from: { row: 1, column: 1 },
                to: { row: 1, column: columns.length },
            };
            worksheet.views = [{ state: 'frozen', ySplit: 1 }];
            worksheet.columns.forEach((column, index) => {
                column.width = index === 5 ? 34 : Math.min(32, Math.max(12, columns[index].label.length + 2));
            });

            const buffer = await workbook.xlsx.writeBuffer();
            const url = URL.createObjectURL(new Blob([buffer], {
                type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
            }));
            const link = document.createElement('a');
            link.href = url;
            link.download = `kunder-${exportAll ? 'alla' : 'visade'}.xlsx`;
            document.body.appendChild(link);
            link.click();
            link.remove();
            window.setTimeout(() => URL.revokeObjectURL(url), 0);
        } catch (error) {
            console.error('Failed to export customers to Excel:', error);
            setExportError('Kundlistan kunde inte exporteras till Excel. Försök igen.');
        } finally {
            setExporting(false);
        }
    };

    return (
        <div className="flex h-full flex-col pt-3 pb-4 ps-10 pe-0">
            <div className="mt-2 flex items-center gap-4 overflow-x-auto whitespace-nowrap pb-2">
                <ActionButton
                    label="Ny kund"
                    icon={Plus}
                    onClick={() => navigate('/order/customers/new')}
                    accent="lime"
                />

                <div className="relative ml-16 w-56">
                    <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
                    <input
                        type="text"
                        value={searchInput}
                        onChange={(event) => setSearchInput(event.target.value)}
                        placeholder="Sok kund"
                        className="h-6 w-full rounded-full border border-lime-600 bg-white pl-8 pr-4 text-xs text-gray-700 outline-none transition placeholder:text-gray-500 focus:border-lime-700"
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

                <div className="flex shrink-0 items-center gap-5 text-xs ml-20">
                    <button
                        type="button"
                        onClick={() => handleExportExcel(true)}
                        disabled={exporting || loading}
                        className="font-medium text-slate-500 transition-colors hover:text-slate-700 disabled:cursor-not-allowed disabled:text-gray-300"
                    >
                        {exporting ? 'Exporterar...' : 'Hämta allt Excel'}
                    </button>
                    <button
                        type="button"
                        onClick={() => handleExportExcel(false)}
                        disabled={exporting || loading || rows.length === 0}
                        className="font-medium text-slate-500 transition-colors hover:text-slate-700 disabled:cursor-not-allowed disabled:text-gray-300"
                    >
                        {exporting ? 'Exporterar...' : 'Hämta visade Excel'}
                    </button>
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

            {exportError && <p role="alert" className="mt-1 text-xs text-red-600">{exportError}</p>}

            <div className="mt-3 min-h-0 flex-1 overflow-auto border-t border-gray-300 py-1">
                <table className="table-fixed w-full border-collapse text-xs" style={TABLE_FONT_STYLE}>
                    <colgroup>
                        {columns.map((column) => (
                            <col key={column.key} {...(column.width ? { style: { width: column.width } } : {})} />
                        ))}
                    </colgroup>
                    <thead>
                        <tr className="text-tiny text-gray-500">
                            {columns.map((column) => {
                                const isSorted = sortConfig.key === column.sortBy;

                                return (
                                    <th
                                        key={column.key}
                                        onClick={() => handleSort(column)}
                                        className={`${column.sortBy ? 'cursor-pointer' : 'cursor-default'} px-2 pt-1 pb-2 text-tiny font-medium text-gray-500 ${column.align === 'right' ? 'text-right' : 'text-left'}`}
                                    >
                                        <span className="inline-flex items-center gap-1">
                                            {column.label}
                                            {column.sortBy ? (
                                                isSorted ? (
                                                    sortConfig.direction === 'asc' ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />
                                                ) : (
                                                    <ChevronUp className="h-3 w-3 opacity-0" />
                                                )
                                            ) : null}
                                        </span>
                                        {column.filter === 'seller' || column.filter === 'support' ? (
                                            <MultiSelectCheckboxFilter
                                                options={userOptions.map((user) => ({ value: user.id, label: user.name }))}
                                                selectedValues={column.filter === 'seller' ? sellerFilter : supportFilter}
                                                onChange={(values) => handleFilterChange(
                                                    column.filter === 'seller' ? setSellerFilter : setSupportFilter,
                                                    values,
                                                )}
                                            />
                                        ) : column.filter === 'category' ? (
                                            <MultiSelectCheckboxFilter
                                                options={categoryOptions.map((category) => ({ value: category, label: category }))}
                                                selectedValues={categoryFilter}
                                                onChange={(values) => handleFilterChange(setCategoryFilter, values)}
                                            />
                                        ) : column.filter === 'prospect' || column.filter === 'complete' ? (
                                            <MultiSelectCheckboxFilter
                                                options={column.filter === 'prospect'
                                                    ? [{ value: true, label: 'Ja' }, { value: false, label: 'Nej' }]
                                                    : [{ value: true, label: 'Komplett' }, { value: false, label: 'Ej komplett' }]}
                                                selectedValues={column.filter === 'prospect' ? prospectFilter : completeFilter}
                                                onChange={(values) => handleFilterChange(
                                                    column.filter === 'prospect' ? setProspectFilter : setCompleteFilter,
                                                    values,
                                                )}
                                            />
                                        ) : null}
                                    </th>
                                );
                            })}
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
                                <td colSpan={columns.length} className="px-4 py-8 text-center text-gray-400">Inga kunder hittades.</td>
                            </tr>
                        ) : (
                            rows.map((row) => (
                                <tr
                                    key={row.id}
                                    className={getRowClass(row.id)}
                                    onClick={() => setSelectedRowId((prev) => (prev === row.id ? null : row.id))}
                                >
                                    <td className="truncate px-2 py-1 text-gray-800"                                    >
                                        <button
                                            type="button"
                                            onClick={(event) => handleOpenCustomer(event, row.id)}
                                            className="text-sky-700 decoration-sky-300 underline-offset-2 hover:underline hover:text-sky-800"
                                        >
                                            {row.name}
                                        </button>
                                    </td>
                                    <td className="truncate px-2 py-1 text-gray-800">{getUserName(row.responsibleUserId)}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{getUserName(row.supportEmployeeId)}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.category ?? ''}</td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.isProspect ? 'Ja' : 'Nej'}</td>
                                    <td className="px-2 py-1">
                                        <span className={`inline-flex items-center whitespace-nowrap rounded-full px-2 py-[1px] text-tiny font-medium ${row.isComplete ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800'}`}>
                                            {row.isComplete ? 'Komplett' : 'Ej komplett'}
                                        </span>
                                    </td>
                                    <td className="truncate px-2 py-1 text-gray-800">{row.lastActivity}</td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

export default CustomerSearch;