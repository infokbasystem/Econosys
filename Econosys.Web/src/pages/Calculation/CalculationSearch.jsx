import { useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeftCircle, ArrowRightCircle, ChevronDown, ChevronUp, FileSpreadsheet, LoaderCircle, RefreshCw, Search } from 'lucide-react';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import apiClient from '../../config/apiClient';
import ActionButton from '../../components/ActionButton';
import { formatDateShort } from '../../helpers/dateUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';

const PAGE_SIZE = 25;
const TABLE_FONT_STYLE = { fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" };
const EMPTY_FILTERS = { productname: '', suppliername: '', customername: '', id: '', supplierordernr: '' };
const FILTER_FIELDS = [
    { key: 'productname', label: 'Produktnamn', width: 'w-48' },
    { key: 'suppliername', label: 'Leverantör', width: 'w-40' },
    { key: 'customername', label: 'Kund', width: 'w-40' },
    { key: 'id', label: 'Kalkylnr', width: 'w-28' },
    { key: 'supplierordernr', label: 'Ordernr', width: 'w-32' },
];
const COLUMNS = [
    { key: 'id', label: 'Nr', width: '6%' },
    { key: 'productname', property: 'productName', label: 'Produktnamn', width: '18%' },
    { key: 'productmaterial', property: 'productMaterial', label: 'Material', width: '16%' },
    { key: 'productconstruction', property: 'productConstruction', label: 'Konstruktion', width: '12%' },
    { key: 'productformat', property: 'productFormat', label: 'Format', width: '12%' },
    { key: 'suppliername', property: 'supplierName', label: 'Leverantör', width: '12%' },
    { key: 'customername', property: 'customerName', label: 'Kund', width: '12%' },
    { key: 'createdat', property: 'createdAt', label: 'Skapad', width: '6%', isDate: true },
    { key: 'editedat', property: 'editedAt', label: 'Redigerad', width: '6%', isDate: true },
];

const getNumberError = (value) => {
    const number = value.trim();
    if (number && (!/^\d+$/.test(number) || Number(number) < 1 || Number(number) > 2147483647)) {
        return 'Kalkylnr måste vara ett heltal mellan 1 och 2147483647.';
    }

    return '';
};

const getCellValue = (row, column) => {
    const value = row[column.property ?? column.key];
    return column.isDate ? formatDateShort(value) : (value ?? '');
};

const CalculationSearch = () => {
    const [filters, setFilters] = useState(EMPTY_FILTERS);
    const [appliedFilters, setAppliedFilters] = useState(EMPTY_FILTERS);
    const [pageNumber, setPageNumber] = useState(1);
    const [sortConfig, setSortConfig] = useState({ key: 'id', direction: 'desc' });
    const [selectedRowId, setSelectedRowId] = useState(null);
    const [retryCount, setRetryCount] = useState(0);
    const [result, setResult] = useState(null);
    const [exporting, setExporting] = useState(false);
    const [exportError, setExportError] = useState('');
    const exportController = useRef(null);

    const numberError = getNumberError(filters.id) || getNumberError(appliedFilters.id);
    const filtersPending = FILTER_FIELDS.some(({ key }) => filters[key] !== appliedFilters[key]);

    useEffect(() => () => exportController.current?.abort(), []);

    useEffect(() => {
        const timer = setTimeout(() => {
            setAppliedFilters(filters);
            setPageNumber(1);
        }, 300);

        return () => clearTimeout(timer);
    }, [filters]);

    const payload = useMemo(() => ({
        filter: {
            conditions: FILTER_FIELDS
                .filter(({ key }) => appliedFilters[key].trim())
                .map(({ key }) => ({
                    field: key,
                    operator: key === 'id' ? 'eq' : 'contains',
                    value: key === 'id' ? Number(appliedFilters[key]) : appliedFilters[key].trim(),
                })),
        },
        pagination: { pageNumber, pageSize: PAGE_SIZE },
        orderBy: [{ field: sortConfig.key, direction: sortConfig.direction }],
    }), [appliedFilters, pageNumber, sortConfig]);

    const requestKey = `calculations:search:${JSON.stringify(payload)}:${retryCount}`;
    const loading = !numberError && result?.key !== requestKey;
    const rows = result?.data.items ?? [];
    const pagination = result?.data ?? {};
    const loadError = result?.key === requestKey ? result.error : '';
    const showSkeleton = loading && !result;

    useEffect(() => {
        if (numberError) return;
        let isActive = true;

        const loadRows = async () => {
            try {
                const response = await getSharedRequest(requestKey, () => apiClient.post('/calculations/search', payload));
                if (isActive) {
                    setResult({ key: requestKey, data: response?.data ?? {}, error: '' });
                    setSelectedRowId(null);
                }
            } catch (error) {
                console.error('Failed to load calculations search:', error);
                if (isActive) {
                    setResult({ key: requestKey, data: {}, error: 'Kalkylerna kunde inte hämtas.' });
                }
            }
        };

        loadRows();
        return () => {
            isActive = false;
        };
    }, [numberError, payload, requestKey]);

    const handleSort = (key) => {
        setSortConfig((previous) => ({
            key,
            direction: previous.key === key && previous.direction === 'asc' ? 'desc' : 'asc',
        }));
        setPageNumber(1);
    };

    const handleExportExcel = async () => {
        if (exportController.current || loading || filtersPending || numberError || loadError || !pagination.totalCount) return;

        const controller = new AbortController();
        exportController.current = controller;
        setExporting(true);
        setExportError('');

        try {
            const { default: ExcelJS } = await import('exceljs');
            const workbook = new ExcelJS.Workbook();
            const worksheet = workbook.addWorksheet('Kalkyler');
            worksheet.columns = COLUMNS.map((column) => ({
                header: column.label,
                key: column.key,
                width: column.isDate ? 14 : column.key === 'id' ? 10 : 28,
            }));
            worksheet.getRow(1).font = { bold: true };
            worksheet.views = [{ state: 'frozen', ySplit: 1 }];

            let exportPage = 1;
            let hasNextPage = true;
            while (hasNextPage) {
                controller.signal.throwIfAborted();
                const response = await apiClient.post('/calculations/search', {
                    ...payload,
                    pagination: { pageNumber: exportPage, pageSize: 200 },
                }, { signal: controller.signal });
                const data = response?.data;
                if (!Array.isArray(data?.items) || (data.items.length === 0 && data.hasNextPage)) {
                    throw new Error('Invalid calculation export response.');
                }

                data.items.forEach((row) => worksheet.addRow(COLUMNS.map((column) => getCellValue(row, column))));
                hasNextPage = Boolean(data.hasNextPage);
                exportPage += 1;
            }

            worksheet.autoFilter = {
                from: { row: 1, column: 1 },
                to: { row: worksheet.rowCount, column: COLUMNS.length },
            };
            const buffer = await workbook.xlsx.writeBuffer();
            controller.signal.throwIfAborted();
            const blob = new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            const timestamp = new Intl.DateTimeFormat('sv-SE', {
                timeZone: 'Europe/Stockholm',
                year: 'numeric',
                month: '2-digit',
                day: '2-digit',
                hour: '2-digit',
                minute: '2-digit',
            }).format(new Date()).replace(/[^0-9]/g, '');
            link.download = `Econosys-kalkyler-${timestamp}.xlsx`;
            document.body.appendChild(link);
            try {
                link.click();
            } finally {
                link.remove();
                setTimeout(() => URL.revokeObjectURL(url), 0);
            }
        } catch (error) {
            if (!controller.signal.aborted) {
                console.error('Failed to export calculations:', error);
                setExportError('Excel-filen kunde inte skapas. Försök igen.');
            }
        } finally {
            if (!controller.signal.aborted) {
                setExporting(false);
            }
            if (exportController.current === controller) {
                exportController.current = null;
            }
        }
    };

    return (
        <div className="flex h-full min-w-0 flex-col pt-3 pb-4 ps-4 md:ps-10 pe-0">
            <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-3 pb-2">
                <div className="shrink-0">
                    <ActionButton
                        label={exporting ? 'Exporterar' : 'Excel'}
                        icon={exporting ? LoaderCircle : FileSpreadsheet}
                        onClick={handleExportExcel}
                        disabled={exporting || loading || filtersPending || Boolean(numberError) || Boolean(loadError) || !pagination.totalCount}
                        accent="lime"
                    />
                </div>
                <div className="flex min-w-0 flex-wrap items-center gap-3">
                    {FILTER_FIELDS.map((field) => (
                        <div key={field.key} className={`relative max-w-full ${field.width}`}>
                            <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
                            <input
                                type="text"
                                inputMode={field.key === 'id' ? 'numeric' : 'text'}
                                aria-label={field.label}
                                aria-invalid={field.key === 'id' && Boolean(numberError)}
                                aria-describedby={field.key === 'id' && numberError ? 'calculation-number-error' : undefined}
                                value={filters[field.key]}
                                onChange={(event) => setFilters((previous) => ({ ...previous, [field.key]: event.target.value }))}
                                placeholder={field.label}
                                className="h-7 w-full rounded-full border border-lime-600 bg-white pl-8 pr-3 text-xs text-gray-700 outline-none transition placeholder:text-gray-500 focus:border-lime-700"
                            />
                        </div>
                    ))}
                </div>

                <div className="ml-auto flex max-w-full flex-wrap items-center gap-x-4 gap-y-2 text-xs text-gray-600" style={TABLE_FONT_STYLE}>
                    <span>Rader <strong>{pagination.totalCount ?? 0}</strong></span>
                    <span>Sida {pageNumber} av {Math.max(1, pagination.totalPages ?? 0)}</span>
                    <div className="flex gap-1">
                        <button
                            type="button"
                            aria-label="Föregående sida"
                            title="Föregående sida"
                            onClick={() => setPageNumber((previous) => previous - 1)}
                            disabled={loading || filtersPending || Boolean(numberError) || !pagination.hasPreviousPage}
                            className="disabled:cursor-not-allowed disabled:opacity-40"
                        >
                            <ArrowLeftCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                        </button>
                        <button
                            type="button"
                            aria-label="Nästa sida"
                            title="Nästa sida"
                            onClick={() => setPageNumber((previous) => previous + 1)}
                            disabled={loading || filtersPending || Boolean(numberError) || !pagination.hasNextPage}
                            className="disabled:cursor-not-allowed disabled:opacity-40"
                        >
                            <ArrowRightCircle className="h-5 w-5 text-red-400 hover:text-red-500" />
                        </button>
                    </div>
                </div>
            </div>

            {numberError && <p id="calculation-number-error" role="alert" className="mt-2 text-xs text-red-600">{numberError}</p>}
            {loadError && (
                <div role="alert" className="mt-2 flex items-center gap-4 text-xs text-red-600">
                    <span>{loadError}</span>
                    <ActionButton label="Försök igen" icon={RefreshCw} onClick={() => setRetryCount((previous) => previous + 1)} accent="lime" />
                </div>
            )}
            {exportError && <p role="alert" className="mt-2 text-xs text-red-600">{exportError}</p>}

            <div className="mt-3 min-h-0 min-w-0 flex-1 overflow-auto border-t border-gray-300 py-1" aria-busy={loading}>
                <table className="w-full min-w-[1200px] table-fixed border-collapse text-xs" style={TABLE_FONT_STYLE}>
                    <colgroup>
                        {COLUMNS.map((column) => <col key={column.key} style={{ width: column.width }} />)}
                    </colgroup>
                    <thead>
                        <tr className="text-tiny text-gray-500">
                            {COLUMNS.map((column) => (
                                <th
                                    key={column.key}
                                    scope="col"
                                    aria-sort={sortConfig.key === column.key ? (sortConfig.direction === 'asc' ? 'ascending' : 'descending') : 'none'}
                                    className="px-2 pt-1 pb-2 text-left text-tiny font-medium text-gray-500"
                                >
                                    <button type="button" onClick={() => handleSort(column.key)} className="inline-flex items-center gap-1">
                                        {column.label}
                                        {sortConfig.key === column.key ? (
                                            sortConfig.direction === 'asc' ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />
                                        ) : <ChevronUp className="h-3 w-3 opacity-0" />}
                                    </button>
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody className={rows.length > 0 ? 'bg-white' : 'bg-transparent'}>
                        {showSkeleton ? (
                            Array.from({ length: 12 }, (_, index) => (
                                <tr key={index}>
                                    <td colSpan={COLUMNS.length} className="px-2 py-1"><Skeleton height={16} /></td>
                                </tr>
                            ))
                        ) : rows.length === 0 ? (
                            <tr>
                                <td colSpan={COLUMNS.length} className="px-4 py-8 text-center text-gray-400">
                                    {loadError ? 'Kunde inte läsa sökresultatet.' : numberError ? 'Kontrollera kalkylnumret.' : 'Inga kalkyler hittades.'}
                                </td>
                            </tr>
                        ) : rows.map((row) => (
                            <tr
                                key={row.id}
                                onClick={() => setSelectedRowId((previous) => previous === row.id ? null : row.id)}
                                className={`h-6 cursor-pointer border-b border-gray-100 hover:bg-lime-200/70 ${selectedRowId === row.id ? 'bg-lime-100/80' : ''}`}
                            >
                                {COLUMNS.map((column) => (
                                    <td key={column.key} title={String(getCellValue(row, column))} className="truncate px-2 py-1 text-gray-800">
                                        {column.key === 'id' || column.key === 'productname' ? (
                                            <Link
                                                to={`/order/calculations/${row.id}`}
                                                onClick={(event) => event.stopPropagation()}
                                                className="text-sky-700 decoration-sky-300 underline-offset-2 hover:underline hover:text-sky-800"
                                            >
                                                {getCellValue(row, column) || row.id}
                                            </Link>
                                        ) : getCellValue(row, column)}
                                    </td>
                                ))}
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

export default CalculationSearch;