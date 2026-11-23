import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Plus, X } from 'lucide-react';
import apiClient from '../config/apiClient';
import ActionButton from '../components/ActionButton';
import LabeledReactSelect from '../components/LabeledReactSelect';
import { parseNullableNumber } from '../helpers/numberUtils';
import useAppUpdateGuard from '../hooks/useAppUpdateGuard';

const editableRows = (rows) => rows.map((row) => ({
    ...row,
    key: crypto.randomUUID(),
    price: row.price == null ? '' : String(row.price).replace('.', ','),
}));

const PartyPricesModal = ({ partyType, partyId, partyName, currency, onClose, onSaved }) => {
    const [data, setData] = useState(null);
    const [palletPrices, setPalletPrices] = useState([]);
    const [costPrices, setCostPrices] = useState([]);
    const [error, setError] = useState('');
    const [saving, setSaving] = useState(false);
    const [dirty, setDirty] = useState(false);
    const [loadAttempt, setLoadAttempt] = useState(0);
    const dialogRef = useRef(null);
    const endpoint = `/party-prices/${partyType}/${partyId}`;
    useAppUpdateGuard(() => dirty);

    useEffect(() => {
        const previousFocus = document.activeElement;
        dialogRef.current?.focus();
        return () => previousFocus?.focus();
    }, []);

    useEffect(() => {
        let active = true;
        const load = async () => {
            setError('');
            try {
                const response = await apiClient.get(endpoint);
                if (!active) return;
                setData(response.data);
                setPalletPrices(editableRows(response.data.palletPrices));
                setCostPrices(editableRows(response.data.costPrices));
            } catch (loadError) {
                console.error('Failed to load party prices:', loadError);
                if (active) setError('Kunde inte läsa priserna. Försök igen.');
            }
        };
        void load();
        return () => { active = false; };
    }, [endpoint, loadAttempt]);

    useEffect(() => {
        if (!dirty) return;
        const beforeUnload = (event) => {
            event.preventDefault();
            event.returnValue = '';
        };
        window.addEventListener('beforeunload', beforeUnload);
        return () => window.removeEventListener('beforeunload', beforeUnload);
    }, [dirty]);

    const updateRows = (setter, change) => {
        setter(change);
        setDirty(true);
        setError('');
    };

    const handleSave = async () => {
        const rows = [...palletPrices, ...costPrices];
        if (rows.some((row) => !row.itemId
            || (row.price.trim() !== '' && (!/^-?\d+([,.]\d{1,2})?$/.test(row.price.replace(/\s/g, ''))
                || Math.abs(parseNullableNumber(row.price)) > 99999999.99)))) {
            setError('Välj pallformat/kostnad för varje rad och ange ett giltigt pris med högst två decimaler.');
            return;
        }

        const toPayload = (items) => items.map((row) => ({
            id: row.id,
            itemId: row.itemId,
            price: parseNullableNumber(row.price),
        }));
        setSaving(true);
        setError('');
        try {
            await apiClient.put(endpoint, {
                palletPrices: toPayload(palletPrices),
                costPrices: toPayload(costPrices),
            });
            onSaved();
        } catch (saveError) {
            console.error('Failed to save party prices:', saveError);
            setError(saveError.response?.data?.detail || 'Kunde inte spara priserna. Dina ändringar finns kvar.');
        } finally {
            setSaving(false);
        }
    };

    const handleKeyDown = (event) => {
        if (event.defaultPrevented) return;
        if (event.key === 'Escape') {
            event.stopPropagation();
            if (!saving) onClose();
        }
        if (event.key === 'Tab') {
            const controls = [...dialogRef.current.querySelectorAll('button:not(:disabled), input:not(:disabled), [tabindex="0"]')];
            const first = controls[0];
            const last = controls[controls.length - 1];
            if (event.shiftKey && (document.activeElement === first || document.activeElement === dialogRef.current)) {
                event.preventDefault();
                last?.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first?.focus();
            }
        }
    };

    const renderSection = (title, rows, setter, options, isCost) => (
        <section className="space-y-2">
            <h3 className="text-xs font-semibold uppercase text-gray-700">{title}</h3>
            <ActionButton
                label={isCost ? 'Lägg till kostnad' : 'Lägg till pallformat'}
                icon={Plus}
                disabled={saving}
                onClick={() => updateRows(setter, (current) => [...current, {
                    id: 0, itemId: '', price: '', key: crypto.randomUUID(),
                }])}
            />
            {rows.length === 0 && <p className="text-xs text-gray-500">Inga priser registrerade.</p>}
            {rows.map((row, index) => {
                const label = `${isCost ? 'Kostnad' : 'Pallformat'} ${index + 1}`;
                const update = (field, value) => updateRows(setter, (current) =>
                    current.map((item) => item.key === row.key ? { ...item, [field]: value } : item));
                return (
                    <div key={row.key} className="grid grid-cols-[minmax(0,1fr)_110px_auto] items-center gap-2">
                        <LabeledReactSelect
                            aria-label={label}
                            value={row.itemId}
                            items={options.map((item) => ({
                                ...item,
                                name: `${isCost ? `${item.id} ` : ''}${item.name}${item.isActive === false ? ' (inaktiv)' : ''}`,
                            }))}
                            onChange={(value) => update('itemId', value)}
                            isDisabled={saving}
                            menuPlacement="auto"
                        />
                        <input
                            type="text"
                            inputMode="decimal"
                            aria-label={`Pris ${label.toLowerCase()}`}
                            value={row.price}
                            onChange={(event) => update('price', event.target.value)}
                            disabled={saving}
                            className="w-full rounded-sm border border-gray-300 bg-white px-2 py-1 text-right text-xs"
                        />
                        <button
                            type="button"
                            aria-label={`Ta bort ${label.toLowerCase()}`}
                            disabled={saving}
                            onClick={() => updateRows(setter, (current) => current.filter((item) => item.key !== row.key))}
                            className="px-1 text-xs text-red-600 hover:underline disabled:opacity-50"
                        >
                            Ta bort
                        </button>
                    </div>
                );
            })}
        </section>
    );

    return createPortal(
        <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/50 px-4 py-12">
            <div
                ref={dialogRef}
                role="dialog"
                aria-modal="true"
                aria-labelledby="party-prices-title"
                tabIndex={-1}
                onKeyDown={handleKeyDown}
                className="w-full max-w-2xl rounded-sm bg-[#f4f3f2] p-6 shadow-xl outline-none"
            >
                <div className="flex items-center justify-between gap-4">
                    <h2 id="party-prices-title" className="text-sm font-semibold uppercase">Pall- och kostnadspriser</h2>
                    <button type="button" aria-label="Stäng" onClick={onClose} disabled={saving} className="text-gray-500 hover:text-gray-800">
                        <X size={18} />
                    </button>
                </div>
                <p className="mt-2 text-xs text-gray-600">{partyName} — {partyType === 'supplier' ? 'Inköpspriser' : 'Försäljningspriser'}{currency ? ` (${currency})` : ''}</p>
                <p className="mt-1 text-xs text-gray-500">Priserna sparas separat från övriga uppgifter. Tomt pris betyder att inget standardpris anges.</p>
                {error && <p role="alert" className="mt-4 text-xs text-red-700">{error}</p>}
                {!data ? (
                    <div className="my-6 text-xs text-gray-600">
                        {error
                            ? <button type="button" onClick={() => setLoadAttempt((value) => value + 1)} className="text-blue-700 underline">Försök igen</button>
                            : <p>Laddar priser...</p>}
                    </div>
                ) : (
                    <div className="my-6 space-y-8">
                        {renderSection('Pallformat', palletPrices, setPalletPrices, data.palletFormats, false)}
                        {renderSection('Extra kostnader', costPrices, setCostPrices, data.costs, true)}
                    </div>
                )}
                <div className="mt-6 flex justify-end gap-4">
                    <button type="button" onClick={onClose} disabled={saving} className="bg-orange-400 px-8 py-1.5 text-xs text-white hover:bg-orange-600 disabled:opacity-50">Avbryt</button>
                    <button type="button" onClick={handleSave} disabled={!data || saving || !dirty} className="bg-lime-700 px-8 py-1.5 text-xs text-white hover:bg-lime-900 disabled:bg-gray-400">
                        {saving ? 'Sparar...' : 'Spara priser'}
                    </button>
                </div>
            </div>
        </div>,
        document.body
    );
};

export default PartyPricesModal;
