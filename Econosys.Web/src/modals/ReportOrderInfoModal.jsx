import React, { useState } from 'react';
import { createPortal } from 'react-dom';

import LabeledInput from '../components/LabeledInput';
import LabeledCheckbox from '../components/LabeledCheckbox';

const ReportOrderInfoModal = ({
    isOpen,
    onClose,
    onSave,
    title = 'ÅTERRAPPORTERA',
    orderedEdition = null,
    producedEdition = null,
    isCompleted = false,
    isLoading = false,
    isSaving = false,
    errorMessage = '',
}) => {
    const [producedEditionValue, setProducedEditionValue] = useState(producedEdition);
    const [isCompletedValue, setIsCompletedValue] = useState(isCompleted);
    const [syncedProps, setSyncedProps] = useState({ isOpen, producedEdition, isCompleted });

    // Reset the editable fields whenever the modal opens or fresh values arrive from the server.
    if (syncedProps.isOpen !== isOpen
        || syncedProps.producedEdition !== producedEdition
        || syncedProps.isCompleted !== isCompleted) {
        setSyncedProps({ isOpen, producedEdition, isCompleted });
        setProducedEditionValue(producedEdition);
        setIsCompletedValue(isCompleted);
    }

    if (!isOpen) return null;

    const handleSave = () => {
        if (isSaving || isLoading) return;

        onSave?.({
            producedEdition: producedEditionValue === '' || producedEditionValue === undefined
                ? null
                : producedEditionValue,
            isCompleted: isCompletedValue,
        });
    };

    return createPortal(
        <div className="fixed inset-0 z-50">
            <div className="absolute inset-0 bg-black/50 z-40" />
            <div className="relative z-50 flex min-h-screen items-start justify-center pt-20" onClick={onClose}>
                <div
                    className="relative bg-white rounded-sm shadow-xl max-w-md w-full mx-4 p-6"
                    style={{ background: 'rgb(255, 255, 234)' }}
                    onClick={(e) => e.stopPropagation()}
                >
                    <div className="relative flex items-center justify-center mb-4">
                        <h2 className="text-sm font-semibold text-center">{title}</h2>
                        <button
                            type="button"
                            onClick={onClose}
                            className="absolute right-0 text-gray-400 hover:text-gray-600 text-l leading-none mb-1"
                        >
                            ×
                        </button>
                    </div>

                    <div className="mx-6 mt-7">
                        <div className="text-xs font-semibold text-gray-800 mb-2">Beställning</div>
                        <LabeledInput
                            label="Beställd upplaga"
                            labelWidth="w-40"
                            type="number"
                            integerOnly
                            value={orderedEdition}
                            disabled
                        />
                        <LabeledInput
                            label="Producerad upplaga"
                            labelWidth="w-40"
                            type="number"
                            integerOnly
                            value={producedEditionValue}
                            onChange={setProducedEditionValue}
                            disabled={isLoading || isSaving}
                        />

                        <div className="text-xs font-semibold text-gray-800 mt-6 mb-2">Ordererkännande</div>
                        <LabeledCheckbox
                            label="Slutlevererad"
                            labelPosition="left"
                            labelWidth="w-40"
                            name="isCompleted"
                            checked={isCompletedValue}
                            onChange={setIsCompletedValue}
                            disabled={isLoading || isSaving}
                        />

                        {errorMessage ? (
                            <div className="mt-4 text-xs text-red-600">{errorMessage}</div>
                        ) : null}

                        <div className="flex gap-4 mt-8 mb-3 pt-4 justify-end">
                            <button
                                type="button"
                                onClick={onClose}
                                disabled={isSaving}
                                className="shadow-md/30 text-xs text-white bg-orange-400 hover:bg-orange-600 px-10 p-[5px] disabled:opacity-50"
                            >
                                Avbryt
                            </button>
                            <button
                                type="button"
                                onClick={handleSave}
                                disabled={isLoading || isSaving}
                                className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 px-10 p-[5px] disabled:opacity-50"
                            >
                                {isSaving ? 'Sparar...' : 'Spara'}
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>,
        document.body
    );
};

export default ReportOrderInfoModal;
