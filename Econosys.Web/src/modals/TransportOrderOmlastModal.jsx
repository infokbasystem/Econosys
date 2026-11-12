import { useEffect, useEffectEvent, useId, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { RefreshCw, Save, X } from 'lucide-react';

import ActionButton from '../components/ActionButton';
import LabeledCheckbox from '../components/LabeledCheckbox';
import LabeledDatePicker from '../components/LabeledDatePicker';
import LabeledInput from '../components/LabeledInput';
import LabeledReactSelect from '../components/LabeledReactSelect';
import LabeledTextArea from '../components/LabeledTextArea';
import apiClient from '../config/apiClient';
import { getSharedRequest } from '../helpers/sharedRequest';
import { getOmlastForm, showsOmlastFields } from '../helpers/transportOrderOmlast';

const OmlastEditor = ({ transportOrderId, delivery, onClose, onSave }) => {
    const [info, setInfo] = useState(null);
    const [form, setForm] = useState(null);
    const [error, setError] = useState('');
    const [reload, setReload] = useState(0);
    const dialogRef = useRef(null);
    const overlayRef = useRef(null);
    const titleId = useId();
    const path = `/transportorder/${transportOrderId}/deliveries/${delivery.id}/omlast-info`;

    useEffect(() => {
        let isActive = true;
        getSharedRequest(`transportorder-omlast:${path}:${reload}`, () => apiClient.get(path))
            .then((response) => {
                if (!isActive) return;
                setInfo(response.data);
                setForm(getOmlastForm(delivery, response.data));
                setError('');
            })
            .catch((failure) => {
                if (!isActive) return;
                const data = failure?.response?.data;
                setError(typeof data === 'string' ? data : data?.message ?? 'Kunde inte läsa in omlastningsuppgifterna.');
            });
        return () => { isActive = false; };
    }, [delivery, path, reload]);

    const requestClose = () => {
        onClose?.();
    };

    const handleKeyDown = useEffectEvent((event) => {
        if (event.defaultPrevented) return;
        if (event.key === 'Escape') {
            event.preventDefault();
            const expandedDate = dialogRef.current.querySelector('button[aria-expanded="true"]');
            if (expandedDate) expandedDate.click();
            else requestClose();
        }
        if (event.key !== 'Tab') return;
        const controls = [...dialogRef.current.querySelectorAll('button:not(:disabled), input:not(:disabled), textarea:not(:disabled), [tabindex="0"]')]
            .filter((element) => element.getClientRects().length > 0);
        const first = controls[0];
        const last = controls[controls.length - 1];
        if (event.shiftKey && (document.activeElement === first || document.activeElement === dialogRef.current)) {
            event.preventDefault();
            last?.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first?.focus();
        }
    });

    useEffect(() => {
        const previousFocus = document.activeElement;
        const previousOverflow = document.body.style.overflow;
        const background = [...document.body.children]
            .filter((element) => element instanceof HTMLElement && element !== overlayRef.current)
            .map((element) => ({ element, inert: element.inert }));
        background.forEach(({ element }) => { element.inert = true; });
        document.body.style.overflow = 'hidden';
        dialogRef.current.focus();
        document.addEventListener('keydown', handleKeyDown);
        return () => {
            document.removeEventListener('keydown', handleKeyDown);
            document.body.style.overflow = previousOverflow;
            background.forEach(({ element, inert }) => { element.inert = inert; });
            if (previousFocus instanceof HTMLElement && previousFocus.isConnected) previousFocus.focus();
        };
    }, []);

    const update = (field, value) => {
        setError('');
        setForm((previous) => ({ ...previous, [field]: value }));
    };

    const save = () => {
        if (!info || !form) return;
        if (form.forcedOmlastStatus === 1 && !info.inventories.some((inventory) => inventory.id === form.omlastInventoryId)) {
            setError('Välj ett omlastningslager.');
            return;
        }
        if (form.omlastPalletPlaces < 0 || form.totalWeight < 0) {
            setError('Pallplatser och vikt får inte vara negativa.');
            return;
        }
        const inventory = info.inventories.find((item) => item.id === form.omlastInventoryId);
        const address = form.forcedOmlastStatus === 1
            ? [info.customerName, inventory?.address].filter(Boolean).join('\n')
            : form.forcedOmlastStatus === 2 ? info.directCustomerAddress : info.defaultCustomerAddress;
        onSave?.(form, address || delivery.customerNameAddress);
        onClose?.();
    };
    const showOmlast = Boolean(info && form && showsOmlastFields(info.isOmlast, form.forcedOmlastStatus));
    const selectedInventory = info?.inventories.find((item) => item.id === form?.omlastInventoryId);

    return createPortal(
        <div ref={overlayRef} className="fixed inset-0 z-[100] flex items-center justify-center bg-black/35 p-3">
            <section
                ref={dialogRef}
                role="dialog"
                aria-modal="true"
                aria-labelledby={titleId}
                aria-busy={!info && !error}
                tabIndex={-1}
                className="flex max-h-[calc(100dvh-1.5rem)] w-full max-w-lg flex-col rounded-sm border border-gray-400 bg-[#f4f3f2] text-xs shadow-xl outline-none"
            >
                <header className="flex shrink-0 items-center justify-between gap-3 border-b border-gray-300 px-5 py-3">
                    <h2 id={titleId} className="text-sm font-semibold text-gray-800">Omlastning {delivery.id}</h2>
                    <button type="button" title="Stäng" aria-label="Stäng" onClick={requestClose} className="flex h-6 w-6 items-center justify-center text-gray-600 hover:text-gray-900">
                        <X size={18} />
                    </button>
                </header>
                <div className="min-h-0 space-y-4 overflow-y-auto px-5 py-4">
                    {!info && !error && <p role="status" className="text-gray-600">Läser in omlastningsuppgifter...</p>}
                    {info && form && (
                        <>
                            <fieldset className="space-y-2" aria-label="Omlastningsläge">
                                <LabeledCheckbox inputType="radio" name={`${titleId}-default`} label="Default" checked={form.forcedOmlastStatus === 0} className="w-full" onChange={() => update('forcedOmlastStatus', 0)} />
                                {!info.isOmlast && <LabeledCheckbox inputType="radio" name={`${titleId}-omlast`} label="Tvingad omlast" checked={form.forcedOmlastStatus === 1} className="w-full" onChange={() => update('forcedOmlastStatus', 1)} />}
                                <LabeledCheckbox inputType="radio" name={`${titleId}-direct`} label="Tvingad direkt" checked={form.forcedOmlastStatus === 2} className="w-full" onChange={() => update('forcedOmlastStatus', 2)} />
                            </fieldset>
                            {showOmlast && (
                                <div className="space-y-2 border-t border-gray-300 pt-4">
                                    <LabeledReactSelect label="Omlast.lager" labelWidth="w-36" aria-label="Omlast.lager" value={form.omlastInventoryId} items={info.inventories} onChange={(value) => update('omlastInventoryId', value === '' ? null : Number(value))} />
                                    <div className="[&_.daypicker]:left-auto [&_.daypicker]:right-0">
                                        <LabeledDatePicker label="Omlast.datum" labelWidth="w-36" name="omlastInDate" value={form.omlastInDate} valueType="input" onChange={(value) => update('omlastInDate', value)} />
                                    </div>
                                    <div className="[&_.daypicker]:left-auto [&_.daypicker]:right-0">
                                        <LabeledDatePicker label="Lev.datum till kund" labelWidth="w-36" name="omlastDeliveryDate" value={form.omlastDeliveryDate} valueType="input" onChange={(value) => update('omlastDeliveryDate', value)} />
                                    </div>
                                    <LabeledTextArea label="Omlastningsnotering" labelWidth="w-36" aria-label="Omlastningsnotering" name="omlastNote" value={form.omlastNote} maxLength={500} rows={3} onChange={(value) => update('omlastNote', value)} />
                                    <div className="pt-2 text-gray-700">
                                        <p className="mb-1 font-medium">Omlastning</p>
                                        <p className="whitespace-pre-line break-words">{selectedInventory?.name || info.omlastAddress || '-'}</p>
                                    </div>
                                </div>
                            )}
                            <div className="border-t border-gray-300 pt-4 text-gray-700">
                                <p className="mb-1 font-medium">Leveransadress</p>
                                <p className="whitespace-pre-line break-words">{info.deliveryAddress || delivery.customerNameAddress || '-'}</p>
                            </div>
                            <div className="space-y-2 border-t border-gray-300 pt-4">
                                <LabeledInput label="Antal" labelWidth="w-36" type="number" value={info.nrOfItems} disabled />
                                <LabeledInput label="Antal pall" labelWidth="w-36" type="number" value={info.nrOfPallets} disabled />
                                <LabeledInput label="Antal pallplatser" labelWidth="w-36" aria-label="Antal pallplatser" type="number" name="omlastPalletPlaces" value={form.omlastPalletPlaces} onChange={(value) => update('omlastPalletPlaces', value)} />
                                <LabeledInput label="Total vikt (kg)" labelWidth="w-36" aria-label="Total vikt (kg)" type="number" name="totalWeight" value={form.totalWeight} onChange={(value) => update('totalWeight', value)} />
                                <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:gap-0">
                                    <span className="w-36 shrink-0 text-gray-700">Pallformat LxBxH</span>
                                    <div className="grid w-full min-w-0 flex-1 grid-cols-3 gap-1">
                                        <LabeledInput label="" aria-label="Pallängd (mm)" type="number" value={info.palletLength} disabled />
                                        <LabeledInput label="" aria-label="Pallbredd (mm)" type="number" value={info.palletWidth} disabled />
                                        <LabeledInput label="" aria-label="Pallhöjd (mm)" type="number" value={info.palletHeight} disabled />
                                    </div>
                                </div>
                                <LabeledInput label="Upplaga per pall" labelWidth="w-36" type="number" value={info.editionPerPallet} disabled />
                                <LabeledCheckbox label="Staplingsbar" labelWidth="w-36" labelPosition="left" className="w-full" checked={info.palletIsStackable} disabled />
                            </div>
                        </>
                    )}
                    {error && <p role="alert" className="break-words text-red-700">{error}</p>}
                </div>
                <footer className="shrink-0 border-t border-gray-300 px-5 py-3">
                    <div className="flex flex-wrap justify-end gap-4">
                        {!info && error && (
                            <ActionButton
                                label="Försök igen"
                                icon={RefreshCw}
                                onClick={() => {
                                    setError('');
                                    setReload((previous) => previous + 1);
                                }}
                            />
                        )}
                        <ActionButton label="Avbryt" icon={X} accent="slate" onClick={requestClose} />
                        <ActionButton label="Spara" icon={Save} accent="lime" onClick={save} disabled={!info || !form} />
                    </div>
                </footer>
            </section>
        </div>,
        document.body,
    );
};

const TransportOrderOmlastModal = ({ isOpen, transportOrderId, delivery, onClose, onSave }) => {
    if (!isOpen || !delivery) return null;
    return <OmlastEditor key={`${transportOrderId}:${delivery.id}`} transportOrderId={transportOrderId} delivery={delivery} onClose={onClose} onSave={onSave} />;
};

export default TransportOrderOmlastModal;