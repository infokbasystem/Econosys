import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { CalendarRange, Plus, Save, Trash2 } from 'lucide-react';
import { useBlocker, useNavigate, useOutletContext, useParams } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import ActionButton from '../../components/ActionButton';
import ConfirmationModal from '../../components/ConfirmationModal';
import OrderDetailLayout from '../../components/OrderDetailLayout';
import useOrderDetailState from '../../hooks/useOrderDetailState';
import apiClient from '../../config/apiClient';
import { formatDateTime } from '../../helpers/dateUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';
import {
    applyBudgetAddition,
    applyBudgetTotal,
    applyDistributionToRow,
    applyMonthEdit,
    buildBefKundRows,
    buildBudgetSummary,
    buildGridRows,
    buildGridTotals,
    buildSalesStatSummary,
    buildSavePayload,
    createEmptyBudgetRow,
    getRowKey,
    isDistributionValid,
    mapBudgetCustomer,
    normalizeMonths,
} from './budgetCalc';
import AddBudgetCustomerModal from './components/AddBudgetCustomerModal';
import AllocateBudgetCustomerModal from './components/AllocateBudgetCustomerModal';
import BudgetCustomerGrid from './components/BudgetCustomerGrid';
import BudgetCustomerMonthEditor from './components/BudgetCustomerMonthEditor';
import BudgetMonthDistributionModal from './components/BudgetMonthDistributionModal';
import BudgetOwnedModal from './components/BudgetOwnedModal';
import BudgetSidebar from './components/BudgetSidebar';
import BudgetSummaryTable from './components/BudgetSummaryTable';

const OWNERSHIP_POLL_MS = 60000;
const RELEASE_DELAY_MS = 500;
const EMPTY_LIST = [];

// Delayed releases survive React Strict Mode's unmount/remount so a re-claim can cancel them.
const pendingReleaseTimers = new Map();

const releaseOwnership = (budgetId, { keepalive = false } = {}) => {
    if (keepalive) {
        fetch(`${apiClient.defaults.baseURL}/budget/${budgetId}/ownership/release`, {
            method: 'POST',
            credentials: 'include',
            keepalive: true,
        }).catch(() => {});
        return;
    }

    apiClient.post(`/budget/${budgetId}/ownership/release`).catch((error) => {
        console.error('Failed to release budget ownership:', error);
    });
};

const renderInfoRow = (label, value) => (
    <div className="grid grid-cols-21 gap-1 mx-2">
        <div className="col-span-8"><span className="font-medium">{label}</span></div>
        <div className="col-span-13">{value || '-'}</div>
    </div>
);

const getBudgetLabel = (budget) => {
    const name = budget?.name?.trim();
    const year = budget?.year != null ? String(budget.year) : '';

    if (name && year && !name.includes(year)) {
        return `${name} - ${year}`;
    }

    return name || year || (budget?.id ? `Budget ${budget.id}` : '');
};

const mapHeader = (dto) => ({
    id: dto.id ?? 0,
    name: dto.name ?? '',
    year: dto.year ?? null,
    isActive: Boolean(dto.isActive),
    isLocked: Boolean(dto.isLocked),
    distributionMonths: normalizeMonths(dto.distributionMonths),
    compareBudgetId: dto.compareBudgetId ?? null,
    comparePrevYear: dto.comparePrevYear ?? null,
    comparePrevPrevYear: dto.comparePrevPrevYear ?? null,
});

const toSnapshot = (header, rows) => JSON.stringify({ header, rows });

const getErrorMessage = (error, fallback) => error?.response?.data?.message || fallback;

const parseIdList = (value) => (value ? value.split(',').map(Number) : []);

