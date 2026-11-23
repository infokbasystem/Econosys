import { useEffect, useState } from 'react';
import { ExternalLink } from 'lucide-react';
import apiClient from '../config/apiClient';
import { positiveId, resolveSupplierOrderCalculationId } from './supplierOrderCalculation';

const formatDimensions = (product) => {
    const dimensions = [product?.lengthMm, product?.widthMm, product?.heightMm];
    if (dimensions.some((value) => value == null || value === '')) return '';
    return dimensions.join(' x ');
};

const InfoRow = ({ label, value }) => (
    <div className="min-w-0">
        <dt className="text-tiny tracking-wide font-bold text-black">{label}</dt>
        <dd className="mt-1 whitespace-pre-wrap break-words text-xs text-black">
            {value == null || value === '' ? '-' : value}
        </dd>
    </div>
);

const OrderProductInfo = ({
    calculationId, productId, inquiryId, quotationId, supplierOrderId, selectedCalculationRowId, linkedSupplierOrderId,
}) => {
    const [loadedInfo, setLoadedInfo] = useState(null);

    const calculationNumber = positiveId(calculationId);
    const productNumber = positiveId(productId);
    const inquiryNumber = positiveId(inquiryId);
    const quotationNumber = positiveId(quotationId);
    const supplierOrderNumber = positiveId(supplierOrderId);
    const calculationRowNumber = positiveId(selectedCalculationRowId);
    const linkedSupplierOrderNumber = positiveId(linkedSupplierOrderId);
    const hasSupplierOrderSource = inquiryNumber || quotationNumber || linkedSupplierOrderNumber || (supplierOrderNumber && calculationRowNumber);
    const requestKey = calculationNumber
        ? `calculation:${calculationNumber}`
        : productNumber
            ? `product:${productNumber}`
            : hasSupplierOrderSource
                ? `supplier-order:${supplierOrderNumber}:${calculationRowNumber}:${inquiryNumber}:${quotationNumber}:${linkedSupplierOrderNumber}`
                : null;

    useEffect(() => {
        if (!requestKey) return undefined;

        let isActive = true;
        const loadProductInfo = async () => {
            try {
                let resolvedProductId = productNumber;
                if (!resolvedProductId || calculationNumber) {
                    const resolvedCalculationId = calculationNumber || await resolveSupplierOrderCalculationId(apiClient, {
                        inquiryId: inquiryNumber,
                        quotationId: quotationNumber,
                        supplierOrderId: supplierOrderNumber,
                        selectedCalculationRowId: calculationRowNumber,
                        linkedSupplierOrderId: linkedSupplierOrderNumber,
                    });
                    const response = await apiClient.get(`/calculations/${resolvedCalculationId}/aggregate`);
                    resolvedProductId = response.data?.product?.id;
                }
                if (!resolvedProductId) {
                    throw new Error('Ingen produkt är kopplad till kalkylen.');
                }
                const productResponse = await apiClient.get(`/products/${resolvedProductId}`);
                if (isActive) {
                    setLoadedInfo({
                        key: requestKey,
                        product: productResponse.data,
                        error: '',
                    });
                }
            } catch (requestError) {
                if (!isActive) return;
                setLoadedInfo({
                    key: requestKey,
                    product: null,
                    error: requestError.response?.data?.message
                        || requestError.message
                        || 'Kunde inte läsa produktinformationen.',
                });
            }
        };
        loadProductInfo();

        return () => {
            isActive = false;
        };
    }, [requestKey, calculationNumber, productNumber, inquiryNumber, quotationNumber, supplierOrderNumber, calculationRowNumber, linkedSupplierOrderNumber]);

    const hasCurrentInfo = requestKey != null && loadedInfo?.key === requestKey;
    const product = hasCurrentInfo ? loadedInfo.product : null;
    const loading = requestKey != null && !hasCurrentInfo;
    const error = hasCurrentInfo ? loadedInfo.error : '';
    const info = product || {};
    const printingMethods = [
        info.isOffset && 'Offset',
        info.isDigital && 'Digital',
        info.isFlexo && 'Flexo',
    ].filter(Boolean).join(', ');
    const varnish = info.varnishOther
        ? `${info.varnishName || 'Övrig'}: ${info.varnishOther}`
        : info.varnishName;
    const format = formatDimensions(info) || info.format;
    const linkedProductId = product?.id || productNumber;
    const productLink = linkedProductId
        ? { href: `/order/products/${linkedProductId}`, label: 'Öppna produkt i ny flik' }
        : { href: '/order/products', label: 'Visa produkter i ny flik' };

    return (
        <div
            aria-busy={loading}
            className="ml-[-5px] mb-5 w-280 rounded-sm border border-gray-200 bg-[#e8eeed] px-5 pt-3 pb-3 shadow-sm"
        >
            <dl className="min-w-0 flex flex-row gap-5">
                <div className="w-60 space-y-2">
                    <InfoRow label="Produkt" value={info.name} />
                    <InfoRow label="Material" value={info.materialName} />
                </div>
                <div className="w-60 space-y-2">
                    <InfoRow label="Konstruktion" value={info.construction?.name} />
                    <InfoRow label="Format" value={format} />
                </div>
                <div className="w-30 space-y-2">
                    <InfoRow label="Färger" value={info.nrOfColors} />
                    <InfoRow label="Lack" value={varnish} />
                </div>
                <div className="w-40 space-y-2">
                    <InfoRow label="Not. intern" value={info.noteInternal} />
                    <InfoRow label="Not. extern" value={info.noteExternal} />
                </div>
                <div className="min-w-0 flex-1 space-y-2">
                    <InfoRow label="Tryckmetod (intern)" value={printingMethods} />
                    <div className="mt-4 flex min-w-0 items-end justify-between gap-2">
                        <a
                            href={productLink.href}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="shrink-0 whitespace-nowrap text-xs text-blue-800 hover:underline"
                        >
                            <span className="inline-flex items-center gap-1">
                                {productLink.label}
                                <ExternalLink size={14} aria-hidden="true" />
                            </span>
                        </a>
                    </div>
                </div>
            </dl>
            {loading ? <span role="status" className="sr-only">Laddar produktinformation...</span> : null}
            {error ? (
                <p role="alert" className="mt-2 text-xs text-red-700">{error}</p>
            ) : null}
            {!requestKey ? (
                <p className="mt-2 text-xs text-gray-700">Ingen produkt är kopplad till dokumentet.</p>
            ) : null}
        </div>
    );
};

export default OrderProductInfo;
