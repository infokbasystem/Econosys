export const MONTH_COUNT = 12;
export const MONTH_LABELS = ['JAN', 'FEB', 'MAR', 'APR', 'MAJ', 'JUN', 'JUL', 'AUG', 'SEP', 'OKT', 'NOV', 'DEC'];

// Pseudo customer id for the calculated "Nysälj bef. kund" rows (never persisted).
export const BEF_KUND_CUSTOMER_ID = -1;

const DEFAULT_SHARE = 100 / MONTH_COUNT;

const toNumber = (value) => {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
};

const sum = (values) => values.reduce((acc, value) => acc + toNumber(value), 0);

const emptyMonths = () => Array(MONTH_COUNT).fill(null);

export const normalizeMonths = (values) => {
    const months = Array.isArray(values) ? values.slice(0, MONTH_COUNT) : [];
    while (months.length < MONTH_COUNT) {
        months.push(null);
    }
    return months;
};

export const getRowKey = (row) => (
    row.customerId != null
        ? `c-${row.customerId}`
        : `n-${row.employeeId}`
);

export const tbFromMarkup = (sales, markup) => {
    const markupValue = toNumber(markup);
    if (100 + markupValue === 0) {
        return 0;
    }
    return toNumber(sales) * (1 - 100 / (100 + markupValue));
};

export const markupFromTb = (sales, tb) => {
    const salesValue = toNumber(sales);
    const tbValue = toNumber(tb);
    return salesValue - tbValue !== 0
        ? 100 * tbValue / (salesValue - tbValue)
        : 0;
};

export const getMonthTbs = (row) => normalizeMonths(row.salesMonths)
    .map((sales, index) => tbFromMarkup(sales, row.additionMonths?.[index]));

export const getRowSalesTotal = (row) => sum(normalizeMonths(row.salesMonths));

export const getRowTbTotal = (row) => sum(getMonthTbs(row));

export const getRowDistribution = (row) => {
    const total = toNumber(row.totalSalesBudget);
    return normalizeMonths(row.salesMonths).map((sales) => (
        total !== 0 ? toNumber(sales) / total * 100 : 0
    ));
};

export const isDistributionValid = (row) => {
    const total = Math.round(sum(getRowDistribution(row)) * 100) / 100;
    return total === 100 || total === 0;
};

export const mapBudgetCustomer = (customer) => {
    const salesMonths = normalizeMonths(customer.salesMonths);
    const additionMonths = normalizeMonths(customer.additionMonths);

    return {
        id: customer.id ?? 0,
        customerId: customer.customerId ?? null,
        customerName: customer.customerName ?? '',
        customerActive: customer.customerActive ?? true,
        budgetCountAsNewUntilMonth: customer.budgetCountAsNewUntilMonth ?? null,
        employeeId: customer.employeeId ?? null,
        employeeName: customer.employeeName ?? '',
        totalSalesBudget: customer.totalSalesBudget ?? sum(salesMonths),
        additionBudget: customer.additionBudget ?? 0,
        isRemovedFromBudget: Boolean(customer.isRemovedFromBudget),
        salesMonths,
        additionMonths,
    };
};

export const applyBudgetTotal = (row, total, distributionMonths) => {
    const totalValue = toNumber(total);
    const distribution = normalizeMonths(distributionMonths);

    return {
        ...row,
        totalSalesBudget: total,
        salesMonths: distribution.map((share) => totalValue * (share == null ? DEFAULT_SHARE : toNumber(share)) / 100),
    };
};

export const applyBudgetAddition = (row, addition) => ({
    ...row,
    additionBudget: addition,
    additionMonths: Array(MONTH_COUNT).fill(addition),
});

export const applyDistributionToRow = (row, distributionMonths) => (
    applyBudgetAddition(applyBudgetTotal(row, row.totalSalesBudget, distributionMonths), row.additionBudget)
);

