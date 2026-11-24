export async function fetchSupplierOrderPackaging(client, order) {
    let calculationId = order.calculationId;
    if (order.inquiryId > 0) {
        const { data: inquiry } = await client.get(`/inquiries/${order.inquiryId}`);
        calculationId = inquiry.calculationId;
    }

    if (!(calculationId > 0)) {
        throw new Error('Beställningen saknar en kopplad kalkyl.');
    }

    const { data: calculation } = await client.get(`/calculations/${calculationId}/aggregate`);
    return {
        loadingInstruction: [calculation.loadingInstruction, calculation.freightNote]
            .filter((text) => text?.trim())
            .join('\n'),
        eurPallet: calculation.palletType === 1,
        oneWayPallet: calculation.palletType === 2,
    };
}

export function hasSupplierOrderPackagingDifference(order, packaging) {
    const normalizeText = (value) => (value ?? '').replace(/\r\n/g, '\n');
    return normalizeText(order.loadingInstruction) !== normalizeText(packaging.loadingInstruction)
        || Boolean(order.eurPallet) !== packaging.eurPallet
        || Boolean(order.oneWayPallet) !== packaging.oneWayPallet;
}
