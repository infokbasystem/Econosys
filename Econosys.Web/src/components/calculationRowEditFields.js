const fields = (definitions) => definitions.map(([name, label, type = 'number', maxLength]) => ({
    name, label, type, maxLength,
}));

export const CALCULATION_ROW_EDIT_GROUPS = [
    { label: 'Kalkylrad', fields: fields([
        ['edition', 'Upplaga', 'integer'],
        ['calculationPrice', 'Kalkylpris'],
        ['purchasePrice', 'Inköpspris'],
        ['freight', 'Frakt'],
        ['other', 'Övrigt'],
        ['storage', 'Lagring'],
        ['percentAddition', 'Påslag %'],
        ['salesPrice', 'Försäljningspris'],
        ['packagingCost', 'Pallkostnad'],
        ['preCalcAdditionPercent', 'Förkalkylpåslag %'],
        ['archived', 'Arkiverad', 'boolean'],
        ['info', 'Info', 'text', 50],
        ['archivedPurchaseCurrencyRate', 'Arkiverad inköpsvalutakurs'],
        ['archivedSalesCurrencyRate', 'Arkiverad försäljningsvalutakurs'],
    ]) },
    { label: 'Frakt och leverans', fields: fields([
        ['nrOfPalletsPopup', 'Antal pallar'],
        ['nrOfVehiclesPopup', 'Antal bilar'],
        ['freightPopup', 'Frakt totalt'],
        ['freightExtra10KmPopup', 'Frakt extra 10 km'],
        ['freightUnloadingPopup', 'Lossningskostnad'],
        ['freightCostPerPalletDomesticPopup', 'Frakt per pall inrikes'],
        ['freightCostPerPalletInternationalPopup', 'Frakt per pall utrikes'],
        ['freightCostPerFtlInternationalPopup', 'Frakt per FTL utrikes'],
        ['freightTotalPopupPerUnit', 'Frakt totalt per enhet'],
        ['loadingCostPerPallet', 'Lastningskostnad per pall'],
        ['customerDeliveryAddressId', 'Leveransadress-ID', 'integer'],
        ['calcPalletPriceUsed', 'Pallpris används', 'boolean'],
        ['calcFtlPriceUsed', 'FTL-pris används', 'boolean'],
    ]) },
    { label: 'Lagring och övrigt', fields: fields([
        ['nrOfStorageMonths', 'Antal lagringsmånader'],
        ['storageNrOfPallets', 'Antal lagerpallar', 'integer'],
        ['otherPopup', 'Övrig kostnad totalt'],
        ['storageUnloadingPopup', 'Lossning till lager'],
        ['storagePopup', 'Lagringskostnad totalt'],
        ['storageFreightToCustomerPopup', 'Lagerfrakt till kund'],
        ['storageInterestPopup', 'Lagerränta'],
    ]) },
    { label: 'Arkiverade fraktinställningar', fields: fields([
        ['archivedAutoFreightCalc', 'Automatisk fraktberäkning', 'boolean'],
        ['archivedAutoFreightPrice', 'Automatiskt fraktpris', 'boolean'],
        ['archivedFreightPerPallet', 'Frakt per pall'],
        ['archivedLoadingPerDelivery', 'Lastning per leverans'],
        ['archivedFreightPerVehicle', 'Frakt per bil'],
        ['archivedSupplierFactoryId', 'Leverantörsfabrik-ID', 'integer'],
        ['archivedCustomerDeliveryAddressId', 'Leveransadress-ID', 'integer'],
        ['archivedDeliveryAddressCountryCode', 'Landskod', 'text', 50],
        ['archivedDeliveryAddressPostalNr', 'Postnummer', 'integer'],
        ['archivedLoadingInstruction', 'Lastningsinstruktion', 'text', 255],
        ['archivedFreightNote', 'Fraktnotering', 'text', 255],
    ]) },
    { label: 'Pall och packning – arkiverade värden', fields: fields([
        ['palletCalcFactor', 'Pallberäkningsfaktor'],
        ['archivedPalletCalcFactor', 'Arkiverad pallberäkningsfaktor'],
        ['archivedPalletWidth', 'Pallbredd', 'integer'],
        ['archivedPalletHeight', 'Pallhöjd', 'integer'],
        ['archivedPalletLength', 'Pallängd', 'integer'],
        ['archivedPalletIsStackable', 'Staplingsbar', 'boolean'],
        ['archivedPalletType', 'Palltyp', 'integer'],
        ['archivedEditionPerPallet', 'Antal per pall', 'integer'],
        ['archivedPalletsPerTruck', 'Pallar per bil', 'integer'],
        ['archivedSupplierEurPalletCost', 'Leverantörens EUR-pallkostnad'],
        ['archivedCustomerPalletCost', 'Kundens pallkostnad'],
        ['archivedPalletFormatId', 'Pallformat-ID', 'integer'],
        ['archivedPalletFormatIdSupplierOrder', 'Beställningens pallformat-ID', 'integer'],
        ['archivedPalletFormatIdCustomerOrder', 'Kundorderns pallformat-ID', 'integer'],
        ['archivedPalletPriceIn', 'Inköpspris pall'],
        ['archivedPalletPriceOut', 'Försäljningspris pall'],
        ['archivedNrOfPerBundle', 'Antal per bunt', 'integer'],
        ['archivedNrOfPerBundlePerPallet', 'Buntar per pall', 'integer'],
        ['archivedNrOfPerOuterPackaging', 'Antal per ytterförpackning', 'integer'],
        ['archivedNrOfOuterPackagingPerPallet', 'Ytterförpackningar per pall', 'integer'],
        ['archivedDoBuntas', 'Buntas', 'boolean'],
        ['archivedIsYtterforpackning', 'Ytterförpackning', 'boolean'],
    ]) },
    { label: 'Arkiverad prislista – utrikes', fields: fields([
        ['archivedPriceListUseAdjustedPalletPlaces', 'Justerade pallplatser', 'boolean'],
        ['archivedCalculatedPriceListColumn', 'Beräknad prislistkolumn', 'integer'],
        ['archivedPriceListPricePerPallet', 'Pris per pall'],
        ['archivedPriceListPriceFtl', 'Pris FTL'],
        ['archivedPriceListAdditionSekPerPallet', 'Tillägg SEK per pall'],
        ['archivedPriceListSecaMarpolPallet', 'SECA/MARPOL pall'],
        ['archivedPriceListSecaMarpolFtl', 'SECA/MARPOL FTL'],
        ['archivedPriceListDmtPercentPallet', 'DMT % pall'],
        ['archivedPriceListDmtPercentFtl', 'DMT % FTL'],
        ['archivedPriceListAdditionCurrencyPercentPallet', 'Valutatillägg % pall'],
        ['archivedPriceListAdditionCurrencyPercentFtl', 'Valutatillägg % FTL'],
        ['archivedPriceListOtherPallet', 'Övrigt pall'],
        ['archivedPriceListOtherFtl', 'Övrigt FTL'],
        ['archivedPriceListOtherPercentPallet', 'Övrigt % pall'],
        ['archivedPriceListOtherPercentFtl', 'Övrigt % FTL'],
        ['archivedPriceListAdditionTotalPercentPallet', 'Totalt tillägg % pall'],
        ['archivedPriceListAdditionTotalPercentFtl', 'Totalt tillägg % FTL'],
        ['archivedPriceListUnloadingCost', 'Lossningskostnad'],
        ['archivedPriceListLoadingCost', 'Lastningskostnad'],
        ['archivedPriceListCurrencyId', 'Valuta-ID pall', 'integer'],
        ['archivedPriceListCurrencyRate', 'Valutakurs pall'],
        ['archivedPriceListCurrencyIdFtl', 'Valuta-ID FTL', 'integer'],
        ['archivedPriceListCurrencyRateFtl', 'Valutakurs FTL'],
        ['archivedPriceListSummary', 'Sammanfattning', 'text', 1000],
    ]) },
    { label: 'Arkiverad prislista – inrikes', fields: fields([
        ['archivedPriceListPricePerPalletDomestic', 'Pris per pall'],
        ['archivedPriceListAdditionSekPerPalletDomestic', 'Tillägg SEK per pall'],
        ['archivedPriceListSecaMarpolPalletDomestic', 'SECA/MARPOL pall'],
        ['archivedPriceListDmtPercentPalletDomestic', 'DMT % pall'],
        ['archivedPriceListAdditionCurrencyPercentPalletDomestic', 'Valutatillägg % pall'],
        ['archivedPriceListOtherPalletDomestic', 'Övrigt pall'],
        ['archivedPriceListOtherPercentPalletDomestic', 'Övrigt % pall'],
        ['archivedPriceListAdditionTotalPercentPalletDomestic', 'Totalt tillägg % pall'],
        ['archivedPriceListUnloadingCostDomestic', 'Lossningskostnad'],
        ['archivedPriceListLoadingCostDomestic', 'Lastningskostnad'],
        ['archivedPriceListCurrencyIdDomestic', 'Valuta-ID', 'integer'],
        ['archivedPriceListCurrencyRateDomestic', 'Valutakurs'],
    ]) },
];

