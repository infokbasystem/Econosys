import { useEffect, useState } from 'react';
import apiClient from '../../config/apiClient';
import LabeledInput from '../../components/LabeledInput';

const defaultForm = {
    freightPerPallet: '',
    freightPerVehicle: '',
    loadingPerPallet: '',
    storagePerM2: '',
    interest: '',
    loadingPerDelivery: '',
    freightFromStockPerPallet: '',
    truckLoadingLengthMm: '',
    truckLoadingWidthMm: '',
};

const parseNullableFloat = (value) => {
    if (value === '' || value === null || value === undefined) return null;
    const parsed = parseFloat(String(value).replace(',', '.'));
    return isNaN(parsed) ? null : parsed;
};

const parseNullableInt = (value) => {
    if (value === '' || value === null || value === undefined) return null;
    const parsed = parseInt(String(value), 10);
    return isNaN(parsed) ? null : parsed;
};

const mapDtoToForm = (dto) => ({
    freightPerPallet: dto?.freightPerPallet?.toString() ?? '',
    freightPerVehicle: dto?.freightPerVehicle?.toString() ?? '',
    loadingPerPallet: dto?.loadingPerPallet?.toString() ?? '',
    storagePerM2: dto?.storagePerM2?.toString() ?? '',
    interest: dto?.interest?.toString() ?? '',
    loadingPerDelivery: dto?.loadingPerDelivery?.toString() ?? '',
    freightFromStockPerPallet: dto?.freightFromStockPerPallet?.toString() ?? '',
    truckLoadingLengthMm: dto?.truckLoadingLengthMm?.toString() ?? '',
    truckLoadingWidthMm: dto?.truckLoadingWidthMm?.toString() ?? '',
});

const mapFormToPayload = (form) => ({
    freightPerPallet: parseNullableFloat(form.freightPerPallet),
    freightPerVehicle: parseNullableFloat(form.freightPerVehicle),
    loadingPerPallet: parseNullableFloat(form.loadingPerPallet),
    storagePerM2: parseNullableFloat(form.storagePerM2),
    interest: parseNullableInt(form.interest),
    loadingPerDelivery: parseNullableFloat(form.loadingPerDelivery),
    freightFromStockPerPallet: parseNullableFloat(form.freightFromStockPerPallet),
    truckLoadingLengthMm: parseNullableInt(form.truckLoadingLengthMm),
    truckLoadingWidthMm: parseNullableInt(form.truckLoadingWidthMm),
});

const CalculationConstantsSettings = () => {
    const [form, setForm] = useState(defaultForm);
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [message, setMessage] = useState(null);

    const updateField = (field, value) => {
        setForm((prev) => ({ ...prev, [field]: value }));
    };

    const load = async () => {
        setIsLoading(true);
        setMessage(null);
        try {
            const response = await apiClient.get('/calculationvariables');
            setForm(mapDtoToForm(response.data));
        } catch (error) {
            if (error.response?.status === 404) {
                setForm(defaultForm);
            } else {
                setMessage({ type: 'error', text: 'Kunde inte hämta kalkylkonstanter.' });
            }
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        load();
    }, []);

    const handleSave = async () => {
        setIsSaving(true);
        setMessage(null);
        try {
            const payload = mapFormToPayload(form);
            const response = await apiClient.put('/calculationvariables', payload);
            setForm(mapDtoToForm(response.data));
            setMessage({ type: 'success', text: 'Kalkylkonstanter sparades.' });
        } catch (error) {
            console.error('Failed to save calculation variables:', error);
            setMessage({ type: 'error', text: 'Kunde inte spara kalkylkonstanter.' });
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <div className="relative flex flex-col h-full">
            <h2 className="ml-5 text-sm pt-2 pb-2 text-gray-700">Kalkylkonstanter</h2>

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
                    </div>

                    {isLoading ? (
                        <p className="text-xs text-gray-600 mt-4">Laddar kalkylkonstanter...</p>
                    ) : (
                        <div className="w-100 flex flex-col">
                            <LabeledInput label="Frakt per pall" value={form.freightPerPallet} onChange={(v) => updateField('freightPerPallet', v)} labelWidth="w-52" margintop="0" type="number" decimals={2} />
                            <LabeledInput label="Frakt per bil" value={form.freightPerVehicle} onChange={(v) => updateField('freightPerVehicle', v)} labelWidth="w-52" margintop="0" type="number" decimals={2} />
                            <LabeledInput label="Lastning per pall" value={form.loadingPerPallet} onChange={(v) => updateField('loadingPerPallet', v)} labelWidth="w-52" margintop="0" type="number" decimals={2} />
                            <LabeledInput label="Lagring per m2" value={form.storagePerM2} onChange={(v) => updateField('storagePerM2', v)} labelWidth="w-52" margintop="0" type="number" decimals={2} />
                            <LabeledInput label="Årsränta" value={form.interest} onChange={(v) => updateField('interest', v)} labelWidth="w-52" margintop="0" type="number" integerOnly={true} />
                            <LabeledInput label="Lastning per leverans" value={form.loadingPerDelivery} onChange={(v) => updateField('loadingPerDelivery', v)} labelWidth="w-52" margintop="0" type="number" decimals={2} />
                            <LabeledInput label="Frakt per pall från lager" value={form.freightFromStockPerPallet} onChange={(v) => updateField('freightFromStockPerPallet', v)} labelWidth="w-52" margintop="0" type="number" decimals={2} />
                            <LabeledInput label="Flaklängd, mm" value={form.truckLoadingLengthMm} onChange={(v) => updateField('truckLoadingLengthMm', v)} labelWidth="w-52" margintop="0" type="number" integerOnly={true} />
                            <LabeledInput label="Flakbredd, mm" value={form.truckLoadingWidthMm} onChange={(v) => updateField('truckLoadingWidthMm', v)} labelWidth="w-52" margintop="0" type="number" integerOnly={true} />
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

export default CalculationConstantsSettings;
