import { useEffect, useState } from 'react';
import { ExternalLink } from 'lucide-react';
import apiClient from '../config/apiClient';

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

const CompactInfoRow = ({ label, value }) => (
    <div className="min-w-0">
        <dt className="truncate text-tiny tracking-wide font-bold text-black">{label}</dt>
        <dd
            title={value == null || value === '' ? '-' : String(value)}
            className="mt-1 truncate text-xs text-black"
        >
            {value == null || value === '' ? '-' : value}
        </dd>
    </div>
);

const OrderProductInfo = ({ calculationId, productId, fallbackProduct, additionalInfo = [] }) => {
    const [loadedInfo, setLoadedInfo] = useState(null);

    useEffect(() => {
        const calculationNumber = Number(calculationId);
        const productNumber = Number(productId);
        const hasCalculationId = Number.isInteger(calculationNumber) && calculationNumber > 0;
        const hasProductId = Number.isInteger(productNumber) && productNumber > 0;
        const requestKey = hasCalculationId
            ? `calculation:${calculationNumber}`
            : hasProductId
                ? `product:${productNumber}`
                : null;
        if (!requestKey) return undefined;

        let isActive = true;
        const loadProductInfo = async () => {
            try {
                if (!hasCalculationId) {
                    const response = await apiClient.get(`/products/${productNumber}`);
                    if (isActive) {
                        setLoadedInfo({
                            key: requestKey,
                            product: {
                                id: productNumber,
                                isOffset: Boolean(response.data?.isOffset),
                                isDigital: Boolean(response.data?.isDigital),
                                isFlexo: Boolean(response.data?.isFlexo),
                            },
                            error: '',
                            useFallbackProduct: true,
                        });
                    }
                    return;
                }

                const response = await apiClient.get(`/calculations/${calculationNumber}/aggregate`);
                const summary = response.data?.product ?? null;
                if (!summary?.id) {
                    if (isActive) {
                        setLoadedInfo({ key: requestKey, product: summary, error: '' });
                    }
                    return;
                }

                const productResponse = await apiClient.get(`/products/${summary.id}`);
                if (isActive) {
                    setLoadedInfo({
                        key: requestKey,
                        product: {
                            ...summary,
                            isOffset: Boolean(productResponse.data?.isOffset),
                            isDigital: Boolean(productResponse.data?.isDigital),
                            isFlexo: Boolean(productResponse.data?.isFlexo),
                        },
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
    }, [calculationId, productId]);

    const calculationNumber = Number(calculationId);
    const productNumber = Number(productId);
    const hasCalculationId = Number.isInteger(calculationNumber) && calculationNumber > 0;
    const hasProductId = Number.isInteger(productNumber) && productNumber > 0;
    const requestKey = hasCalculationId
        ? `calculation:${calculationNumber}`
        : hasProductId
            ? `product:${productNumber}`
            : null;
    const hasCurrentInfo = requestKey != null && loadedInfo?.key === requestKey;
    const product = hasCurrentInfo ? loadedInfo.product : null;
    const loading = requestKey != null && !hasCurrentInfo;
    const error = hasCurrentInfo ? loadedInfo.error : '';
    const info = hasCurrentInfo && loadedInfo.useFallbackProduct
        ? { ...(fallbackProduct || {}), ...product }
        : product || fallbackProduct || {};
    const printingMethods = [
        info.isOffset && 'Offset',
        info.isDigital && 'Digital',
        info.isFlexo && 'Flexo',
    ].filter(Boolean).join(', ');
    const varnish = info.varnishOther
        ? `${info.varnish || 'Övrig'}: ${info.varnishOther}`
        : info.varnish;
    const format = formatDimensions(info) || info.format;
    const linkedProductId = product?.id || (hasProductId ? productNumber : null);
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
                    <InfoRow label="Produkt" value={info.name || info.product} />
                    <InfoRow label="Material" value={info.material} />
                </div>
                <div className="w-60 space-y-2">
                    <InfoRow label="Konstruktion" value={info.construction} />
                    <InfoRow label="Format" value={format} />
                </div>
                <div className="w-30 space-y-2">
                    <InfoRow label="Färger" value={info.colors ?? info.color} />
                    <InfoRow label="Lack" value={varnish} />
                </div>
                <div className="w-40 space-y-2">
                    <InfoRow label="Not. intern" value={info.noteInternal} />
                    <InfoRow label="Not. extern" value={info.noteExternal} />
                </div>
                <div className="min-w-0 flex-1 space-y-2">
                    <InfoRow label="Tryckmetod (intern)" value={printingMethods} />
                    <div className="mt-4 flex min-w-0 items-end justify-between gap-2">
                        {additionalInfo.length > 0 ? (
                            <div className="min-w-0 flex-1 space-y-2">
                                {additionalInfo.map(({ label, value }) => (
                                    <CompactInfoRow key={label} label={label} value={value} />
                                ))}
                            </div>
                        ) : null}
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
        </div>
    );
};

export default OrderProductInfo;
