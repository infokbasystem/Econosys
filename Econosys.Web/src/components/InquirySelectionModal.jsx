import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import LabeledReactSelect from './LabeledReactSelect';

const InquirySelectionModal = ({ inquiries, onClose, onSelect, onCreate, isCreating, error }) => {
    const [selectedId, setSelectedId] = useState('');
    const dialogRef = useRef(null);

    useEffect(() => {
        const previousFocus = document.activeElement;
        dialogRef.current?.focus();
        return () => previousFocus?.focus();
    }, []);

    useEffect(() => {
        if (isCreating) dialogRef.current?.focus();
    }, [isCreating]);

    const handleKeyDown = (event) => {
        if (event.key === 'Escape') {
            event.stopPropagation();
            if (!isCreating) onClose();
        }
        if (event.key !== 'Tab') return;
        const elements = dialogRef.current.querySelectorAll('button:not(:disabled), input:not(:disabled), [tabindex="0"]');
        const first = elements[0];
        const last = elements[elements.length - 1];
        if (event.shiftKey && (document.activeElement === first || document.activeElement === dialogRef.current)) {
            event.preventDefault();
            last?.focus();
        } else if (!event.shiftKey && (document.activeElement === last || document.activeElement === dialogRef.current)) {
            event.preventDefault();
            first?.focus();
        }
    };

    return createPortal(
        <div className="fixed inset-0 z-50">
            <div className="absolute inset-0 bg-black/50 z-40" />
            <div className="relative z-50 flex min-h-screen items-start justify-center pt-20" onClick={() => !isCreating && onClose()}>
                <div
                    ref={dialogRef}
                    role="dialog"
                    aria-modal="true"
                    aria-labelledby="inquiry-selection-title"
                    aria-busy={isCreating}
                    tabIndex={-1}
                    className="relative bg-white rounded-sm shadow-xl max-w-xl w-full mx-4 p-6"
                    style={{ background: 'rgb(255, 255, 234)' }}
                    onClick={(event) => event.stopPropagation()}
                    onKeyDown={handleKeyDown}
                >
                    <div className="relative flex items-center justify-center mb-4">
                        <h2 id="inquiry-selection-title" className="text-sm font-semibold">Välj förfrågan</h2>
                        <button
                            type="button"
                            onClick={onClose}
                            disabled={isCreating}
                            aria-label="Stäng"
                            className="absolute right-0 text-gray-400 hover:text-gray-600 text-l leading-none mb-1 disabled:opacity-50"
                        >
                            ×
                        </button>
                    </div>
                    <div className="mx-10 mt-7 space-y-5 text-xs text-gray-700">
                        {inquiries.length > 0 ? (
                            <>
                                <p>Välj den förfrågan som affärsförslaget ska baseras på, eller skapa en ny automatiskt från kalkylen.</p>
                                <LabeledReactSelect
                                    name="inquiryId"
                                    aria-label="Förfrågan"
                                    label="Förfrågan"
                                    labelWidth="w-24"
                                    value={selectedId}
                                    items={inquiries}
                                    onChange={setSelectedId}
                                    isDisabled={isCreating}
                                />
                            </>
                        ) : (
                            <p>Kalkylen saknar förfrågningar. Du kan skapa en ny automatiskt från kalkylen.</p>
                        )}
                        <p>En automatiskt skapad förfrågan sparas direkt. Affärsförslaget förblir ett osparat utkast.</p>
                        {error && <p role="alert" className="border border-red-200 bg-red-100 p-2 text-red-700">{error}</p>}
                        <div className="flex flex-wrap gap-3 pt-4 justify-end">
                            <button
                                type="button"
                                disabled={isCreating}
                                onClick={onClose}
                                className="shadow-md/30 text-xs text-white bg-orange-400 hover:bg-orange-600 px-5 p-[5px] disabled:opacity-50"
                            >
                                Avbryt
                            </button>
                            <button
                                type="button"
                                disabled={isCreating}
                                onClick={onCreate}
                                className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 px-5 p-[5px] disabled:opacity-50"
                            >
                                {isCreating ? 'Skapar förfrågan...' : 'Skapa ny automatiskt'}
                            </button>
                            {inquiries.length > 0 && (
                                <button
                                    type="button"
                                    disabled={isCreating || !selectedId}
                                    onClick={() => onSelect(selectedId)}
                                    className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 px-5 p-[5px] disabled:opacity-50"
                                >
                                    Fortsätt
                                </button>
                            )}
                        </div>
                    </div>
                </div>
            </div>
        </div>,
        document.body,
    );
};

export default InquirySelectionModal;
