import { useEffect, useMemo, useState } from 'react';
import apiClient from '../../config/apiClient';
import LabeledInput from '../../components/LabeledInput';

const defaultForm = {
    month1: '',
    month2: '',
    month3: '',
    month4: '',
    month5: '',
    month6: '',
    month7: '',
    month8: '',
    month9: '',
    month10: '',
    month11: '',
    month12: '',
};

const monthFields = [
    { key: 'month1', label: 'Januari' },
    { key: 'month2', label: 'Februari' },
    { key: 'month3', label: 'Mars' },
    { key: 'month4', label: 'April' },
    { key: 'month5', label: 'Maj' },
    { key: 'month6', label: 'Juni' },
    { key: 'month7', label: 'Juli' },
    { key: 'month8', label: 'Augusti' },
    { key: 'month9', label: 'September' },
    { key: 'month10', label: 'Oktober' },
    { key: 'month11', label: 'November' },
    { key: 'month12', label: 'December' },
];

const parseNullableDecimal = (value) => {
    if (value === '' || value === null || value === undefined) return null;
    const normalized = String(value).trim().replace(',', '.');
    const parsed = Number.parseFloat(normalized);
    return Number.isNaN(parsed) ? null : parsed;
};

const mapDtoToForm = (dto) => ({
    month1: dto?.month1?.toString() ?? '',
    month2: dto?.month2?.toString() ?? '',
    month3: dto?.month3?.toString() ?? '',
    month4: dto?.month4?.toString() ?? '',
    month5: dto?.month5?.toString() ?? '',
    month6: dto?.month6?.toString() ?? '',
    month7: dto?.month7?.toString() ?? '',
    month8: dto?.month8?.toString() ?? '',
    month9: dto?.month9?.toString() ?? '',
    month10: dto?.month10?.toString() ?? '',
    month11: dto?.month11?.toString() ?? '',
    month12: dto?.month12?.toString() ?? '',
});

const mapFormToPayload = (form) => ({
    month1: parseNullableDecimal(form.month1),
    month2: parseNullableDecimal(form.month2),
    month3: parseNullableDecimal(form.month3),
    month4: parseNullableDecimal(form.month4),
    month5: parseNullableDecimal(form.month5),
    month6: parseNullableDecimal(form.month6),
    month7: parseNullableDecimal(form.month7),
    month8: parseNullableDecimal(form.month8),
    month9: parseNullableDecimal(form.month9),
    month10: parseNullableDecimal(form.month10),
    month11: parseNullableDecimal(form.month11),
    month12: parseNullableDecimal(form.month12),
});

const BudgetMonthDistributionSettings = () => {
    const [form, setForm] = useState(defaultForm);
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [message, setMessage] = useState(null);

    const total = useMemo(() => monthFields.reduce((acc, field) => {
        const value = parseNullableDecimal(form[field.key]);
        return acc + (value ?? 0);
    }, 0), [form]);

    const load = async () => {
        setIsLoading(true);
        setMessage(null);
        try {
            const response = await apiClient.get('/budgetmonthdistribution');
            setForm(mapDtoToForm(response.data));
        } catch (error) {
            if (error.response?.status === 404) {
                setForm(defaultForm);
            } else {
                console.error('Failed to load budget month distribution:', error);
                setMessage({ type: 'error', text: 'Kunde inte hamta budgetens manadsfordelning.' });
            }
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        load();
    }, []);

    const updateField = (field, value) => {
        setForm((prev) => ({ ...prev, [field]: value ?? '' }));
    };

    const handleSave = async () => {
        setIsSaving(true);
        setMessage(null);
        try {
            const payload = mapFormToPayload(form);
            const response = await apiClient.put('/budgetmonthdistribution', payload);
            setForm(mapDtoToForm(response.data));
            setMessage({ type: 'success', text: 'Budgetens manadsfordelning sparades.' });
        } catch (error) {
            console.error('Failed to save budget month distribution:', error);
            const apiMessage = typeof error?.response?.data?.message === 'string' ? error.response.data.message.trim() : '';
            setMessage({ type: 'error', text: apiMessage || 'Kunde inte spara budgetens manadsfordelning.' });
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <div className="relative flex flex-col h-full">
            <h2 className="ml-5 text-sm pt-2 pb-2 text-gray-700">Budget, manadsfordelning</h2>

            <div className="flex h-full min-w-0 items-stretch">
                <div className="flex-1 min-w-0 px-10 py-2 overflow-x-auto">
                    <div className="flex items-center gap-5 mb-6 mt-1">
                        <button
                            type="button"
                            onClick={handleSave}
                            disabled={isSaving || isLoading}
                            className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 disabled:opacity-60 disabled:cursor-not-allowed px-5 p-[5px]"
                        >
                            {isSaving ? 'Sparar...' : 'Spara'}
                        </button>
                        <p className="text-xs text-gray-600">Summa: {total.toLocaleString('sv-SE', { minimumFractionDigits: 0, maximumFractionDigits: 8 })}</p>
                        <p className="text-xs text-gray-500">Summan ska vara 100</p>
                    </div>

                    {isLoading ? (
                        <p className="text-xs text-gray-600 mt-4">Laddar budgetens manadsfordelning...</p>
                    ) : (
                        <div className="w-[520px] grid grid-cols-2 gap-x-8">
                            {monthFields.map((field) => (
                                <LabeledInput
                                    key={field.key}
                                    label={field.label}
                                    labelWidth="w-28"
                                    margintop="0"
                                    value={form[field.key]}
                                    onChange={(value) => updateField(field.key, value)}
                                />
                            ))}
                        </div>
                    )}
                </div>

                <div className="flex flex-col w-80 shrink-0 border-l border-gray-300 pl-4 py-2 mb-5">
                    <h2 className="text-sm text-center text-gray-700 mt-1">Meddelanden</h2>
                    {!message ? (
                        <p className="text-xs text-center font-light mt-4">Inga meddelanden</p>
                    ) : (
                        <ul className="mt-2 space-y-2">
                            <li className={`text-center text-xs p-2 rounded border border-gray-200 ${message.type === 'error' ? 'bg-red-100 text-red-700' : 'bg-green-100 text-green-700'}`}>
                                {message.text}
                            </li>
                        </ul>
                    )}
                </div>
            </div>
        </div>
    );
};

export default BudgetMonthDistributionSettings;