const allFields = CALCULATION_ROW_EDIT_GROUPS.flatMap((group) => group.fields);

export const createCalculationRowEditDraft = (values) => {
    if (!values || allFields.some(({ name, type }) => !(name in values)
        || (type === 'boolean' && typeof values[name] !== 'boolean'))) {
        throw new Error('Kalkylradens fält kunde inte läsas.');
    }
    return Object.fromEntries(allFields.map(({ name, type }) => [
        name, type === 'boolean' ? values[name] : values[name] == null ? '' : String(values[name]),
    ]));
};

export const buildCalculationRowEditValues = (draft) => Object.fromEntries(allFields.map(({ name, label, type }) => {
    const value = draft[name];
    if (type === 'boolean') {
        if (typeof value !== 'boolean') throw new Error(`${label}: välj ja eller nej.`);
        return [name, value];
    }
    if (value == null || value === '') return [name, null];
    if (type === 'text') return [name, value];
    const normalized = String(value).trim().replace(/\s/g, '').replace(',', '.');
    if (!/^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?$/.test(normalized))
        throw new Error(`${label}: ange ett giltigt tal.`);
    const parsed = Number(normalized);
    if (!Number.isFinite(parsed) || (type === 'integer' && (!Number.isInteger(parsed) || parsed < -2147483648 || parsed > 2147483647)))
        throw new Error(`${label}: ange ${type === 'integer' ? 'ett giltigt heltal' : 'ett ändligt tal'}.`);
    return [name, parsed];
}));
