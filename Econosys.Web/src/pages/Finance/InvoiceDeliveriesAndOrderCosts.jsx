import React, { useEffect, useMemo, useState } from 'react';
import { ChevronDown, ChevronUp, ChevronsUpDown, Mail } from 'lucide-react';
import SwitchSelector from 'react-switch-selector';
import LabeledReactSelect from '../../components/LabeledReactSelect';
import apiClient from '../../config/apiClient';
import { getSharedRequest } from '../../helpers/sharedRequest';

const DRAFT_STORAGE_KEY_PREFIX = 'invoice-draft:';

const buildSelectionKey = (type, id) => {
    if (!type || id == null || id === '') return '';
    return `${String(type).toLowerCase()}:${String(id)}`;
};

const isDeliveryType = (type) => {
    const normalized = String(type ?? '').toLowerCase();
    return normalized === 'deliverytocustomer' || normalized === 'deliveryfromstock';
};

const isOrderCostType = (type) => String(type ?? '').toLowerCase().startsWith('ordercost');

const formatAmount = (value) => {
    if (value == null) return '';
    return new Intl.NumberFormat('sv-SE', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    }).format(Number(value));
};

const toArray = (value) => (Array.isArray(value) ? value : []);

const toDateString = (value) => {
    if (!value) return '';
    const date = String(value);
    return date.includes('T') ? date.split('T')[0] : date;
};

const getRequestErrorMessage = (error) => {
    const responseData = error?.response?.data;

    if (typeof responseData === 'string' && responseData.trim()) {
        return responseData.trim();
    }

    if (responseData && typeof responseData === 'object') {
        if (typeof responseData.message === 'string' && responseData.message.trim()) {
            return responseData.message.trim();
        }

        const validationErrors = responseData.errors;
        if (validationErrors && typeof validationErrors === 'object') {
            const firstError = Object.values(validationErrors)
                .flat()
                .find((msg) => typeof msg === 'string' && msg.trim());
            if (firstError) {
                return firstError.trim();
            }
        }
    }

    if (typeof error?.message === 'string' && error.message.trim()) {
        return error.message.trim();
    }

    return 'Okänt fel vid hämtning av underlag.';
};

const buildPills = ({ hasMissingWeight = false, isWeightMissingEmailSent = false, hasMissingCostSalesPrice = false, isCostMissingEmailSent = false, isSupplierOrder = false } = {}) => {
    const pills = [];

    if (hasMissingWeight) {
        if (isWeightMissingEmailSent) {
            pills.push({ label: 'Viktmail skickat', color: 'bg-lime-600', icon: false });
        }
        else {
            pills.push({ label: 'Vikt', color: 'bg-red-400', icon: true });
        }
    }
    if (hasMissingCostSalesPrice) {
        if (isCostMissingEmailSent) {
            pills.push({ label: 'Kostnadsmail skickat', color: 'bg-lime-600', icon: false });
        } else {
            pills.push({ label: 'Kostnad', color: 'bg-red-400', icon: true });
        }
    }
    if (isSupplierOrder) pills.push({ label: 'Förskott', color: 'bg-sky-500', icon: false });

    return pills;
};

const normalizeGroupedCustomer = (raw) => {
    const deliveries = toArray(raw?.deliveries);
    const orderCosts = toArray(raw?.orderCosts);
    const supplierOrders = toArray(raw?.supplierOrders);

    return {
        customerId: raw?.customerId ?? null,
        customerName: raw?.customerName ?? '',
        invoicingInfo: raw?.customerInvoicingInfo ?? '',
        deliveries: deliveries.map((d) => {
            const hasMissingWeight = Boolean(d?.hasMissingWeight);
            const isWeightMissingEmailSent = Boolean(d?.isWeightMissingEmailSent);
            const hasMissingCostSalesPrice = Boolean(d?.hasMissingCostSalesPrice);
            const isCostMissingEmailSent = Boolean(d?.isCostMissingEmailSent);

            return {
                id: String(d?.deliveryId ?? ''),
                deliveryId: d?.deliveryId ?? null,
                selectionKey: buildSelectionKey(d?.type, d?.deliveryId),
                parentDeliveryId: d?.parentDeliveryId ?? 0,
                leveranstyp: d?.type ?? '',
                leverantor: d?.supplierName ?? '',
                leveransdatum: toDateString(d?.deliveryDate ?? ''),
                avropsnummer: d?.callOffNr ?? '',
                customerOrderId: d?.customerOrderId ?? null,
                ordernr: d?.customerOrderNr ?? '',
                kundsOrdernr: d?.customersOwnOrderNr ?? '',
                antal: d?.quantity ?? 0,
                pris: d?.purchasePrice ?? 0,
                nrOfPalletsToInvoice: d?.nrOfNotInvoicedPallets ?? 0,
                hasMissingWeight,
                hasMissingCostSalesPrice,
                pills: buildPills({
                    hasMissingWeight,
                    isWeightMissingEmailSent,
                    hasMissingCostSalesPrice,
                    isCostMissingEmailSent,
                }),
            };
        }),
        orderCosts: orderCosts.map((oc) => {
            const hasMissingCostSalesPrice = Boolean(oc?.hasMissingCostSalesPrice);

            return {
                id: String(oc?.id ?? ''),
                orderCostId: oc?.id ?? null,
                selectionKey: buildSelectionKey(oc?.type, oc?.id),
                leveranstyp: oc?.type ?? '',
                info: oc?.costName ?? '',
                customerOrderId: oc?.customerOrderId ?? null,
                ordernr: oc?.customerOrderNr ?? '',
                antal: oc?.quantity ?? 0,
                pris: oc?.purchasePrice ?? 0,
                hasMissingCostSalesPrice,
                pills: buildPills({
                    hasMissingCostSalesPrice,
                }),
            };
        }),
        supplierorders: supplierOrders.map((so) => {
            const hasMissingCostSalesPrice = Boolean(so?.hasMissingCostSalesPrice);

            return {
                id: String(so?.supplierOrderId ?? ''),
                selectionKey: buildSelectionKey('SupplierOrder', so?.supplierOrderId),
                leveranstyp: 'SupplierOrder',
                leverantor: so?.supplierName ?? '',
                ordernr: so?.supplierOrderNr ?? '',
                kundsOrdernr: '',
                antal: so?.quantity ?? 0,
                pris: so?.purchasePrice ?? 0,
                hasMissingCostSalesPrice,
                pills: buildPills({
                    hasMissingCostSalesPrice,
                    isSupplierOrder: true,
                }),
            };
        }),
    };
};

