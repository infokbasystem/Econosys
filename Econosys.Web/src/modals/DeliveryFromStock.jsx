import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';

import apiClient from '../config/apiClient';
import { getSharedRequest } from '../helpers/sharedRequest';
import LabeledDatePicker from '../components/LabeledDatePicker';
import LabeledInput from '../components/LabeledInput';
import LabeledSelect from '../components/LabeledSelect';
import LabeledCheckbox from '../components/LabeledCheckbox';

const buildFormFromCandidate = (candidate, group, details) => ({
    deliveryDate: details?.deliveryDate != null ? String(details.deliveryDate).slice(0, 10) : '',
    nrOfItems: details?.nrOfItems != null
        ? details.nrOfItems
        : group?.nrOf != null ? group.nrOf : null,
    nrOfPallets: details?.nrOfPallets != null
        ? details.nrOfPallets
        : group?.nrOfPallets != null ? group.nrOfPallets : null,
    callOff: details?.callOff != null ? String(details.callOff) : '',
    inventoryId: (details?.inventoryId ?? group?.inventoryId) == null ? '' : String(details?.inventoryId ?? group?.inventoryId),
    palletLength: (details?.palletLength ?? group?.palletLength) != null ? details?.palletLength ?? group?.palletLength : null,
    palletWidth: (details?.palletWidth ?? group?.palletWidth) != null ? details?.palletWidth ?? group?.palletWidth : null,
    palletHeight: (details?.palletHeight ?? group?.palletHeight) != null ? details?.palletHeight ?? group?.palletHeight : null,
    palletIsStackable: Boolean(details ? details.palletIsStackable : group?.palletIsStackable),
    palletCalcFactor: (details?.palletCalcFactor ?? group?.palletCalcFactor) != null ? details?.palletCalcFactor ?? group?.palletCalcFactor : null,
    isDelivered: Boolean(details?.isDelivered),
});