// Mirrors the legacy month grid: Förd. keeps the total fixed, Oms./Påslag recalculate totals.
export const applyMonthEdit = (row, field, monthIndex, value) => {
    const salesMonths = normalizeMonths(row.salesMonths).slice();
    const additionMonths = normalizeMonths(row.additionMonths).slice();
    let totalSalesBudget = row.totalSalesBudget;

    if (field === 'distribution') {
        salesMonths[monthIndex] = toNumber(row.totalSalesBudget) * toNumber(value) / 100;
    } else if (field === 'sales') {
        salesMonths[monthIndex] = value;
        totalSalesBudget = sum(salesMonths);
    } else if (field === 'addition') {
        additionMonths[monthIndex] = value;
    }

    const nextRow = {
        ...row,
        totalSalesBudget,
        salesMonths,
        additionMonths,
    };

    const salesTotal = toNumber(totalSalesBudget);
    return {
        ...nextRow,
        additionBudget: salesTotal !== 0
            ? markupFromTb(salesTotal, getRowTbTotal(nextRow))
            : null,
    };
};

const getUntilMonth = (value) => {
    if (!value) {
        return null;
    }
    const [year, month] = String(value).split('-').map(Number);
    return Number.isFinite(year) && Number.isFinite(month) ? { year, month } : null;
};

export const isCountedAsNewInYear = (row, budgetYear) => {
    const until = getUntilMonth(row.budgetCountAsNewUntilMonth);
    return Boolean(until) && until.year === Number(budgetYear);
};

export const buildBefKundRows = (rows, employees, budgetYear) => employees
    .map((employee) => {
        const salesMonths = Array(MONTH_COUNT).fill(0);
        const tbMonths = Array(MONTH_COUNT).fill(0);

        rows
            .filter((row) => (
                row.customerId != null &&
                row.customerId > 0 &&
                row.employeeId === employee.id &&
                !row.isRemovedFromBudget &&
                isCountedAsNewInYear(row, budgetYear)
            ))
            .forEach((row) => {
                const untilMonth = getUntilMonth(row.budgetCountAsNewUntilMonth).month;
                const monthTbs = getMonthTbs(row);
                normalizeMonths(row.salesMonths).forEach((sales, index) => {
                    if (untilMonth > index + 1) {
                        salesMonths[index] += toNumber(sales);
                        tbMonths[index] += monthTbs[index];
                    }
                });
            });

        const totalSales = sum(salesMonths);
        const totalTb = sum(tbMonths);

        return {
            id: -1,
            customerId: BEF_KUND_CUSTOMER_ID,
            customerName: `Nysälj ${employee.initials || employee.name || ''}, bef. kund`,
            customerActive: true,
            budgetCountAsNewUntilMonth: null,
            employeeId: employee.id,
            employeeName: employee.initials || '',
            totalSalesBudget: totalSales,
            additionBudget: markupFromTb(totalSales, totalTb),
            isRemovedFromBudget: false,
            salesMonths,
            additionMonths: salesMonths.map((sales, index) => markupFromTb(sales, tbMonths[index])),
            totalTb,
        };
    })
    .filter((row) => row.totalSalesBudget !== 0);

const buildMonthRow = (values) => ({
    months: values,
    total: sum(values),
});

