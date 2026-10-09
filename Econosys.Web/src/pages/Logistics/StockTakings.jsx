import { useCallback, useEffect, useMemo, useState } from 'react';
import { useBlocker } from 'react-router-dom';
import ExcelJS from 'exceljs';
import { ClipboardCheck, Download, Plus, Save, Search, Warehouse } from 'lucide-react';
import apiClient from '../../config/apiClient';
import ActionButton from '../../components/ActionButton';
import ConfirmationModal from '../../components/ConfirmationModal';
import LabeledDatePicker from '../../components/LabeledDatePicker';
import LabeledInput from '../../components/LabeledInput';
import LabeledReactSelect from '../../components/LabeledReactSelect';
import { formatDateShort, getSwedishTodayDateString } from '../../helpers/dateUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';

const formatStockTakingDate = (value) => value ? formatDateShort(value) : 'Datum saknas';

const formatCount = (value) => (
  value === null || value === undefined ? '–' : Number(value).toLocaleString('sv-SE')
);

const getDifference = (count, calculated) => (
  count === null || count === undefined || calculated === null || calculated === undefined
    ? null
    : count - calculated
);

const getApiErrorMessage = (error, fallback) => {
  const data = error?.response?.data;
  if (typeof data === 'string' && data.trim()) return data.trim();
  if (typeof data?.message === 'string' && data.message.trim()) return data.message.trim();
  return fallback;
};

