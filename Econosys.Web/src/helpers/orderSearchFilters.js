export const buildOrderTextFilters = (values) => ({
    searchTerm: values.searchTerm.trim() || null,
    product: values.product.trim() || null,
    supplierName: values.supplierName.trim() || null,
    customerName: values.customerName.trim() || null,
    ...(values.yourOrderNr === undefined ? {} : { yourOrderNr: values.yourOrderNr.trim() || null }),
    ...(values.customerOrderNr === undefined ? {} : { customerOrderNr: values.customerOrderNr.trim() || null }),
});

export const buildOrderPersonFilters = (sellerIds, creatorIds, options) => {
    const isFiltered = (ids) => ids !== null
        && (options.length === 0 || !options.every((option) => ids.includes(option.value)));

    return {
        ...(isFiltered(sellerIds) ? { customerResponsibleUserIds: sellerIds } : {}),
        ...(isFiltered(creatorIds) ? { createdByIds: creatorIds } : {}),
    };
};
