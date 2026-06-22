import React, { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import apiClient from '../config/apiClient';
import { getSwedishTodayDateString, toSwedishDateInputValue } from '../helpers/dateUtils';

const AREA_OPTIONS = [
    { value: 'SALES', label: 'Försäljning' },
    { value: 'ORDER', label: 'Order' },
    { value: 'DELIVERY', label: 'Leverans' },
    { value: 'INVOICE', label: 'Faktura' },
    { value: 'QUALITY_ENVIRONMENT', label: 'Kvalitet/Miljö' },
    { value: 'SUPPLIER', label: 'Leverantör' },
];

const STATUS_OPTIONS = [
    { value: 'NEW', label: 'Ny' },
    { value: 'ONGOING', label: 'Pågående' },
    { value: 'POSTPONED', label: 'Uppskjuten' },
    { value: 'FINISHED', label: 'Avslutad' },
];

const AREA_CODE_TO_ENUM = {
    SALES: 0,
    ORDER: 1,
    DELIVERY: 2,
    INVOICE: 3,
    QUALITY_ENVIRONMENT: 4,
    SUPPLIER: 5,
};

const STATUS_CODE_TO_ENUM = {
    NEW: 0,
    POSTPONED: 1,
    FINISHED: 2,
    ONGOING: 3,
};

const AREA_ENUM_TO_CODE = Object.fromEntries(
    Object.entries(AREA_CODE_TO_ENUM).map(([k, v]) => [v, k])
);

const STATUS_ENUM_TO_CODE = Object.fromEntries(
    Object.entries(STATUS_CODE_TO_ENUM).map(([k, v]) => [v, k])
);

const emptyForm = {
    createdByName: '',
    createdTimestamp: getSwedishTodayDateString(),
    responsible: '',
    areaCode: 'SALES',
    statusCode: 'NEW',
    description: '',
    proposedMeasure: '',
    note: '',
    followUp: '',
};

const getApiValidationMessage = (err) => {
    const errors = err?.response?.data?.errors;
    if (errors && typeof errors === 'object') {
        const firstKey = Object.keys(errors)[0];
        const firstMessage = Array.isArray(errors[firstKey]) ? errors[firstKey][0] : null;
        if (firstMessage) return firstMessage;
    }

    return err?.response?.data?.title || err?.response?.data?.message || null;
};

const ImprovementPropositionModal = ({ isOpen, onClose, onSaved, initialData = null }) => {
    const [form, setForm] = useState(emptyForm);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState('');

    const isEdit = Boolean(initialData?.id);

    useEffect(() => {
        if (!isOpen) return;
        if (initialData) {
            setForm({
                createdByName: initialData.createdByName ?? '',
                createdTimestamp: initialData.createdTimestamp
                    ? toSwedishDateInputValue(initialData.createdTimestamp)
                    : getSwedishTodayDateString(),
                responsible: initialData.responsible ?? '',
                areaCode: typeof initialData.areaCode === 'number'
                    ? (AREA_ENUM_TO_CODE[initialData.areaCode] ?? 'SALES')
                    : (initialData.areaCode ?? 'SALES'),
                statusCode: typeof initialData.statusCode === 'number'
                    ? (STATUS_ENUM_TO_CODE[initialData.statusCode] ?? 'NEW')
                    : (initialData.statusCode ?? 'NEW'),
                description: initialData.description ?? '',
                proposedMeasure: initialData.proposedMeasure ?? '',
                note: initialData.note ?? '',
                followUp: initialData.followUp ?? '',
            });
        } else {
            setForm(emptyForm);
        }
        setError('');
    }, [isOpen, initialData]);

    const handleChange = (e) => {
        const { name, value } = e.target;
        setForm((prev) => ({ ...prev, [name]: value }));
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setError('');

        const requiredFields = [
            ['createdByName', 'Skapad av'],
            ['responsible', 'Ansvarig'],
            ['description', 'Beskrivning'],
            ['proposedMeasure', 'Foreslagen atgard'],
        ];

        const missingLabel = requiredFields
            .find(([key]) => !String(form[key] ?? '').trim())?.[1];

        if (missingLabel) {
            setError(`${missingLabel} ar obligatorisk.`);
            return;
        }

        setSaving(true);

        const payload = {
            createdByName: form.createdByName.trim(),
            createdTimestamp: form.createdTimestamp || null,
            responsible: form.responsible.trim(),
            areaCode: AREA_CODE_TO_ENUM[form.areaCode],
            statusCode: STATUS_CODE_TO_ENUM[form.statusCode],
            description: form.description.trim(),
            proposedMeasure: form.proposedMeasure.trim(),
            note: form.note.trim() || null,
            followUp: form.followUp.trim() || null,
        };

        try {
            if (isEdit) {
                await apiClient.put(`/ImprovementPropositions/${initialData.id}`, payload);
            } else {
                await apiClient.post('/ImprovementPropositions', payload);
            }
            onSaved();
            onClose();
        } catch (err) {
            const details = getApiValidationMessage(err) || '';
            setError(details ? `Kunde inte spara: ${details}` : 'Kunde inte spara förbattringsforslaget. Forsok igen.');
        } finally {
            setSaving(false);
        }
    };

    if (!isOpen) return null;

    return createPortal(
        <div className="fixed inset-0 z-50">
            <div className="absolute inset-0 bg-black/50 z-40" />
            <div
                className="relative z-50 flex min-h-screen items-start justify-center pt-16 px-4"
                onClick={onClose}
            >
                <div
                    className="relative bg-white rounded-sm shadow-xl w-full max-w-2xl py-6"
                    style={{ background: 'rgb(255, 255, 234)' }}
                    onClick={(e) => e.stopPropagation()}
                >
                    {/* Header */}
                    <div className="relative flex items-center justify-center mb-4">
                        <h2 className="text-sm font-semibold text-center">
                            {isEdit ? 'REDIGERA FÖRBÄTTRINGSFÖRSLAG' : 'NYTT FÖRBÄTTRINGSFÖRSLAG'}
                        </h2>
                        <button
                            type="button"
                            onClick={onClose}
                            className="absolute right-0 text-gray-400 hover:text-gray-600 text-xl leading-none mr-5"
                        >
                            ×
                        </button>
                    </div>

                    <form onSubmit={handleSubmit} noValidate className="mx-8 mt-6 space-y-4">

                        {/* Row 1: Created by + Date */}
                        <div className="grid grid-cols-2 gap-4">
                            <div>
                                <label className="block text-[10px] text-gray-500 mb-1">Skapad av *</label>
                                <input
                                    type="text"
                                    name="createdByName"
                                    value={form.createdByName}
                                    onChange={handleChange}
                                    maxLength={200}
                                    className="w-full text-xs border border-gray-300 rounded-sm px-2 py-1.5 bg-white focus:outline-none focus:border-gray-400"
                                />
                            </div>
                            <div>
                                <label className="block text-[10px] text-gray-500 mb-1">Datum</label>
                                <input
                                    type="date"
                                    name="createdTimestamp"
                                    value={form.createdTimestamp}
                                    onChange={handleChange}
                                    className="w-full text-xs border border-gray-300 rounded-sm px-2 py-1.5 bg-white focus:outline-none focus:border-gray-400"
                                />
                            </div>
                        </div>

                        {/* Row 2: Responsible + Area + Status */}
                        <div className="grid grid-cols-3 gap-4">
                            <div>
                                <label className="block text-[10px] text-gray-500 mb-1">Ansvarig *</label>
                                <input
                                    type="text"
                                    name="responsible"
                                    value={form.responsible}
                                    onChange={handleChange}
                                    maxLength={200}
                                    className="w-full text-xs border border-gray-300 rounded-sm px-2 py-1.5 bg-white focus:outline-none focus:border-gray-400"
                                />
                            </div>
                            <div>
                                <label className="block text-[10px] text-gray-500 mb-1">Område *</label>
                                <select
                                    name="areaCode"
                                    value={form.areaCode}
                                    onChange={handleChange}
                                    className="w-full text-xs border border-gray-300 rounded-sm px-2 py-1.5 bg-white focus:outline-none focus:border-gray-400"
                                >
                                    {AREA_OPTIONS.map((o) => (
                                        <option key={o.value} value={o.value}>{o.label}</option>
                                    ))}
                                </select>
                            </div>
                            <div>
                                <label className="block text-[10px] text-gray-500 mb-1">Status *</label>
                                <select
                                    name="statusCode"
                                    value={form.statusCode}
                                    onChange={handleChange}
                                    className="w-full text-xs border border-gray-300 rounded-sm px-2 py-1.5 bg-white focus:outline-none focus:border-gray-400"
                                >
                                    {STATUS_OPTIONS.map((o) => (
                                        <option key={o.value} value={o.value}>{o.label}</option>
                                    ))}
                                </select>
                            </div>
                        </div>

                        {/* Description */}
                        <div>
                            <label className="block text-[10px] text-gray-500 mb-1">Beskrivning *</label>
                            <textarea
                                name="description"
                                value={form.description}
                                onChange={handleChange}
                                maxLength={4000}
                                rows={3}
                                className="w-full text-xs border border-gray-300 rounded-sm px-2 py-1.5 bg-white focus:outline-none focus:border-gray-400 resize-none"
                            />
                        </div>

                        {/* Proposed measure */}
                        <div>
                            <label className="block text-[10px] text-gray-500 mb-1">Föreslagen åtgärd *</label>
                            <textarea
                                name="proposedMeasure"
                                value={form.proposedMeasure}
                                onChange={handleChange}
                                maxLength={4000}
                                rows={3}
                                className="w-full text-xs border border-gray-300 rounded-sm px-2 py-1.5 bg-white focus:outline-none focus:border-gray-400 resize-none"
                            />
                        </div>

                        {/* Note */}
                        <div>
                            <label className="block text-[10px] text-gray-500 mb-1">Anteckning</label>
                            <textarea
                                name="note"
                                value={form.note}
                                onChange={handleChange}
                                maxLength={4000}
                                rows={2}
                                className="w-full text-xs border border-gray-300 rounded-sm px-2 py-1.5 bg-white focus:outline-none focus:border-gray-400 resize-none"
                            />
                        </div>

                        {/* Follow-up */}
                        <div>
                            <label className="block text-[10px] text-gray-500 mb-1">Uppföljning</label>
                            <textarea
                                name="followUp"
                                value={form.followUp}
                                onChange={handleChange}
                                maxLength={4000}
                                rows={2}
                                className="w-full text-xs border border-gray-300 rounded-sm px-2 py-1.5 bg-white focus:outline-none focus:border-gray-400 resize-none"
                            />
                        </div>

                        {error && (
                            <p className="text-xs text-red-600">{error}</p>
                        )}

                        {/* Actions */}
                        <div className="flex gap-4 justify-end pt-2 pb-2">
                            <button
                                type="button"
                                onClick={onClose}
                                className="shadow-md/30 text-xs text-white bg-orange-400 hover:bg-orange-600 px-10 py-[5px]"
                            >
                                Avbryt
                            </button>
                            <button
                                type="submit"
                                disabled={saving}
                                className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 px-10 py-[5px] disabled:opacity-50"
                            >
                                {saving ? 'Sparar…' : 'Spara'}
                            </button>
                        </div>
                    </form>
                </div>
            </div>
        </div>,
        document.body
    );
};

export default ImprovementPropositionModal;
