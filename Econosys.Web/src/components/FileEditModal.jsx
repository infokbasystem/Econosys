import React, { useState, useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';

const FileEditModal = ({ isOpen, onClose, onSubmit, initialFile = null, entityId = null, entityType = 'inquiry' }) => {
  const [fileName, setFileName] = useState('');
  const [description, setDescription] = useState('');
  const [selectedPath, setSelectedPath] = useState('');
  const [pathOptions, setPathOptions] = useState([]);
  const [file, setFile] = useState(null);
  const [uploading, setUploading] = useState(false);
  const [loadingPathOptions, setLoadingPathOptions] = useState(false);
  const [isDragOverFile, setIsDragOverFile] = useState(false);
  const fileInputRef = useRef(null);
  const apiUrl = import.meta.env.VITE_API_URL;
  const lastSavedDateTime = initialFile?.lastSavedDateTime || initialFile?.editedDate || initialFile?.createdDate || null;
  const lastSavedByUserName = initialFile?.lastSavedByUserName || '';

  useEffect(() => {
    const loadPathOptions = async () => {
      if (!isOpen) return;

      setLoadingPathOptions(true);

      try {
        const token = localStorage.getItem('token');
        const response = await fetch(`${apiUrl}/attachmentpathoption`, {
          method: 'GET',
          headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${token}`,
          },
        });

        if (!response.ok) {
          throw new Error(await response.text() || 'Kunde inte hämta sökvägar');
        }

        const data = await response.json();
        const items = Array.isArray(data) ? data : [];
        setPathOptions(items);
      } catch (error) {
        console.error('Error loading attachment path options:', error);
        setPathOptions([]);
      } finally {
        setLoadingPathOptions(false);
      }
    };

    loadPathOptions();
  }, [apiUrl, isOpen]);

  useEffect(() => {
    if (initialFile) {
      setFileName(initialFile.name || '');
      setDescription(initialFile.description || '');
      setSelectedPath(initialFile.path || '');
      setFile(null);
      if (fileInputRef.current) fileInputRef.current.value = '';
    } else {
      setFileName('');
      setDescription('');
      setSelectedPath('');
      setFile(null);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  }, [initialFile, isOpen]);

  useEffect(() => {
    if (!isOpen || initialFile || selectedPath || pathOptions.length === 0) return;

    const defaultFieldByEntityType = {
      product: 'defaultViewOnProduct',
      calculation: 'defaultViewOnCalculation',
      inquiry: 'defaultViewOnInquiry',
      quotation: 'defaultViewOnQuotation',
      order: 'defaultViewOnOrder',
      invoice: 'defaultViewOnInvoice',
      customer: 'defaultViewOnCustomer',
      supplier: 'defaultViewOnSupplier',
    };

    const defaultField = defaultFieldByEntityType[entityType] || 'defaultViewOnInquiry';
    const defaultPath = pathOptions.find((item) => Boolean(item?.[defaultField]));

    if (defaultPath?.code) {
      setSelectedPath(defaultPath.code);
    }
  }, [entityType, initialFile, isOpen, pathOptions, selectedPath]);

  const applySelectedFile = (selectedFile) => {
    if (!selectedFile) return;

    setFile(selectedFile);
    if (!fileName) {
      setFileName(selectedFile.name || 'fil');
    }
  };

  const handleFileSelect = (event) => {
    const selectedFile = event.target.files?.[0];
    applySelectedFile(selectedFile);
    event.target.value = '';
  };

  const handleFileDragOver = (event) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = 'copy';
    setIsDragOverFile(true);
  };

  const handleFileDragLeave = (event) => {
    event.preventDefault();
    if (event.currentTarget.contains(event.relatedTarget)) return;
    setIsDragOverFile(false);
  };

  const handleFileDrop = (event) => {
    event.preventDefault();
    setIsDragOverFile(false);

    const droppedFromItems = Array.from(event.dataTransfer?.items || [])
      .filter((item) => item.kind === 'file')
      .map((item) => item.getAsFile())
      .filter(Boolean);

    const droppedFiles = droppedFromItems.length > 0
      ? droppedFromItems
      : Array.from(event.dataTransfer?.files || []);

    applySelectedFile(droppedFiles[0]);
  };

  const handleFilePaste = (event) => {
    const pastedFromItems = Array.from(event.clipboardData?.items || [])
      .filter((item) => item.kind === 'file')
      .map((item) => item.getAsFile())
      .filter(Boolean);

    const pastedFiles = pastedFromItems.length > 0
      ? pastedFromItems
      : Array.from(event.clipboardData?.files || []);

    if (pastedFiles.length === 0) return;

    event.preventDefault();
    applySelectedFile(pastedFiles[0]);
  };

  useEffect(() => {
    if (!isOpen) return;

    const handleDocumentPaste = (event) => {
      handleFilePaste(event);
    };

    document.addEventListener('paste', handleDocumentPaste);

    return () => {
      document.removeEventListener('paste', handleDocumentPaste);
    };
  }, [isOpen]);

  const handleOpenDocument = async () => {
    if (!initialFile?.url) return;

    const token = localStorage.getItem('token');

    try {
      const response = await fetch(`${apiUrl}/attachment/${initialFile.fileName}`, {
        headers: {
          'Authorization': `Bearer ${token}`,
        },
      });

      if (!response.ok) {
        throw new Error('Unauthorized or not found');
      }

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      window.open(url, '_blank', 'noopener,noreferrer');
    } catch (error) {
      console.error('Error opening document:', error);
      alert('Kunde inte öppna dokumentet');
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!initialFile && !file) {
      alert('Välj en fil');
      return;
    }

    // If there's a file to upload (either new file or replacement file)
    if (file) {
      setUploading(true);
      try {
        const formData = new FormData();
        formData.append('file', file);
        formData.append('name', fileName || file.name);
        formData.append('description', description);
        formData.append('path', selectedPath);

        // Add entity reference based on type
        if (entityType === 'inquiry' && entityId) {
          formData.append('inquiryId', entityId);
        } else if (entityType === 'calculation' && entityId) {
          formData.append('calculationId', entityId);
        } else if (entityType === 'quotation' && entityId) {
          formData.append('quotationId', entityId);
        } else if (entityType === 'order' && entityId) {
          formData.append('orderId', entityId);
        } else if (entityType === 'invoice' && entityId) {
          formData.append('invoiceId', entityId);
        }

        const token = localStorage.getItem('token');

        const res = await fetch(`${apiUrl}/attachment/upload`, {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${token}`,
          },
          body: formData,
        });

        if (!res.ok) {
          throw new Error('File upload failed');
        }

        console.log('File uploaded successfully');

        const uploadedFile = await res.json();

        console.log('Uploaded file data:', uploadedFile);

        setUploading(false);

        // Call onSubmit with the uploaded file data
        onSubmit({
          id: uploadedFile.id,
          name: uploadedFile.name,
          description: uploadedFile.description,
          path: uploadedFile.path,
          url: uploadedFile.url
        });

        onClose();
      } catch (error) {
        console.error('Error uploading file:', error);
        alert('Kunde inte ladda upp filen');
        setUploading(false);
        return;
      }
    } else if (initialFile) {
      // For edited files without a new file, just call onSubmit with updated metadata
      onSubmit({
        id: initialFile.id,
        name: fileName,
        description,
        path: selectedPath,
        url: initialFile.url
      });
      onClose();
    }
  };

  if (!isOpen) return null;

  return createPortal(
    <div className="fixed inset-0 z-50">

      <div
        className="absolute inset-0 bg-black/50 z-40"
      />

      <div className="relative z-50 flex min-h-screen items-start justify-center pt-20">

        <div
          className="relative bg-white rounded-sm shadow-xl max-w-xl w-full py-6"
          style={{ background: 'rgb(255, 255, 234)' }}
          onClick={(e) => e.stopPropagation()}
        >
          <div className="relative flex items-center justify-center mb-4">
            <h2 className="text-sm font-semibold text-center">
              {initialFile ? 'REDIGERA FIL' : 'LÄGG TILL FIL'}
            </h2>
            <button
              onClick={onClose}
              className="absolute right-0 text-gray-400 hover:text-gray-600 text-l leading-none mb-1 mr-5"
            >
              ×
            </button>
          </div>
          <form onSubmit={handleSubmit} className="space-y-3 mx-10 mt-7">
            <div
              className={`border rounded-sm p-3 bg-gray-50 transition-colors ${isDragOverFile ? 'border-blue-500 bg-blue-50' : 'border-gray-300'}`}
              onDragOver={handleFileDragOver}
              onDragEnter={handleFileDragOver}
              onDragLeave={handleFileDragLeave}
              onDrop={handleFileDrop}
              onPaste={handleFilePaste}
              tabIndex={0}
            >
              <div className='flex items-center space-x-1 w-full pb-[1px]'>
                <label className="block text-xs text-gray-700 w-15">Fil</label>
                <button
                  type="button"
                  onClick={() => fileInputRef.current?.click()}
                  disabled={uploading}
                  className="flex-1 bg-white px-2 py-1 border border-gray-300 text-xs rounded-sm text-gray-700 hover:bg-gray-50 text-left disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {file?.name || initialFile?.name || 'Välj fil...'}
                </button>
                <div className="flex gap-2">
                  {initialFile?.url && (
                    <button
                      type="button"
                      onClick={handleOpenDocument}
                      className="shadow-md/30 text-xs text-gray bg-green-200 hover:bg-green-300 px-4 py-[5px] ml-3 mb-[1px] w-40 text-center"
                    >
                      Öppna dokument
                    </button>
                  )}
                  <input ref={fileInputRef} type="file" className="hidden" onChange={handleFileSelect} disabled={uploading} />
                </div>
              </div>
              <div className="text-[11px] text-gray-500 mt-2">
                Dra en fil hit, klistra in med Cmd+V, eller klicka på filknappen.
              </div>
            </div>
            {initialFile && (
              <p className="text-[11px] text-gray-500">Du kan välja en ny fil för att ersätta den befintliga (valfritt).</p>
            )}
            {initialFile && (lastSavedDateTime || lastSavedByUserName) && (
              <div className="text-[11px] text-gray-600 bg-white border border-gray-200 rounded-sm px-2 py-2">
                <div>
                  Senast sparad: {lastSavedDateTime ? new Date(lastSavedDateTime).toLocaleString('sv-SE') : 'Okänt datum'}
                </div>
                <div>
                  Senast sparad av: {lastSavedByUserName || 'Okänd användare'}
                </div>
              </div>
            )}
            <div className='flex items-center space-x-1 w-full pb-[1px] mt-5'>
              <label className="block text-xs text-gray-700 w-20">Filnamn</label>
              <input
                type="text"
                value={fileName}
                onChange={(e) => setFileName(e.target.value)}
                placeholder="t.ex. specifikation.pdf"
                className="w-full bg-white px-2 py-1 border border-gray-300 text-xs rounded-sm px-2 py-1 focus:outline-none"
              />
            </div>
            <div className='flex items-center space-x-1 w-full pb-[1px]'>
              <label className="block text-xs text-gray-700 w-20">Beskrivning</label>
              <textarea
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Valfri beskrivning..."
                rows={3}
                className="w-full bg-white px-2 py-1 border border-gray-300 rounded-sm text-xs focus:outline-none"
              />
            </div>
            {!!pathOptions?.length && (
              <div className='flex items-center space-x-1 w-full pb-[1px]'>
                <label className="block text-xs font-medium text-gray-700 w-20">Mapp</label>
                <select
                  value={selectedPath}
                  onChange={(e) => setSelectedPath(e.target.value)}
                  disabled={loadingPathOptions}
                  className="w-full bg-white px-2 py-1 border border-gray-300 rounded-sm text-xs focus:outline-none"
                >
                  <option value="">Välj mapp...</option>
                  {pathOptions.map((p) => (
                    <option key={p.id || p.code} value={p.code}>{p.name}</option>
                  ))}
                </select>
              </div>
            )}
            <div className="flex gap-4 mt-6 mb-3 pt-4 justify-end">
              <button type="button" onClick={onClose} disabled={uploading} className="shadow-md/30 text-xs text-white bg-orange-400 hover:bg-orange-600 px-10 p-[5px] hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed">Avbryt</button>
              <button type="submit" disabled={uploading} className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 px-10 p-[5px] disabled:opacity-50 disabled:cursor-not-allowed">{uploading ? 'Laddar upp...' : (initialFile ? 'Spara' : 'Ladda upp')}</button>
            </div>
          </form>
        </div>

      </div>

    </div>,
    document.body
  );
};

export default FileEditModal;
