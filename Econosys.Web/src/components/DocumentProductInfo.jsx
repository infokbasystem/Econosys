import { useEffect, useState } from 'react';
import { AlertTriangle, ExternalLink, RefreshCw } from 'lucide-react';
import apiClient from '../config/apiClient';
import ConfirmationModal from './ConfirmationModal';
import { canRefreshDocumentProduct, hasDocumentProductDifference } from '../helpers/documentProductSnapshot';

const InfoRow = ({ label, value }) => (
    <div className="min-w-0">
        <dt className="text-tiny tracking-wide font-bold text-black">{label}</dt>
        <dd className="mt-1 whitespace-pre-wrap break-words text-xs text-black">
            {value == null || value === '' ? '-' : value}
        </dd>
    </div>
);

const DocumentProductInfo = ({ type, document, dirty, onRefreshed }) => {
    const [comparison, setComparison] = useState(null);
    const [error, setError] = useState('');
    const [refreshing, setRefreshing] = useState(false);
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const [draftProduct, setDraftProduct] = useState(null);
    const id = Number(document?.id) > 0 ? Number(document.id) : null;
    const edited = document?.editedAt ?? document?.edited ?? null;
    const calculationId = Number(document?.calculationId) > 0 ? Number(document.calculationId) : null;

    useEffect(() => {
        if (!id) return undefined;
        let active = true;
        apiClient.get(`/document-products/${type}/${id}`).then(({ data }) => {
            if (active) {
                setComparison({ key: `${type}:${id}:${edited}`, ...data });
                setError('');
            }
        }).catch((requestError) => {
            if (active) {
                setComparison(null);
                setError(requestError.response?.data?.detail || 'Kunde inte jämföra produktinformationen.');
            }
        });
        return () => { active = false; };
    }, [type, id, edited]);

    useEffect(() => {
        if (id || !calculationId) return undefined;
        let active = true;
        apiClient.get(`/calculations/${calculationId}/aggregate`).then(({ data }) => {
            if (active) setDraftProduct({ calculationId, productId: data?.product?.id });
        }).catch((requestError) => {
            if (active) setError(requestError.response?.data?.detail || 'Kunde inte läsa produktlänken.');
        });
        return () => { active = false; };
    }, [id, calculationId]);

    const activeComparison = comparison?.key === `${type}:${id}:${edited}` ? comparison : null;
    const changed = activeComparison && hasDocumentProductDifference(activeComparison.saved, activeComparison.current);
    const canRefresh = canRefreshDocumentProduct({ id, changed, dirty, refreshing });
    const externalNote = type === 'quotations' || type === 'customerorders'
        ? document?.productMessage : document?.productNoteExternal;
    const productId = activeComparison?.productId || (!id && draftProduct?.calculationId === calculationId ? draftProduct.productId : null);
    const productLink = productId ? `/order/products/${productId}` : '/order/products';

    const refresh = async () => {
        if (dirty && id && changed && !refreshing) {
            setShowUnsavedWarning(true);
            return;
        }
        if (!canRefresh) return;
        setRefreshing(true);
        setError('');
        try {
            const { data } = await apiClient.put(`/document-products/${type}/${id}`, { edited: activeComparison.edited });
            setComparison({ key: `${type}:${id}:${data.edited}`, ...data });
            onRefreshed(data);
        } catch (requestError) {
            setError(requestError.response?.data?.detail || 'Kunde inte uppdatera produktinformationen.');
        } finally {
            setRefreshing(false);
        }
    };

    return (
        <div className="ml-[-5px] mb-5 w-280 rounded-sm border border-gray-200 bg-[#e8eeed] px-5 pt-3 pb-3 shadow-sm">
            <dl className="min-w-0 flex flex-row gap-5">
                <div className="w-60 space-y-2">
                    <InfoRow label="Produkt" value={document?.product} />
                    <InfoRow label="Material" value={[document?.material, document?.materialThickness].filter(Boolean).join(' ')} />
                </div>
                <div className="w-60 space-y-2">
                    <InfoRow label="Konstruktion" value={document?.construction} />
                    <InfoRow label="Format" value={document?.format} />
                </div>
                <div className="w-30 space-y-2">
                    <InfoRow label="Färger" value={document?.color} />
                    <InfoRow label="Lack" value={document?.varnish} />
                </div>
                <div className="w-40 space-y-2">
                    <InfoRow label="Not. intern" value={document?.productNoteInternal} />
                    <InfoRow label="Not. extern" value={externalNote} />
                </div>
                <div className="min-w-0 flex-1 space-y-2">
                    <InfoRow label="Tryckmetod (intern)" value={document?.printMethod} />
                    <div className="mt-4 flex min-w-0 items-end justify-between gap-2">
                        <a href={productLink} target="_blank" rel="noopener noreferrer"
                            className="shrink-0 whitespace-nowrap text-xs text-blue-800 hover:underline">
                            <span className="inline-flex items-center gap-1">
                                {productId ? 'Öppna produkt i ny flik' : 'Visa produkter i ny flik'}
                                <ExternalLink size={14} aria-hidden="true" />
                            </span>
                        </a>
                        {changed ? (
                            <span className="inline-flex items-center gap-2 text-amber-700">
                                <AlertTriangle size={17} aria-label="Produktinformationen skiljer sig från produkten" role="img" />
                                <button type="button" onClick={refresh} disabled={refreshing}
                                    title="Uppdatera produktinformationen"
                                    aria-label="Uppdatera produktinformationen"
                                    className="disabled:cursor-not-allowed disabled:opacity-50">
                                    <RefreshCw size={17} aria-hidden="true" />
                                </button>
                            </span>
                        ) : null}
                    </div>
                </div>
            </dl>
            {error ? <p role="alert" className="mt-2 text-xs text-red-700">{error}</p> : null}
            <ConfirmationModal
                isOpen={showUnsavedWarning}
                onClose={() => setShowUnsavedWarning(false)}
                title="Osparade ändringar"
                message="Spara eller ångra ändringarna innan produktinformationen uppdateras."
                confirmText=""
                cancelText="Stäng"
            />
        </div>
    );
};

export default DocumentProductInfo;