const StockTakings = () => {
  const [history, setHistory] = useState([]);
  const [inventories, setInventories] = useState([]);
  const [selectedId, setSelectedId] = useState(null);
  const [selectedStockTaking, setSelectedStockTaking] = useState(null);
  const [draft, setDraft] = useState(null);
  const [mode, setMode] = useState('view');
  const [search, setSearch] = useState('');
  const [stockTakingDate, setStockTakingDate] = useState(getSwedishTodayDateString());
  const [inventoryId, setInventoryId] = useState('0');
  const [isLoadingHistory, setIsLoadingHistory] = useState(true);
  const [isLoadingInventories, setIsLoadingInventories] = useState(true);
  const [isLoadingDetails, setIsLoadingDetails] = useState(false);
  const [isCalculating, setIsCalculating] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [isExporting, setIsExporting] = useState(false);
  const [message, setMessage] = useState(null);
  const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
  const [pendingAction, setPendingAction] = useState(null);

  const hasUnsavedChanges = useCallback(() => (
    Boolean(draft?.items?.some((item) => item.nrOfItems != null || item.nrOfPallets != null))
  ), [draft]);
  const blocker = useBlocker(hasUnsavedChanges);

  useEffect(() => {
    let isActive = true;
    getSharedRequest('stock-takings-history', () => apiClient.get('/stocktakings/history'))
      .then((response) => {
        if (!isActive) return;
        const items = Array.isArray(response?.data) ? response.data : [];
        setHistory(items);
        setSelectedId(items[0]?.id ?? null);
      })
      .catch((error) => {
        if (isActive) {
          setMessage({ type: 'error', text: getApiErrorMessage(error, 'Ke inte hamta inventeringar.') });
        }
      })
      .finally(() => {
        if (isActive) setIsLoadingHistory(false);
      });

    return () => {
      isActive = false;
    };
  }, []);

  useEffect(() => {
    let isActive = true;
    getSharedRequest('stock-takings-inventories', () => apiClient.post('/inventories/search', {
      filter: {
        conditions: [{ field: 'isinventory', operator: 'eq', value: true }],
      },
      pagination: { pageNumber: 1, pageSize: 500 },
      orderBy: [{ field: 'name', direction: 'asc' }],
    }))
      .then((response) => {
        if (isActive) setInventories(Array.isArray(response?.data?.items) ? response.data.items : []);
      })
      .catch((error) => {
        if (isActive) {
          setMessage({ type: 'error', text: getApiErrorMessage(error, 'Ke inte hamta lagerlistan.') });
        }
      })
      .finally(() => {
        if (isActive) setIsLoadingInventories(false);
      });

    return () => {
      isActive = false;
    };
  }, []);

  useEffect(() => {
    if (!selectedId || mode !== 'view' || selectedStockTaking?.id === selectedId) return undefined;

    let isActive = true;
    setIsLoadingDetails(true);
    getSharedRequest(`stock-taking-detail-${selectedId}`, () => apiClient.get(`/stocktakings/${selectedId}/aggregate`))
      .then((response) => {
        if (isActive) setSelectedStockTaking(response?.data ?? null);
      })
      .catch((error) => {
        if (isActive) {
          setMessage({ type: 'error', text: getApiErrorMessage(error, 'Ke inte hamta inventeringen.') });
          setSelectedStockTaking(null);
        }
      })
      .finally(() => {
        if (isActive) setIsLoadingDetails(false);
      });

    return () => {
      isActive = false;
    };
  }, [mode, selectedId, selectedStockTaking?.id]);

  useEffect(() => {
    const handleBeforeUnload = (event) => {
      if (!hasUnsavedChanges()) return;
      event.preventDefault();
      event.returnValue = '';
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [hasUnsavedChanges]);

  useEffect(() => {
    if (blocker.state === 'blocked') {
      setPendingAction({ type: 'navigate' });
      setShowUnsavedWarning(true);
    }
  }, [blocker.state]);

  const filteredHistory = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return history;
    return history.filter((item) => (
      String(item.id ?? '').includes(term)
      || formatStockTakingDate(item.stockTakingDate).toLowerCase().includes(term)
      || String(item.inventoryNames ?? '').toLowerCase().includes(term)
    ));
  }, [history, search]);

  const selectedInventoryName = inventoryId === '0'
    ? 'Alla lager'
    : inventories.find((inventory) => inventory.id === Number(inventoryId))?.name ?? 'Valt lager';

  const beginNewStockTaking = () => {
    setMessage(null);
    setSelectedId(null);
    setSelectedStockTaking(null);
    setDraft(null);
    setStockTakingDate(getSwedishTodayDateString());
    setInventoryId('0');
    setMode('configure');
  };

  const handleNewStockTaking = () => {
    if (hasUnsavedChanges()) {
      setPendingAction({ type: 'new' });
      setShowUnsavedWarning(true);
      return;
    }
    beginNewStockTaking();
  };

  const selectStockTaking = (id) => {
    setMessage(null);
    setDraft(null);
    setSelectedStockTaking(null);
    setMode('view');
    setSelectedId(id);
  };

  const handleSelectStockTaking = (id) => {
    if (id === selectedId && mode === 'view') return;
    if (hasUnsavedChanges()) {
      setPendingAction({ type: 'select', id });
      setShowUnsavedWarning(true);
      return;
    }
    selectStockTaking(id);
  };

  const handleUnsavedWarningConfirm = () => {
    const action = pendingAction;
    setShowUnsavedWarning(false);
    setPendingAction(null);

    if (action?.type === 'new') {
      beginNewStockTaking();
    } else if (action?.type === 'select') {
      selectStockTaking(action.id);
    } else if (action?.type === 'navigate' && blocker.state === 'blocked') {
      blocker.proceed();
    }
  };

  const handleUnsavedWarningAbort = () => {
    setShowUnsavedWarning(false);
    setPendingAction(null);
    if (blocker.state === 'blocked') blocker.reset();
  };

  const handleCalculate = async () => {
    if (!stockTakingDate) {
      setMessage({ type: 'error', text: 'Ange ett inventeringsdatum.' });
      return;
    }

    setIsCalculating(true);
    setMessage(null);
    try {
      const response = await apiClient.post('/stocktakings/calculate', {
        stockTakingDate,
        inventoryId: Number(inventoryId),
      });
      setDraft({
        stockTakingDate: response.data.stockTakingDate,
        inventoryId: response.data.inventoryId,
        inventoryName: selectedInventoryName,
        items: (response.data.items ?? []).map((item) => ({
          ...item,
          nrOfItems: null,
          nrOfPallets: null,
        })),
      });
    } catch (error) {
      setMessage({ type: 'error', text: getApiErrorMessage(error, 'Ke inte berakna lagernivaerna.') });
    } finally {
      setIsCalculating(false);
    }
  };

  const updateDraftCount = (supplierOrderId, field, value) => {
    setDraft((current) => ({
      ...current,
      items: current.items.map((item) => (
        item.supplierOrderId === supplierOrderId ? { ...item, [field]: value } : item
      )),
    }));
  };

  const setCountsToCalculated = () => {
    setDraft((current) => ({
      ...current,
      items: current.items.map((item) => ({
        ...item,
        nrOfItems: item.calculatedNrOfItems,
        nrOfPallets: item.calculatedNrOfPallets,
      })),
    }));
  };

  const loadHistory = async () => {
    const response = await apiClient.get('/stocktakings/history');
    setHistory(Array.isArray(response?.data) ? response.data : []);
  };

  const handleSave = async () => {
    const items = (draft?.items ?? [])
      .filter((item) => item.nrOfItems != null || item.nrOfPallets != null)
      .map((item) => ({
        supplierOrderId: item.supplierOrderId,
        nrOfItems: item.nrOfItems,
        nrOfPallets: item.nrOfPallets,
      }));

    if (items.length === 0) {
      setMessage({ type: 'error', text: 'Ange minst ett lagervarde innan inventeringen sparas.' });
      return;
    }

    setIsSaving(true);
    setMessage(null);
    try {
      const response = await apiClient.post('/stocktakings', {
        stockTakingDate,
        items,
      });
      const saved = response?.data;
      await loadHistory();
      setDraft(null);
      setMode('view');
      setSelectedStockTaking(saved ?? null);
      setSelectedId(saved?.id ?? null);
      setMessage({ type: 'success', text: 'Inventeringen sparades.' });
    } catch (error) {
      setMessage({ type: 'error', text: getApiErrorMessage(error, 'Ke inte spara inventeringen.') });
    } finally {
      setIsSaving(false);
    }
  };

  const handleExport = async () => {
    if (!selectedStockTaking || isExporting) return;

    setIsExporting(true);
    try {
      const workbook = new ExcelJS.Workbook();
      const sheet = workbook.addWorksheet('Inventering');
      sheet.addRow(['Inventering', selectedStockTaking.id]);
      sheet.addRow(['Datum', formatStockTakingDate(selectedStockTaking.stockTakingDate)]);
      sheet.addRow(['Lager', selectedStockTaking.inventoryNames || 'Inget lager angivet']);
      sheet.addRow([]);
      sheet.addRow(['Beställning', 'Produkt', 'Kund', 'Upplaga', 'Antal', 'Pallar', 'Diff antal', 'Diff pall']);
      sheet.getRow(5).font = { bold: true };

      (selectedStockTaking.items ?? []).forEach((item) => {
        sheet.addRow([
          item.supplierOrderNr ?? '',
          item.productName ?? '',
          item.customerName ?? '',
          item.edition ?? null,
          item.nrOfItems ?? null,
          item.nrOfPallets ?? null,
          item.diffNrOfItems ?? null,
          item.diffNrOfPallets ?? null,
        ]);
      });
      [18, 28, 28, 12, 12, 12, 14, 14].forEach((width, index) => {
        sheet.getColumn(index + 1).width = width;
      });

      const buffer = await workbook.xlsx.writeBuffer();
      const url = URL.createObjectURL(new Blob([buffer], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      }));
      const link = document.createElement('a');
      link.href = url;
      link.download = `inventering-${selectedStockTaking.id}-${formatStockTakingDate(selectedStockTaking.stockTakingDate)}.xlsx`;
      link.click();
      URL.revokeObjectURL(url);
    } catch {
      setMessage({ type: 'error', text: 'Kunde inte exportera inventeringen till Excel.' });
    } finally {
      setIsExporting(false);
    }
  };

  return (
    <div className="flex h-full min-h-0 flex-col px-[clamp(4px,3vw,6vw)]">
      <ConfirmationModal
        isOpen={showUnsavedWarning}
        onClose={handleUnsavedWarningAbort}
        onConfirm={handleUnsavedWarningConfirm}
        title="Osparade ändringar"
        message="Räkneraderna har ändrats. Vill du lämna inventeringen utan att spara?"
        confirmText="Lämna utan att spara"
        cancelText="Stanna kvar"
        isDestructive={true}
      />

      {/* <h2 className="px-5 py-2 text-sm text-gray-700">Inventeringar</h2> */}

      {message && (
        <div className={`mx-5 mb-2 rounded-sm border px-3 py-2 text-xs ${message.type === 'error' ? 'border-red-200 bg-red-50 text-red-700' : 'border-green-200 bg-green-50 text-green-700'}`}>
          {message.text}
        </div>
      )}

      <div className="flex min-h-0 flex-1 flex-col items-stretch lg:flex-row">
        <aside className="flex max-h-[38vh] w-full shrink-0 flex-col border-gray-300 pr-5 mt-8 pt-3 pb-3 lg:max-h-none lg:w-100 lg:border-r">
          <div className="flex items-end gap-3">
            <div className="min-w-0 flex-1">
              <LabeledInput
                label="Sök"
                labelWidth="w-9"
                margintop="0"
                value={search}
                onChange={(value) => setSearch(value ?? '')}
              />
            </div>
            <ActionButton label="Ny inventering" icon={Plus} onClick={handleNewStockTaking} />
          </div>

          <div className="mt-3 min-h-0 flex-1 space-y-1 overflow-y-auto border-t border-gray-200 pt-2">
            {isLoadingHistory ? (
              <p className="py-2 text-xs text-gray-500">Laddar inventeringar...</p>
            ) : filteredHistory.length === 0 ? (
              <p className="py-2 text-xs text-gray-500">Inga sparade inventeringar</p>
            ) : filteredHistory.map((item) => {
              const selected = mode === 'view' && selectedId === item.id;
              return (
                <button
                  key={item.id}
                  type="button"
                  onClick={() => handleSelectStockTaking(item.id)}
                  className={`flex min-h-14 w-full flex-col justify-center rounded-sm px-2 py-1.5 text-left ${selected ? 'bg-lime-100 text-gray-950' : 'text-gray-800 hover:bg-gray-100'}`}
                >
                  <span className="flex w-full items-center justify-between gap-2 text-xs font-medium">
                    <span>{formatStockTakingDate(item.stockTakingDate)}</span>
                    <span className="text-tiny text-gray-500">#{item.id}</span>
                  </span>
                  <span className="mt-0.5 truncate text-[11px] text-gray-600">
                    {item.inventoryNames || 'Inget lager angivet'}
                  </span>
                </button>
              );
            })}
          </div>
        </aside>

        <main className="min-h-0 min-w-0 flex-1 overflow-y-auto ps-10 pr-5 py-4">
          {mode === 'configure' && !draft && (
            <section className="max-w-3xl">
              <div className="mb-5 flex items-center gap-2 border-b border-gray-200 pb-3 pl-2">
                {/* <Warehouse size={17} className="text-lime-700" /> */}
                <h3 className="text-sm text-gray-500 tracking-[0.10em] font-semibold uppercase">Ny inventering</h3>
              </div>
              <div className="grid max-w-2xl gap-3 sm:grid-cols-2 pl-2">
                <LabeledDatePicker
                  label="Inventeringsdatum"
                  labelWidth="w-28"
                  inputWidth="w-40"
                  margintop="0"
                  value={stockTakingDate}
                  valueType="input"
                  clearable={false}
                  onChange={(value) => setStockTakingDate(value ?? '')}
                />
                <LabeledReactSelect
                  label="Lager"
                  labelWidth="w-12"
                  inputWidth="w-40"
                  margintop="0"
                  inputId="stock-taking-inventory"
                  aria-label="Lager"
                  value={inventoryId}
                  onChange={(value) => setInventoryId(String(value))}
                  isDisabled={isLoadingInventories}
                  isClearable={false}
                  items={[{ id: '0', name: 'Alla lager' }, ...inventories]}
                />
              </div>
              <div className="mt-6 pl-2">
                <ActionButton
                  label={isCalculating ? 'Beräknar...' : 'Beräkna lagernivåer'}
                  icon={ClipboardCheck}
                  onClick={handleCalculate}
                  disabled={isCalculating || isLoadingInventories || !stockTakingDate}
                  accent="lime"
                />
              </div>
            </section>
          )}

          {mode === 'configure' && draft && (
            <section className="min-w-0">
              <div className="mb-4 flex flex-wrap items-center justify-between gap-3 border-b border-gray-200 pb-3 pl-2">
                <div>
                  <h3 className="text-sm font-medium text-gray-800">Ny inventering</h3>
                  <p className="mt-2 text-xs text-gray-500">
                    {formatStockTakingDate(draft.stockTakingDate)} · {draft.inventoryName}
                  </p>
                </div>
                <div className="flex flex-wrap items-center gap-4">
                  <ActionButton label="Sätt till beräknat" icon={ClipboardCheck} onClick={setCountsToCalculated} />
                  <ActionButton
                    label={isSaving ? 'Sparar...' : 'Spara'}
                    icon={Save}
                    onClick={handleSave}
                    disabled={isSaving || isCalculating}
                    accent="lime"
                  />
                </div>
              </div>

              {draft.items.length === 0 ? (
                <p className="py-5 text-xs text-gray-500">Inga order har lagersaldo för valt datum och lager.</p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[900px] border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <thead>
                      <tr className="border-b border-gray-300 text-left px-2 pt-1 pb-2 text-tiny font-medium text-gray-500">
                        <th className="px-2 py-2">Beställning</th>
                        <th className="px-2 py-2">Produkt</th>
                        <th className="px-2 py-2">Kund</th>
                        <th className="px-2 py-2 text-right">Upplaga</th>
                        <th className="px-2 py-2 text-right">Lagernivå</th>
                        <th className="px-2 py-2 text-right">Pallar</th>
                        <th className="px-4 py-2 text-right">Ny nivå</th>
                        <th className="px-4 py-2 text-right">Nya pallar</th>
                        <th className="px-2 py-2 text-right w-24">Diff</th>
                        <th className="px-2 py-2 text-right w-24">Palldiff</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr className="h-1"></tr>
                      {draft.items.map((item) => {
                        const itemDifference = getDifference(item.nrOfItems, item.calculatedNrOfItems);
                        const palletDifference = getDifference(item.nrOfPallets, item.calculatedNrOfPallets);
                        return (
                          <tr key={item.supplierOrderId} className="border-b border-gray-100 hover:bg-gray-50/70 h-7">
                            <td className="px-2 text-gray-600">{item.supplierOrderNr}</td>
                            <td className="max-w-48 truncate px-2" title={item.productName}>{item.productName}</td>
                            <td className="max-w-40 truncate px-2" title={item.customerName}>{item.customerName}</td>
                            <td className="px-2 text-right">{formatCount(item.edition)}</td>
                            <td className="px-2 text-right">{formatCount(item.calculatedNrOfItems)}</td>
                            <td className="px-2 text-right">{formatCount(item.calculatedNrOfPallets)}</td>
                            <td className="w-24 px-2">
                              <LabeledInput
                                label=""
                                labelWidth="w-0"
                                margintop="0"
                                type="number"
                                integerOnly
                                min="0"
                                aria-label={`Ny lagernivå ${item.supplierOrderNr}`}
                                value={item.nrOfItems}
                                onChange={(value) => updateDraftCount(item.supplierOrderId, 'nrOfItems', value)}
                              />
                            </td>
                            <td className="w-24 px-2">
                              <LabeledInput
                                label=""
                                labelWidth="w-0"
                                margintop="0"
                                type="number"
                                integerOnly
                                min="0"
                                aria-label={`Nya pallar ${item.supplierOrderNr}`}
                                value={item.nrOfPallets}
                                onChange={(value) => updateDraftCount(item.supplierOrderId, 'nrOfPallets', value)}
                              />
                            </td>
                            <td className={`px-2 text-right ${itemDifference < 0 ? 'text-red-700' : 'text-gray-700'}`}>
                              {formatCount(itemDifference)}
                            </td>
                            <td className={`px-2 text-right ${palletDifference < 0 ? 'text-red-700' : 'text-gray-700'}`}>
                              {formatCount(palletDifference)}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </section>
          )}

          {mode === 'view' && !selectedId && (
            <div className="flex min-h-48 flex-col items-center justify-center text-center">
              <Search size={22} className="mb-2 text-gray-400" />
              <p className="text-sm text-gray-700">Välj en inventering eller starta en ny.</p>
            </div>
          )}

          {mode === 'view' && selectedId && (
            isLoadingDetails || !selectedStockTaking ? (
              <p className="py-4 text-xs text-gray-500">{isLoadingDetails ? 'Laddar inventering...' : 'Ingen inventering ke visas.'}</p>
            ) : (
              <section className="min-w-0">
                <div className="pl-2 mb-4 flex flex-wrap items-center justify-between gap-3 border-b border-gray-200 pb-3">
                  <div>
                    <h3 className="text-sm text-gray-500 tracking-[0.10em] font-semibold uppercase">
                      Inventering #{selectedStockTaking.id}
                    </h3>
                    <p className="mt-2 text-xs text-gray-500">
                      {formatStockTakingDate(selectedStockTaking.stockTakingDate)} · {selectedStockTaking.inventoryNames || 'Inget lager angivet'}
                    </p>
                  </div>
                  <div className="flex items-center gap-10">
                    <span className="text-xs text-gray-500">{selectedStockTaking.items?.length ?? 0} rader</span>
                    <ActionButton
                      label={isExporting ? 'Exporterar...' : 'Exportera Excel'}
                      icon={Download}
                      onClick={handleExport}
                      disabled={isExporting}
                    />
                  </div>
                </div>

                {(selectedStockTaking.items ?? []).length === 0 ? (
                  <p className="py-5 text-xs text-gray-500">Inventeringen innehåller inga rader.</p>
                ) : (
                  <div className="overflow-x-auto">
                    <table className="w-full min-w-[720px] border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                      <thead>
                        <tr className="border-b border-gray-300 text-left px-2 pt-1 pb-2 text-tiny font-medium text-gray-500">
                          <th className="px-2 py-2">Beställning</th>
                          <th className="px-2 py-2">Produkt</th>
                          <th className="px-2 py-2">Kund</th>
                          <th className="px-2 py-2 text-right">Upplaga</th>
                          <th className="px-2 py-2 text-right">Antal</th>
                          <th className="px-2 py-2 text-right">Pallar</th>
                          <th className="px-2 py-2 text-right">Diff antal</th>
                          <th className="px-2 py-2 text-right">Diff pall</th>
                        </tr>
                      </thead>
                      <tbody>
                        {selectedStockTaking.items.map((item) => (
                          <tr key={item.id} className="border-b border-gray-100">
                            <td className="px-2 py-1.5 text-gray-600">{item.supplierOrderNr}</td>
                            <td className="max-w-48 truncate px-2 py-1.5" title={item.productName}>{item.productName}</td>
                            <td className="max-w-40 truncate px-2 py-1.5" title={item.customerName}>{item.customerName}</td>
                            <td className="px-2 py-1.5 text-right">{formatCount(item.edition)}</td>
                            <td className="px-2 py-1.5 text-right">{formatCount(item.nrOfItems)}</td>
                            <td className="px-2 py-1.5 text-right">{formatCount(item.nrOfPallets)}</td>
                            <td className="px-2 py-1.5 text-right">{formatCount(item.diffNrOfItems)}</td>
                            <td className="px-2 py-1.5 text-right">{formatCount(item.diffNrOfPallets)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </section>
            )
          )}
        </main>
      </div>
    </div>
  );
};

export default StockTakings;
