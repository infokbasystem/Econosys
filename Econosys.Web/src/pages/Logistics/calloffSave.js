export const buildCallOffSavePayload = ({ callOffForm, callOffDeliveryList }) => ({
    shipperId: callOffForm.shipperId === '' ? null : Number(callOffForm.shipperId),
    reference: callOffForm.reference || null,
    note: callOffForm.note || null,
    deliveryDate: callOffForm.deliveryDate || null,
    deliveryStatus: Number(callOffForm.deliveryStatus),
    isSentToShipper: Boolean(callOffForm.isSentToShipper),
    doDebitFreight: Boolean(callOffForm.doDebitFreight),
    freightCostToDebit: callOffForm.freightCostToDebit === '' ? null : Number(callOffForm.freightCostToDebit),
    customerDeliveryAddressId: callOffForm.customerDeliveryAddressId === '' ? null : Number(callOffForm.customerDeliveryAddressId),
    callOffDeliveryList: callOffDeliveryList.map((delivery) => ({
        id: Number(delivery.id),
        note: delivery.note || null,
        nrOfPalletPlaces: delivery.nrOfPalletPlaces === '' ? null : Number(delivery.nrOfPalletPlaces),
        deliveryFromStockId: delivery.id === 0 ? delivery.deliveryFromStockId : null,
        newDelivery: delivery.newDelivery ?? null,
    })),
});

export const saveSentFreightOrder = async (client, callOffId, snapshot, version) => {
    const response = await client.put(`/calloff/${callOffId}/aggregate`, {
        ...buildCallOffSavePayload(snapshot),
        version,
        deliveryStatus: 1,
        isSentToShipper: true,
    });
    const updated = response?.data;
    if (typeof updated?.version !== 'string' || !updated.version.trim()
        || updated.callOff?.deliveryStatus !== 1 || updated.callOff?.isSentToShipper !== true) {
        throw new Error('The saved call-off status could not be confirmed.');
    }
    return updated.version;
};