const toListTypeLabel = (type) => {
    const normalized = String(type ?? '').toLowerCase();
    if (normalized === 'deliverytocustomer') return 'Direkt till kund';
    if (normalized === 'deliveryfromstock') return 'Lagerleverens';
    if (normalized === 'ordercost') return '(faktureras med leverans)';
    if (normalized === 'ordercostseparately') return '(faktureras separat)';
    if (normalized === 'ordercostimmediateseparately') return '(faktureras omgående separat)';
    if (normalized === 'supplierorder') return 'Beställning';
    return type ?? '';
};

const normalizeViewMode = (value) => {
    if (value === 'grouped' || value === 'list') return value;
    if (typeof value === 'number') {
        const numeric = Number(value);
        // react-switch-selector can emit either selected index (0/1) or numeric option values.
        if (numeric === 0) return 'grouped';
        if (numeric === 1) return 'list';
        if (numeric === 2) return 'list';
        return 'grouped';
    }

    const candidate = value?.value;
    if (candidate === 'grouped' || candidate === 'list') return candidate;

    return 'grouped';
};

const mergeDeliveriesForDisplay = (deliveries) => {
    const cloned = deliveries.map((delivery) => ({
        ...delivery,
        displaySelectionKeys: [delivery.selectionKey],
    }));

    const byId = new Map(cloned.map((delivery) => [delivery.id, delivery]));
    const hiddenDeliveryIds = new Set();

    const mergePills = (target, source) => {
        const combined = [...(target.pills ?? []), ...(source.pills ?? [])];
        const seen = new Set();

        target.pills = combined.filter((pill) => {
            const key = `${pill.label}|${pill.color}|${pill.icon ? '1' : '0'}`;
            if (seen.has(key)) {
                return false;
            }

            seen.add(key);
            return true;
        });
    };

    const findRootParent = (delivery) => {
        const visited = new Set();
        let current = delivery;

        while (current?.parentDeliveryId) {
            const parentId = String(current.parentDeliveryId);
            if (parentId === current.id || visited.has(parentId)) {
                break;
            }

            const parent = byId.get(parentId);
            if (!parent) {
                break;
            }

            visited.add(parentId);
            current = parent;
        }

        return current;
    };

    cloned.forEach((delivery) => {
        if (!delivery.parentDeliveryId) {
            return;
        }

        const rootParent = findRootParent(delivery);
        if (!rootParent || rootParent.id === delivery.id) {
            return;
        }

        rootParent.antal = Number(rootParent.antal ?? 0) + Number(delivery.antal ?? 0);
        rootParent.nrOfPalletsToInvoice = Number(rootParent.nrOfPalletsToInvoice ?? 0) + Number(delivery.nrOfPalletsToInvoice ?? 0);
        mergePills(rootParent, delivery);
        rootParent.displaySelectionKeys = [...rootParent.displaySelectionKeys, ...delivery.displaySelectionKeys];
        hiddenDeliveryIds.add(delivery.id);
    });

    return cloned
        .filter((delivery) => !hiddenDeliveryIds.has(delivery.id))
        .map((delivery) => ({
            ...delivery,
            displaySelectionKeys: Array.from(new Set(delivery.displaySelectionKeys)),
        }));
};

const normalizeListRow = (raw, index) => {
    const type = raw?.type ?? '';
    const deliveryId = raw?.deliveryId ?? null;
    const orderCostId = raw?.orderCostId ?? null;
    const supplierOrderId = raw?.supplierOrderId ?? null;
    const selectionId = deliveryId ?? orderCostId ?? supplierOrderId;

    const hasMissingWeight = Boolean(raw?.hasMissingWeight);
    const hasMissingCostSalesPrice = Boolean(raw?.hasMissingCostSalesPrice);

    const pills = [];
    if (hasMissingWeight) pills.push({ label: 'Vikt', color: 'bg-red-400', icon: true });
    if (hasMissingCostSalesPrice) pills.push({ label: 'Kostnad', color: 'bg-red-400', icon: true });
    if (String(type).toLowerCase() === 'supplierorder') pills.push({ label: 'Förskott', color: 'bg-sky-500', icon: false });

    return {
        id: selectionId != null ? String(selectionId) : `row-${index}`,
        rowKey: `${String(type ?? 'row')}-${selectionId != null ? String(selectionId) : String(index)}`,
        selectionKeys: [buildSelectionKey(type, selectionId != null ? String(selectionId) : `row-${index}`)],
        customerId: raw?.customerId ?? null,
        customerOrderId: raw?.customerOrderId ?? null,
        customerName: raw?.customerName ?? '',
        leverantor: raw?.supplierName ?? '',
        itemType: toListTypeLabel(type),
        type,
        deliveryId,
        orderCostId,
        parentDeliveryId: raw?.parentDeliveryId ?? null,
        leveransdatum: toDateString(raw?.deliveryDate ?? ''),
        avropsnummer: raw?.callOffNr ?? '',
        ordernr: raw?.customerOrderNr ?? raw?.supplierOrderNr ?? '',
        kundsOrdernr: raw?.customersOwnOrderNr ?? '',
        antal: raw?.quantity ?? 0,
        salesprice: raw?.purchasePrice ?? 0,
        weightPer1000: raw?.weightPer1000 ?? null,
        nrOfPalletsToInvoice: raw?.nrOfNotInvoicedPallets ?? 0,
        pills,
        orderInfo: raw?.costName ?? raw?.customerInvoicingInfo ?? '',
    };
};

