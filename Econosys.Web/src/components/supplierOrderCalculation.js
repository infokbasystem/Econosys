export const positiveId = (value) => {
    const number = Number(value);
    return Number.isInteger(number) && number > 0 ? number : null;
};

export async function resolveSupplierOrderCalculationId(client, {
    inquiryId, quotationId, supplierOrderId, selectedCalculationRowId, linkedSupplierOrderId,
}) {
    const sources = [
        inquiryId && `/inquiries/${inquiryId}`,
        quotationId && `/quotations/${quotationId}`,
        supplierOrderId && selectedCalculationRowId && `/supplierorders/${supplierOrderId}/calculation-row`,
    ].filter(Boolean);

    for (const url of sources) {
        const { data } = await client.get(url);
        const calculationId = positiveId(data?.calculationId);
        if (calculationId) return calculationId;
    }

    if (linkedSupplierOrderId) {
        const { data: order } = await client.get(`/supplierorders/${linkedSupplierOrderId}`);
        const calculationId = positiveId(order?.calculationId);
        if (calculationId) return calculationId;
        const orderQuotationId = positiveId(order?.quotationId);

        return resolveSupplierOrderCalculationId(client, {
            inquiryId: positiveId(order?.inquiryId),
            quotationId: orderQuotationId === quotationId ? null : orderQuotationId,
            supplierOrderId: linkedSupplierOrderId,
            selectedCalculationRowId: positiveId(order?.selectedCalculationRowId),
        });
    }

    throw new Error('Beställningen saknar en kopplad kalkyl.');
}