// Legacy parity: Econosys.Wpf DeliveryView / DeliveryViewModel, minus the order search and transport-leg map
// (the customer order and pallet grouping are already chosen via SelectNewDeliveryInfo).
const DeliveryFromStock = ({
    isOpen,
    onClose,
    onSave,
    candidate,
    group,
    details = null,
    isSubmitting = false,
    submitError = '',
    title = 'Leverans från lager',
}) => {
    // View mode shows an already created delivery read-only; create mode is used when picking a new balance group.
    const isViewMode = details != null;
    const [form, setForm] = useState(() => buildFormFromCandidate(candidate, group, details));
    const [inventoryOptions, setInventoryOptions] = useState([]);
    const [isLoadingInventoryOptions, setIsLoadingInventoryOptions] = useState(false);
    const [validationError, setValidationError] = useState('');

    useEffect(() => {
        if (!isOpen) {
            return;
        }

        setForm(buildFormFromCandidate(candidate, group, details));
        setValidationError('');
    }, [isOpen, candidate, group, details]);

    useEffect(() => {
        if (!isOpen || isViewMode) {
            return;
        }

        let isActive = true;
        setIsLoadingInventoryOptions(true);

        const loadInventoryOptions = async () => {
            try {
                const response = await getSharedRequest('inventories:delivery-details-options', () => apiClient.post('/inventories/search', {
                    pagination: { pageNumber: 1, pageSize: 1000 },
                    orderBy: [{ field: 'name', direction: 'asc' }],
                }));

                if (!isActive) {
                    return;
                }

                const items = Array.isArray(response?.data?.items) ? response.data.items : [];
                setInventoryOptions(items.filter((item) => Boolean(item?.isInventory)));
            } catch (error) {
                console.error('Failed to load inventory options:', error);
                if (isActive) {
                    setInventoryOptions([]);
                }
            } finally {
                if (isActive) {
                    setIsLoadingInventoryOptions(false);
                }
            }
        };

        loadInventoryOptions();

        return () => {
            isActive = false;
        };
    }, [isOpen, isViewMode]);

    if (!isOpen) {
        return null;
    }

    const updateField = (field, value) => {
        setForm((prev) => ({ ...prev, [field]: value }));
    };

    const editionPerPallet = isViewMode ? details?.editionPerPallet ?? null : group?.editionPerPallet ?? null;

    // Legacy parity: DeliveryViewModel recalculates nrOfItems <-> nrOfPallets from EditionPerPallet.
    const handleNrOfItemsChange = (value) => {
        if (isViewMode) {
            return;
        }
        setValidationError('');
        setForm((prev) => ({
            ...prev,
            nrOfItems: value,
            nrOfPallets: value != null && editionPerPallet != null && editionPerPallet > 0
                ? Math.ceil(value / editionPerPallet)
                : prev.nrOfPallets,
        }));
    };

    const handleNrOfPalletsChange = (value) => {
        if (isViewMode) {
            return;
        }
        setValidationError('');
        setForm((prev) => ({
            ...prev,
            nrOfPallets: value,
            nrOfItems: value != null && editionPerPallet != null
                ? value * editionPerPallet
                : prev.nrOfItems,
        }));
    };

    const orderedQty = candidate?.orderedQty ?? details?.orderedQty ?? null;
    const nrOfItemsValue = form.nrOfItems == null ? null : Number(form.nrOfItems);
    // Legacy parity: DeliveryViewModel.SetInfo warns when the delivered quantity is below 90% of the ordered quantity.
    const showLowQuantityWarning = orderedQty != null && nrOfItemsValue != null && nrOfItemsValue < orderedQty * 0.9;

    const handleSave = () => {
        if (isSubmitting || isViewMode) {
            return;
        }

        if (editionPerPallet != null && editionPerPallet > 0 && form.nrOfItems != null && Number(form.nrOfItems) % editionPerPallet !== 0) {
            setValidationError('Antal går ej jämnt upp med upplagan per pall');
            return;
        }

        setValidationError('');

        onSave?.({
            customerOrderId: candidate?.customerOrderId,
            editionPerPallet: group?.editionPerPallet,
            deliveryDate: form.deliveryDate || null,
            nrOfItems: form.nrOfItems == null ? null : Number(form.nrOfItems),
            nrOfPallets: form.nrOfPallets == null ? null : Number(form.nrOfPallets),
            callOff: form.callOff || null,
            inventoryId: form.inventoryId === '' ? null : Number(form.inventoryId),
            palletLength: form.palletLength == null ? null : Number(form.palletLength),
            palletWidth: form.palletWidth == null ? null : Number(form.palletWidth),
            palletHeight: form.palletHeight == null ? null : Number(form.palletHeight),
            palletIsStackable: form.palletIsStackable,
            palletCalcFactor: form.palletCalcFactor == null ? null : Number(form.palletCalcFactor),
            isDelivered: form.isDelivered,
        });
    };

    return createPortal(
        <div className="fixed inset-0 z-50">
            <div className="absolute inset-0 bg-black/50 z-40" />
            <div className="relative z-50 flex min-h-screen items-start justify-center pt-16">
                <div
                    className="relative bg-white rounded-sm shadow-xl w-full max-w-md mx-4 p-6"
                    style={{ background: 'rgb(255, 255, 234)' }}
                    onClick={(event) => event.stopPropagation()}
                >
                    <div className="relative flex items-center justify-center mb-4">
                        <h2 className="text-sm font-semibold text-center">{title}</h2>
                        <button
                            type="button"
                            onClick={onClose}
                            disabled={isSubmitting || isViewMode}
                            className="absolute right-0 text-gray-400 hover:text-gray-600 text-l leading-none mb-1"
                        >
                            x
                        </button>
                    </div>

                    <div className="text-center">
                        <div className="text-sm font-semibold text-gray-800">
                            {(candidate?.customerOrderNr ?? details?.customerOrderNr) || ''} , {(candidate?.productName ?? details?.productName) || ''}
                        </div>
                        {isViewMode ? null : (
                            <>
                                <div className="mt-1 text-xs text-red-600">
                                    {group?.nrOf} st. i lager
                                </div>
                                <div className="text-xs text-red-600">
                                    {group?.nrOfPallets} pall i lager
                                </div>
                            </>
                        )}
                    </div>

                    <div className="mt-5 mx-5 space-y-2">
                        <div className="">
                            <LabeledDatePicker
                                label="Datum"
                                value={form.deliveryDate}
                                onChange={(value) => updateField('deliveryDate', value || '')}
                                valueType="input"
                                labelWidth="w-25"
                                inputWidth="w-40"
                                margintop="0"
                                disabled={isSubmitting || isViewMode}
                            />

                            <div className="flex items-center mt-3">
                                <label className="w-27 text-xs text-gray-700">Beställt antal</label>
                                <div className="text-xs text-gray-800">{orderedQty ?? '-'}</div>
                            </div>

                            <LabeledInput
                                label="Antal"
                                labelWidth="w-25"
                                inputWidth="w-40"
                                margintop="2"
                                type="number"
                                integerOnly
                                value={form.nrOfItems}
                                onChange={handleNrOfItemsChange}
                                disabled={isSubmitting || isViewMode}
                            />

                            <LabeledInput
                                label="Antal pall"
                                labelWidth="w-25"
                                inputWidth="w-40"
                                type="number"
                                integerOnly
                                value={form.nrOfPallets}
                                onChange={handleNrOfPalletsChange}
                                disabled={isSubmitting || isViewMode}
                            />

                            <LabeledInput
                                label="Märkning"
                                labelWidth="w-25"
                                margintop="2"
                                value={form.callOff}
                                onChange={(value) => updateField('callOff', value)}
                                disabled={isSubmitting || isViewMode}
                            />

                            <LabeledSelect
                                label="Lager"
                                labelWidth="w-25"
                                value={form.inventoryId}
                                items={isViewMode
                                ? [{ id: form.inventoryId, name: details?.inventoryName || '-' }]
                                : [{ id: '', name: '-' }, ...inventoryOptions]}
                                onChange={(value) => updateField('inventoryId', value)}
                                disabled={isLoadingInventoryOptions || isSubmitting}
                            />

                            <div className="flex items-center">
                                <label className="w-25 flex-none text-xs text-gray-700">Pallformat LxBxH</label>
                                <div className="grid flex-1 grid-cols-3 gap-1">
                                    <LabeledInput
                                        label=""
                                        labelWidth="w-0"
                                        type="number"
                                        integerOnly
                                        value={form.palletLength}
                                        onChange={(value) => updateField('palletLength', value)}
                                        disabled={isSubmitting || isViewMode}
                                    />
                                    <LabeledInput
                                        label=""
                                        labelWidth="w-0"
                                        type="number"
                                        integerOnly
                                        value={form.palletWidth}
                                        onChange={(value) => updateField('palletWidth', value)}
                                        disabled={isSubmitting || isViewMode}
                                    />
                                    <LabeledInput
                                        label=""
                                        labelWidth="w-0"
                                        type="number"
                                        integerOnly
                                        value={form.palletHeight}
                                        onChange={(value) => updateField('palletHeight', value)}
                                        disabled={isSubmitting || isViewMode}
                                    />
                                </div>
                            </div>

                            <div className="mt-3">
                                <LabeledCheckbox
                                    label="Staplingsbar"
                                    labelPosition="left"
                                    labelWidth="w-25"
                                    margintop="2"
                                    checked={form.palletIsStackable}
                                    onChange={(checked) => updateField('palletIsStackable', checked)}
                                    disabled={isSubmitting || isViewMode}
                                />
                            </div>

                            <div className="flex items-center mb-3">
                                <LabeledCheckbox
                                    label="Slattpall"
                                    labelPosition="left"
                                    labelWidth="w-25"
                                    checked={false}
                                    disabled
                                />
                            </div>

                            <LabeledInput
                                label="Upplaga per pall"
                                labelWidth="w-25"
                                inputWidth="w-40"
                                value={group?.editionPerPallet ?? ''}
                                disabled
                            />

                            <LabeledInput
                                label="Pallfaktor"
                                labelWidth="w-25"
                                inputWidth="w-40"
                                type="number"
                                value={form.palletCalcFactor}
                                onChange={(value) => updateField('palletCalcFactor', value)}
                                disabled={isSubmitting || isViewMode}
                            />

                            <div className="flex items-center mt-3">
                                <LabeledCheckbox
                                    label="Levererad"
                                    labelPosition="left"
                                    labelWidth="w-25"
                                    checked={form.isDelivered}
                                    onChange={(checked) => updateField('isDelivered', checked)}
                                    disabled={isSubmitting || isViewMode}
                                />
                            </div>
                        </div>


                        {validationError ? (
                            <p className="mt-5 text-xs font-semibold text-red-600 text-center">{validationError}</p>
                        ) : null}

                        {showLowQuantityWarning ? (
                            <p className="mt-5 text-xs text-red-600 text-center">Leveransantal mindre är 90%</p>
                        ) : null}

                        {submitError ? (
                            <p className="mt-5 text-xs text-red-600 text-center">{submitError}</p>
                        ) : null}

                        <div className="flex gap-4 mt-10 mb-3 justify-end">
                            {isViewMode ? (
                                <button
                                    type="button"
                                    onClick={onClose}
                                    className="shadow-md/30 text-xs text-white bg-orange-400 hover:bg-orange-600 px-10 p-[5px]"
                                >
                                    Stäng
                                </button>
                            ) : (
                                <>
                                    <button
                                        type="button"
                                        onClick={onClose}
                                        disabled={isSubmitting || isViewMode}
                                        className="shadow-md/30 text-xs text-white bg-orange-400 hover:bg-orange-600 px-10 p-[5px] disabled:cursor-not-allowed disabled:opacity-60"
                                    >
                                        Avbryt
                                    </button>
                                    <button
                                        type="button"
                                        onClick={handleSave}
                                        disabled={isSubmitting || isViewMode}
                                        className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 disabled:bg-gray-400 disabled:cursor-not-allowed px-10 p-[5px]"
                                    >
                                        {isSubmitting ? 'Sparar...' : 'Spara'}
                                    </button>
                                </>
                            )}
                        </div>
                    </div>
                </div>
            </div>
        </div>,
        document.body
    );
};

export default DeliveryFromStock;
