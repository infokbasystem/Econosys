import { useEffect, useEffectEvent, useId, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Check, RefreshCw, Save, X } from 'lucide-react';

import ActionButton from '../components/ActionButton';
import LabeledCheckbox from '../components/LabeledCheckbox';
import LabeledDatePicker from '../components/LabeledDatePicker';
import LabeledInput from '../components/LabeledInput';
import LabeledReactSelect from '../components/LabeledReactSelect';
import apiClient from '../config/apiClient';
import { getDeliveryEditForm, updateDeliveryQuantity } from '../helpers/deliveryEditing';
import { getSharedRequest } from '../helpers/sharedRequest';

const titles = {
    DeliveryToStock: 'Leverans till lager',
    DeliveryFromStock: 'Leverans från lager',
    DeliveryToCustomer: 'Direktleverans',
};

const errorMessage = (error, fallback) => {
    const data = error?.response?.data;
    return typeof data === 'string' ? data : data?.message ?? data?.title ?? fallback;
};

const formatQuantity = (value) => value == null ? '-' : new Intl.NumberFormat('sv-SE').format(value);

const DeliveryEditor = ({ deliveryType, deliveryId, onClose, onSaved }) => {
    const [metadata, setMetadata] = useState(null);
    const [form, setForm] = useState(null);
    const [error, setError] = useState('');
    const [previewError, setPreviewError] = useState('');
    const [previewStock, setPreviewStock] = useState(null);
    const [isSaving, setIsSaving] = useState(false);
    const [isStale, setIsStale] = useState(false);
    const [reload, setReload] = useState(0);
    const [confirmation, setConfirmation] = useState(null);
    const [discardRequested, setDiscardRequested] = useState(false);
    const savingRef = useRef(false);
    const activeRef = useRef(false);
    const dialogRef = useRef(null);
    const overlayRef = useRef(null);
    const confirmationRef = useRef(null);
    const titleId = useId();
    const path = `/deliveries/${encodeURIComponent(deliveryType)}/${deliveryId}/edit`;
    const isDirty = Boolean(metadata && form && JSON.stringify(form) !== JSON.stringify(getDeliveryEditForm(metadata)));

    useEffect(() => {
        let isActive = true;
        getSharedRequest(`delivery-editor:${path}:${reload}`, () => apiClient.get(path))
            .then((response) => {
                if (!isActive) return;
                setMetadata(response.data);
                setForm(getDeliveryEditForm(response.data));
                setError('');
                setIsStale(false);
            })
            .catch((failure) => {
                if (isActive) setError(errorMessage(failure, 'Kunde inte läsa in leveransen.'));
            });
        return () => { isActive = false; };
    }, [path, reload]);

    useEffect(() => {
        if (!metadata || !form || !isDirty || isSaving || isStale) return;
        let isActive = true;
        const controller = new AbortController();
        const timer = setTimeout(() => {
            apiClient.post(`${path}/preview`, { ...form, version: metadata.version }, { signal: controller.signal })
                .then((response) => {
                    if (!isActive) return;
                    setPreviewError('');
                    setPreviewStock({ items: response.data.availableItems, pallets: response.data.availablePallets });
                    if (metadata.autoFreightCalc) {
                        const factor = response.data.delivery.palletCalcFactor;
                        setForm((previous) => previous.palletCalcFactor === factor ? previous : { ...previous, palletCalcFactor: factor });
                    }
                })
                .catch((failure) => {
                    if (isActive && failure.code !== 'ERR_CANCELED') {
                        setPreviewError(errorMessage(failure, 'Kunde inte kontrollera leveransen.'));
                    }
                });
        }, 300);
        return () => {
            isActive = false;
            clearTimeout(timer);
            controller.abort();
        };
    }, [path, metadata, form, isDirty, isSaving, isStale]);

    const requestClose = () => {
        if (savingRef.current) return;
        if (isDirty) setDiscardRequested(true);
        else onClose?.();
    };

    const onKeyDown = useEffectEvent((event) => {
        if (event.defaultPrevented) return;
        if (event.key === 'Escape') {
            event.preventDefault();
            if (savingRef.current) return;
            if (confirmation) setConfirmation(null);
            else if (discardRequested) setDiscardRequested(false);
            else {
                const expandedButton = dialogRef.current?.querySelector('button[aria-expanded="true"]');
                if (expandedButton) expandedButton.click();
                else requestClose();
            }
        }
        if (event.key !== 'Tab') return;
        const focusable = [...dialogRef.current.querySelectorAll('button:not(:disabled), input:not(:disabled), [tabindex="0"]')]
            .filter((element) => element.getClientRects().length > 0);
        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        if (!first) {
            event.preventDefault();
            dialogRef.current.focus();
        } else if (event.shiftKey && (document.activeElement === first || document.activeElement === dialogRef.current)) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first.focus();
        }
    });

    useEffect(() => {
        activeRef.current = true;
        const previousFocus = document.activeElement;
        const previousOverflow = document.body.style.overflow;
        const background = [...document.body.children]
            .filter((element) => element instanceof HTMLElement && element !== overlayRef.current)
            .map((element) => ({ element, inert: element.inert }));
        background.forEach(({ element }) => { element.inert = true; });
        document.body.style.overflow = 'hidden';
        dialogRef.current.focus();
        document.addEventListener('keydown', onKeyDown);
        return () => {
            activeRef.current = false;
            document.removeEventListener('keydown', onKeyDown);
            background.forEach(({ element, inert }) => { element.inert = inert; });
            document.body.style.overflow = previousOverflow;
            if (previousFocus instanceof HTMLElement && previousFocus.isConnected) previousFocus.focus();
        };
    }, []);

    useEffect(() => {
        if (confirmation || discardRequested) confirmationRef.current?.focus();
    }, [confirmation, discardRequested]);

    const updateField = (field, value) => {
        setError('');
        setPreviewError('');
        setPreviewStock(null);
        setForm((previous) => updateDeliveryQuantity(previous, field, value, metadata));
    };

    const reloadDelivery = () => {
        if (isDirty && !window.confirm('Läs in leveransen igen och kasta dina ändringar?')) return;
        setMetadata(null);
        setForm(null);
        setError('');
        setPreviewError('');
        setPreviewStock(null);
        setConfirmation(null);
        setReload((previous) => previous + 1);
    };

    const save = async (confirmedStockTakingDate = null) => {
        if (!metadata || !form || savingRef.current || isStale) return;
        savingRef.current = true;
        setIsSaving(true);
        setError('');
        setConfirmation(null);
        let saved;
        try {
            const response = await apiClient.put(path, {
                ...form,
                version: metadata.version,
                confirmedStockTakingDate,
            });
            saved = response.data;
        } catch (failure) {
            if (activeRef.current) {
                const data = failure?.response?.data;
                if (data?.code === 'StockTakingConfirmationRequired') setConfirmation(data);
                else {
                    setError(errorMessage(failure, 'Kunde inte spara leveransen.'));
                    if (data?.code === 'StaleVersion') setIsStale(true);
                }
            }
        } finally {
            savingRef.current = false;
            if (activeRef.current) setIsSaving(false);
        }
        if (saved && activeRef.current) {
            onClose?.();
            onSaved?.(saved);
        }
    };

    const numberInput = (field, label, disabled = false) => (
        <LabeledInput
            label={label}
            labelWidth="w-32"
            name={field}
            aria-label={label}
            type="number"
            integerOnly={field !== 'nrOfItems' && field !== 'palletCalcFactor'}
            value={form[field]}
            disabled={disabled || isSaving}
            onChange={(value) => updateField(field, value)}
        />
    );
    const delivery = metadata?.delivery;
    const isFromStock = delivery?.type === 'DeliveryFromStock';
    const hasInventory = delivery?.type !== 'DeliveryToCustomer';
    const lowQuantity = form?.nrOfItems > 0 && metadata?.orderedQty > 0 && form.nrOfItems < metadata.orderedQty * 0.9;

    return createPortal(
        <div ref={overlayRef} className="fixed inset-0 z-[100] flex items-center justify-center bg-black/35 p-3">
            <section
                ref={dialogRef}
                role="dialog"
                aria-modal="true"
                aria-labelledby={titleId}
                aria-busy={!metadata && !error || isSaving}
                tabIndex={-1}
                className="flex max-h-[calc(100dvh-1.5rem)] w-full max-w-lg flex-col rounded-sm border border-gray-400 bg-[#f4f3f2] text-xs shadow-xl outline-none"
            >
                <header className="flex shrink-0 items-center justify-between gap-3 border-b border-gray-300 px-5 py-3">
                    <h2 id={titleId} className="text-sm font-semibold text-gray-800">{titles[deliveryType] ?? 'Leverans'} {deliveryId}</h2>
                    <button type="button" onClick={requestClose} disabled={isSaving} title="Stäng" aria-label="Stäng" className="flex h-6 w-6 shrink-0 items-center justify-center text-gray-600 hover:text-gray-900 disabled:opacity-50">
                        <X size={18} />
                    </button>
                </header>
                <div className="min-h-0 overflow-y-auto px-5 py-4">
                    {!metadata && !error && <p role="status" className="text-gray-600">Läser in leveransen...</p>}
                    {metadata && form && (
                        <>
                            <div className="mb-4 space-y-1 border-b border-gray-300 pb-3 text-gray-700">
                                <p className="break-words font-semibold">{delivery.productName || '-'}</p>
                                <p className="break-words">{delivery.customerName}</p>
                                <p>Beställning {delivery.supplierOrderNr || '-'} · Kundorder {delivery.customersOrderNr || '-'}</p>
                                {isFromStock && (
                                    <p className="text-sky-700">
                                        {formatQuantity(previewStock?.items ?? metadata.availableItems)} st. / {formatQuantity(previewStock?.pallets ?? metadata.availablePallets)} pall tillgängligt
                                    </p>
                                )}
                            </div>
                            <fieldset disabled={isSaving} className="min-w-0 space-y-2">
                                <div className="[&_.daypicker]:left-auto [&_.daypicker]:right-0">
                                    <LabeledDatePicker label="Datum" labelWidth="w-32" name="deliveryDate" value={form.deliveryDate} valueType="input" disabled={isSaving} onChange={(value) => updateField('deliveryDate', value)} />
                                </div>
                                <LabeledInput label="Beställt antal" labelWidth="w-32" type="number" value={metadata.orderedQty} disabled />
                                {numberInput('nrOfItems', 'Antal')}
                                {isFromStock && metadata.packagingType === 'BUNT' && numberInput('nrOfBunt', 'Antal bunt')}
                                {isFromStock && metadata.packagingType === 'YTTERFORPACKNING' && numberInput('nrOfYtterforpackning', 'Antal ytterförp.')}
                                {numberInput('nrOfPallets', 'Antal pall')}
                                {isFromStock && <LabeledInput label="Märkning" labelWidth="w-32" name="callOff" aria-label="Märkning" value={form.callOff} maxLength={50} onChange={(value) => updateField('callOff', value)} />}
                                {hasInventory && <LabeledReactSelect label="Lager" labelWidth="w-32" name="inventoryId" aria-label="Lager" value={form.inventoryId} items={metadata.inventories} isDisabled={isSaving} onChange={(value) => updateField('inventoryId', value === '' ? null : Number(value))} />}
                                {numberInput('palletLength', 'Pallängd (mm)', delivery.isSlattPallet)}
                                {numberInput('palletWidth', 'Pallbredd (mm)', delivery.isSlattPallet)}
                                {numberInput('palletHeight', 'Pallhöjd (mm)', delivery.isSlattPallet)}
                                <LabeledCheckbox label="Staplingsbar" labelWidth="w-32" labelPosition="left" className="w-full" checked={form.palletIsStackable} disabled={isSaving} onChange={(value) => updateField('palletIsStackable', value)} />
                                <LabeledCheckbox label="Slattpall" labelWidth="w-32" labelPosition="left" className="w-full" checked={delivery.isSlattPallet} disabled />
                                <LabeledInput label="Upplaga per pall" labelWidth="w-32" type="number" value={delivery.editionPerPallet} disabled />
                                {numberInput('palletCalcFactor', 'Pallfaktor', metadata.autoFreightCalc)}
                                <LabeledCheckbox label="Levererad" labelWidth="w-32" labelPosition="left" className="w-full" checked={form.isDelivered} disabled={isSaving} onChange={(value) => updateField('isDelivered', value)} />
                                {!isFromStock && (
                                    <div className="space-y-2 border-t border-gray-300 pt-3">
                                        <LabeledInput label="Spcs ref.nr" labelWidth="w-32" name="spcsRefNr" aria-label="Spcs ref.nr" value={form.spcsRefNr} maxLength={50} onChange={(value) => updateField('spcsRefNr', value)} />
                                        {numberInput('supplierInvoiceNr', 'Lev.fakturanr.')}
                                    </div>
                                )}
                            </fieldset>
                            {lowQuantity && <p role="status" className="mt-3 text-amber-700">Leveransantal mindre än 90% av beställt antal.</p>}
                        </>
                    )}
                    {(error || previewError) && <p role="alert" className="mt-3 break-words text-red-700">{error || previewError}</p>}
                </div>
                <footer className="shrink-0 space-y-3 border-t border-gray-300 px-5 py-3">
                    {confirmation ? (
                        <div role="alert" className="space-y-3">
                            <p className="text-amber-800">{confirmation.message}</p>
                            <div className="flex flex-wrap justify-end gap-4">
                                <button ref={confirmationRef} type="button" onClick={() => setConfirmation(null)} className="text-gray-700 hover:underline">Tillbaka</button>
                                <ActionButton label="Spara ändå" icon={Check} accent="lime" onClick={() => save(confirmation.stockTakingDate)} />
                            </div>
                        </div>
                    ) : discardRequested ? (
                        <div role="alert" className="space-y-3">
                            <p className="text-gray-700">Kasta osparade ändringar?</p>
                            <div className="flex flex-wrap justify-end gap-4">
                                <button ref={confirmationRef} type="button" onClick={() => setDiscardRequested(false)} className="text-gray-700 hover:underline">Tillbaka</button>
                                <ActionButton label="Kasta ändringar" icon={X} accent="rose" onClick={onClose} />
                            </div>
                        </div>
                    ) : (
                        <div className="flex flex-wrap items-center justify-end gap-4">
                            {(!metadata || isStale) && error && <ActionButton label="Läs in igen" icon={RefreshCw} onClick={reloadDelivery} />}
                            <ActionButton label="Avbryt" icon={X} accent="slate" onClick={requestClose} disabled={isSaving} />
                            <ActionButton label={isSaving ? 'Sparar...' : 'Spara'} icon={Save} accent="lime" onClick={() => save()} disabled={!metadata || isSaving || isStale} />
                        </div>
                    )}
                </footer>
            </section>
        </div>,
        document.body,
    );
};

const DeliveryModal = ({ isOpen, deliveryType, deliveryId, onClose, onSaved }) => {
    if (!isOpen) return null;
    return <DeliveryEditor key={`${deliveryType}:${deliveryId}`} deliveryType={deliveryType} deliveryId={deliveryId} onClose={onClose} onSaved={onSaved} />;
};

export default DeliveryModal;