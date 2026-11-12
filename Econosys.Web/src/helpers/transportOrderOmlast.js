export const getOmlastForm = (row, info) => {
    const delivery = row.transportOrderDelivery;
    return {
        forcedOmlastStatus: Number(delivery.forcedOmlastStatus ?? 0),
        omlastInventoryId: delivery.omlastInventoryId ?? info.defaultInventoryId ?? null,
        omlastInDate: delivery.omlastInDate ? String(delivery.omlastInDate).slice(0, 10) : null,
        omlastDeliveryDate: delivery.omlastDeliveryDate ? String(delivery.omlastDeliveryDate).slice(0, 10) : null,
        omlastNote: delivery.omlastNote ?? '',
        omlastPalletPlaces: delivery.omlastPalletPlaces ?? Math.ceil((info.nrOfPallets ?? 0) / (info.palletIsStackable ? 2 : 1)),
        totalWeight: delivery.totalWeight ?? info.defaultTotalWeight ?? null,
    };
};

export const showsOmlastFields = (isOmlast, status) => status !== 2 && (isOmlast || status === 1);

export const getOmlastCaption = (status) => status === 1 ? 'Tvingad omlast'
    : status === 2 ? 'Tvingad direkt' : 'Omlast / direkt';

export const applyOmlastDraft = (overview, rowId, form, address) => ({
    ...overview,
    transportOrderDeliveryList: overview.transportOrderDeliveryList.map((row) => row.id === rowId ? {
        ...row,
        transportOrderDelivery: { ...row.transportOrderDelivery, ...form },
        forcedOmlastStatus: form.forcedOmlastStatus,
        forcedOmlastCaption: getOmlastCaption(form.forcedOmlastStatus),
        customerNameAddress: address,
    } : row),
});