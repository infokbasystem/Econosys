import React, { useEffect, useId, useMemo, useState } from 'react';
import { NavLink } from 'react-router-dom';
import { ChevronDown, ChevronRight } from 'lucide-react';

import apiClient from '../config/apiClient';

// Last known tree per entity, shown immediately while a fresh copy loads in the background.
const navigationCache = new Map();
const getNavigationKey = (entityType, entityId) => `ordernavigation-${entityType.toLowerCase()}-${entityId}`;

// Share in-flight requests (StrictMode double-mount, focus + visibilitychange firing together).
const navigationInFlight = new Map();
const fetchNavigation = (key, fn) => {
    if (navigationInFlight.has(key)) return navigationInFlight.get(key);

    const promise = fn().finally(() => navigationInFlight.delete(key));
    navigationInFlight.set(key, promise);
    return promise;
};

const nodeBaseClass = 'block rounded-sm px-2 py-1 text-xs';
const getNodeLink = (type, id) => {
    if (!id) return null;

    switch (type) {
        case 'product':
            return `/order/products/${id}`;
        case 'calculation':
            return `/order/calculations/${id}`;
        case 'inquiry':
            return `/order/inquiries/${id}`;
        case 'quotation':
            return `/order/quotations/${id}`;
        case 'supplierOrder':
            return `/order/supplierorders/${id}`;
        case 'customerOrder':
            return `/order/customerorders/${id}`;
        default:
            return null;
    }
};

const getNodeClassName = (isActive) => `${nodeBaseClass} ${isActive ? 'bg-lime-50 text-stone-900 font-semibold shadow-sm shadow-lime-900/10' : 'text-gray-700 hover:bg-gray-100'}`;

