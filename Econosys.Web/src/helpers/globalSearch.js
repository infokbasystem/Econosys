import apiClient from '../config/apiClient.js';

const PAGE_SIZE = 5;

const getCalculationId = (term) => {
    if (!/^\d+$/.test(term)) return null;
    const id = Number(term);
    return id >= 1 && id <= 2147483647 ? id : null;
};

export const canSearchHeader = (searchText) => {
    const term = searchText.trim();
    return term.length >= 4 || getCalculationId(term) !== null;
};

const getItems = (data) => {
    if (Array.isArray(data)) return data;
    return Array.isArray(data?.items) ? data.items : [];
};

const createGroup = async ({ key, label, endpoint, request, mapResult }) => {
    try {
        const response = await apiClient.post(endpoint, request);
        const results = getItems(response?.data).map(mapResult);
        return { key, label, results, failed: false };
    } catch {
        return { key, label, results: [], failed: true };
    }
};

const result = (row, number, name, details, date, path) => ({
    id: row.id,
    number: number ?? '',
    name: name ?? '',
    details: details ?? '',
    date: date ?? null,
    path: path(row.id),
});

const pageRequest = { pagination: { pageNumber: 1, pageSize: PAGE_SIZE } };

export const searchHeader = async (searchText) => {
    const term = searchText.trim();
    const isNumeric = /^\d+$/.test(term);
    const calculationId = getCalculationId(term);
    const searchOtherGroups = term.length >= 4;
    const searches = [];

    if (calculationId !== null) {
        searches.push(createGroup({
            key: 'calculations',
            label: 'KALKYLER',
            endpoint: '/calculations/search',
            request: {
                ...pageRequest,
                filter: { conditions: [{ field: 'id', operator: 'eq', value: calculationId }] },
            },
            mapResult: (row) => result(
                row,
                row.id,
                row.productName,
                [row.customerName, row.supplierName].filter(Boolean).join(' · '),
                row.createdAt,
                (id) => `/order/calculations/${encodeURIComponent(id)}`,
            ),
        }));
    }

    if (searchOtherGroups && !isNumeric) {
        searches.push(createGroup({
            key: 'products',
            label: 'PRODUKTER',
            endpoint: '/products/search',
            request: {
                ...pageRequest,
                filter: { conditions: [{ field: 'name', operator: 'contains', value: term }] },
            },
            mapResult: (row) => result(
                row,
                row.productCode || row.id,
                row.name,
                [row.construction?.name, row.materialThickness, row.format].filter(Boolean).join(' · '),
                null,
                (id) => `/order/products/${encodeURIComponent(id)}`,
            ),
        }));
    }

    if (term.length >= 5) {
        searches.push(createGroup({
            key: 'supplierOrders',
            label: 'BESTÄLLNINGAR',
            endpoint: '/supplierorders/search',
            request: { ...pageRequest, searchTerm: term },
            mapResult: (row) => result(
                row,
                row.supplierOrderNr,
                row.supplierName,
                row.customerOrderNr,
                row.created,
                (id) => `/order/supplierorders/${encodeURIComponent(id)}`,
            ),
        }));
    }

    if (searchOtherGroups && (isNumeric || term.length >= 5)) {
        searches.push(createGroup({
            key: 'customerOrders',
            label: 'KUNDORDER',
            endpoint: '/customerorders/search',
            request: {
                ...pageRequest,
                searchTerm: term,
                orderBy: [{ field: 'created', direction: 'desc' }],
            },
            mapResult: (row) => result(
                row,
                row.customerOrderNr,
                row.customerName,
                row.product,
                row.created,
                (id) => `/order/customerorders/${encodeURIComponent(id)}`,
            ),
        }));
    }

    if (searchOtherGroups) {
        searches.push(createGroup({
            key: 'transportOrders',
            label: 'TRANSPORTORDER',
            endpoint: '/transportorder/search',
            request: { searchTerm: term },
            mapResult: (row) => result(
                row,
                row.number,
                row.name,
                row.details,
                row.date,
                (id) => `/logistics/transportorder/${encodeURIComponent(id)}`,
            ),
        }));

        searches.push(createGroup({
            key: 'callOffs',
            label: 'AVROP',
            endpoint: '/calloff/search',
            request: { searchTerm: term },
            mapResult: (row) => result(
                row,
                row.number,
                row.name,
                row.details,
                row.date,
                (id) => `/logistics/calloff/${encodeURIComponent(id)}`,
            ),
        }));
    }

    if (searchOtherGroups && !isNumeric) {
        searches.push(createGroup({
            key: 'suppliers',
            label: 'LEVERANTÖRER',
            endpoint: '/suppliers/search',
            request: { ...pageRequest, searchTerm: term },
            mapResult: (row) => result(
                row,
                row.id,
                row.name,
                row.reference || row.email,
                null,
                (id) => `/order/suppliers/${encodeURIComponent(id)}`,
            ),
        }));

        searches.push(createGroup({
            key: 'customers',
            label: 'KUNDER',
            endpoint: '/customers/search',
            request: { ...pageRequest, searchTerm: term },
            mapResult: (row) => result(
                row,
                row.id,
                row.name,
                row.orgNr,
                null,
                (id) => `/order/customers/${encodeURIComponent(id)}`,
            ),
        }));
    }

    if (searchOtherGroups && isNumeric) {
        searches.push(createGroup({
            key: 'invoices',
            label: 'FAKTUROR',
            endpoint: '/invoices/header-search',
            request: { searchTerm: term },
            mapResult: (row) => result(
                row,
                row.number,
                row.name,
                '',
                row.date,
                (id) => `/finance/invoice/${encodeURIComponent(id)}`,
            ),
        }));
    }

    const groups = await Promise.all(searches);
    return {
        groups: groups.filter((group) => group.results.length > 0),
        hadErrors: groups.some((group) => group.failed),
    };
};