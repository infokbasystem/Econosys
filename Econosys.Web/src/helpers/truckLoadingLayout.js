// Port of the legacy AddPallet auto-placement (TransportOrderViewModel.AddPallet, "placera ut alla pallar").
// Legacy picks orientation and placement strategy from the pallet size:
//  - 80/120 cm pallets: 80 (else 120) side across, rows filled left to right after the last placed pallet.
//  - 240 cm pallets: 240 side across, one per row after the rearmost pallet.
//  - other sizes: dragged orientation, filling a left and a right column.
// Pallets that do not fit within the truck length are left out.
const getLegacyLayout = ({ widthCm, heightCm }) => {
    const sides = [widthCm, heightCm];
    if (sides.includes(80) || sides.includes(120)) {
        const acrossCm = sides.includes(80) ? 80 : 120;
        return { strategy: 'rows', widthCm: acrossCm, heightCm: acrossCm === widthCm ? heightCm : widthCm };
    }
    if (sides.includes(240)) {
        return { strategy: 'single', widthCm: 240, heightCm: widthCm === 240 ? heightCm : widthCm };
    }
    return { strategy: 'columns', widthCm, heightCm };
};

const bottomOf = (pallet) => pallet.yCm + pallet.heightCm;

const getNextPosition = (strategy, occupied, widthCm, areaWidthCm) => {
    if (strategy === 'rows') {
        const last = occupied.reduce((best, pallet) => (!best || pallet.yCm > best.yCm
            || (pallet.yCm === best.yCm && pallet.xCm > best.xCm) ? pallet : best), null);
        if (!last) return { xCm: 0, yCm: 0 };
        return last.xCm + last.widthCm + widthCm <= areaWidthCm
            ? { xCm: last.xCm + last.widthCm, yCm: last.yCm }
            : { xCm: 0, yCm: bottomOf(last) };
    }

    if (strategy === 'single') {
        const last = occupied.reduce((best, pallet) => (!best || pallet.yCm > best.yCm ? pallet : best), null);
        return { xCm: 0, yCm: last ? bottomOf(last) : 0 };
    }

    const maxYCm = occupied.reduce((max, pallet) => Math.max(max, bottomOf(pallet)), 0);
    const rightXCm = areaWidthCm - widthCm;
    const maxRightYCm = occupied.filter((pallet) => pallet.xCm >= rightXCm)
        .reduce((max, pallet) => Math.max(max, bottomOf(pallet)), 0);
    // Legacy would overlap the left column for pallets wider than half the truck; use only the left column then.
    return widthCm * 2 <= areaWidthCm && maxRightYCm < maxYCm
        ? { xCm: rightXCm, yCm: maxRightYCm }
        : { xCm: 0, yCm: maxYCm };
};

// footprint is the delivery's default footprint (narrow side across, rotation 0).
export const getAutoPlacement = (existingPallets, footprint, count, { areaWidthCm, areaLengthCm }) => {
    const { strategy, widthCm, heightCm } = getLegacyLayout(footprint);
    const occupied = existingPallets.map(({ xCm, yCm, widthCm: w, heightCm: h }) => ({ xCm, yCm, widthCm: w, heightCm: h }));
    const positions = [];

    for (let i = 0; i < count; i += 1) {
        const position = getNextPosition(strategy, occupied, widthCm, areaWidthCm);
        if (position.yCm + heightCm > areaLengthCm) break;
        occupied.push({ ...position, widthCm, heightCm });
        positions.push(position);
    }

    const rotation = widthCm === footprint.widthCm && heightCm === footprint.heightCm ? 0 : 90;
    return { widthCm, heightCm, rotation, positions };
};