const mergeListDeliveriesForDisplay = (rows) => {
    const mergedRows = rows.map((row) => ({
        ...row,
        selectionKeys: row.selectionKeys?.length ? row.selectionKeys : [buildSelectionKey(row.type, row.id)],
    }));

    const deliveryRowsByDeliveryId = new Map(
        mergedRows
            .filter((row) => row.deliveryId != null && isDeliveryType(row.type))
            .map((row) => [String(row.deliveryId), row])
    );

    const hiddenRowKeys = new Set();

    const mergePills = (target, source) => {
        const combined = [...(target.pills ?? []), ...(source.pills ?? [])];
        const seen = new Set();

        target.pills = combined.filter((pill) => {
            const key = `${pill.label}|${pill.color}|${pill.icon ? '1' : '0'}`;
            if (seen.has(key)) {
                return false;
            }

            seen.add(key);
            return true;
        });
    };

    const findRootParent = (row) => {
        const visited = new Set();
        let current = row;

        while (current?.parentDeliveryId != null && Number(current.parentDeliveryId) > 0) {
            const parentId = String(current.parentDeliveryId);
            if (current.deliveryId != null && parentId === String(current.deliveryId)) {
                break;
            }

            if (visited.has(parentId)) {
                break;
            }

            const parent = deliveryRowsByDeliveryId.get(parentId);
            if (!parent) {
                break;
            }

            visited.add(parentId);
            current = parent;
        }

        return current;
    };

    mergedRows.forEach((row) => {
        if (!isDeliveryType(row.type) || row.deliveryId == null || row.parentDeliveryId == null || Number(row.parentDeliveryId) <= 0) {
            return;
        }

        const rootParent = findRootParent(row);
        if (!rootParent || rootParent.rowKey === row.rowKey) {
            return;
        }

        rootParent.antal = Number(rootParent.antal ?? 0) + Number(row.antal ?? 0);
        rootParent.nrOfPalletsToInvoice = Number(rootParent.nrOfPalletsToInvoice ?? 0) + Number(row.nrOfPalletsToInvoice ?? 0);
        mergePills(rootParent, row);
        rootParent.selectionKeys = Array.from(new Set([...(rootParent.selectionKeys ?? []), ...(row.selectionKeys ?? [])]));
        hiddenRowKeys.add(row.rowKey);
    });

    return mergedRows.filter((row) => !hiddenRowKeys.has(row.rowKey));
};

const viewModeOptions = [
    {
        label: <span className="py-[1px]">Per kund</span>,
        value: 'grouped',
        id: 1,
        index: 0,
        selectedBackgroundColor: '#f59e0b',
        fontColor: '#f5f6fa',
    },
    {
        label: <span className="py-[1px]">Lista</span>,
        value: 'list',
        id: 2,
        index: 1,
        selectedBackgroundColor: '#f59e0b',
        fontColor: '#f5f6fa',
    },
];

const deliveryScopeOptions = [
    {
        label: <span className="py-[1px]">Skall faktureras nu</span>,
        value: 'onlyDelivered',
        id: 1,
        index: 0,
        selectedBackgroundColor: '#f59e0b',
        fontColor: '#f5f6fa',
    },
    {
        label: <span className="py-[1px]">Inkl. kommande</span>,
        value: 'allDeliveries',
        id: 2,
        index: 1,
        selectedBackgroundColor: '#f59e0b',
        fontColor: '#f5f6fa',
    },
];

const listColumns = [
    { key: 'select', label: '', align: 'left', width: 'w-8', sortable: false },
    { key: 'customerName', label: 'Kund', align: 'left', width: 'w-[12%]', sortable: true, type: 'string' },
    { key: 'leverantor', label: 'Leverantör', align: 'left', width: 'w-[12%]', sortable: true, type: 'string' },
    { key: 'itemType', label: 'Typ', align: 'left', width: 'w-[9%]', sortable: true, type: 'string' },
    { key: 'leveransdatum', label: 'Lev.datum', align: 'left', width: 'w-[8%]', sortable: true, type: 'date' },
    { key: 'avropsnummer', label: 'Avropsnr', align: 'left', width: 'w-[8%]', sortable: true, type: 'string' },
    { key: 'ordernr', label: 'Ordernr', align: 'left', width: 'w-[8%]', sortable: true, type: 'string' },
    { key: 'kundsOrdernr', label: 'Kunds ordernr', align: 'left', width: 'w-[10%]', sortable: true, type: 'string' },
    { key: 'antal', label: 'Antal', align: 'right', width: 'w-[6%]', sortable: true, type: 'number' },
    { key: 'salesprice', label: 'Försäljningspris', align: 'right', width: 'w-[8%]', sortable: true, type: 'number' },
    { key: 'weightPer1000', label: 'Vikt/1000', align: 'right', width: 'w-[7%]', sortable: true, type: 'number' },
    { key: 'pills', label: '', align: 'left', width: 'w-[6%]', sortable: false },
    { key: 'orderInfo', label: 'Orderinfo', align: 'left', width: 'w-[10%]', sortable: true, type: 'string' },
];

