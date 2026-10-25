export const ORDER_FLOW_SECTIONS = [
    { type: 'product', label: 'PRODUKTER', currentLabel: 'AKTUELL PRODUKT', path: 'products' },
    { type: 'calculation', label: 'KALKYLER', currentLabel: 'AKTUELL KALKYL', path: 'calculations' },
    { type: 'inquiry', label: 'FÖRFRÅGNINGAR', currentLabel: 'AKTUELL FÖRFRÅGAN', path: 'inquiries', limit: 3 },
    { type: 'quotation', label: 'AFFÄRSFÖRSLAG', currentLabel: 'AKTUELLT AFFÄRSFÖRSLAG', path: 'quotations', limit: 3 },
    { type: 'supplierOrder', label: 'BESTÄLLNINGAR', currentLabel: 'AKTUELL BESTÄLLNING', path: 'supplierorders', limit: 8 },
    { type: 'customerOrder', label: 'ORDERERKÄNNANDEN', currentLabel: 'AKTUELLT ORDERERKÄNNANDE', path: 'customerorders', limit: 8 },
];

export const getOrderFlowSections = (items, currentEntityType) => ORDER_FLOW_SECTIONS.map((section) => ({
    ...section,
    limit: currentEntityType === 'calculation' && ['inquiry', 'quotation'].includes(section.type)
        ? 1 : section.limit,
    label: section.type === currentEntityType
        || (section.type === 'customerOrder' && ['calculation', 'inquiry', 'quotation'].includes(currentEntityType))
        ? section.currentLabel : section.label,
    items: items.filter((item) => item.type === section.type).sort((a, b) => {
        if (section.type === 'calculation') {
            return (a.supplierName ?? '').localeCompare(b.supplierName ?? '', 'sv')
                || (a.customerName ?? '').localeCompare(b.customerName ?? '', 'sv')
                || b.id - a.id;
        }
        return b.id - a.id;
    }),
})).filter((section) => section.items.length > 0);
