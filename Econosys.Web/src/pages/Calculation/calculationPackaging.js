const LOADING_INSTRUCTION_FIELDS = [
    'automaticLoadingInstruction',
    'palletFormatId',
    'palletLength',
    'palletWidth',
    'palletHeight',
    'editionPerPallet',
    'palletIsStackable',
    'doBuntas',
    'nrOfPerBundle',
    'isYtterforpackning',
    'nrOfPerOuterPackaging',
];

export const calculatePackagesPerPallet = (editionPerPallet, unitsPerPackage) => {
    const palletQuantity = Number(editionPerPallet);
    const packageQuantity = Number(unitsPerPackage);
    if (!palletQuantity || !packageQuantity) return { count: null, uneven: false };
    return {
        count: Math.floor(palletQuantity / packageQuantity),
        uneven: palletQuantity % packageQuantity !== 0,
    };
};

export const updateAutomaticLoadingInstruction = (previous, next) => {
    if (!next.automaticLoadingInstruction
        || !LOADING_INSTRUCTION_FIELDS.some((field) => previous[field] !== next[field])) return next;

    const text = (value) => value ?? '';
    let loadingInstruction = `${text(next.palletLength)}x${text(next.palletWidth)}x${text(next.palletHeight)} - ${text(next.editionPerPallet)}pcs/pallet`;
    if (next.palletIsStackable) loadingInstruction += ' - 2 pallets in height on truck';
    if (next.doBuntas) {
        const bundles = calculatePackagesPerPallet(next.editionPerPallet, next.nrOfPerBundle).count;
        loadingInstruction += `\nBundled: ${text(next.nrOfPerBundle)} pcs/bundle, ${text(bundles)} bundles/pallet`;
    }
    if (next.isYtterforpackning) {
        const boxes = calculatePackagesPerPallet(next.editionPerPallet, next.nrOfPerOuterPackaging).count;
        loadingInstruction += next.doBuntas ? ' - ' : '\n';
        loadingInstruction += `Packed in outer boxes: ${text(next.nrOfPerOuterPackaging)}pcs/box, ${text(boxes)} boxes/pallet`;
    }
    return { ...next, loadingInstruction };
};
