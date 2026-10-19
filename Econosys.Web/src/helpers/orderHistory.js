import { formatNumber } from './numberUtils.js';

export const formatHistoryQuantity = (row) => {
    const quantity = Number(row.quantity);
    if (row.type === 'DeliveryToStock' || row.type === 'DeliveryFromStock') {
        const movement = row.type === 'DeliveryFromStock' ? -quantity : quantity;
        return `${movement < 0 ? '-' : '+'} ${formatNumber(Math.abs(movement))}`;
    }
    return `${formatNumber(quantity)}${row.type === 'Invoice' ? ' st.' : ''}`;
};

export const getHistoryTransportLink = (row) => {
    if (row.transportOrderId) {
        return { to: `/logistics/transportorder/${row.transportOrderId}`, label: String(row.transportOrderNr ?? row.transportOrderId) };
    }
    if (row.callOffId) {
        return { to: `/logistics/calloff/${row.callOffId}`, label: String(row.callOffId) };
    }
    return null;
};

export const getHistoryDetailUrl = (row) => {
    if (row.type === 'StockTaking') return `/stocktakingitems/${row.id}`;
    if (['DeliveryToStock', 'DeliveryToCustomer', 'DeliveryFromStock'].includes(row.type)) {
        return `/deliveries/${row.type}/${row.id}`;
    }
    return null;
};