const useBudgetSalesStats = ({ endpoint, year, ytd, employeeKey, excludeKey }) => {
    const [result, setResult] = useState({ requestKey: null, data: null });
    const requestKey = year && employeeKey
        ? `budget:sales-stat:${endpoint}:${year}:${ytd}:${employeeKey}:${excludeKey}`
        : null;

    useEffect(() => {
        if (!requestKey) {
            return undefined;
        }

        let isActive = true;
        const body = {
            year,
            ytd,
            employeeIds: parseIdList(employeeKey),
            excludeCustomerIds: parseIdList(excludeKey),
        };

        getSharedRequest(requestKey, () => apiClient.post(`/budget/sales-stat/${endpoint}`, body))
            .then((response) => {
                if (isActive) {
                    setResult({ requestKey, data: Array.isArray(response.data) ? response.data : null });
                }
            })
            .catch((error) => {
                console.error('Failed to load budget sales stat:', error);
                if (isActive) {
                    setResult({ requestKey, data: null });
                }
            });

        return () => {
            isActive = false;
        };
    }, [employeeKey, endpoint, excludeKey, requestKey, year, ytd]);

    return requestKey && result.requestKey === requestKey ? result.data : null;
};

const Budget = ({ isNew = false }) => {
    const navigate = useNavigate();
    const { id } = useParams();
    const { refreshBudgets } = useOutletContext() ?? {};
    const routeBudgetId = /^\d+$/.test(id ?? '') ? Number(id) : null;
    const isNewBudget = isNew;
    const routeKey = isNew ? 'new' : id ?? 'active';

    const { messages, setMessages, isInfoPanelExpanded, toggleInfoPanel } = useOrderDetailState({
        initialInfoPanelExpanded: false,
    });

    const [loading, setLoading] = useState(true);
    const [notFound, setNotFound] = useState(false);
    const [formOptions, setFormOptions] = useState(null);
    const [header, setHeader] = useState(null);
    const [rows, setRows] = useState(EMPTY_LIST);
    const [ownerInfo, setOwnerInfo] = useState(null);
    const [originalSnapshot, setOriginalSnapshot] = useState('');
    const [selectedEmployeeIds, setSelectedEmployeeIds] = useState(EMPTY_LIST);
    const [selectedRowKey, setSelectedRowKey] = useState(null);
    const [compareCustomers, setCompareCustomers] = useState(EMPTY_LIST);
    const [prevYtd, setPrevYtd] = useState(false);
    const [prevPrevYtd, setPrevPrevYtd] = useState(false);
    const [saving, setSaving] = useState(false);
    const [isReadOnly, setIsReadOnly] = useState(false);
    const [ownershipConflict, setOwnershipConflict] = useState(null);
    const [showDistributionModal, setShowDistributionModal] = useState(false);
    const [showAddCustomerModal, setShowAddCustomerModal] = useState(false);
    const [allocateRow, setAllocateRow] = useState(null);
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const skipUnsavedCheckRef = useRef(false);
    const ownsBudgetRef = useRef(false);

    const budgetId = header?.id > 0 ? header.id : null;
    const employees = formOptions?.employees ?? EMPTY_LIST;
    const effectiveEmployeeIds = selectedEmployeeIds;
    const isLocked = Boolean(header?.isLocked);
    const isEditingDisabled = isReadOnly || isLocked;

    const pushMessage = useCallback((type, text) => {
        setMessages((prev) => [
            ...prev.filter((message) => message.type !== type),
            { type, text },
        ]);
    }, [setMessages]);

    const hasUnsavedChanges = useCallback(() => {
        if (skipUnsavedCheckRef.current || !header) {
            return false;
        }
        return toSnapshot(header, rows) !== originalSnapshot;
    }, [header, originalSnapshot, rows]);

    const blocker = useBlocker(hasUnsavedChanges);

    useEffect(() => {
        if (blocker.state === 'blocked') {
            setShowUnsavedWarning(true);
        }
    }, [blocker.state]);

    useEffect(() => {
        const handleBeforeUnload = (event) => {
            if (!hasUnsavedChanges()) {
                return undefined;
            }
            event.preventDefault();
            event.returnValue = '';
            return '';
        };

        window.addEventListener('beforeunload', handleBeforeUnload);
        return () => window.removeEventListener('beforeunload', handleBeforeUnload);
    }, [hasUnsavedChanges]);

    useEffect(() => {
        skipUnsavedCheckRef.current = false;
    }, [routeKey]);

    const applyLoadedBudget = useCallback((dto) => {
        const nextHeader = mapHeader(dto);
        const nextRows = (dto.customers ?? []).map(mapBudgetCustomer);
        setHeader(nextHeader);
        setRows(nextRows);
        setOwnerInfo({
            isOwned: Boolean(dto.isOwned),
            ownedByUserName: dto.ownedByUserName ?? '',
            ownedDateTime: dto.ownedDateTime ?? null,
        });
        setOriginalSnapshot(toSnapshot(nextHeader, nextRows));
    }, []);

    useEffect(() => {
        let isActive = true;

        getSharedRequest('budget:form-options', () => apiClient.get('/budget/form-options'))
            .then((response) => {
                if (isActive) {
                    setFormOptions(response.data ?? null);
                }
            })
            .catch((error) => {
                console.error('Failed to load budget form options:', error);
                if (isActive) {
                    pushMessage('error', 'Kunde inte hämta säljare och budgetar.');
                }
            });

        return () => {
            isActive = false;
        };
    }, [pushMessage]);

    useEffect(() => {
        let isActive = true;

        setLoading(true);
        setNotFound(false);
        setSelectedEmployeeIds(EMPTY_LIST);
        setSelectedRowKey(null);
        setIsReadOnly(false);
        setOwnershipConflict(null);

        const requestKey = isNewBudget
            ? 'budget:new-template'
            : routeBudgetId
                ? `budget:${routeBudgetId}`
                : 'budget:active';
        const url = isNewBudget
            ? '/budget/new-template'
            : routeBudgetId
                ? `/budget/${routeBudgetId}`
                : '/budget/active';

        getSharedRequest(requestKey, () => apiClient.get(url))
            .then((response) => {
                if (!isActive) {
                    return;
                }

                if (!response.data) {
                    setHeader(null);
                    setRows(EMPTY_LIST);
                    setNotFound(true);
                    return;
                }

                applyLoadedBudget(response.data);
            })
            .catch((error) => {
                console.error('Failed to load budget:', error);
                if (isActive) {
                    setHeader(null);
                    setRows(EMPTY_LIST);
                    setNotFound(true);
                    if (error?.response?.status !== 404) {
                        pushMessage('error', 'Kunde inte ladda budgeten.');
                    }
                }
            })
            .finally(() => {
                if (isActive) {
                    setLoading(false);
                }
            });

        return () => {
            isActive = false;
        };
    }, [applyLoadedBudget, isNewBudget, pushMessage, routeBudgetId]);

    useEffect(() => {
        if (!budgetId) {
            return undefined;
        }

        let isActive = true;
        const pendingRelease = pendingReleaseTimers.get(budgetId);
        if (pendingRelease) {
            clearTimeout(pendingRelease);
            pendingReleaseTimers.delete(budgetId);
        }

        getSharedRequest(`budget:${budgetId}:claim`, () => apiClient.post(`/budget/${budgetId}/ownership/claim`, { force: false }))
            .then(() => {
                if (isActive) {
                    ownsBudgetRef.current = true;
                }
            })
            .catch((error) => {
                if (!isActive) {
                    return;
                }
                if (error?.response?.status === 409) {
                    setOwnershipConflict(error.response.data ?? {});
                    return;
                }
                console.error('Failed to claim budget ownership:', error);
            });

        const handlePageHide = () => {
            if (ownsBudgetRef.current) {
                releaseOwnership(budgetId, { keepalive: true });
            }
        };

        window.addEventListener('pagehide', handlePageHide);

        return () => {
            isActive = false;
            window.removeEventListener('pagehide', handlePageHide);

            if (ownsBudgetRef.current) {
                ownsBudgetRef.current = false;
                pendingReleaseTimers.set(budgetId, setTimeout(() => {
                    pendingReleaseTimers.delete(budgetId);
                    releaseOwnership(budgetId);
                }, RELEASE_DELAY_MS));
            }
        };
    }, [budgetId]);

    useEffect(() => {
        if (!budgetId || isReadOnly || ownershipConflict) {
            return undefined;
        }

        const interval = setInterval(() => {
            apiClient.get(`/budget/${budgetId}/ownership`)
                .then((response) => {
                    const ownership = response.data;
                    if (ownership && !ownership.isOwnedByCurrentUser) {
                        ownsBudgetRef.current = false;
                        setIsReadOnly(true);
                        pushMessage(
                            'warning',
                            `Annan användare (${ownership.ownedByUserName || 'okänd'}) har nu tagit kontroll över denna budget.`,
                        );
                    }
                })
                .catch((error) => {
                    console.error('Failed to check budget ownership:', error);
                });
        }, OWNERSHIP_POLL_MS);

        return () => clearInterval(interval);
    }, [budgetId, isReadOnly, ownershipConflict, pushMessage]);

    const handleTakeControl = async () => {
        try {
            await apiClient.post(`/budget/${budgetId}/ownership/claim`, { force: true });
            ownsBudgetRef.current = true;
            setOwnershipConflict(null);
            setIsReadOnly(false);
        } catch (error) {
            console.error('Failed to take control of budget:', error);
            pushMessage('error', 'Kunde inte ta kontroll över budgeten.');
        }
    };

    const handleOpenReadOnly = () => {
        setOwnershipConflict(null);
        setIsReadOnly(true);
    };

    const compareBudgetId = header?.compareBudgetId ?? null;

    useEffect(() => {
        if (!compareBudgetId) {
            setCompareCustomers(EMPTY_LIST);
            return undefined;
        }

        let isActive = true;

        getSharedRequest(`budget:${compareBudgetId}`, () => apiClient.get(`/budget/${compareBudgetId}`))
            .then((response) => {
                if (isActive) {
                    setCompareCustomers(response.data?.customers ?? EMPTY_LIST);
                }
            })
            .catch((error) => {
                console.error('Failed to load compare budget:', error);
                if (isActive) {
                    setCompareCustomers(EMPTY_LIST);
                }
            });

        return () => {
            isActive = false;
        };
    }, [compareBudgetId]);

    const employeeKey = effectiveEmployeeIds.join(',');
    const excludeKey = rows
        .filter((row) => row.isRemovedFromBudget && row.customerId > 0)
        .map((row) => row.customerId)
        .join(',');

    const prevStats = useBudgetSalesStats({
        endpoint: 'per-customer',
        year: header?.comparePrevYear,
        ytd: prevYtd,
        employeeKey,
        excludeKey,
    });
    const prevPrevStats = useBudgetSalesStats({
        endpoint: 'per-customer',
        year: header?.comparePrevPrevYear,
        ytd: prevPrevYtd,
        employeeKey,
        excludeKey,
    });
    const prevMonthStats = useBudgetSalesStats({
        endpoint: 'per-month',
        year: header?.comparePrevYear,
        ytd: false,
        employeeKey,
        excludeKey,
    });
    const prevPrevMonthStats = useBudgetSalesStats({
        endpoint: 'per-month',
        year: header?.comparePrevPrevYear,
        ytd: false,
        employeeKey,
        excludeKey,
    });

    const befKundRows = useMemo(
        () => buildBefKundRows(rows, employees, header?.year),
        [employees, header?.year, rows],
    );

    const gridRows = useMemo(() => buildGridRows({
        rows,
        befKundRows,
        compareCustomers,
        prevStats: prevStats ?? EMPTY_LIST,
        prevPrevStats: prevPrevStats ?? EMPTY_LIST,
        selectedEmployeeIds: effectiveEmployeeIds,
    }), [befKundRows, compareCustomers, effectiveEmployeeIds, prevPrevStats, prevStats, rows]);

    const gridTotals = useMemo(() => buildGridTotals(gridRows), [gridRows]);

    const budgetSummaryRows = useMemo(
        () => buildBudgetSummary(rows, befKundRows, effectiveEmployeeIds),
        [befKundRows, effectiveEmployeeIds, rows],
    );

    const statBlocks = useMemo(() => {
        const blocks = [];
        if (header?.comparePrevYear) {
            blocks.push({
                ...buildSalesStatSummary(header.comparePrevYear, prevMonthStats),
                hasData: Boolean(prevMonthStats?.length),
            });
        }
        if (header?.comparePrevPrevYear) {
            blocks.push({
                ...buildSalesStatSummary(header.comparePrevPrevYear, prevPrevMonthStats),
                hasData: Boolean(prevPrevMonthStats?.length),
            });
        }
        return blocks;
    }, [header?.comparePrevPrevYear, header?.comparePrevYear, prevMonthStats, prevPrevMonthStats]);

    const selectedRow = rows.find((row) => getRowKey(row) === selectedRowKey && !row.isRemovedFromBudget) ?? null;

    const compareBudgetOptions = useMemo(
        () => (formOptions?.budgets ?? EMPTY_LIST)
            .filter((budget) => budget.id !== budgetId)
            .map((budget) => ({ id: budget.id, name: getBudgetLabel(budget) })),
        [budgetId, formOptions?.budgets],
    );

    const updateHeader = (patch) => {
        setHeader((prev) => ({ ...prev, ...patch }));
    };

    const updateRow = (key, updater) => {
        setRows((prev) => prev.map((row) => (getRowKey(row) === key ? updater(row) : row)));
    };

    const handleSelectRow = (gridRow) => {
        if (!gridRow.isEditable) {
            return;
        }

        if (selectedRow && getRowKey(selectedRow) !== gridRow.key && !isDistributionValid(selectedRow)) {
            pushMessage('warning', 'Fördelningen på kunden i redigering måste vara 100 %.');
            return;
        }

        setSelectedRowKey(gridRow.key);
    };

    const handleMonthChange = (field, monthIndex, value) => {
        if (!selectedRow) {
            return;
        }
        updateRow(getRowKey(selectedRow), (row) => applyMonthEdit(row, field, monthIndex, value));
    };

    const handleTotalChange = (gridRow, value) => {
        updateRow(gridRow.key, (row) => applyBudgetTotal(row, value, header.distributionMonths));
    };

    const handleAdditionChange = (gridRow, value) => {
        updateRow(gridRow.key, (row) => applyBudgetAddition(row, value));
    };

    const handleDistributionConfirm = (distributionMonths) => {
        setShowDistributionModal(false);
        setHeader((prev) => ({ ...prev, distributionMonths }));
        setRows((prev) => prev.map((row) => applyDistributionToRow(row, distributionMonths)));
    };

    const addCustomerRow = (customer) => {
        const key = `c-${customer.id}`;
        const existing = rows.find((row) => getRowKey(row) === key);

        if (existing) {
            updateRow(key, (row) => ({
                ...createEmptyBudgetRow(customer),
                id: row.id,
                employeeId: row.employeeId ?? customer.responsibleUserId ?? null,
                employeeName: row.employeeName || customer.responsibleUserName || '',
                budgetCountAsNewUntilMonth: row.budgetCountAsNewUntilMonth ?? customer.budgetCountAsNewUntilMonth ?? null,
            }));
        } else {
            setRows((prev) => [...prev, createEmptyBudgetRow(customer)]);
        }

        setSelectedRowKey(key);
    };

    const handleAddCustomer = (customer) => {
        setShowAddCustomerModal(false);
        addCustomerRow(customer);
    };

    const handleRowAction = async (action, gridRow) => {
        if (action === 'remove') {
            updateRow(gridRow.key, (row) => ({ ...row, isRemovedFromBudget: true }));
            if (selectedRowKey === gridRow.key) {
                setSelectedRowKey(null);
            }
            return;
        }

        if (action === 'add') {
            addCustomerRow({
                id: gridRow.customerId,
                name: gridRow.customerName,
                responsibleUserId: gridRow.employeeId,
                responsibleUserName: gridRow.employeeName,
                budgetCountAsNewUntilMonth: gridRow.budgetCountAsNewUntilMonth,
            });
            return;
        }

        if (action === 'allocate') {
            setAllocateRow(gridRow);
            return;
        }

        const active = action === 'activate';
        try {
            await apiClient.post(`/budget/customers/${gridRow.customerId}/active`, { active });

            if (active) {
                updateRow(gridRow.key, (row) => ({ ...row, customerActive: true }));
            } else {
                setRows((prev) => prev.filter((row) => getRowKey(row) !== gridRow.key));
                if (selectedRowKey === gridRow.key) {
                    setSelectedRowKey(null);
                }
            }

            pushMessage('success', active ? 'Kunden aktiverades.' : 'Kunden inaktiverades.');
        } catch (error) {
            console.error('Failed to change customer active state:', error);
            pushMessage('error', active ? 'Kunde inte aktivera kunden.' : 'Kunde inte inaktivera kunden.');
        }
    };

    const handleAllocateConfirm = async ({ employeeId, allocateFromDate }) => {
        const gridRow = allocateRow;
        setAllocateRow(null);

        try {
            await apiClient.post(`/budget/customers/${gridRow.customerId}/allocation`, {
                employeeId,
                allocateFromDate,
            });

            const employee = employees.find((item) => item.id === employeeId);
            updateRow(gridRow.key, (row) => ({
                ...row,
                employeeId,
                employeeName: employee?.initials ?? row.employeeName,
            }));
            pushMessage('success', 'Kunden allokerades om.');
        } catch (error) {
            console.error('Failed to allocate customer:', error);
            pushMessage('error', getErrorMessage(error, 'Kunde inte allokera om kunden.'));
        }
    };

    const handleSave = async () => {
        if (!header || saving) {
            return;
        }

        if (selectedRow && !isDistributionValid(selectedRow)) {
            pushMessage('warning', 'Fördelningen på kunden i redigering måste vara 100 %.');
            return;
        }

        setSaving(true);
        try {
            const payload = buildSavePayload(header, rows);
            const isCreating = !budgetId;
            const response = isCreating
                ? await apiClient.post('/budget/aggregate', payload)
                : await apiClient.put(`/budget/${budgetId}/aggregate`, payload);

            refreshBudgets?.();

            if (isCreating && response.data?.id) {
                ownsBudgetRef.current = false;
                skipUnsavedCheckRef.current = true;
                navigate(`/budget/${response.data.id}`, { replace: true });
                return;
            }

            applyLoadedBudget(response.data);
            pushMessage('success', 'Budgeten sparades.');
        } catch (error) {
            console.error('Failed to save budget:', error);
            if (error?.response?.status === 409) {
                ownsBudgetRef.current = false;
                setIsReadOnly(true);
            }
            pushMessage('error', getErrorMessage(error, 'Kunde inte spara budgeten.'));
        } finally {
            setSaving(false);
        }
    };

    const handleDelete = async () => {
        setShowDeleteConfirm(false);

        if (!budgetId) {
            skipUnsavedCheckRef.current = true;
            navigate('/budget');
            return;
        }

        try {
            await apiClient.delete(`/budget/${budgetId}`);
            ownsBudgetRef.current = false;
            refreshBudgets?.();
            skipUnsavedCheckRef.current = true;
            navigate('/budget', { replace: true });
        } catch (error) {
            console.error('Failed to delete budget:', error);
            pushMessage('error', getErrorMessage(error, 'Kunde inte radera budgeten.'));
        }
    };

    const handleUnsavedWarningConfirm = () => {
        setShowUnsavedWarning(false);
        blocker.proceed?.();
    };

    const handleUnsavedWarningAbort = () => {
        setShowUnsavedWarning(false);
        if (blocker.state === 'blocked') {
            blocker.reset();
        }
    };

    const toggleEmployee = (employeeId) => {
        setSelectedEmployeeIds(() => {
            const current = new Set(effectiveEmployeeIds);
            if (current.has(employeeId)) {
                current.delete(employeeId);
            } else {
                current.add(employeeId);
            }
            return employees.map((employee) => employee.id).filter((employeeIdItem) => current.has(employeeIdItem));
        });
    };

    if (loading || (!formOptions && !notFound)) {
        return (
            <div className="pl-10 space-y-4 pt-1 mr-60">
                <Skeleton height={30} width={420} className="mb-5" />
                <Skeleton height={220} />
                <Skeleton height={180} />
            </div>
        );
    }

    if (notFound || !header) {
        return (
            <div className="pl-10 pt-6 text-sm text-gray-600">
                {routeBudgetId ? 'Budgeten kunde inte hittas.' : 'Det finns ingen aktiv budget.'}
                <button
                    type="button"
                    onClick={() => navigate('/budget/new')}
                    className="ml-4 rounded-sm border border-gray-300 bg-white px-3 py-1 text-xs text-gray-700 hover:bg-gray-100"
                >
                    Skapa ny budget
                </button>
            </div>
        );
    }

    const title = budgetId ? getBudgetLabel(header) : 'Ny budget';
    const ownerLabel = ownerInfo?.isOwned ? ownerInfo.ownedByUserName : null;

    return (
        <div className="relative flex min-h-full flex-col">
            <ConfirmationModal
                isOpen={showDeleteConfirm}
                onClose={() => setShowDeleteConfirm(false)}
                onConfirm={handleDelete}
                title="RADERA BUDGET"
                message={`Är du säker på att du vill radera budgeten ${title}? Åtgärden kan inte ångras.`}
                confirmText="Radera"
                cancelText="Avbryt"
                isDestructive
            />

            <ConfirmationModal
                isOpen={showUnsavedWarning}
                onClose={handleUnsavedWarningAbort}
                onConfirm={handleUnsavedWarningConfirm}
                title="Osparade ändringar"
                message="Det finns osparade ändringar, vill du ändå fortsätta?"
                confirmText="Fortsätt ändå"
                cancelText="Avbryt"
            />

            <BudgetOwnedModal
                isOpen={Boolean(ownershipConflict)}
                ownership={ownershipConflict}
                onTakeControl={handleTakeControl}
                onOpenReadOnly={handleOpenReadOnly}
            />

            {showDistributionModal && (
                <BudgetMonthDistributionModal
                    isOpen
                    distributionMonths={header.distributionMonths}
                    onClose={() => setShowDistributionModal(false)}
                    onConfirm={handleDistributionConfirm}
                />
            )}

            {showAddCustomerModal && (
                <AddBudgetCustomerModal
                    isOpen
                    budgetId={budgetId}
                    excludedCustomerIds={rows
                        .filter((row) => row.customerId > 0 && !row.isRemovedFromBudget)
                        .map((row) => row.customerId)}
                    onClose={() => setShowAddCustomerModal(false)}
                    onSelect={handleAddCustomer}
                />
            )}

            {allocateRow && (
                <AllocateBudgetCustomerModal
                    isOpen
                    customerName={allocateRow.customerName}
                    employees={employees}
                    onClose={() => setAllocateRow(null)}
                    onConfirm={handleAllocateConfirm}
                />
            )}

            <OrderDetailLayout
                title={title}
                mainMaxWidthClass="max-w-none"
                messages={messages}
                isInfoPanelExpanded={isInfoPanelExpanded}
                onToggleInfoPanel={toggleInfoPanel}
                infoContent={(
                    <div className="space-y-3">
                        <h2 className="text-sm text-center text-gray-700">Info</h2>
                        <div className="space-y-2 text-xs text-gray-600">
                            {renderInfoRow('År:', header.year)}
                            {renderInfoRow('Aktiv:', header.isActive ? 'Ja' : 'Nej')}
                            {renderInfoRow('Låst:', header.isLocked ? 'Ja' : 'Nej')}
                            {renderInfoRow('Skrivskyddad:', isReadOnly ? 'Ja' : 'Nej')}
                            {ownerLabel && renderInfoRow('Öppnad av:', ownerLabel)}
                            {ownerLabel && renderInfoRow('Öppnad:', formatDateTime(ownerInfo.ownedDateTime))}
                        </div>
                    </div>
                )}
            >
                <div className="flex justify-between w-full mb-8">
                    <div className="flex items-center gap-6">
                        <ActionButton
                            label="Spara"
                            icon={Save}
                            onClick={handleSave}
                            accent="lime"
                            disabled={isReadOnly || saving}
                        />
                        <ActionButton
                            label="Lägg till kund"
                            icon={Plus}
                            onClick={() => setShowAddCustomerModal(true)}
                            accent="sky"
                            disabled={isEditingDisabled}
                        />
                        <ActionButton
                            label="Visa månadsfördelning"
                            icon={CalendarRange}
                            onClick={() => setShowDistributionModal(true)}
                            accent="teal"
                            disabled={isEditingDisabled}
                        />
                    </div>
                    <div className="flex items-center space-x-4">
                        <ActionButton
                            label="Radera"
                            icon={Trash2}
                            onClick={() => setShowDeleteConfirm(true)}
                            accent="rose"
                            disabled={isEditingDisabled}
                        />
                    </div>
                </div>

                {/* {(isReadOnly || isLocked) && (
                    <div className="mb-4 rounded-sm border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
                        {isReadOnly
                            ? 'Budgeten är skrivskyddad eftersom en annan användare har den öppen.'
                            : 'Budgeten är låst. Lås upp den för att kunna ändra värden.'}
                    </div>
                )} */}

                <div className="flex min-w-0 flex-col gap-6 xl:flex-row xl:gap-12">
                    <BudgetSidebar
                        header={header}
                        onHeaderChange={updateHeader}
                        employees={employees}
                        selectedEmployeeIds={effectiveEmployeeIds}
                        onToggleEmployee={toggleEmployee}
                        onSelectOnlyEmployee={(employeeId) => setSelectedEmployeeIds([employeeId])}
                        onSelectAllEmployees={() => setSelectedEmployeeIds(employees.map((employee) => employee.id))}
                        onSelectNoEmployees={() => setSelectedEmployeeIds([])}
                        disabled={isEditingDisabled}
                        lockDisabled={isReadOnly}
                    />

                    <div className="min-w-0 flex-1 space-y-6">
                        <BudgetSummaryTable statBlocks={statBlocks} budgetRows={budgetSummaryRows} />

                        {selectedRow && <div className="overflow-x-auto">
                            <BudgetCustomerMonthEditor
                                row={{ ...selectedRow, isEditable: true }}
                                disabled={isEditingDisabled}
                                onMonthChange={handleMonthChange}
                                onHide={() => setSelectedRowKey(null)}
                            />
                        </div>}

                        <BudgetCustomerGrid
                            budgetName={title}
                            budgetYear={header.year}
                            gridRows={gridRows}
                            totals={gridTotals}
                            selectedRowKey={selectedRowKey}
                            disabled={isEditingDisabled}
                            compareBudgetOptions={compareBudgetOptions}
                            yearOptions={formOptions?.years ?? EMPTY_LIST}
                            compareBudgetId={header.compareBudgetId}
                            comparePrevYear={header.comparePrevYear}
                            comparePrevPrevYear={header.comparePrevPrevYear}
                            prevYtd={prevYtd}
                            prevPrevYtd={prevPrevYtd}
                            onCompareChange={updateHeader}
                            onPrevYtdChange={setPrevYtd}
                            onPrevPrevYtdChange={setPrevPrevYtd}
                            onSelectRow={handleSelectRow}
                            onTotalChange={handleTotalChange}
                            onAdditionChange={handleAdditionChange}
                            onRowAction={handleRowAction}
                        />
                    </div>
                </div>
            </OrderDetailLayout>
        </div>
    );
};

export default Budget;
