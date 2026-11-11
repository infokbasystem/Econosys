import { useEffect, useId, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import NumberInput from './NumberInput';
import useAppUpdateGuard from '../hooks/useAppUpdateGuard';

const DeviationWorkdaysOverrideModal = ({
    overrideHandlingWorkdays,
    reason,
    onClose,
    onSaved,
}) => {
    const titleId = useId();
    useAppUpdateGuard(true);
    const dialogRef = useRef(null);
    const closeRef = useRef(onClose);
    const [days, setDays] = useState(overrideHandlingWorkdays ?? null);
    const [comment, setComment] = useState(reason ?? '');
    const [error, setError] = useState('');

    useEffect(() => {
        closeRef.current = onClose;
    }, [onClose]);

    useEffect(() => {
        const previousOverflow = document.body.style.overflow;
        document.body.style.overflow = 'hidden';
        dialogRef.current?.focus();

        const handleKeyDown = (event) => {
            if (event.key === 'Escape') {
                event.preventDefault();
                closeRef.current();
                return;
            }
            if (event.key !== 'Tab') return;

            const controls = [...dialogRef.current.querySelectorAll(
                'button:not(:disabled), input:not(:disabled), textarea:not(:disabled)'
            )];
            const first = controls[0];
            const last = controls[controls.length - 1];
            if (!controls.length) {
                event.preventDefault();
            } else if (!dialogRef.current.contains(document.activeElement)
                || document.activeElement === dialogRef.current
                || (event.shiftKey && document.activeElement === first)) {
                event.preventDefault();
                (event.shiftKey ? last : first)?.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first?.focus();
            }
        };

        document.addEventListener('keydown', handleKeyDown, true);
        return () => {
            document.body.style.overflow = previousOverflow;
            document.removeEventListener('keydown', handleKeyDown, true);
        };
    }, []);

    const close = () => {
        onClose();
    };

    const applyOverride = (clearOverride = false) => {
        const trimmedComment = comment.trim();
        if (!clearOverride && (!Number.isInteger(days) || days < 0)) {
            setError('Ange ett heltal på minst 0 dagar.');
            return;
        }
        if (!clearOverride && !trimmedComment) {
            setError('Ange en kommentar till justeringen.');
            return;
        }

        onSaved(clearOverride
            ? { overrideHandlingWorkdays: null, handlingWorkdaysOverrideReason: null }
            : { overrideHandlingWorkdays: days, handlingWorkdaysOverrideReason: trimmedComment });
        onClose();
    };

    const handleSubmit = (event) => {
        event.preventDefault();
        applyOverride();
    };

    return createPortal(
        <div className="fixed inset-0 z-50">
            <div className="absolute inset-0 z-40 bg-black/50" />
            <div className="relative z-50 flex min-h-screen items-start justify-center mx-4 p-6" onClick={close}>
                <section
                    ref={dialogRef}
                    role="dialog"
                    aria-modal="true"
                    aria-labelledby={titleId}
                    tabIndex={-1}
                    className="relative w-full max-w-xl rounded-sm bg-[#f4f3f2] py-6 shadow-xl"
                    onClick={(event) => event.stopPropagation()}
                >
                    <div className="relative mb-4 flex items-center justify-center">
                        <h2 id={titleId} className="text-sm text-center font-semibold">
                            Justera arbetsdagar i statistiken
                        </h2>
                        <button
                            type="button"
                            onClick={close}
                            aria-label="Stäng"
                            className="absolute right-0 mr-5 text-xl leading-none text-gray-400 hover:text-gray-600 disabled:opacity-50"
                        >
                            ×
                        </button>
                    </div>

                    <form onSubmit={handleSubmit} noValidate className="mx-10 mt-7 space-y-2">
                        {error && <p role="alert" className="border border-red-200 bg-red-100 p-2 text-xs text-red-700">{error}</p>}
                        <label className="grid items-center gap-4 text-tiny text-gray-500 sm:grid-cols-[100px_minmax(0,1fr)]">
                            <span>Antal arbetsdagar *</span>
                            <NumberInput
                                rowId="workdays-override"
                                field="days"
                                value={days}
                                decimals={0}
                                min="0"
                                step="1"
                                placeholder="t.ex. 12"
                                onChange={(rowId, field, value) => setDays(value)}
                                className="w-20 rounded-sm border border-gray-300 bg-white px-2 py-1.5 text-xs focus:border-gray-400 focus:outline-none"
                            />
                        </label>
                        <label className="grid items-start gap-4 text-tiny text-gray-500 sm:grid-cols-[100px_minmax(0,1fr)]">
                            <span className="sm:pt-2">Kommentar *</span>
                            <textarea
                                value={comment}
                                onChange={(event) => setComment(event.target.value)}
                                maxLength={4000}
                                required
                                placeholder="Varför justerades tiden?"
                                className="min-h-32 w-full resize-y rounded-sm border border-gray-300 bg-white px-2 py-1.5 text-xs focus:border-gray-400 focus:outline-none"
                            />
                        </label>

                        <div className="flex flex-wrap items-center justify-between gap-3 pt-4">
                            {overrideHandlingWorkdays != null ? (
                                <button type="button" onClick={() => applyOverride(true)}
                                    className="text-xs text-rose-700 hover:underline">
                                    Rensa justering
                                </button>
                            ) : <span />}
                            <div className="flex flex-wrap justify-end gap-3">
                                <button type="button" onClick={close}
                                    className="shadow-md/30 bg-orange-400 px-5 p-[5px] text-xs text-white hover:bg-orange-600">
                                    Avbryt
                                </button>
                                <button type="submit"
                                    className="shadow-md/30 bg-lime-700 px-5 p-[5px] text-xs text-white hover:bg-lime-900">
                                    Använd
                                </button>
                            </div>
                        </div>
                    </form>
                </section>
            </div>
        </div>,
        document.body,
    );
};

export default DeviationWorkdaysOverrideModal;