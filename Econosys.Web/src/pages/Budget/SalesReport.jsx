import { useEffect, useMemo, useState } from 'react';
import LabeledCheckbox from '../../components/LabeledCheckbox';
import LabeledSelect from '../../components/LabeledSelect';
import apiClient from '../../config/apiClient';
import { formatNumber } from '../../helpers/numberUtils';
import { getSharedRequest } from '../../helpers/sharedRequest';
import { MONTH_LABELS } from './budgetCalc';

const EMPTY_LIST = [];
const numberCellClass = 'px-1 py-[3px] text-right tabular-nums';

const sum = (values) => values.reduce((total, value) => total + (Number(value) || 0), 0);

const sumRows = (rows, field) => MONTH_LABELS.map((_, index) => rows.reduce(
    (total, row) => total + (Number(row[field]?.[index]) || 0),
    0,
));

const addMonths = (first, second) => first.map((value, index) => value + (Number(second[index]) || 0));

const tbFromAddition = (sales, addition) => sales.map((value, index) => {
    const percent = Number(addition[index]);
    return Number.isFinite(percent) && 100 + percent !== 0
        ? value * (1 - 100 / (100 + percent))
        : 0;
});

const additionFromTb = (sales, tb) => sales.map((value, index) => (
    value - tb[index] !== 0 ? 100 * tb[index] / (value - tb[index]) : null
));

const additionFromTotals = (sales, tb) => (
    sales - tb !== 0 ? 100 * tb / (sales - tb) : null
);

const prorateCurrentMonth = (months, year) => {
    if (year !== new Date().getFullYear()) return months;
    const today = new Date();
    return months.map((value, index) => (
        index === today.getMonth() ? value * today.getDate() / new Date(year, index + 1, 0).getDate() : value
    ));
};

const blankFutureMonths = (months, year) => {
    const today = new Date();
    if (year !== today.getFullYear()) return months;
    return months.map((value, index) => (index > today.getMonth() ? null : value));
};

const isBudgetRowCountedAsNew = (row, monthIndex, year, includeExistingNewCustomers) => {
    if (row.customerId == null) return true;
    if (!includeExistingNewCustomers) return false;

    const countAsNewUntil = String(row.budgetCountAsNewUntilMonth ?? '').slice(0, 10);
    const monthStart = `${year}-${String(monthIndex + 1).padStart(2, '0')}-01`;
    return countAsNewUntil >= `${year}-01-01` &&
        countAsNewUntil < `${year + 1}-01-01` &&
        monthStart < countAsNewUntil;
};

const ytdValue = (months, year) => {
    const lastMonth = year === new Date().getFullYear() ? new Date().getMonth() : 11;
    return sum(months.slice(0, lastMonth + 1));
};

const getDisplayValues = (months, year, isThousands, digits = 0, shouldScale = true, ytdOverride, totalOverride) => {
    const values = [
        ...months,
        ytdOverride ?? ytdValue(months, year),
        totalOverride ?? sum(months),
    ];
    return values.map((value) => (
        value == null ? null : isThousands && shouldScale && digits === 0 ? value / 1000 : value
    ));
};

const MetricTable = ({ title, rows, year, isThousands }) => (
    <section className="mb-2 min-w-0 border-b border-gray-300 pb-2">
        <div className="flex min-w-0 flex-col sm:flex-row sm:items-start gap-2">
            <h2 className="shrink-0 pt-1 text-xs font-semibold text-gray-800 sm:w-14">{title}</h2>
            <div className="min-w-0 flex-1 overflow-x-auto">
                <table className="w-full table-fixed border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                    <colgroup>
                        <col className="w-30" />
                        {MONTH_LABELS.map((label) => <col key={label} />)}
                        <col className="w-15" />
                        <col className="w-18" />
                    </colgroup>
                    <thead>
                        <tr className="h-5 bg-lime-200/50 text-gray-900">
                            <th className="px-2 py-[3px] text-left font-normal">{year}</th>
                            {MONTH_LABELS.map((label) => (
                                <th key={label} className="px-1 py-[3px] text-right font-normal">{label}</th>
                            ))}
                            <th className="px-1 py-[3px] text-right font-normal">YTD</th>
                            <th className="px-1 py-[3px] text-right font-normal">ÅR</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr className="h-0.5"></tr>
                        {rows.map((row) => {
                            const displayValues = getDisplayValues(
                                row.values,
                                year,
                                isThousands,
                                row.digits ?? 0,
                                row.scale !== false,
                                row.ytd,
                                row.total,
                            );
                            return (
                                <tr key={row.label} className={`${row.emphasis ? 'font-semibold' : ''} ${row.muted ? 'text-gray-500' : 'text-gray-800'} ${row.difference ? 'italic' : ''}`}>
                                    <td className="px-2 py-[3px]">{row.label}</td>
                                    {displayValues.map((value, index) => (
                                        <td key={index} className={`${numberCellClass} ${row.difference && value < 0 ? 'text-rose-700' : ''}`}>
                                            {formatNumber(value, row.digits ?? 0)}
                                        </td>
                                    ))}
                                </tr>
                            );
                        })}
                    </tbody>
                </table>
            </div>
        </div>
    </section>
);

