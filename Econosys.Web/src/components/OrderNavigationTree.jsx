import React, { useEffect, useMemo, useState } from 'react';
import { NavLink } from 'react-router-dom';

import apiClient from '../config/apiClient';

// Prevent duplicate fetches in React StrictMode development double-mount.
const navigationSingleFlight = new Map();
const fetchNavigationOnce = (key, fn) => {
    if (navigationSingleFlight.has(key)) return navigationSingleFlight.get(key);

    const promise = (async () => {
        try {
            const result = await fn();
            navigationSingleFlight.set(key, Promise.resolve(result));
            return result;
        } catch (error) {
            navigationSingleFlight.delete(key);
            throw error;
        }
    })();

    navigationSingleFlight.set(key, promise);
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

const getNodeClassName = (isActive) => `${nodeBaseClass} ${isActive ? 'text-red-600 font-semibold' : 'text-gray-700 hover:bg-gray-100'}`;

const OrderNode = ({ label, type, id, isActive = false, depth = 0 }) => {
    const link = getNodeLink(type, id);
    const text = `${label}${id ? ` ${id}` : ''}`;

    return (
        <div className="relative" style={{ marginLeft: `${depth * 14}px` }}>
            {depth > 0 && <span className="absolute -left-2 top-1/2 h-px w-2 -translate-y-1/2 bg-gray-300" aria-hidden="true" />}
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
            return;
        }

        let disposed = false;

        const loadTree = async () => {
            setIsLoading(true);
            setHasError(false);

            try {
                const apiEntityType = normalizedEntityType.toLowerCase();
                const requestKey = `ordernavigation-${apiEntityType}-${entityId}`;
                const response = await fetchNavigationOnce(requestKey, () => apiClient.get(`/ordernavigation/${apiEntityType}/${entityId}`));

                if (!disposed) {
                    setTree(response.data ?? null);
                }
            } catch (error) {
                console.error('Failed to load order navigation tree:', error);
                if (!disposed) {
                    setHasError(true);
                    setTree(null);
                }
            } finally {
                if (!disposed) {
                    setIsLoading(false);
                }
            }
        };

        loadTree();

        return () => {
            disposed = true;
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
                        <div key={`calculation-${calculationNode.id}`} className="space-y-1">
                            <OrderNode
                                label="Kalkyl"
                                type="calculation"
                                id={calculationNode.id}
                                isActive={isCurrent('calculation', calculationNode.id, calculationNode.isCurrent)}
                                depth={1}
                            />

                            {(calculationNode.inquiries ?? []).map((inquiryNode) => (
                                <div key={`inquiry-${inquiryNode.id}`} className="space-y-1">
                                    <OrderNode
                                        label="Förfrågan"
                                        type="inquiry"
                                        id={inquiryNode.id}
                                        isActive={isCurrent('inquiry', inquiryNode.id, inquiryNode.isCurrent)}
                                        depth={2}
                                    />

                                    {(inquiryNode.quotations ?? []).map((quotationNode) => (
                                        <div key={`inquiry-quotation-${quotationNode.id}`} className="space-y-1">
                                            <OrderNode
                                                label="Offert"
                                                type="quotation"
                                                id={quotationNode.id}
                                                isActive={isCurrent('quotation', quotationNode.id, quotationNode.isCurrent)}
                                                depth={3}
                                            />

                                            {(quotationNode.supplierOrders ?? []).map((supplierOrderNode) => (
                                                <div key={`inquiry-quotation-supplier-order-${supplierOrderNode.id}`} className="space-y-1">
                                                    <OrderNode
                                                        label="Leverantörsorder"
                                                        type="supplierOrder"
                                                        id={supplierOrderNode.id}
                                                        isActive={isCurrent('supplierOrder', supplierOrderNode.id, supplierOrderNode.isCurrent)}
                                                        depth={4}
                                                    />
                                                    {(supplierOrderNode.customerOrders ?? []).map((customerOrderNode) => (
                                                        <OrderNode
                                                            key={`inquiry-quotation-customer-order-${customerOrderNode.id}`}
                                                            label="Kundorder"
                                                            type="customerOrder"
                                                            id={customerOrderNode.id}
                                                            isActive={isCurrent('customerOrder', customerOrderNode.id, customerOrderNode.isCurrent)}
                                                            depth={5}
                                                        />
                                                    ))}
                                                </div>
                                            ))}

                                            {(quotationNode.customerOrders ?? []).map((customerOrderNode) => (
                                                <OrderNode
                                                    key={`inquiry-quotation-direct-customer-order-${customerOrderNode.id}`}
                                                    label="Kundorder"
                                                    type="customerOrder"
                                                    id={customerOrderNode.id}
                                                    isActive={isCurrent('customerOrder', customerOrderNode.id, customerOrderNode.isCurrent)}
                                                    depth={4}
                                                />
                                            ))}
                                        </div>
                                    ))}

                                    {(inquiryNode.supplierOrders ?? []).map((supplierOrderNode) => (
                                        <div key={`inquiry-supplier-order-${supplierOrderNode.id}`} className="space-y-1">
                                            <OrderNode
                                                label="Leverantörsorder"
                                                type="supplierOrder"
                                                id={supplierOrderNode.id}
                                                isActive={isCurrent('supplierOrder', supplierOrderNode.id, supplierOrderNode.isCurrent)}
                                                depth={3}
                                            />
                                            {(supplierOrderNode.customerOrders ?? []).map((customerOrderNode) => (
                                                <OrderNode
                                                    key={`inquiry-supplier-customer-order-${customerOrderNode.id}`}
                                                    label="Kundorder"
                                                    type="customerOrder"
                                                    id={customerOrderNode.id}
                                                    isActive={isCurrent('customerOrder', customerOrderNode.id, customerOrderNode.isCurrent)}
                                                    depth={4}
                                                />
                                            ))}
                                        </div>
                                    ))}
                                </div>
                            ))}

                            {(calculationNode.quotations ?? []).map((quotationNode) => (
                                <div key={`calculation-quotation-${quotationNode.id}`} className="space-y-1">
                                    <OrderNode
                                        label="Offert"
                                        type="quotation"
                                        id={quotationNode.id}
                                        isActive={isCurrent('quotation', quotationNode.id, quotationNode.isCurrent)}
                                        depth={2}
                                    />

                                    {(quotationNode.supplierOrders ?? []).map((supplierOrderNode) => (
                                        <div key={`calculation-quotation-supplier-order-${supplierOrderNode.id}`} className="space-y-1">
                                            <OrderNode
                                                label="Leverantörsorder"
                                                type="supplierOrder"
                                                id={supplierOrderNode.id}
                                                isActive={isCurrent('supplierOrder', supplierOrderNode.id, supplierOrderNode.isCurrent)}
                                                depth={3}
                                            />
                                            {(supplierOrderNode.customerOrders ?? []).map((customerOrderNode) => (
                                                <OrderNode
                                                    key={`calculation-quotation-customer-order-${customerOrderNode.id}`}
                                                    label="Kundorder"
                                                    type="customerOrder"
                                                    id={customerOrderNode.id}
                                                    isActive={isCurrent('customerOrder', customerOrderNode.id, customerOrderNode.isCurrent)}
                                                    depth={4}
                                                />
                                            ))}
                                        </div>
                                    ))}

                                    {(quotationNode.customerOrders ?? []).map((customerOrderNode) => (
                                        <OrderNode
                                            key={`calculation-quotation-direct-customer-order-${customerOrderNode.id}`}
                                            label="Kundorder"
                                            type="customerOrder"
                                            id={customerOrderNode.id}
                                            isActive={isCurrent('customerOrder', customerOrderNode.id, customerOrderNode.isCurrent)}
                                            depth={3}
                                        />
                                    ))}
                                </div>
                            ))}

                            {(calculationNode.supplierOrders ?? []).map((supplierOrderNode) => (
                                <div key={`calculation-supplier-order-${supplierOrderNode.id}`} className="space-y-1">
                                    <OrderNode
                                        label="Leverantörsorder"
                                        type="supplierOrder"
                                        id={supplierOrderNode.id}
                                        isActive={isCurrent('supplierOrder', supplierOrderNode.id, supplierOrderNode.isCurrent)}
                                        depth={2}
                                    />
                                    {(supplierOrderNode.customerOrders ?? []).map((customerOrderNode) => (
                                        <OrderNode
                                            key={`calculation-supplier-customer-order-${customerOrderNode.id}`}
                                            label="Kundorder"
                                            type="customerOrder"
                                            id={customerOrderNode.id}
                                            isActive={isCurrent('customerOrder', customerOrderNode.id, customerOrderNode.isCurrent)}
                                            depth={3}
                                        />
                                    ))}
                                </div>
                            ))}
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
};

export default OrderNavigationTree;
