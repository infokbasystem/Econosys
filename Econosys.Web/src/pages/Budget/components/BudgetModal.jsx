import { createPortal } from 'react-dom';

const BudgetModal = ({
    isOpen,
    title,
    onClose,
    onConfirm,
    confirmText = 'OK',
    cancelText = 'Avbryt',
    confirmDisabled = false,
    widthClass = 'max-w-xl',
    children,
}) => {
    if (!isOpen) {
        return null;
    }

    return createPortal(
        <div className="fixed inset-0 z-50">
            <div className="absolute inset-0 z-40 bg-black/50" />
            <div className="relative z-50 flex min-h-screen items-start justify-center pt-20" onClick={onClose}>
                <div
                    className={`relative mx-4 w-full ${widthClass} rounded-sm p-6 shadow-xl`}
                    style={{ background: 'rgb(255, 255, 234)' }}
                    onClick={(event) => event.stopPropagation()}
                >
                    <div className="relative mb-4 flex items-center justify-center">
                        <h2 className="text-center text-sm font-semibold">{title}</h2>
                        {onClose && (
                            <button
                                type="button"
                                onClick={onClose}
                                className="absolute right-0 mb-1 leading-none text-gray-400 hover:text-gray-600"
                                aria-label="Stäng"
                            >
                                ×
                            </button>
                        )}
                    </div>
                    <div className="mx-4 mt-5 text-xs text-gray-700">
                        {children}
                    </div>
                    <div className="mx-4 mb-1 mt-6 flex justify-end gap-4 pt-2">
                        {cancelText && onClose && (
                            <button
                                type="button"
                                onClick={onClose}
                                className="bg-orange-400 p-[5px] px-10 text-xs text-white shadow-md/30 hover:bg-orange-600"
                            >
                                {cancelText}
                            </button>
                        )}
                        {confirmText && onConfirm && (
                            <button
                                type="button"
                                onClick={onConfirm}
                                disabled={confirmDisabled}
                                className="bg-lime-700 p-[5px] px-10 text-xs text-white shadow-md/30 hover:bg-lime-900 disabled:cursor-not-allowed disabled:opacity-50"
                            >
                                {confirmText}
                            </button>
                        )}
                    </div>
                </div>
            </div>
        </div>,
        document.body,
    );
};

export default BudgetModal;
