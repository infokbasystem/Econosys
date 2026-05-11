import React, { useState } from 'react';
import FileEditModal from './FileEditModal';

const FileList = ({ files = [], onRemove, onAdd, onEdit, entityId = null, entityType = 'inquiry' }) => {
  const [modalOpen, setModalOpen] = useState(false);
  const [editingFile, setEditingFile] = useState(null);

  const handleAddClick = () => {
    if (!entityId) return;
    setEditingFile(null);
    setModalOpen(true);
  };

  const handleRowClick = (file) => {
    if (!entityId) return;
    setEditingFile(file);
    setModalOpen(true);
  };

  const handleModalSubmit = (payload) => {
    if (editingFile && onEdit) {
      onEdit(payload);
    } else if (onAdd) {
      onAdd(payload);
    }
    setModalOpen(false);
  };

  return (
    <div className="flex flex-col h-full">

      <div className="relative flex items-center justify-center mt-5 mb-3">
        <h2 className="text-sm text-gray-700">Filer</h2>
        {entityId && onAdd ? (
          <button
            type="button"
            onClick={handleAddClick}
            className="absolute left-0 text-xs text-blue-600 hover:bg-blue-50 px-2 py-1 rounded border border-transparent hover:border-blue-200"
          >
            +
          </button>
        ) : null}
      </div>

      {!entityId ? (
        <p className="text-center text-xs text-amber-700 px-3 py-2">
          Spara posten innan du kan lägga till filer.
        </p>
      ) : (
        <>
          <div className="flex-1 overflow-y-auto">
            {files && files.length > 0 ? (
              <ul className="">
                {files.map((file, idx) => (
                  <li
                    key={file.id || idx}
                    className="flex items-center justify-between p-1 hover:bg-purple-100 text-xs cursor-pointer px-2"
                    onClick={() => handleRowClick(file)}
                    title="Klicka för att öppna/redigera"
                  >
                    <div className="flex items-center space-x-2 min-w-0">
                      <span className="text-sm">{getFileIcon(file.name)}</span>
                      <span className="truncate">{file.name}</span>
                    </div>
                    {onRemove && (
                      <button
                        type="button"
                        onClick={(e) => { e.stopPropagation(); onRemove(file.id || idx); }}
                        className="text-red-600 hover:text-red-800 flex-shrink-0 ml-2"
                        title="Ta bort fil"
                      >
                        ✕
                      </button>
                    )}
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-xs text-gray-500 text-center py-2">Inga filer</p>
            )}
          </div>

          <FileEditModal
            isOpen={modalOpen}
            onClose={() => setModalOpen(false)}
            onSubmit={handleModalSubmit}
            initialFile={editingFile}
            entityId={entityId}
            entityType={entityType}
          />
        </>
      )}
    </div>
  );
};

const getFileIcon = (fileName) => {
  const ext = fileName?.split('.')?.pop()?.toLowerCase();
  switch (ext) {
    case 'pdf': return '📄';
    case 'doc':
    case 'docx': return '📝';
    case 'xls':
    case 'xlsx': return '📊';
    case 'jpg':
    case 'jpeg':
    case 'png':
    case 'gif': return '🖼️';
    case 'zip':
    case 'rar': return '📦';
    default: return '📎';
  }
};

export default FileList;