const ProgressIndicator = ({ label, actual, previous, target, year, isThousands }) => {
    const actualYtd = ytdValue(actual, year);
    const previousYtd = ytdValue(prorateCurrentMonth(previous, year), year);
    const annualBudget = sum(target);
    const budgetYtd = ytdValue(prorateCurrentMonth(target, year), year);
    const actualRatio = annualBudget !== 0 ? actualYtd / annualBudget : 1;
    const previousRatio = annualBudget !== 0 ? previousYtd / annualBudget : 0;
    const budgetRatio = annualBudget !== 0 ? budgetYtd / annualBudget : 0;
    const actualPercent = annualBudget !== 0 ? Math.round(actualRatio * 100) : 100;
    const previousPercent = annualBudget !== 0 ? Math.round(previousRatio * 100) : 0;
    const budgetPercent = annualBudget !== 0 ? Math.round(budgetRatio * 100) : 0;
    const actualWidth = `${Math.min(Math.max(actualRatio, 0), 1) * 100}%`;
    const previousWidth = `${Math.min(Math.max(previousRatio, 0), 1) * 100}%`;
    const budgetWidth = `${Math.min(Math.max(budgetRatio, 0), 1) * 100}%`;
    const scale = (value) => (isThousands ? value / 1000 : value);
    const bars = [
        {
            name: 'Utfall',
            value: actualYtd,
            percent: actualPercent,
            width: actualWidth,
            color: actualYtd >= budgetYtd ? 'bg-teal-600' : 'bg-rose-600',
        },
        {
            name: String(year - 1),
            value: previousYtd,
            percent: previousPercent,
            width: previousWidth,
            color: 'bg-slate-500',
        },
        {
            name: 'Budget YTD',
            value: budgetYtd,
            percent: budgetPercent,
            width: budgetWidth,
            color: 'bg-blue-700',
        },
    ];

    return (
        <div className="min-w-0 py-2 text-tiny" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
            <div className="mb-1.5 flex items-baseline justify-between gap-3">
                <span className="font-semibold text-gray-900">{label}</span>
                <span className="shrink-0 tabular-nums text-gray-500">Budget {formatNumber(scale(annualBudget))}</span>
            </div>
            <div className="space-y-1">
                {bars.map((bar) => (
                    <div key={bar.name} className="grid min-w-0 grid-cols-[4.5rem_minmax(0,1fr)] items-center gap-x-2 sm:grid-cols-[5.5rem_minmax(0,1fr)_auto]">
                        <span className="text-[10px] text-gray-600">{bar.name}</span>
                        <div
                            className="relative h-4 min-w-0 overflow-hidden rounded-full border border-gray-200 bg-gray-100"
                            role="progressbar"
                            aria-label={`${label} ${bar.name}`}
                            aria-valuenow={Math.min(Math.max(bar.percent, 0), 100)}
                            aria-valuemin="0"
                            aria-valuemax="100"
                        >
                            <div className={`h-full rounded-full transition-[width] duration-300 ease-out ${bar.color}`} style={{ width: bar.width }} />
                        </div>
                        <span className="col-start-2 text-right text-[10px] font-medium tabular-nums text-gray-700 sm:col-start-3">
                            {bar.percent}% <span className="ml-1 text-gray-500">{formatNumber(scale(bar.value))}</span>
                        </span>
                    </div>
                ))}
            </div>
        </div>
    );
};

