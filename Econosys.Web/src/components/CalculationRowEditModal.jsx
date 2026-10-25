import { useEffect, useId, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Save, X } from 'lucide-react';
import apiClient from '../config/apiClient';
import { CALCULATION_ROW_EDIT_GROUPS, buildCalculationRowEditValues, createCalculationRowEditDraft } from './calculationRowEditFields';

const CalculationRowEditModal = ({ supplierOrderId, supplierOrderNr, onClose }) => {
    const titleId = useId();
    const dialogRef = useRef(null);
    const savingRef = useRef(false);
    const closeRef = useRef(onClose);
    closeRef.current = onClose;
    const [record, setRecord] = useState(null);
    const [draft, setDraft] = useState(null);
    const [error, setError] = useState('');
    const [saving, setSaving] = useState(false);
    const [loading, setLoading] = useState(true);
    const [retry, setRetry] = useState(0);

    useEffect(() => {
        let active = true;
        const controller = new AbortController();
        apiClient.get(`/supplierorders/${supplierOrderId}/calculation-row`, { signal: controller.signal })
            .then(({ data }) => {
                if (!active) return;
                if (!(data?.id > 0)) throw new Error('Ogiltig kalkylrad.');
                const nextDraft = createCalculationRowEditDraft(data.values);
                setRecord(data);
                setDraft(nextDraft);
            })
            .catch((requestError) => {
                if (!active) return;
                console.error('Failed to load linked calculation row:', requestError);
                setError(requestError.response?.data?.message || 'Kunde inte hämta kalkylraden.');
            })
            .finally(() => { if (active) setLoading(false); });
        return () => { active = false; controller.abort(); };
    }, [supplierOrderId, retry]);

    useEffect(() => {
        const previousOverflow = document.body.style.overflow;
        document.body.style.overflow = 'hidden';
        dialogRef.current?.focus();
        const handleKey = (event) => {
            if (event.key === 'Escape') {
                event.preventDefault();
                event.stopPropagation();
                if (!savingRef.current) closeRef.current();
            }
            if (event.key !== 'Tab') return;
            const controls = [...dialogRef.current.querySelectorAll('button:not(:disabled), input:not(:disabled), textarea:not(:disabled)')];
            const first = controls[0];
            const last = controls[controls.length - 1];
            if (!controls.length) { event.preventDefault(); return; }
            if (!dialogRef.current.contains(document.activeElement) || document.activeElement === dialogRef.current
                || (event.shiftKey && document.activeElement === first)) {
                event.preventDefault();
                (event.shiftKey ? last : first)?.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first?.focus();
            }
        };
        document.addEventListener('keydown', handleKey, true);
        return () => {
            document.body.style.overflow = previousOverflow;
            document.removeEventListener('keydown', handleKey, true);
        };
    }, []);

    const close = () => { if (!savingRef.current) onClose(); };
    const save = async (event) => {
        event.preventDefault();
        if (savingRef.current || !record || !draft) return;
        setError('');
        let values;
        try {
            values = buildCalculationRowEditValues(draft);
        } catch (validationError) {
            setError(validationError.message);
            return;
        }
        savingRef.current = true;
        setSaving(true);
        try {
            await apiClient.put(`/supplierorders/${supplierOrderId}/calculation-row`, { expectedRowId: record.id, values });
            onClose();
        } catch (requestError) {
            console.error('Failed to save linked calculation row:', requestError);
            const response = requestError.response?.data;
            setError(response?.message || (response?.errors && Object.values(response.errors).flat().join(' '))
                || 'Kunde inte spara kalkylraden.');
        } finally {
            savingRef.current = false;
            setSaving(false);
        }
    };

    return createPortal(
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={close}>
            <section ref={dialogRef} role="dialog" aria-modal="true" aria-labelledby={titleId} tabIndex={-1}
                className="flex max-h-[90vh] w-full max-w-5xl flex-col rounded-sm bg-white shadow-xl"
                onClick={(event) => event.stopPropagation()}>
                <div className="flex items-center justify-between gap-4 border-b border-gray-200 p-4">
                    <div>
                        <h2 id={titleId} className="text-sm font-semibold">Redigera kalkylrad – beställning {supplierOrderNr || supplierOrderId}</h2>
                        {record && <p className="mt-1 text-xs text-gray-500">Kalkylrad {record.id} · Kalkyl {record.calculationId ?? 'saknas'} · Företag {record.companyId} · Ursprungligt ID {record.oldDbId ?? 'saknas'}</p>}
                    </div>
                    <button type="button" aria-label="Stäng kalkylrad" disabled={saving} onClick={close} className="rounded p-1 hover:bg-gray-100 disabled:opacity-50"><X size={18} /></button>
                </div>
                <form onSubmit={save} className="flex min-h-0 flex-1 flex-col">
                    <div className="min-h-0 flex-1 overflow-y-auto p-4">
                        <p className="mb-4 text-xs text-gray-600">Endast kalkylraden ändras. Beställningens och kundorderns värden ändras inte. Ingen automatisk omberäkning görs.</p>
                        {loading && <p role="status" className="text-xs">Hämtar kalkylrad...</p>}
                        {error && <p role="alert" className="mb-4 rounded border border-red-200 bg-red-50 p-3 text-xs text-red-700">{error}</p>}
                        {!loading && !draft && <button type="button" className="text-xs text-red-600 underline" onClick={() => {
                            setLoading(true); setError(''); setRetry((value) => value + 1);
                        }}>Försök igen</button>}
                        {draft && CALCULATION_ROW_EDIT_GROUPS.map((group) => (
                            <fieldset key={group.label} disabled={saving} className="mb-5 border-t border-gray-200 pt-3">
                                <legend className="px-1 text-xs font-semibold text-sky-800">{group.label}</legend>
                                <div className="grid grid-cols-1 gap-x-6 gap-y-3 sm:grid-cols-2 lg:grid-cols-3">
                                    {group.fields.map(({ name, label, type, maxLength }) => (
                                        <label key={name} className={`flex text-xs ${type === 'boolean' ? 'items-center gap-2' : 'flex-col gap-1'}`}>
                                            <span>{label}</span>
                                            <input name={name} type={type === 'boolean' ? 'checkbox' : 'text'}
                                                inputMode={type === 'number' ? 'decimal' : type === 'integer' ? 'numeric' : undefined}
                                                maxLength={maxLength}
                                                {...(type === 'boolean' ? { checked: draft[name] } : { value: draft[name] })}
                                                onChange={(event) => setDraft((previous) => ({
                                                    ...previous, [name]: type === 'boolean' ? event.target.checked : event.target.value,
                                                }))}
                                                className={type === 'boolean' ? 'accent-lime-700' : 'w-full rounded-sm border border-gray-300 px-2 py-1 focus:border-lime-700 focus:outline-none'} />
                                        </label>
                                    ))}
                                </div>
                            </fieldset>
                        ))}
                    </div>
                    <div className="flex justify-end gap-3 border-t border-gray-200 p-4">
                        <button type="button" disabled={saving} onClick={close} className="rounded-sm px-4 py-2 text-xs hover:bg-gray-100 disabled:opacity-50">Avbryt</button>
                        <button type="submit" disabled={loading || saving || !draft} className="inline-flex items-center gap-2 rounded-sm bg-lime-700 px-4 py-2 text-xs text-white hover:bg-lime-800 disabled:opacity-50">
                            <Save size={14} />{saving ? 'Sparar...' : 'Spara'}
                        </button>
                    </div>
                </form>
            </section>
        </div>,
        document.body,
    );
};

export default CalculationRowEditModal;
