export const getFreightMode = (calculation) => {
    const active = !calculation.deactivateFreightCalculation;
    return {
        active,
        automaticCalculation: active && Boolean(calculation.autoFreightCalc),
        automaticPrices: active && Boolean(calculation.autoFreightPrice),
    };
};

export const getFreightWarningMessage = (calculation, supplier) => {
    if (supplier?.econopackTransportResponsible !== true) return '';
    const disabledSettings = [
        calculation?.autoFreightCalc === false ? 'Automatisk fraktberäkning' : null,
        calculation?.autoFreightPrice === false ? 'Automatiskt fraktpris' : null,
    ].filter(Boolean);
    if (disabledSettings.length === 0) return '';
    return `Econopac är transportansvarig för den valda leverantören, men följande är avstängt i kalkylen: ${disabledSettings.join(' och ')}. Kontrollera fraktinställningarna.`;
};

export const getFreightEditState = (calculation, saved) => {
    const enabledAutomaticPrices = Boolean(calculation?.autoFreightPrice && !saved?.autoFreightPrice);
    return {
        selectDefaultFactory: enabledAutomaticPrices || calculation?.supplier?.id !== saved?.supplier?.id,
        selectDefaultAddress: enabledAutomaticPrices || calculation?.customer?.id !== saved?.customer?.id,
        recalculateGeometry: Boolean(calculation && saved && [
            'autoFreightCalc',
            'deactivateFreightCalculation',
            'palletLength',
            'palletWidth',
            'palletIsStackable',
            'unitId',
        ].some((field) => calculation[field] !== saved[field])),
    };
};

export const changeFreightMode = (calculation, field, enabled) => {
    if (field !== 'deactivateFreightCalculation' && calculation.deactivateFreightCalculation) return calculation;
    const next = { ...calculation, [field]: enabled };
    if (field === 'deactivateFreightCalculation' && enabled) {
        Object.assign(next, {
            autoFreightCalc: false,
            autoFreightPrice: false,
            palletsPerTruck: 0,
            freightPerPallet: null,
            freightPerVehicle: null,
            loadingPerDelivery: null,
            approveFreight: true,
        });
        next.rows = calculation.rows.map((row) => row.archived || row.isDraft ? row : {
            ...row,
            freight: null,
            freightDomesticPerPallet: 0,
            freightInternationalPerPallet: 0,
            freightPerVehicle: 0,
            freightUnloading: 0,
            freightTotal: 0,
            freightPerUnit: 0,
            calcPalletPriceUsed: false,
            calcFtlPriceUsed: false,
        });
    }
    if (field === 'autoFreightCalc' && !enabled) next.palletsPerTruck = 0;
    if (field === 'autoFreightPrice' || (field === 'deactivateFreightCalculation' && enabled)) {
        if (next.autoFreightPrice) {
            next.freightPerPallet = null;
            next.freightPerVehicle = null;
        } else {
            next.supplierFactoryId = null;
            next.customerDeliveryAddressId = null;
            next.deliveryAddressCountryCode = '';
            next.deliveryAddressPostalNr = null;
            next.rows = (next.rows ?? calculation.rows).map((row) => row.archived || row.isDraft
                ? row : { ...row, preCalcAdditionPercent: 0 });
        }
    }
    return next;
};

export const parseFreightFactors = (items, length, width) => {
    const matches = items.filter((item) => item.lengthFrom < length && item.lengthTo >= length
        && item.widthFrom < width && item.widthTo >= width);
    if (matches.length !== 1) throw new Error('Pallfaktor saknas eller har dubbletter för pallens mått.');
    const values = String(matches[0].palletFactors ?? '').split(';').map((value) => {
        if (!value.trim()) return null;
        return Number(value.trim().replace(',', '.'));
    });
    if (values.length !== 35 || values.some((value) => value !== null && (!Number.isFinite(value) || value < 0))
        || !Number.isInteger(values[34]) || values[34] <= 0) {
        throw new Error('Pallfaktorerna i registret är ogiltiga.');
    }
    return values;
};

export const getAutomaticFreightGeometry = (row, calculation, factors) => {
    const palletsPerTruck = factors[34] * (calculation.palletIsStackable ? 2 : 1);
    const nrOfPallets = Number(calculation.editionPerPallet) > 0 && row.edition != null
        ? Number(row.edition) / Number(calculation.editionPerPallet) : null;
    const rounded = Math.ceil(nrOfPallets ?? 0);
    const places = Math.ceil((rounded % palletsPerTruck) / (calculation.palletIsStackable ? 2 : 1));
    return {
        palletsPerTruck,
        nrOfPallets,
        nrOfVehicles: Math.floor(rounded / palletsPerTruck),
        palletCalcFactor: places === 0 ? 1 : factors[places - 1] ?? null,
    };
};
