const numericValue = (value) => {
    const number = Number(value);
    return Number.isFinite(number) ? number : 0;
};

export const recalculateCostCalcRow = (row) => {
    const transportOrderTotal = numericValue(row.toInternationalCost)
        + numericValue(row.transportOrderDomesticCost)
        + numericValue(row.transportOrderUnloading)
        + numericValue(row.transportOrderLoading)
        + numericValue(row.transportOrderOther);
    const resultTotal = numericValue(row.resultInternationalCost)
        + numericValue(row.resultDomesticCost)
        + numericValue(row.resultOther);

    return {
        ...row,
        transportOrderTotal,
        diffTransportOrderCaclulation: numericValue(row.caclulationUsedTotal) - transportOrderTotal,
        resultTotal,
        diffResultCaclulation: numericValue(row.caclulationUsedTotal) - resultTotal,
        diffResultTransportOrder: transportOrderTotal - resultTotal,
    };
};

export const recalculateCostCalcRows = (rows) => rows.map(recalculateCostCalcRow);

export const distributeResultInternationalCost = (rows, headerValue) => {
    const totalArea = rows.reduce(
        (sum, row) => sum + numericValue(row.totalNrOfPalletPlaces) * numericValue(row.palletFactor),
        0,
    );
    const denominator = totalArea === 0 ? 1 : totalArea;
    const total = numericValue(headerValue);

    return recalculateCostCalcRows(rows.map((row) => ({
        ...row,
        resultInternationalCost: total
            * numericValue(row.totalNrOfPalletPlaces)
            * numericValue(row.palletFactor)
            / denominator,
    })));
};