const OrderNode = ({ label, type, id, isActive = false, depth = 0, disclosure }) => {
    const link = getNodeLink(type, id);
    const text = `${label}${id ? ` ${id}` : ''}`;

    return (
        <div className="relative" style={{ marginLeft: `${depth * 14}px` }}>
            {disclosure ? (
                <button
                    type="button"
                    className="absolute -left-4 top-1/2 flex h-5 w-5 -translate-y-1/2 items-center justify-center rounded-sm text-gray-600 hover:bg-gray-100 focus-visible:outline-2 focus-visible:outline-lime-700"
                    aria-label={`${disclosure.isExpanded ? 'Fäll ihop' : 'Expandera'} ${text}`}
                    aria-expanded={disclosure.isExpanded}
                    aria-controls={disclosure.contentId}
                    onClick={disclosure.onToggle}
                >
                    {disclosure.isExpanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
                </button>
            ) : depth > 0 && <span className="absolute -left-2 top-1/2 h-px w-2 -translate-y-1/2 bg-gray-300" aria-hidden="true" />}
            {link ? (
                <NavLink to={link} className={getNodeClassName(isActive)}>
                    {text}
                </NavLink>
            ) : (
                <span className={getNodeClassName(isActive)}>{text}</span>
            )}
        </div>
    );
};

const childNodeTypes = [
    ['inquiries', 'inquiry'],
    ['quotations', 'quotation'],
    ['supplierOrders', 'supplierOrder'],
    ['customerOrders', 'customerOrder'],
];

const containsCurrentNode = (node, type, isCurrent) => (
    isCurrent(type, node.id, node.isCurrent)
    || childNodeTypes.some(([collection, childType]) => (
        (node[collection] ?? []).some((child) => containsCurrentNode(child, childType, isCurrent))
    ))
);

const nodeLabels = {
    calculation: 'Kalkyl',
    inquiry: 'Förfrågan',
    quotation: 'Offert',
    supplierOrder: 'Beställning',
    customerOrder: 'Kundorder',
};

const OrderBranch = ({ node, type, depth, isCurrent }) => {
    const [isExpanded, setIsExpanded] = useState(() => containsCurrentNode(node, type, isCurrent));
    const contentId = useId();
    const children = childNodeTypes.flatMap(([collection, childType]) => (
        (node[collection] ?? []).map((child) => ({ node: child, type: childType }))
    ));
    const hasChildren = children.length > 0;

    return (
        <div className="space-y-1">
            <OrderNode
                label={nodeLabels[type]}
                type={type}
                id={node.id}
                isActive={isCurrent(type, node.id, node.isCurrent)}
                depth={depth}
                disclosure={hasChildren ? {
                    isExpanded,
                    contentId,
                    onToggle: () => setIsExpanded((expanded) => !expanded),
                } : undefined}
            />
            {hasChildren && (
                <div id={contentId} hidden={!isExpanded} className="space-y-1">
                    {children.map((child) => (
                        <OrderBranch
                            key={`${child.type}-${child.node.id}`}
                            node={child.node}
                            type={child.type}
                            depth={depth + 1}
                            isCurrent={isCurrent}
                        />
                    ))}
                </div>
            )}
        </div>
    );
};

const normalizeEntityType = (entityType) => {
    const normalized = String(entityType ?? '').trim().toLowerCase().replace(/[-_]/g, '');

    if (normalized === 'product') return 'product';
    if (normalized === 'calculation') return 'calculation';
    if (normalized === 'inquiry') return 'inquiry';
    if (normalized === 'quotation') return 'quotation';
    if (normalized === 'supplierorder') return 'supplierOrder';
    if (normalized === 'customerorder') return 'customerOrder';

    return '';
};

const OrderNavigationTree = ({ entityType, entityId }) => {
    const [tree, setTree] = useState(null);
    const [isLoading, setIsLoading] = useState(false);
    const [hasError, setHasError] = useState(false);

    const normalizedEntityType = useMemo(() => normalizeEntityType(entityType), [entityType]);

    useEffect(() => {
        if (!normalizedEntityType || !entityId || entityId <= 0) {
            setTree(null);
            setHasError(false);
            setIsLoading(false);
            return;
        }

        let disposed = false;
        const requestKey = getNavigationKey(normalizedEntityType, entityId);
        let hasLoadedTree = navigationCache.has(requestKey);

        if (hasLoadedTree) {
            setTree(navigationCache.get(requestKey));
            setHasError(false);
            setIsLoading(false);
        }

        const loadTree = async ({ background = false } = {}) => {
            if (!background) {
                setIsLoading(true);
                setHasError(false);
            }

            try {
                const apiEntityType = normalizedEntityType.toLowerCase();
                const response = await fetchNavigation(requestKey, () => apiClient.get(`/ordernavigation/${apiEntityType}/${entityId}`));
                const data = response.data ?? null;
                navigationCache.set(requestKey, data);

                if (!disposed) {
                    hasLoadedTree = true;
                    setTree(data);
                    setHasError(false);
                }
            } catch (error) {
                console.error('Failed to load order navigation tree:', error);
                // Keep the current tree visible if a background refresh fails.
                if (!disposed && !(background && hasLoadedTree)) {
                    setHasError(true);
                    setTree(null);
                }
            } finally {
                if (!disposed && !background) {
                    setIsLoading(false);
                }
            }
        };

        const refreshInBackground = () => {
            if (document.visibilityState !== 'visible') return;
            loadTree({ background: hasLoadedTree });
        };

        loadTree({ background: hasLoadedTree });
        window.addEventListener('focus', refreshInBackground);
        document.addEventListener('visibilitychange', refreshInBackground);

        return () => {
            disposed = true;
            window.removeEventListener('focus', refreshInBackground);
            document.removeEventListener('visibilitychange', refreshInBackground);
        };
    }, [entityId, normalizedEntityType]);

    const hasData = useMemo(
        () => Boolean(tree && (tree.productId || (Array.isArray(tree.calculations) && tree.calculations.length > 0))),
        [tree]
    );

    const isCurrent = (type, id, explicitFlag = false) => {
        if (explicitFlag) return true;
        const currentType = normalizeEntityType(tree?.currentEntityType);
        return currentType === normalizeEntityType(type) && Number(tree?.currentEntityId) === Number(id);
    };

    return (
        <div className="mt-5 border-t border-gray-300 pt-1">
            {/* <h2 className="text-sm text-center text-gray-700">Orderträd</h2> */}

            {isLoading && <p className="mt-3 text-center text-xs text-gray-500">Laddar orderträd...</p>}
            {!isLoading && hasError && <p className="mt-3 text-center text-xs text-red-700">Kunde inte ladda orderträd.</p>}
            {!isLoading && !hasError && !hasData && <p className="mt-3 text-center text-xs text-gray-500">Inget orderträd för vald post.</p>}

            {!isLoading && !hasError && hasData && (
                <div className="mt-3 space-y-1">
                    <OrderNode
                        label="Produkt"
                        type="product"
                        id={tree?.productId}
                        isActive={isCurrent('product', tree?.productId)}
                        depth={0}
                    />

                    {(tree?.calculations ?? []).map((calculationNode) => (
                        <OrderBranch
                            key={`${normalizedEntityType}-${entityId}-calculation-${calculationNode.id}`}
                            node={calculationNode}
                            type="calculation"
                            depth={1}
                            isCurrent={isCurrent}
                        />
                    ))}
                </div>
            )}
        </div>
    );
};

export default OrderNavigationTree;