export const buildBudgetSummary = (rows, befKundRows, selectedEmployeeIds) => {
    const selected = new Set(selectedEmployeeIds);
    const included = rows.filter((row) => (
        selected.has(row.employeeId) &&
        !row.isRemovedFromBudget &&
        row.customerId !== BEF_KUND_CUSTOMER_ID
    ));

    const sumMonths = (source, pick) => Array.from({ length: MONTH_COUNT }, (_, index) => (
        source.reduce((acc, row) => acc + toNumber(pick(row)[index]), 0)
    ));

    const oms = sumMonths(included, (row) => normalizeMonths(row.salesMonths));
    const tb = sumMonths(included, (row) => getMonthTbs(row));
    const omsBefKund = sumMonths(included.filter((row) => row.customerId != null), (row) => normalizeMonths(row.salesMonths));
    const nysalj = sumMonths(included.filter((row) => row.customerId == null), (row) => normalizeMonths(row.salesMonths));
    const nysaljBefKund = sumMonths(
        befKundRows.filter((row) => selected.has(row.employeeId)),
        (row) => row.salesMonths,
    );

    const omsRow = buildMonthRow(oms);
    const tbRow = buildMonthRow(tb);

    return [
        { key: 'oms', title: 'Oms.', digits: 0, ...omsRow },
        { key: 'tb', title: 'TB', digits: 0, ...tbRow },
        { key: 'omsBefKund', title: 'Oms bef kund', digits: 0, ...buildMonthRow(omsBefKund) },
        { key: 'nysalj', title: 'Nysälj', digits: 0, ...buildMonthRow(nysalj) },
        { key: 'nysaljBefKund', title: 'Nysälj bef. kund', digits: 0, ...buildMonthRow(nysaljBefKund) },
        {
            key: 'paslag',
            title: 'Påslag',
            digits: 1,
            months: oms.map((value, index) => markupFromTb(value, tb[index])),
            total: markupFromTb(omsRow.total, tbRow.total),
        },
    ];
};

export const buildSalesStatSummary = (year, months) => {
    const oms = Array.from({ length: MONTH_COUNT }, (_, index) => Math.round(toNumber(months?.[index]?.totalSales)));
    const tb = Array.from({ length: MONTH_COUNT }, (_, index) => Math.round(toNumber(months?.[index]?.totalTb)));
    const omsTotal = sum(oms);
    const tbTotal = sum(tb);

    return {
        year,
        rows: [
            { key: 'oms', title: 'Oms.', digits: 0, months: oms, total: omsTotal },
            { key: 'tb', title: 'TB', digits: 0, months: tb, total: tbTotal },
            {
                key: 'paslag',
                title: 'Påslag',
                digits: 1,
                months: Array.from({ length: MONTH_COUNT }, (_, index) => Math.round(toNumber(months?.[index]?.averageAddition))),
                total: omsTotal !== 0 ? markupFromTb(omsTotal, tbTotal) : 0,
            },
        ],
    };
};

const totalsFor = (rows, totalKey, tbKey) => {
    const total = sum(rows.map((row) => row[totalKey]));
    const tb = sum(rows.map((row) => row[tbKey]));
    return {
        total,
        tb,
        addition: total !== 0 ? markupFromTb(total, tb) : 0,
    };
};

export const buildGridTotals = (gridRows) => {
    const current = gridRows.filter((row) => row.isEditable && row.customerId !== BEF_KUND_CUSTOMER_ID);

    return {
        current: totalsFor(current, 'currentTotal', 'currentTb'),
        compare: totalsFor(gridRows, 'compareTotal', 'compareTb'),
        prev: totalsFor(gridRows, 'prevTotal', 'prevTb'),
        prevPrev: totalsFor(gridRows, 'prevPrevTotal', 'prevPrevTb'),
    };
};