const InvoiceDeliveriesAndOrderCosts = () => {
    const [viewMode, setViewMode] = useState('grouped');
    const [selectedCustomerId, setSelectedCustomerId] = useState('all');
    const [checkedDeliveries, setCheckedDeliveries] = useState({});
    const [listSortConfig, setListSortConfig] = useState({ key: 'customerName', direction: 'asc' });
    const [deliveryScope, setDeliveryScope] = useState('onlyDelivered');
    const [groupedCustomers, setGroupedCustomers] = useState([]);
    const [groupedDataError, setGroupedDataError] = useState('');
    const [listRows, setListRows] = useState([]);
    const [listDataError, setListDataError] = useState('');
    const [createInvoiceError, setCreateInvoiceError] = useState('');
    const [isCreatingDraft, setIsCreatingDraft] = useState(false);

    const handleViewModeChange = (nextMode) => {
        setViewMode(normalizeViewMode(nextMode));
    };

    const handleDeliveryScopeChange = (value) => {
        setDeliveryScope(value === 'allDeliveries' ? 'allDeliveries' : 'onlyDelivered');
    };

    useEffect(() => {
        let isActive = true;

        const loadData = async () => {
            if (viewMode === 'grouped') {
                try {
                    const requestConfig = {
                        params: {
                            onlyDelivered: deliveryScope === 'onlyDelivered',
                        },
                    };
                    const requestKey = `invoices:invoice-source:grouped:${deliveryScope === 'onlyDelivered'}`;
                    const response = await getSharedRequest(requestKey, () => apiClient.get('/invoices/invoice-source/grouped', requestConfig));
                    if (!isActive) return;
                    const customers = toArray(response?.data).map(normalizeGroupedCustomer);
                    setGroupedCustomers(customers);
                    setGroupedDataError('');
                } catch (error) {
                    console.error('Failed to load invoice source grouped data:', error);
                    if (!isActive) return;
                    setGroupedCustomers([]);
                    setGroupedDataError(getRequestErrorMessage(error));
                }
                return;
            }

            try {
                const requestConfig = {
                    params: {
                        onlyDelivered: deliveryScope === 'onlyDelivered',
                    },
                };
                const requestKey = `invoices:invoice-source:list:${deliveryScope === 'onlyDelivered'}`;
                const response = await getSharedRequest(requestKey, () => apiClient.get('/invoices/invoice-source/list', requestConfig));
                if (!isActive) return;
                const rows = toArray(response?.data).map((row, index) => normalizeListRow(row, index));
                setListRows(rows);
                setListDataError('');
            } catch (error) {
                console.error('Failed to load invoice source list data:', error);
                if (!isActive) return;
                setListRows([]);
                setListDataError(getRequestErrorMessage(error));
            }
        };

        void loadData();
        return () => {
            isActive = false;
        };
    }, [viewMode, deliveryScope]);

    const customerSelectItems = useMemo(() => {
        const fromApi = groupedCustomers
            .map((customer) => ({
                id: String(customer.customerId),
                name: customer.customerName,
            }))
            .filter((item) => item.id !== 'null' && item.id !== 'undefined' && item.name)
            .sort((a, b) => a.name.localeCompare(b.name, 'sv-SE'));

        return [{ id: 'all', name: 'Alla kunder' }, ...fromApi];
    }, [groupedCustomers]);

    const visibleCustomers =
        selectedCustomerId === 'all'
            ? groupedCustomers
            : groupedCustomers.filter((c) => String(c.customerId) === selectedCustomerId);

    const visibleListRows =
        selectedCustomerId === 'all'
            ? listRows
            : listRows.filter((r) => String(r.customerId) === selectedCustomerId);

    const displayListRows = useMemo(() => mergeListDeliveriesForDisplay(visibleListRows), [visibleListRows]);

    const sortedListRows = useMemo(() => {
        const activeColumn = listColumns.find((col) => col.key === listSortConfig.key);
        if (!activeColumn?.sortable) return displayListRows;

        const rows = [...displayListRows];
        const direction = listSortConfig.direction === 'asc' ? 1 : -1;

        rows.sort((left, right) => {
            const leftValue = left?.[activeColumn.key];
            const rightValue = right?.[activeColumn.key];

            if (activeColumn.type === 'number') {
                return ((Number(leftValue) || 0) - (Number(rightValue) || 0)) * direction;
            }

            if (activeColumn.type === 'date') {
                const leftDate = leftValue ? new Date(leftValue).getTime() : 0;
                const rightDate = rightValue ? new Date(rightValue).getTime() : 0;
                return (leftDate - rightDate) * direction;
            }

            return String(leftValue ?? '').localeCompare(String(rightValue ?? ''), 'sv-SE') * direction;
        });

        return rows;
    }, [displayListRows, listSortConfig]);

    const selectionLinkIndex = useMemo(() => {
        const deliveryMetaById = new Map();
        const deliveryIdsByCallOffNr = new Map();
        const orderCostIdsByCustomerOrderId = new Map();

        const addToSetMap = (map, key, value) => {
            if (!key || !value) return;

            const existing = map.get(key);
            if (existing) {
                existing.add(value);
                return;
            }

            map.set(key, new Set([value]));
        };

        groupedCustomers.forEach((customer) => {
            customer.deliveries?.forEach((delivery) => {
                const selectionKey = delivery.selectionKey;
                if (!selectionKey) return;

                const callOffNr = String(delivery.avropsnummer ?? '').trim();
                const customerOrderId = delivery.customerOrderId != null ? String(delivery.customerOrderId) : '';

                deliveryMetaById.set(selectionKey, { callOffNr, customerOrderId });
                addToSetMap(deliveryIdsByCallOffNr, callOffNr, selectionKey);
            });

            customer.orderCosts?.forEach((orderCost) => {
                const id = orderCost.selectionKey;
                const customerOrderId = orderCost.customerOrderId != null ? String(orderCost.customerOrderId) : '';
                addToSetMap(orderCostIdsByCustomerOrderId, customerOrderId, id);
            });
        });

        listRows.forEach((row) => {
            const normalizedType = String(row.type ?? '').toLowerCase();
            const isDelivery = isDeliveryType(normalizedType);
            const isOrderCost = row.orderCostId != null || normalizedType.startsWith('ordercost');

            if (isDelivery && row.deliveryId != null) {
                const id = buildSelectionKey(row.type, row.deliveryId);
                const callOffNr = String(row.avropsnummer ?? '').trim();
                const customerOrderId = row.customerOrderId != null ? String(row.customerOrderId) : '';

                deliveryMetaById.set(id, { callOffNr, customerOrderId });
                addToSetMap(deliveryIdsByCallOffNr, callOffNr, id);
            }

            if (isOrderCost) {
                const id = buildSelectionKey(row.type, row.orderCostId != null ? row.orderCostId : row.id);
                const customerOrderId = row.customerOrderId != null ? String(row.customerOrderId) : '';
                addToSetMap(orderCostIdsByCustomerOrderId, customerOrderId, id);
            }
        });

        return {
            deliveryMetaById,
            deliveryIdsByCallOffNr,
            orderCostIdsByCustomerOrderId,
        };
    }, [groupedCustomers, listRows]);

    const selectionEntityIndex = useMemo(() => {
        const index = new Map();

        groupedCustomers.forEach((customer) => {
            customer.deliveries?.forEach((delivery) => {
                if (!delivery.selectionKey) return;
                index.set(delivery.selectionKey, {
                    kind: 'delivery',
                    customerId: customer.customerId,
                    type: delivery.leveranstyp,
                    id: delivery.deliveryId,
                });
            });

            customer.orderCosts?.forEach((orderCost) => {
                if (!orderCost.selectionKey) return;
                index.set(orderCost.selectionKey, {
                    kind: 'orderCost',
                    customerId: customer.customerId,
                    type: orderCost.leveranstyp,
                    id: orderCost.orderCostId,
                });
            });

            customer.supplierorders?.forEach((supplierOrder) => {
                if (!supplierOrder.selectionKey) return;
                index.set(supplierOrder.selectionKey, {
                    kind: 'supplierOrder',
                    customerId: customer.customerId,
                    type: supplierOrder.leveranstyp,
                    id: supplierOrder.id,
                });
            });
        });

        listRows.forEach((row) => {
            const selectionKey = buildSelectionKey(row.type, row.deliveryId ?? row.orderCostId ?? row.id);
            if (!selectionKey) return;

            index.set(selectionKey, {
                kind: isDeliveryType(row.type) ? 'delivery' : isOrderCostType(row.type) ? 'orderCost' : 'other',
                customerId: row.customerId,
                type: row.type,
                id: row.deliveryId ?? row.orderCostId ?? row.id,
            });
        });

        return index;
    }, [groupedCustomers, listRows]);

    const expandWithLinkedIdsForDeliveryToggle = (deliveryIds) => {
        const ids = Array.from(new Set((deliveryIds ?? []).filter(Boolean).map((id) => String(id))));
        if (ids.length === 0) {
            return ids;
        }

        const expanded = new Set(ids);

        ids.forEach((id) => {
            const meta = selectionLinkIndex.deliveryMetaById.get(id);
            if (!meta) return;

            if (meta.callOffNr) {
                selectionLinkIndex.deliveryIdsByCallOffNr.get(meta.callOffNr)?.forEach((linkedDeliveryId) => {
                    expanded.add(linkedDeliveryId);
                });
            }

            if (meta.customerOrderId) {
                selectionLinkIndex.orderCostIdsByCustomerOrderId.get(meta.customerOrderId)?.forEach((linkedOrderCostId) => {
                    expanded.add(linkedOrderCostId);
                });
            }
        });

        return Array.from(expanded);
    };

    const toggleDelivery = (selectionKey) => {
        setCheckedDeliveries((prev) => ({
            ...prev,
            [selectionKey]: !prev[selectionKey],
        }));
    };

    const toggleDeliveryGroup = (selectionKeys) => {
        if (!selectionKeys?.length) return;

        setCheckedDeliveries((prev) => {
            const allChecked = selectionKeys.every((id) => prev[id]);
            const targetChecked = !allChecked;
            const idsToUpdate = expandWithLinkedIdsForDeliveryToggle(selectionKeys);
            const updated = { ...prev };

            idsToUpdate.forEach((id) => {
                updated[id] = targetChecked;
            });

            return updated;
        });
    };

    const toggleAllForCustomer = (customer) => {
        const allIds = customer.deliveries.map((d) => d.selectionKey).filter(Boolean);
        const allChecked = allIds.every((id) => checkedDeliveries[id]);
        setCheckedDeliveries((prev) => {
            const updated = { ...prev };
            allIds.forEach((id) => {
                updated[id] = !allChecked;
            });
            return updated;
        });
    };

    const handleCreateInvoice = async () => {
        const selectedKeys = Object.entries(checkedDeliveries)
            .filter(([, checked]) => checked)
            .map(([key]) => key);

        if (selectedKeys.length === 0) {
            setCreateInvoiceError('Välj minst en leverans eller orderkostnad innan fakturan skapas.');
            return;
        }

        const selectedEntities = selectedKeys
            .map((key) => selectionEntityIndex.get(key))
            .filter(Boolean);

        if (selectedEntities.length === 0) {
            setCreateInvoiceError('Valda poster kunde inte matchas mot underlaget. Ladda om sidan och försök igen.');
            return;
        }

        if (selectedEntities.some((entity) => entity.kind === 'supplierOrder' || entity.kind === 'other')) {
            setCreateInvoiceError('Valda förskottsbeställningar stöds inte för utkastsfaktura ännu.');
            return;
        }

        const distinctCustomerIds = Array.from(new Set(selectedEntities.map((entity) => String(entity.customerId ?? '')).filter(Boolean)));
        if (distinctCustomerIds.length !== 1) {
            setCreateInvoiceError('Alla valda poster måste tillhöra samma kund för att skapa ett fakturautkast.');
            return;
        }

        const deliveries = selectedEntities
            .filter((entity) => entity.kind === 'delivery')
            .map((entity) => ({ type: entity.type, id: Number(entity.id) }))
            .filter((entity) => entity.id > 0);

        const orderCostIds = selectedEntities
            .filter((entity) => entity.kind === 'orderCost')
            .map((entity) => Number(entity.id))
            .filter((entity) => entity > 0);

        const draftWindow = window.open('', '_blank');
        if (!draftWindow) {
            setCreateInvoiceError('Webbläsaren blockerade det nya fönstret för fakturautkastet. Tillåt popup-fönster och försök igen.');
            return;
        }

        setCreateInvoiceError('');
        setIsCreatingDraft(true);

        try {
            const response = await apiClient.post('/invoices/draft/from-selection', {
                customerId: Number(distinctCustomerIds[0]),
                deliveries,
                orderCostIds,
            });

            const draftKey = `${DRAFT_STORAGE_KEY_PREFIX}${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
            window.localStorage.setItem(draftKey, JSON.stringify(response?.data ?? null));
            draftWindow.location.href = `/finance/invoice/new?draftKey=${encodeURIComponent(draftKey)}&openedInNewTab=1`;
        } catch (error) {
            draftWindow.close();
            console.error('Failed to create draft invoice:', error);
            setCreateInvoiceError(getRequestErrorMessage(error));
        } finally {
            setIsCreatingDraft(false);
        }
    };

    const handleListSort = (key) => {
        const column = listColumns.find((col) => col.key === key);
        if (!column?.sortable) return;

        setListSortConfig((prev) => ({
            key,
            direction: prev.key === key && prev.direction === 'asc' ? 'desc' : 'asc',
        }));
    };

    const renderListSortIcon = (column) => {
        if (!column.sortable) return null;
        if (listSortConfig.key !== column.key) return <ChevronsUpDown className="h-3.5 w-3.5 text-gray-300" />;
        return listSortConfig.direction === 'asc'
            ? <ChevronUp className="h-3.5 w-3.5 text-gray-500" />
            : <ChevronDown className="h-3.5 w-3.5 text-gray-500" />;
    };

    return (
        <div className="flex flex-col h-full py-2 px-7">
            <div className="ml-5 text-sm text-gray-500">Fakturera leveranser och orderkostnader</div>

            {/* Toolbar */}
            <div className="flex flex-wrap items-center gap-22 mt-3 ml-5">

                <div className="flex items-center text-xs">
                    <div className="w-36">
                        <SwitchSelector
                            name="viewMode"
                            options={viewModeOptions}
                            initialSelectedIndex={viewMode === 'grouped' ? 0 : 1}
                            onChange={handleViewModeChange}
                            backgroundColor="#353b48"
                            fontColor="#374151"
                        />
                    </div>
                </div>

                <div className='w-70'>
                    <LabeledReactSelect
                        name='customer'
                        label='Kund'
                        value={selectedCustomerId}
                        items={customerSelectItems}
                        onChange={(e) => setSelectedCustomerId(e)}
                        disableInactive
                        labelWidth="w-10"
                    />
                </div>

                <div className="flex items-center text-xs">
                    <div className="w-60">
                        <SwitchSelector
                            name="deliveryScope"
                            options={deliveryScopeOptions}
                            initialSelectedIndex={deliveryScope === 'onlyDelivered' ? 0 : 1}
                            onChange={handleDeliveryScopeChange}
                            backgroundColor="#353b48"
                            fontColor="#374151"
                        />
                    </div>
                </div>


                <button
                    type="button"
                    onClick={handleCreateInvoice}
                    disabled={isCreatingDraft}
                    className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-amber-600 px-5 p-[5px]"
                >
                    {isCreatingDraft ? 'Skapar utkast...' : 'Skapa faktura för valda leveranser och kostnader'}
                </button>
            </div>

            {createInvoiceError && (
                <div className="mt-3 ml-5 mr-5 border border-red-300 bg-red-50 px-3 py-2 text-xs text-red-700 whitespace-pre-wrap">
                    Kunde inte skapa fakturautkast: {createInvoiceError}
                </div>
            )}

            {groupedDataError && viewMode === 'grouped' && (
                <div className="mt-3 ml-5 mr-5 border border-red-300 bg-red-50 px-3 py-2 text-xs text-red-700 whitespace-pre-wrap">
                    Kunde inte hämta grupperat underlag: {groupedDataError}
                </div>
            )}

            {listDataError && viewMode === 'list' && (
                <div className="mt-3 ml-5 mr-5 border border-red-300 bg-red-50 px-3 py-2 text-xs text-red-700 whitespace-pre-wrap">
                    Kunde inte hämta listunderlag: {listDataError}
                </div>
            )}

            {/* Customer groups */}
            {viewMode === 'grouped' && (
                <div className="border-t border-gray-300 py-1 mt-4 flex-1 overflow-auto" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>

                    <div className="flex flex-col gap-2">
                        {visibleCustomers.map((customer) => {
                            const displayDeliveries = mergeDeliveriesForDisplay(customer.deliveries);
                            const allDeliveryIds = customer.deliveries.map((d) => d.selectionKey).filter(Boolean);
                            const allChecked =
                                allDeliveryIds.length > 0 &&
                                allDeliveryIds.every((id) => checkedDeliveries[id]);
                            const someChecked =
                                !allChecked && allDeliveryIds.some((id) => checkedDeliveries[id]);

                            return (
                                <div
                                    key={customer.customerId}
                                    className="overflow-hidden"
                                >
                                    {/* Customer header */}
                                    <div className="px-4 py-1 border-b border-gray-200">
                                        <div className="text-xs text-gray-700 font-semibold">
                                            {customer.customerName}
                                        </div>
                                        <div className="pt-1 text-xs text-red-500">
                                            {customer.invoicingInfo || ''}
                                        </div>
                                    </div>

                                    <div className="overflow-x-auto">
                                        <table className="w-full table-fixed text-xs">
                                            <thead>
                                                <tr className="">
                                                    <th className="w-[30px]"></th>
                                                    <th className="w-[15%]"></th>
                                                    <th className="w-[13%]"></th>
                                                    <th className="w-[9%]"></th>
                                                    <th className="w-[6%]"></th>
                                                    <th className="w-[6%]"></th>
                                                    <th className="w-[12%]"></th>
                                                    <th className="w-[7%]"></th>
                                                    <th className="w-[8%]"></th>
                                                    <th className="w-[7%]"></th>
                                                    <th className="w-[23%]"></th>
                                                </tr>
                                            </thead>
                                            <tbody>
                                                {displayDeliveries?.length > 0 && (
                                                    displayDeliveries.map((d, idx) => {
                                                        const deliveryIdsToToggle = d.displaySelectionKeys?.length ? d.displaySelectionKeys : [d.selectionKey];
                                                        const deliveryGroupChecked = deliveryIdsToToggle.every((id) => checkedDeliveries[id]);

                                                        return (
                                                            <tr
                                                                key={d.id}
                                                                className={
                                                                    'border-b border-gray-100 ' +
                                                                    (deliveryGroupChecked
                                                                        ? 'bg-red-50'
                                                                        : idx % 2 === 0
                                                                            ? 'bg-white'
                                                                            : 'bg-white')
                                                                }
                                                            >
                                                                <td className="px-4 pt-[8px] pb-[2px]">
                                                                    <input
                                                                        type="checkbox"
                                                                        className="accent-red-600"
                                                                        checked={deliveryGroupChecked}
                                                                        onChange={() => toggleDeliveryGroup(deliveryIdsToToggle)}
                                                                    />
                                                                </td>
                                                                <td title={d.leveranstyp} className="px-3 pt-[6px] pb-[4px] text-gray-700 whitespace-nowrap overflow-hidden truncate">
                                                                    {toListTypeLabel(d.leveranstyp)}
                                                                </td>
                                                                <td title={d.leverantor} className="px-3 pt-[6px] pb-[4px] text-gray-700 whitespace-nowrap overflow-hidden truncate">
                                                                    {d.leverantor}
                                                                </td>
                                                                <td title={d.leveransdatum} className="px-3 pt-[6px] pb-[4px] text-gray-700 whitespace-nowrap overflow-hidden truncate">
                                                                    <span className='text-gray-400 mr-1'>Lev.dat</span>{d.leveransdatum}
                                                                </td>
                                                                <td title={d.avropsnummer} className="px-3 pt-[6px] pb-[4px] text-gray-700 text-right whitespace-nowrap overflow-hidden truncate">
                                                                    {d.avropsnummer ? <><span className='text-gray-400 mr-1'>Avrop</span> {d.avropsnummer}</> : ''}
                                                                </td>
                                                                <td title={d.ordernr} className="px-3 pt-[6px] pb-[4px] text-gray-700 text-right whitespace-nowrap overflow-hidden truncate">
                                                                    {d.ordernr ? <><span className='text-gray-400 mr-1'>Order</span>{d.ordernr}</> : ''}
                                                                </td>
                                                                <td title={d.kundsOrdernr} className="px-3 pt-[6px] pb-[4px] text-gray-700 whitespace-nowrap overflow-hidden truncate">
                                                                    {d.kundsOrdernr ? (<><span className='text-gray-400 mr-1'>Kunds onr</span>{d.kundsOrdernr}</>) : ''}
                                                                </td>
                                                                <td className="px-3 pt-[6px] pb-[4px] text-gray-700 text-right whitespace-nowrap">
                                                                    {d.antal}<span className='text-gray-400 ml-1'>st.</span>
                                                                </td>
                                                                <td className="px-3 pt-[6px] pb-[4px] text-gray-700 text-right whitespace-nowrap">
                                                                    <div className='flex items-center'>
                                                                        <div className='w-15'>{formatAmount(d.pris)}</div><div className='w-20 text-left text-gray-400 ml-1'>SEK/1000 st</div>
                                                                    </div>
                                                                </td>
                                                                <td className="pl-5 pt-[6px] pb-[4px] text-gray-700 text-right whitespace-nowrap">
                                                                    {d.nrOfPalletsToInvoice ? <>{d.nrOfPalletsToInvoice} <span className='text-gray-400 ml-1'>pallar att fakt.</span></>: ''}
                                                                </td>
                                                                <td className="px-3 pt-[6px] pb-[4px] text-gray-700 text-right whitespace-nowrap">
                                                                    <div className="flex items-center gap-1 justify-end">
                                                                        {d.pills.map((pill) => (
                                                                            <span key={pill.label} className={`inline-flex h-4 items-center rounded-full ${pill.color} text-white px-2 text-[10px] leading-none`}>
                                                                                {pill.icon && <Mail className="w-3 h-3 mr-1" />}
                                                                                {pill.label}
                                                                            </span>
                                                                        ))}
                                                                    </div>
                                                                </td>
                                                            </tr>
                                                        );
                                                    })
                                                )}

                                                {customer.orderCosts?.length > 0 && (
                                                    customer.orderCosts.map((oc, idx) => (
                                                        <tr
                                                            key={oc.id}
                                                            className={
                                                                'border-b border-gray-100 ' +
                                                                (idx % 2 === 0 ? 'bg-white' : 'bg-white')
                                                            }
                                                        >
                                                            <td className="px-4 pt-[8px] pb-[2px]">
                                                                <input
                                                                    type="checkbox"
                                                                    className="accent-red-600"
                                                                    checked={!!checkedDeliveries[oc.selectionKey]}
                                                                    onChange={() => toggleDelivery(oc.selectionKey)}
                                                                />
                                                            </td>
                                                            <td title={oc.info} className="px-3 py-2 text-gray-700 whitespace-nowrap overflow-hidden truncate">
                                                                {oc.info}
                                                            </td>
                                                            <td title={oc.leveranstyp} className="px-3 pt-[6px] pb-[4px] text-gray-700 whitespace-nowrap overflow-hidden truncate">
                                                                {toListTypeLabel(oc.leveranstyp)}
                                                            </td>
                                                            <td></td>
                                                            <td></td>
                                                            <td className="px-3 py-2 text-gray-700 text-right whitespace-nowrap">
                                                                <span className='text-gray-400 mr-1'>Order</span>{oc.ordernr}
                                                            </td>
                                                            <td></td>
                                                            <td className="px-3 py-2 text-gray-700 text-right whitespace-nowrap">
                                                                {oc.antal}<span className='text-gray-400 ml-1'>st.</span>
                                                            </td>
                                                            <td className="px-3 py-2 text-gray-700 text-right whitespace-nowrap">
                                                                <div className='flex items-center'>
                                                                    <div className='w-15'>{formatAmount(oc.pris)}</div><div className='text-left text-gray-400 ml-1'>SEK/st</div>
                                                                </div>
                                                            </td>
                                                            <td></td>
                                                            <td className="px-3 pt-[6px] pb-[4px] text-gray-700 text-right whitespace-nowrap">
                                                                <div className="flex items-center gap-1 justify-end">
                                                                    {oc.pills.map((pill) => (
                                                                        <span key={pill.label} className={`inline-flex h-4 items-center rounded-full ${pill.color} text-white px-2 text-[10px] leading-none`}>
                                                                            {pill.icon && <Mail className="w-3 h-3 mr-1" />}
                                                                            {pill.label}
                                                                        </span>
                                                                    ))}
                                                                </div>
                                                            </td>
                                                        </tr>
                                                    ))
                                                )}

                                                {customer.supplierorders?.length > 0 && (
                                                    customer.supplierorders.map((so, idx) => (
                                                        <tr
                                                            key={so.id}
                                                            className={
                                                                'border-b border-gray-100 ' +
                                                                (idx % 2 === 0 ? 'bg-white' : 'bg-white')
                                                            }
                                                        >
                                                            <td className="px-4 pt-[8px] pb-[2px]">
                                                                <input
                                                                    type="checkbox"
                                                                    className="accent-red-600"
                                                                    checked={!!checkedDeliveries[so.selectionKey]}
                                                                    onChange={() => toggleDelivery(so.selectionKey)}
                                                                />
                                                            </td>
                                                            <td className="px-3 py-2 text-gray-700">
                                                                {toListTypeLabel(so.leveranstyp)} - Förskottsfaktura
                                                            </td>
                                                            <td className="px-3 py-2 text-gray-700">
                                                                {so.leverantor}
                                                            </td>
                                                            <td></td>
                                                            <td></td>
                                                            <td></td>
                                                            <td></td>
                                                            <td className="px-3 py-2 text-gray-700 text-right whitespace-nowrap">
                                                                {so.antal}<span className='text-gray-400 ml-1'>st.</span>
                                                            </td>
                                                            <td className="px-3 py-2 text-gray-700 text-right whitespace-nowrap">
                                                                <div className='flex items-center'>
                                                                    <div className='w-15'>{formatAmount(so.pris)}</div><div className='text-left text-gray-400 ml-1'>SEK/1000 st</div>
                                                                </div>
                                                            </td>
                                                            <td></td>
                                                            <td className="px-3 pt-[6px] pb-[4px] text-gray-700 text-right whitespace-nowrap">
                                                                <div className="flex items-center gap-1 justify-end">
                                                                    {so.pills.map((pill) => (
                                                                        <span key={pill.label} className={`inline-flex h-4 items-center rounded-full ${pill.color} text-white px-2 text-[10px] leading-none`}>
                                                                            {pill.icon && <Mail className="w-3 h-3 mr-1" />}
                                                                            {pill.label}
                                                                        </span>
                                                                    ))}
                                                                </div>
                                                            </td>
                                                        </tr>
                                                    ))
                                                )}

                                            </tbody>
                                        </table>
                                    </div>


                                    {customer.deliveries.length === 0 &&
                                        customer.orderCosts.length === 0 && (
                                            <p className="text-xs text-gray-400 px-4 py-3">
                                                Inga poster att fakturera.
                                            </p>
                                        )}
                                </div>
                            );
                        })}
                    </div>
                </div>
            )}

            {/* Flat list view */}
            {viewMode === 'list' && (
                <div className="border-t border-gray-300 py-1 mt-4 flex-1 overflow-auto" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <table className="w-full border-collapse text-xs">
                        <thead>
                            <tr>
                                {listColumns.map((column) => (
                                    <th
                                        key={column.key}
                                        className={`${column.width} px-2 py-1.5 text-tiny font-medium text-gray-400 whitespace-nowrap ${column.align === 'right' ? 'text-right' : 'text-left'}`}
                                    >
                                        {column.sortable ? (
                                            <button
                                                type="button"
                                                onClick={() => handleListSort(column.key)}
                                                className={`inline-flex items-center gap-1 ${column.align === 'right' ? 'ml-auto' : ''} hover:text-gray-600`}
                                            >
                                                <span>{column.label}</span>
                                                {renderListSortIcon(column)}
                                            </button>
                                        ) : (
                                            column.label
                                        )}
                                    </th>
                                ))}
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-100 bg-white">
                            {sortedListRows.map((row) => {
                                const rowSelectionIds = row.selectionKeys?.length ? row.selectionKeys : [buildSelectionKey(row.type, row.id)];
                                const rowChecked = rowSelectionIds.every((id) => checkedDeliveries[id]);

                                return (
                                    <tr key={row.rowKey} className={'cursor-pointer border-b border-gray-100 hover:bg-amber-50 ' + (rowChecked ? 'bg-red-50 hover:bg-red-100' : '')}>
                                        <td className="px-4 pt-[8px] pb-[2px]">
                                            <input
                                                type="checkbox"
                                                className="accent-red-600"
                                                checked={rowChecked}
                                                onChange={() => toggleDeliveryGroup(rowSelectionIds)}
                                            />
                                        </td>
                                        <td className="px-3 pt-[6px] pb-[4px] text-gray-700 whitespace-nowrap">{row.customerName}</td>
                                        <td className="px-3 pt-[6px] pb-[4px] text-gray-700 whitespace-nowrap">{row.leverantor}</td>
                                        <td className="px-3 pt-[6px] pb-[4px] text-gray-700 whitespace-nowrap">{row.itemType}</td>
                                        <td className="px-3 pt-[6px] pb-[4px] text-gray-700 whitespace-nowrap">{row.leveransdatum}</td>
                                        <td className="px-3 pt-[6px] pb-[4px] text-gray-700 whitespace-nowrap">{row.avropsnummer}</td>
                                        <td className="px-3 pt-[6px] pb-[4px] text-gray-700 whitespace-nowrap">{row.ordernr}</td>
                                        <td className="px-3 pt-[6px] pb-[4px] text-gray-700 whitespace-nowrap">{row.kundsOrdernr}</td>
                                        <td className="px-3 pt-[6px] pb-[4px] text-gray-700 text-right whitespace-nowrap">
                                            {row.antal}<span className="text-gray-400 ml-1">st.</span>
                                        </td>
                                        <td className="px-3 pt-[6px] pb-[4px] text-gray-700 text-right whitespace-nowrap">
                                            {formatAmount(row.salesprice)}<span className="text-gray-400 ml-1">SEK</span>
                                        </td>
                                        <td className="px-3 pt-[6px] pb-[4px] text-gray-700 text-right whitespace-nowrap">
                                            {row.weightPer1000 != null ? <>{row.weightPer1000}<span className="text-gray-400 ml-1">kg</span></> : ''}
                                        </td>
                                        <td className="px-3 pt-[6px] pb-[4px] whitespace-nowrap">
                                            <div className="flex items-center gap-1">
                                                {row.pills.map((pill) => (
                                                    <span key={pill.label} className={`inline-flex h-4 items-center rounded-full ${pill.color} text-white px-2 text-[10px] leading-none`}>
                                                        {pill.icon && <Mail className="w-3 h-3 mr-1" />}
                                                        {pill.label}
                                                    </span>
                                                ))}
                                            </div>
                                        </td>
                                        <td className="px-3 pt-[6px] pb-[4px] text-gray-500 whitespace-nowrap">{row.orderInfo}</td>
                                    </tr>
                                );
                            })}
                        </tbody>
                    </table>
                </div>
            )}

        </div>
    );
};

export default InvoiceDeliveriesAndOrderCosts;
