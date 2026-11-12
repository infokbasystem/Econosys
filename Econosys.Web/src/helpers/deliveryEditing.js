export const getDeliveryReference = (row) => {
    const delivery = row?.transportOrderDelivery ?? row;
    const references = [
        { deliveryType: 'DeliveryFromStock', deliveryId: Number(delivery?.deliveryFromStockId) },
        { deliveryType: 'DeliveryToCustomer', deliveryId: Number(delivery?.deliveryToCustomerId) },
        { deliveryType: 'DeliveryToStock', deliveryId: Number(delivery?.deliveryToStockId) },
    ].filter((reference) => reference.deliveryId > 0);
    return references.length === 1 ? references[0] : null;
};

export const getDeliveryEditForm = (response) => {
    const delivery = response.delivery;
    return {
        deliveryDate: delivery.deliveryDate ? String(delivery.deliveryDate).slice(0, 10) : null,
        nrOfItems: delivery.nrOfItems ?? null,
        nrOfPallets: delivery.nrOfPallets ?? null,
        nrOfBunt: delivery.nrOfBunt ?? null,
        nrOfYtterforpackning: delivery.nrOfYtterforpackning ?? null,
        inventoryId: delivery.inventoryId ?? null,
        callOff: delivery.callOff ?? '',
        palletLength: delivery.palletLength ?? null,
        palletWidth: delivery.palletWidth ?? null,
        palletHeight: delivery.palletHeight ?? null,
        palletIsStackable: Boolean(delivery.palletIsStackable),
        palletCalcFactor: delivery.palletCalcFactor ?? null,
        isDelivered: delivery.deliveryStatus === 2,
        spcsRefNr: delivery.spcsRefNr ?? '',
        supplierInvoiceNr: delivery.supplierInvoiceNr ?? null,
    };
};

export const updateDeliveryQuantity = (form, field, value, metadata) => {
    const next = { ...form, [field]: value };
    if (metadata.delivery.type !== 'DeliveryFromStock') return next;

    const edition = Number(metadata.delivery.editionPerPallet ?? 0);
    const units = Number(metadata.unitsPerPackage ?? 0);
    const packageField = metadata.packagingType === 'BUNT'
        ? 'nrOfBunt'
        : metadata.packagingType === 'YTTERFORPACKNING' ? 'nrOfYtterforpackning' : null;
    if (field === 'nrOfPallets' && edition > 0) {
        next.nrOfItems = value == null ? null : value * edition;
    } else if (field === packageField && units > 0) {
        next.nrOfItems = value == null ? null : value * units;
    }
    if (['nrOfItems', 'nrOfBunt', 'nrOfYtterforpackning'].includes(field) && edition > 0) {
        next.nrOfPallets = next.nrOfItems == null ? null : Math.ceil(next.nrOfItems / edition);
    }
    if (packageField && units > 0 && field !== packageField) {
        next[packageField] = next.nrOfItems == null || next.nrOfItems % units !== 0
            ? null
            : next.nrOfItems / units;
    }
    return next;
};

export const mergeEditedDeliveryOverview = (current, refreshed, saved) => {
    const affectedIds = new Set([saved.delivery.id, ...(saved.relatedDeliveryIds ?? [])]);
    const freshById = new Map(refreshed.transportOrderDeliveryList.map((row) => [row.id, row]));
    return {
        ...current,
        transportOrderDeliveryList: current.transportOrderDeliveryList.map((row) => {
            const reference = getDeliveryReference(row);
            const fresh = freshById.get(row.id);
            if (!fresh || reference?.deliveryType !== saved.delivery.type || !affectedIds.has(reference.deliveryId)) return row;
            const hasOmlastDraft = (row.transportOrderDelivery.forcedOmlastStatus ?? 0) !== (fresh.transportOrderDelivery.forcedOmlastStatus ?? 0)
                || (row.transportOrderDelivery.omlastInventoryId ?? null) !== (fresh.transportOrderDelivery.omlastInventoryId ?? null);
            return {
                ...row,
                customerNameAddress: hasOmlastDraft ? row.customerNameAddress : fresh.customerNameAddress,
                palletInfo: fresh.palletInfo,
                nrOfPallets: fresh.nrOfPallets,
                palletIsStackable: fresh.palletIsStackable,
                palletLength: fresh.palletLength,
                palletWidth: fresh.palletWidth,
                palletHeight: fresh.palletHeight,
            };
        }),
    };
};

export const getDeliveryPalletFootprint = (delivery) => {
    const width = delivery.palletWidth > 0 ? delivery.palletWidth / 10 : 80;
    const length = delivery.palletLength > 0 ? delivery.palletLength / 10 : 120;
    return { widthCm: Math.min(width, length), heightCm: Math.max(width, length) };
};

export const getDeliveryPalletPlaces = (delivery) => Math.ceil((Number(delivery?.nrOfPallets) || 0) / (delivery?.palletIsStackable ? 2 : 1));

export const getDeliveryPalletLabel = (delivery, palletIndex = 0) => {
    const orderNr = String(delivery?.supplierOrderNr ?? '');
    const isDoubleStack = delivery?.palletIsStackable
        && Number(delivery.nrOfPallets) - palletIndex * 2 >= 2;
    return `${orderNr}${isDoubleStack ? '\n2p' : ''}`;
};

export const getDeliveryPlacementStatus = (delivery, pallets) => {
    const placed = pallets.filter((pallet) => pallet.deliveryId === delivery.id);
    const places = getDeliveryPalletPlaces(delivery);
    const remaining = places - placed.length;
    const { widthCm, heightCm } = getDeliveryPalletFootprint(delivery);
    const dimensionsChanged = placed.some((pallet) => !(
        pallet.widthCm === widthCm && pallet.heightCm === heightCm
        || pallet.widthCm === heightCm && pallet.heightCm === widthCm
    ));
    return {
        remainingText: remaining < 0 ? `${-remaining} för många` : `${remaining} kvar`,
        warning: widthCm > 240 || heightCm > 1350 ? 'Pallformatet ryms inte i lastplaneringen.'
            : remaining < 0 ? 'Fler pallplatser är placerade än leveransen kräver.'
            : dimensionsChanged ? 'Placerade pallars format skiljer sig från leveransen.' : '',
    };
};