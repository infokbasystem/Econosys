const childNodeTypes = [
    ['inquiries', 'inquiry'],
    ['quotations', 'quotation'],
    ['supplierOrders', 'supplierOrder'],
    ['customerOrders', 'customerOrder'],
];

const normalizeEntityType = (entityType) => String(entityType ?? '').trim().toLowerCase().replace(/[-_]/g, '');

export const getVisibleNavigationIds = (tree, currentEntityType, currentEntityId, limit = 3) => {
    const visibleIdsByType = new Map();
    const normalizedCurrentType = normalizeEntityType(tree?.currentEntityType ?? currentEntityType);
    const normalizedCurrentId = Number(tree?.currentEntityId ?? currentEntityId);

    const collectSiblings = (nodes, type) => {
        if (!nodes.length) return false;

        const entries = nodes.map((node) => ({ id: node.id, isOnCurrentPath: collect(node, type) }));
        const visibleIds = visibleIdsByType.get(type) ?? new Set();
        entries.slice().sort((a, b) => Number(b.id) - Number(a.id))
            .slice(0, limit).forEach(({ id }) => visibleIds.add(id));
        entries.filter(({ isOnCurrentPath }) => isOnCurrentPath)
            .forEach(({ id }) => visibleIds.add(id));
        visibleIdsByType.set(type, visibleIds);
        return entries.some(({ isOnCurrentPath }) => isOnCurrentPath);
    };

    const collect = (node, type) => {
        let descendantIsCurrent = false;
        childNodeTypes.forEach(([collection, childType]) => {
            if (collectSiblings(node[collection] ?? [], childType)) descendantIsCurrent = true;
        });
        const isCurrent = Boolean(node.isCurrent)
            || (normalizeEntityType(type) === normalizedCurrentType && Number(node.id) === normalizedCurrentId);
        return isCurrent || descendantIsCurrent;
    };

    collectSiblings(tree?.calculations ?? [], 'calculation');

    return Object.fromEntries(visibleIdsByType);
};