// Builds the customer grid: current budget rows plus read-only rows that only exist in comparisons.
export const buildGridRows = ({
    rows,
    befKundRows,
    compareCustomers,
    prevStats,
    prevPrevStats,
    selectedEmployeeIds,
}) => {
    const selected = new Set(selectedEmployeeIds);
    const byKey = new Map();

    const ensureRow = (source, isEditable) => {
        const key = getRowKey(source);
        if (!byKey.has(key)) {
            byKey.set(key, {
                key,
                id: source.id ?? 0,
                customerId: source.customerId ?? null,
                customerName: source.customerName ?? '',
                customerActive: source.customerActive ?? true,
                budgetCountAsNewUntilMonth: source.budgetCountAsNewUntilMonth ?? null,
                employeeId: source.employeeId ?? null,
                employeeName: source.employeeName ?? '',
                isEditable,
                isRemovedFromBudget: Boolean(source.isRemovedFromBudget),
                currentTotal: null,
                currentAddition: null,
                currentTb: null,
                compareTotal: null,
                compareAddition: null,
                compareTb: null,
                prevTotal: null,
                prevAddition: null,
                prevTb: null,
                prevPrevTotal: null,
                prevPrevAddition: null,
                prevPrevTb: null,
            });
        }
        return byKey.get(key);
    };

    rows.forEach((row) => {
        const gridRow = ensureRow(row, true);
        gridRow.currentTotal = toNumber(row.totalSalesBudget);
        gridRow.currentAddition = row.additionBudget;
        gridRow.currentTb = getRowTbTotal(row);
    });

    befKundRows.forEach((row) => {
        const gridRow = ensureRow(row, false);
        gridRow.currentTotal = row.totalSalesBudget;
        gridRow.currentAddition = row.additionBudget;
        gridRow.currentTb = row.totalTb;
    });

    (compareCustomers ?? []).forEach((customer) => {
        const gridRow = ensureRow(customer, false);
        const total = toNumber(customer.totalSalesBudget);
        const addition = toNumber(customer.additionBudget);
        gridRow.compareTotal = total;
        gridRow.compareAddition = addition;
        gridRow.compareTb = total - total / (1 + addition / 100);
    });

    const applyStats = (stats, totalKey, additionKey, tbKey) => {
        (stats ?? []).forEach((stat) => {
            const gridRow = ensureRow({
                customerId: stat.customerId,
                customerName: stat.customerName,
                employeeId: stat.employeeId,
                employeeName: stat.employeeName,
            }, false);
            gridRow[totalKey] = toNumber(stat.totalSales);
            gridRow[additionKey] = toNumber(stat.averageAddition);
            gridRow[tbKey] = toNumber(stat.totalTb);
        });
    };

    applyStats(prevStats, 'prevTotal', 'prevAddition', 'prevTb');
    applyStats(prevPrevStats, 'prevPrevTotal', 'prevPrevAddition', 'prevPrevTb');

    return Array.from(byKey.values())
        .filter((row) => selected.has(row.employeeId))
        .filter((row) => (
            (row.isEditable && !row.isRemovedFromBudget) ||
            row.customerId === BEF_KUND_CUSTOMER_ID ||
            toNumber(row.compareTotal) !== 0 ||
            toNumber(row.prevTotal) !== 0 ||
            toNumber(row.prevPrevTotal) !== 0
        ))
        .sort((a, b) => a.customerName.localeCompare(b.customerName, 'sv'));
};

export const buildSavePayload = (header, rows) => ({
    name: header.name,
    year: header.year,
    isActive: Boolean(header.isActive),
    isLocked: Boolean(header.isLocked),
    distributionMonths: normalizeMonths(header.distributionMonths),
    compareBudgetId: header.compareBudgetId ?? null,
    comparePrevYear: header.comparePrevYear ?? null,
    comparePrevPrevYear: header.comparePrevPrevYear ?? null,
    customers: rows
        .filter((row) => row.customerId !== BEF_KUND_CUSTOMER_ID)
        .map((row) => ({
            id: row.id > 0 ? row.id : 0,
            customerId: row.customerId,
            employeeId: row.employeeId,
            totalSalesBudget: row.totalSalesBudget ?? null,
            additionBudget: row.additionBudget ?? null,
            isRemovedFromBudget: Boolean(row.isRemovedFromBudget),
            salesMonths: normalizeMonths(row.salesMonths),
            additionMonths: normalizeMonths(row.additionMonths),
        })),
});

export const createEmptyBudgetRow = (customer) => ({
    id: 0,
    customerId: customer.id,
    customerName: customer.name ?? '',
    customerActive: true,
    budgetCountAsNewUntilMonth: customer.budgetCountAsNewUntilMonth ?? null,
    employeeId: customer.responsibleUserId ?? null,
    employeeName: customer.responsibleUserName ?? '',
    totalSalesBudget: 0,
    additionBudget: 0,
    isRemovedFromBudget: false,
    salesMonths: emptyMonths(),
    additionMonths: emptyMonths(),
});
