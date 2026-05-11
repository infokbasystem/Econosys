import React from 'react';
import { createPortal } from 'react-dom';

const ConfirmationModal = ({
  isOpen,
  onClose,
  onConfirm,
  title = 'BEKRÄFTELSE',
  message = 'Är du säker?',
  confirmText = 'Bekräfta',
  cancelText = 'Avbryt',
  isDestructive = false,
}) => {
  
  if (!isOpen) return null;

  return createPortal(
    <div className="fixed inset-0 z-50">
      <div className="absolute inset-0 bg-black/50 z-40" />
      <div className="relative z-50 flex min-h-screen items-start justify-center pt-20" onClick={onClose}>
        <div
          className="relative bg-white rounded-sm shadow-xl max-w-xl w-full mx-4 p-6"
          style={{ background: 'rgb(255, 255, 234)' }}
          onClick={(e) => e.stopPropagation()}
        >
          <div className="relative flex items-center justify-center mb-4">
            <h2 className="text-sm font-semibold text-center">
              {title}
            </h2>
            <button
              onClick={onClose}
              className="absolute right-0 text-gray-400 hover:text-gray-600 text-l leading-none mb-1"
            >
              ×
            </button>
          </div>
          <div className="mx-10 mt-7">
            <div className="text-xs text-gray-700 mb-6 text-center">
              {message}
            </div>
            <div className="flex gap-4 mt-6 mb-3 pt-4 justify-end">
              {cancelText && (
                <button
                  onClick={onClose}
                  className="shadow-md/30 text-xs text-white bg-orange-400 hover:bg-orange-600 px-10 p-[5px]"
                >
                  {cancelText}
                </button>
              )}
              {confirmText && (
                <button
                  onClick={onConfirm}
                  className={`shadow-md/30 text-xs text-white px-10 p-[5px] ${
                    isDestructive
                      ? 'bg-red-600 hover:bg-red-800'
                      : 'bg-lime-700 hover:bg-lime-900'
                  }`}
                >
                  {confirmText}
                </button>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>,
    document.body
  );
};

export default ConfirmationModal;