const SalesReport = () => {
    const [budgets, setBudgets] = useState(EMPTY_LIST);
    const [selectedBudgetId, setSelectedBudgetId] = useState('');
    const [report, setReport] = useState(null);
    const [selectedEmployeeIds, setSelectedEmployeeIds] = useState(EMPTY_LIST);
    const [selectedCustomerIds, setSelectedCustomerIds] = useState(EMPTY_LIST);
    const [selectedNewSalesEmployeeIds, setSelectedNewSalesEmployeeIds] = useState(EMPTY_LIST);
    const [selectedCategories, setSelectedCategories] = useState(null);
    const [isThousands, setIsThousands] = useState(true);
    const [includeExistingNewCustomers, setIncludeExistingNewCustomers] = useState(true);
    const [isLoadingBudgets, setIsLoadingBudgets] = useState(true);
    const [isLoadingReport, setIsLoadingReport] = useState(false);
    const [error, setError] = useState('');

    useEffect(() => {
        let isActive = true;

        getSharedRequest('budget:sales-report:list', () => apiClient.get('/budget'))
            .then((response) => {
                if (!isActive) return;
                const list = Array.isArray(response.data) ? response.data : [];
                setBudgets(list);
                const selected = list.find((budget) => budget.isActive) ?? list[0];
                if (selected) setIsLoadingReport(true);
                setSelectedBudgetId(selected ? String(selected.id) : '');
            })
            .catch((requestError) => {
                console.error('Failed to load budgets for sales report:', requestError);
                if (isActive) setError('Kunde inte hämta budgetar.');
            })
            .finally(() => {
                if (isActive) setIsLoadingBudgets(false);
            });

        return () => {
            isActive = false;
        };
    }, []);

    useEffect(() => {
        if (!selectedBudgetId) {
            return undefined;
        }

        let isActive = true;
        const requestKey = `budget:sales-report:${selectedBudgetId}`;

        getSharedRequest(requestKey, () => apiClient.get(`/budget/${selectedBudgetId}/sales-report`))
            .then((response) => {
                if (!isActive) return;
                const data = response.data;
                setReport(data);
                setSelectedEmployeeIds((data.employees ?? []).map((employee) => employee.id));
                setSelectedCustomerIds((data.customers ?? []).map((customer) => customer.id));
                setSelectedNewSalesEmployeeIds([...new Set((data.budgetRows ?? [])
                    .filter((row) => row.customerId == null && row.employeeId != null)
                    .map((row) => row.employeeId))]);
                setSelectedCategories(null);
            })
            .catch((requestError) => {
                console.error('Failed to load sales report:', requestError);
                if (isActive) {
                    setReport(null);
                    setError(requestError.response?.data?.message || 'Kunde inte hämta säljrapporten.');
                }
            })
            .finally(() => {
                if (isActive) setIsLoadingReport(false);
            });

        return () => {
            isActive = false;
        };
    }, [selectedBudgetId]);

    const categories = useMemo(() => [...new Set(
        (report?.customers ?? []).map((customer) => customer.category).filter(Boolean),
    )].sort(), [report]);

    const visibleCustomers = useMemo(() => (report?.customers ?? []).filter((customer) => (
        selectedEmployeeIds.includes(customer.employeeId) &&
        (!selectedCategories || selectedCategories.includes(customer.category))
    )), [report, selectedCategories, selectedEmployeeIds]);
    const visibleNewSalesEmployees = useMemo(() => {
        const employeeNames = new Map((report?.employees ?? []).map((employee) => [employee.id, employee.name]));
        const employees = new Map();

        for (const row of report?.budgetRows ?? []) {
            if (row.customerId == null && row.employeeId != null && selectedEmployeeIds.includes(row.employeeId)) {
                employees.set(row.employeeId, {
                    id: row.employeeId,
                    name: employeeNames.get(row.employeeId) || row.employeeName || '',
                });
            }
        }

        return [...employees.values()].sort((first, second) => first.name.localeCompare(second.name, 'sv'));
    }, [report, selectedEmployeeIds]);

    const selectedCustomerSet = useMemo(() => new Set(selectedCustomerIds), [selectedCustomerIds]);
    const visibleCustomerSet = useMemo(() => new Set(visibleCustomers.map((customer) => customer.id)), [visibleCustomers]);
    const selectedNewSalesEmployeeSet = useMemo(() => new Set(selectedNewSalesEmployeeIds), [selectedNewSalesEmployeeIds]);
    const hasSelectedNewSalesEmployee = selectedNewSalesEmployeeSet.size > 0;
    const allVisibleCustomersSelected = (visibleCustomers.length + visibleNewSalesEmployees.length) > 0 &&
        visibleCustomers.every((customer) => selectedCustomerSet.has(customer.id)) &&
        visibleNewSalesEmployees.every((employee) => selectedNewSalesEmployeeSet.has(employee.id));
    const selectedReportRows = useMemo(() => (report?.currentYear ?? []).filter((row) => (
        row.customerId === 0
            ? hasSelectedNewSalesEmployee
            : selectedCustomerSet.has(row.customerId) && visibleCustomerSet.has(row.customerId)
    )), [report, hasSelectedNewSalesEmployee, selectedCustomerSet, visibleCustomerSet]);
    const previousReportRows = useMemo(() => (report?.previousYear ?? []).filter((row) => (
        row.customerId === 0
            ? hasSelectedNewSalesEmployee
            : selectedCustomerSet.has(row.customerId) && visibleCustomerSet.has(row.customerId)
    )), [report, hasSelectedNewSalesEmployee, selectedCustomerSet, visibleCustomerSet]);
    const selectedBudgetRows = useMemo(() => (report?.budgetRows ?? []).filter((row) => (
        row.customerId == null
            ? selectedEmployeeIds.includes(row.employeeId) && selectedNewSalesEmployeeSet.has(row.employeeId)
            : selectedCustomerSet.has(row.customerId) && visibleCustomerSet.has(row.customerId)
    )), [report, selectedCustomerSet, selectedEmployeeIds, selectedNewSalesEmployeeSet, visibleCustomerSet]);

    const metrics = useMemo(() => {
        const actualSales = sumRows(selectedReportRows, 'salesMonths');
        const actualTb = sumRows(selectedReportRows, 'tbMonths');
        const previousSales = sumRows(previousReportRows, 'salesMonths');
        const previousTb = sumRows(previousReportRows, 'tbMonths');
        const orderCount = sumRows(selectedReportRows, 'orderCountMonths');
        const previousOrderCount = sumRows(previousReportRows, 'orderCountMonths');
        const activeCustomers = sumRows(selectedReportRows, 'activeCustomerMonths');
        const previousActiveCustomers = sumRows(previousReportRows, 'activeCustomerMonths');
        const newSales = sumRows(selectedReportRows, 'newSalesMonths');
        const newTb = sumRows(selectedReportRows, 'newTbMonths');
        const existingNewSales = sumRows(selectedReportRows, 'existingNewSalesMonths');
        const existingNewTb = sumRows(selectedReportRows, 'existingNewTbMonths');
        const newCustomerCount = sumRows(selectedReportRows, 'newCustomerCountMonths');
        const existingNewCustomerCount = sumRows(selectedReportRows, 'existingNewCustomerCountMonths');
        const currentMonthIndex = new Date().getMonth();
        const reportYear = report?.year ?? new Date().getFullYear();
        const countUniqueCustomers = (rows, fields, monthCount) => rows.filter((row) => fields.some((field) => (
            (row[field] ?? []).slice(0, monthCount).some((value) => Number(value) > 0)
        ))).length;
        const orderCountYtd = selectedReportRows.reduce((total, row) => total + (Number(row.ytdOrderCount) || 0), 0);
        const previousOrderCountYtd = previousReportRows.reduce((total, row) => total + (Number(row.ytdOrderCount) || 0), 0);
        const displayedOrderCount = orderCount.map((value, index) => (
            reportYear === new Date().getFullYear() && index > currentMonthIndex ? null : value
        ));
        const orderCountDifference = orderCount.map((value, index) => (
            index < currentMonthIndex ? value - previousOrderCount[index] : null
        ));
        const budgetSales = MONTH_LABELS.map((_, index) => selectedBudgetRows.reduce(
            (total, row) => total + (Number(row.salesMonths?.[index]) || 0),
            0,
        ));
        const budgetAdditionRows = selectedBudgetRows;
        const budgetAddition = MONTH_LABELS.map((_, index) => {
            const denominator = budgetAdditionRows.reduce((total, row) => total + (Number(row.salesMonths?.[index]) || 0), 0);
            const tb = budgetAdditionRows.reduce((total, row) => total + (tbFromAddition([Number(row.salesMonths?.[index]) || 0], [Number(row.additionMonths?.[index]) || 0])[0] || 0), 0);
            return denominator - tb !== 0 ? 100 * tb / (denominator - tb) : null;
        });
        const budgetTb = tbFromAddition(budgetSales, budgetAddition);
        const budgetNewSales = MONTH_LABELS.map((_, index) => selectedBudgetRows.reduce((total, row) => (
            isBudgetRowCountedAsNew(row, index, reportYear, includeExistingNewCustomers)
                ? total + (Number(row.salesMonths?.[index]) || 0)
                : total
        ), 0));
        const budgetNewTb = MONTH_LABELS.map((_, index) => selectedBudgetRows.reduce((total, row) => (
            isBudgetRowCountedAsNew(row, index, reportYear, includeExistingNewCustomers)
                ? total + (tbFromAddition([Number(row.salesMonths?.[index]) || 0], [Number(row.additionMonths?.[index]) || 0])[0] || 0)
                : total
        ), 0));
        const year = report?.year ?? new Date().getFullYear();
        const budgetSalesYtd = ytdValue(prorateCurrentMonth(budgetSales, year), year);
        const budgetTbYtd = ytdValue(prorateCurrentMonth(budgetTb, year), year);
        const previousSalesYtd = ytdValue(prorateCurrentMonth(previousSales, year), year);
        const previousTbYtd = ytdValue(prorateCurrentMonth(previousTb, year), year);
        const newSalesActual = includeExistingNewCustomers ? addMonths(newSales, existingNewSales) : newSales;
        const newTbActual = includeExistingNewCustomers ? addMonths(newTb, existingNewTb) : newTb;
        const newCountActual = newCustomerCount.map((value, index) => (
            includeExistingNewCustomers ? Math.max(value, existingNewCustomerCount[index]) : value
        ));
        const newCustomerCountYtd = selectedReportRows.filter((row) => (
            Number(row.ytdNewCustomerCount) > 0 ||
            (includeExistingNewCustomers && Number(row.ytdExistingNewCustomerCount) > 0)
        )).length;
        const newSalesYtd = ytdValue(newSalesActual, year);
        const newTbYtd = ytdValue(newTbActual, year);
        const budgetNewSalesYtd = ytdValue(prorateCurrentMonth(budgetNewSales, year), year);
        const budgetNewTbYtd = ytdValue(prorateCurrentMonth(budgetNewTb, year), year);

        return {
            actualSales,
            actualTb,
            previousSales,
            previousTb,
            orderCount,
            previousOrderCount,
            displayedOrderCount,
            previousOrderCountYtd,
            orderCountYtd,
            orderCountYear: sum(orderCount),
            previousOrderCountYear: sum(previousOrderCount),
            orderCountDifference,
            orderCountYtdDifference: orderCountYtd - previousOrderCountYtd,
            orderCountYearDifference: sum(orderCount) - sum(previousOrderCount),
            activeCustomers,
            previousActiveCustomers,
            activeCustomerYtd: selectedReportRows.reduce((total, row) => total + (Number(row.ytdActiveCustomer) || 0), 0),
            activeCustomerYear: countUniqueCustomers(selectedReportRows, ['activeCustomerMonths'], 12),
            previousActiveCustomerYtd: previousReportRows.reduce((total, row) => total + (Number(row.ytdActiveCustomer) || 0), 0),
            previousActiveCustomerYear: countUniqueCustomers(previousReportRows, ['activeCustomerMonths'], 12),
            newSales: newSalesActual,
            newTb: newTbActual,
            newCustomerCount: newCountActual,
            newCustomerCountYtd,
            newCustomerCountYear: selectedReportRows.filter((row) => (
                row.newCustomerCountMonths.some((value) => Number(value) > 0) ||
                (includeExistingNewCustomers && (
                    Number(row.yearExistingNewCustomerCount) > 0 ||
                    row.existingNewCustomerCountMonths.some((value) => Number(value) > 0)
                ))
            )).length,
            budgetSales,
            budgetTb,
            budgetAddition,
            budgetNewSales,
            budgetNewSalesYtd,
            budgetNewTb,
            budgetNewTbYtd,
            actualAddition: additionFromTb(actualSales, actualTb),
            previousAddition: additionFromTb(previousSales, previousTb),
            actualAdditionYtd: additionFromTotals(ytdValue(actualSales, year), ytdValue(actualTb, year)),
            actualAdditionYear: additionFromTotals(sum(actualSales), sum(actualTb)),
            budgetAdditionYtd: additionFromTotals(budgetSalesYtd, budgetTbYtd),
            budgetAdditionYear: additionFromTotals(sum(budgetSales), sum(budgetTb)),
            previousAdditionYtd: additionFromTotals(previousSalesYtd, previousTbYtd),
            previousAdditionYear: additionFromTotals(sum(previousSales), sum(previousTb)),
            newAddition: additionFromTb(newSalesActual, newTbActual),
            newAdditionYtd: additionFromTotals(newSalesYtd, newTbYtd),
            newAdditionYear: additionFromTotals(sum(newSalesActual), sum(newTbActual)),
            budgetNewAddition: additionFromTb(budgetNewSales, budgetNewTb),
            budgetNewAdditionYtd: additionFromTotals(budgetNewSalesYtd, budgetNewTbYtd),
            budgetNewAdditionYear: additionFromTotals(sum(budgetNewSales), sum(budgetNewTb)),
        };
    }, [includeExistingNewCustomers, report?.year, selectedBudgetRows, selectedReportRows, previousReportRows]);

    const reportSections = useMemo(() => {
        if (!report) return EMPTY_LIST;
        const year = report.year;
        const difference = (first, second) => first.map((value, index) => value - (Number(second[index]) || 0));
        const adjustedPreviousSales = prorateCurrentMonth(metrics.previousSales, year);
        const adjustedPreviousTb = prorateCurrentMonth(metrics.previousTb, year);
        const adjustedBudgetSales = prorateCurrentMonth(metrics.budgetSales, year);
        const adjustedBudgetTb = prorateCurrentMonth(metrics.budgetTb, year);

        return [
            {
                title: 'Order-ingång',
                rows: [
                    { label: 'Utfall', values: blankFutureMonths(metrics.actualSales, year), emphasis: true },
                    { label: 'Budget', values: metrics.budgetSales },
                    { label: 'Utfall mot budget', values: difference(metrics.actualSales, adjustedBudgetSales), difference: true },
                    { label: String(year - 1), values: metrics.previousSales, muted: true },
                    { label: `${year} mot ${year - 1}`, values: difference(metrics.actualSales, adjustedPreviousSales), difference: true, muted: true },
                ],
            },
            {
                title: 'TB',
                rows: [
                    { label: 'Utfall', values: blankFutureMonths(metrics.actualTb, year), emphasis: true },
                    { label: 'Budget', values: metrics.budgetTb },
                    { label: 'Utfall mot budget', values: difference(metrics.actualTb, adjustedBudgetTb), difference: true },
                    { label: String(year - 1), values: metrics.previousTb, muted: true },
                    { label: `${year} mot ${year - 1}`, values: difference(metrics.actualTb, adjustedPreviousTb), difference: true, muted: true },
                ],
            },
            {
                title: 'Påslag',
                rows: [
                    { label: 'Utfall', values: blankFutureMonths(metrics.actualAddition, year), ytd: metrics.actualAdditionYtd, total: metrics.actualAdditionYear, digits: 2, emphasis: true },
                    { label: 'Budget', values: metrics.budgetAddition, ytd: metrics.budgetAdditionYtd, total: metrics.budgetAdditionYear, digits: 2 },
                    { label: String(year - 1), values: metrics.previousAddition, ytd: metrics.previousAdditionYtd, total: metrics.previousAdditionYear, digits: 2, muted: true },
                ],
            },
            {
                title: 'Antal order',
                rows: [
                    { label: 'Utfall', values: metrics.displayedOrderCount, ytd: metrics.orderCountYtd, total: metrics.orderCountYear, emphasis: true, scale: false },
                    { label: String(year - 1), values: metrics.previousOrderCount, ytd: metrics.previousOrderCountYtd, total: metrics.previousOrderCountYear, muted: true, scale: false },
                    { label: `${year} mot ${year - 1}`, values: metrics.orderCountDifference, ytd: metrics.orderCountYtdDifference, total: metrics.orderCountYearDifference, difference: true, muted: true, scale: false },
                ],
            },
            {
                title: 'Aktiva kunder',
                rows: [
                    { label: 'Utfall', values: blankFutureMonths(metrics.activeCustomers, year), ytd: metrics.activeCustomerYtd, total: metrics.activeCustomerYear, emphasis: true, scale: false },
                    { label: String(year - 1), values: metrics.previousActiveCustomers, ytd: metrics.previousActiveCustomerYtd, total: metrics.previousActiveCustomerYear, muted: true, scale: false },
                ],
            },
            {
                title: 'Nya kunder',
                rows: [
                    { label: 'Antal nya kunder', values: blankFutureMonths(metrics.newCustomerCount, year), ytd: metrics.newCustomerCountYtd, total: metrics.newCustomerCountYear, emphasis: true, scale: false },
                    { label: 'Nysälj', values: blankFutureMonths(metrics.newSales, year) },
                    { label: 'TB', values: blankFutureMonths(metrics.newTb, year) },
                    { label: 'Påslag %', values: blankFutureMonths(metrics.newAddition, year), ytd: metrics.newAdditionYtd, total: metrics.newAdditionYear, digits: 2 },
                    { label: 'Budget nysälj', values: metrics.budgetNewSales, ytd: metrics.budgetNewSalesYtd },
                    { label: 'Budget TB', values: metrics.budgetNewTb, ytd: metrics.budgetNewTbYtd },
                    { label: 'Budget påslag %', values: metrics.budgetNewAddition, ytd: metrics.budgetNewAdditionYtd, total: metrics.budgetNewAdditionYear, digits: 2 },
                ],
            },
        ];
    }, [metrics, report]);

    const toggleEmployee = (id) => setSelectedEmployeeIds((current) => (
        current.includes(id) ? current.filter((employeeId) => employeeId !== id) : [...current, id]
    ));

    const toggleCustomer = (id) => setSelectedCustomerIds((current) => (
        current.includes(id) ? current.filter((customerId) => customerId !== id) : [...current, id]
    ));

    const toggleCategory = (category) => setSelectedCategories((current) => {
        const selected = current ?? categories;
        const next = selected.includes(category)
            ? selected.filter((value) => value !== category)
            : [...selected, category];
        return next.length === categories.length ? null : next;
    });

    const toggleAllVisibleCustomers = () => {
        const visibleIds = visibleCustomers.map((customer) => customer.id);
        const visibleEmployeeIds = visibleNewSalesEmployees.map((employee) => employee.id);
        setSelectedCustomerIds((current) => (
            allVisibleCustomersSelected
                ? current.filter((id) => !visibleIds.includes(id))
                : [...new Set([...current, ...visibleIds])]
        ));
        setSelectedNewSalesEmployeeIds((current) => (
            allVisibleCustomersSelected
                ? current.filter((id) => !visibleEmployeeIds.includes(id))
                : [...new Set([...current, ...visibleEmployeeIds])]
        ));
    };

    const selectAllEmployees = (selected) => {
        setSelectedEmployeeIds(selected ? (report?.employees ?? []).map((employee) => employee.id) : EMPTY_LIST);
    };

    const handleBudgetChange = (nextBudgetId) => {
        setError('');
        setIsLoadingReport(Boolean(nextBudgetId));
        setSelectedBudgetId(nextBudgetId);
    };

    const toggleNewSalesEmployee = (employeeId) => {
        const isSelected = selectedNewSalesEmployeeSet.has(employeeId);
        const newCustomerIds = (report?.currentYear ?? [])
            .filter((row) => {
                const customer = report?.customers?.find((item) => item.id === row.customerId);
                const isNew = row.newCustomerCountMonths.some((value) => Number(value) > 0) ||
                    row.existingNewCustomerCountMonths.some((value) => Number(value) > 0);
                return customer?.employeeId === employeeId && isNew;
            })
            .map((row) => row.customerId);

        setSelectedNewSalesEmployeeIds((current) => (
            isSelected
                ? current.filter((id) => id !== employeeId)
                : [...new Set([...current, employeeId])]
        ));
        setSelectedCustomerIds((current) => (
            isSelected
                ? current.filter((id) => !newCustomerIds.includes(id))
                : [...new Set([...current, ...newCustomerIds])]
        ));
    };

    return (
        <div className="flex h-full min-h-0 w-full min-w-0 flex-col overflow-y-auto ps-10 pe-0 py-2">
            <h2 className="text-sm text-gray-500 tracking-[0.10em] font-semibold uppercase">
                Säljrapport
            </h2>
            <div className="mt-4 mb-6 flex min-w-0 flex-wrap items-center gap-x-5 gap-y-2">
                <div className="w-full sm:w-72 mr-10">
                    <LabeledSelect
                        label="Välj budget"
                        labelWidth="w-24"
                        inputWidth="min-w-0 flex-1 sm:w-48"
                        margintop="0"
                        value={selectedBudgetId}
                        onChange={handleBudgetChange}
                        disabled={isLoadingBudgets || budgets.length === 0}
                        items={budgets.map((budget) => ({
                            id: budget.id,
                            name: budget.name || budget.year,
                        }))}
                    />
                </div>
                <LabeledCheckbox
                    name="viewSumsInThousands"
                    label="Visa belopp i tusental"
                    checked={isThousands}
                    onChange={setIsThousands}
                    className="min-w-0"
                    color="green"
                />
                <LabeledCheckbox
                    name="includeExistingNewCustomers"
                    label="Nysälj med befintlig kund"
                    checked={includeExistingNewCustomers}
                    onChange={setIncludeExistingNewCustomers}
                    className="min-w-0"
                    color="green"
                />
            </div>

            {error && <p className="mb-3 border-l-2 border-rose-600 bg-rose-50 px-3 py-2 text-xs text-rose-800">{error}</p>}

            {isLoadingReport ? (
                <p className="py-8 text-center text-xs text-gray-600">Hämtar säljrapport...</p>
            ) : report ? (
                <div className="grid min-h-0 min-w-0 grid-cols-1 gap-6 xl:grid-cols-[minmax(0,1fr)_300px]">
                    <div className="min-w-0">
                        {reportSections.map((section) => (
                            <MetricTable key={section.title} title={section.title} rows={section.rows} year={report.year} isThousands={isThousands} />
                        ))}
                        <div className="grid gap-x-20 py-2 md:grid-cols-2">
                            <ProgressIndicator label="Omsättning" actual={metrics.actualSales} previous={metrics.previousSales} target={metrics.budgetSales} year={report.year} isThousands={isThousands} />
                            <ProgressIndicator label="TB" actual={metrics.actualTb} previous={metrics.previousTb} target={metrics.budgetTb} year={report.year} isThousands={isThousands} />
                        </div>
                    </div>

                    <aside className="min-w-0 border-t border-gray-300 pt-3 xl:border-l xl:border-t-0 xl:pl-6 xl:pt-0">
                        <section className="mb-4">
                            <div className="mb-2 flex items-center justify-between gap-2">
                                <h2 className="text-xs font-semibold text-gray-800">Säljare</h2>
                                <div className="flex gap-2 text-xs">
                                    <button type="button" onClick={() => selectAllEmployees(false)} className="text-gray-600 underline hover:text-gray-900">Ingen</button>
                                    <button type="button" onClick={() => selectAllEmployees(true)} className="text-gray-600 underline hover:text-gray-900">Alla</button>
                                </div>
                            </div>
                            <div className="max-h-48 overflow-y-auto border-y border-gray-200 py-1">
                                {(report.employees ?? []).map((employee) => (
                                    <label key={employee.id} className="flex items-center gap-2 px-1 py-1 text-xs text-gray-700 hover:bg-white/70">
                                        <input type="checkbox" checked={selectedEmployeeIds.includes(employee.id)} onChange={() => toggleEmployee(employee.id)} />
                                        {employee.name}
                                    </label>
                                ))}
                            </div>
                        </section>

                        <section className="mb-3">
                            <h2 className="mb-2 text-xs font-semibold text-gray-800">Kundkategori</h2>
                            <div className="grid grid-cols-2 gap-x-2">
                                {categories.map((category) => (
                                    <label key={category} className="flex items-center gap-2 py-1 text-xs text-gray-700">
                                        <input
                                            type="checkbox"
                                            checked={!selectedCategories || selectedCategories.includes(category)}
                                            onChange={() => toggleCategory(category)}
                                        />
                                        {category}
                                    </label>
                                ))}
                            </div>
                        </section>

                        <section>
                            <div className="mb-2 flex items-center justify-between gap-2">
                                <h2 className="text-xs font-semibold text-gray-800">Kunder</h2>
                                <button type="button" onClick={toggleAllVisibleCustomers} className="text-xs text-gray-600 underline hover:text-gray-900">
                                    {allVisibleCustomersSelected ? 'Ingen' : 'Alla'}
                                </button>
                            </div>
                            <div className="max-h-72 overflow-y-auto border-y border-gray-200 py-1">
                                {visibleCustomers.length === 0 && visibleNewSalesEmployees.length === 0 && (
                                    <p className="px-1 py-2 text-xs text-gray-500">Inga kunder för urvalet</p>
                                )}
                                {visibleNewSalesEmployees.map((employee) => (
                                    <label key={`new-sales-${employee.id}`} className="flex items-center gap-2 px-1 py-1 text-xs text-gray-700 hover:bg-white/70">
                                        <input
                                            type="checkbox"
                                            checked={selectedNewSalesEmployeeSet.has(employee.id)}
                                            onChange={() => toggleNewSalesEmployee(employee.id)}
                                        />
                                        <span className="w-7 shrink-0 text-gray-500" />
                                        <span className="min-w-0 truncate">Nysälj {employee.name}</span>
                                    </label>
                                ))}
                                {visibleCustomers.map((customer) => (
                                    <label key={customer.id} className="flex items-center gap-2 px-1 py-1 text-xs text-gray-700 hover:bg-white/70">
                                        <input type="checkbox" checked={selectedCustomerSet.has(customer.id)} onChange={() => toggleCustomer(customer.id)} />
                                        <span className="w-7 shrink-0 text-gray-500">{customer.category || ''}</span>
                                        <span className="min-w-0 truncate">{customer.name}</span>
                                    </label>
                                ))}
                            </div>
                        </section>
                    </aside>
                </div>
            ) : (
                <p className="py-8 text-center text-xs text-gray-600">
                    {isLoadingBudgets ? 'Hämtar budgetar...' : budgets.length === 0 ? 'Det finns inga budgetar att visa.' : 'Välj en budget för att visa rapporten.'}
                </p>
            )}
        </div>
    );
};

export default SalesReport;
