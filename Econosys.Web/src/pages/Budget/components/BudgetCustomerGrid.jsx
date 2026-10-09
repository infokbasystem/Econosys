import { useEffect, useRef, useState } from 'react';

import { formatNumber } from '../../../helpers/numberUtils';
import LabeledCheckbox from '../../../components/LabeledCheckbox';
import { BEF_KUND_CUSTOMER_ID, MONTH_LABELS, isCountedAsNewInYear } from '../budgetCalc';
import BudgetNumberInput from './BudgetNumberInput';

const readOnlyCellClass = 'flex h-6 items-center justify-center rounded-sm border border-gray-200 px-1 text-xs tabular-nums text-gray-700';

const ReadOnlyNumber = ({ value, digits = 0 }) => (
    <div className={readOnlyCellClass}>
        {value == null ? '' : formatNumber(value, digits)}
    </div>
);

const selectClass = 'h-6 w-40 rounded-sm border border-gray-300 bg-white px-1 text-xs font-normal focus:outline-none';

const columnWidths = [
    '70px', '40px', '40px',
    '7%', '4%', '6%',
    '7%', '4%', '6%',
    '7%', '4%', '6%',
    '7%', '4%', '6%',
    '25px',
];

const RowMenu = ({ row, onAction, onClose }) => {
    const menuRef = useRef(null);

    useEffect(() => {
        const handleOutside = (event) => {
            if (menuRef.current && !menuRef.current.contains(event.target)) {
                onClose();
            }
        };

        document.addEventListener('mousedown', handleOutside);
        return () => document.removeEventListener('mousedown', handleOutside);
    }, [onClose]);

    const actions = [];

    if (row.isEditable) {
        actions.push({ key: 'remove', label: 'Ta bort från budget' });
    } else {
        actions.push({ key: 'add', label: 'Lägg till i budget' });
    }

    actions.push(row.customerActive
        ? { key: 'inactivate', label: 'Inaktivera kund' }
        : { key: 'activate', label: 'Aktivera kund' });

    actions.push({ key: 'allocate', label: 'Allokera om' });

    return (
        <div
            ref={menuRef}
            className="absolute right-0 top-full z-30 mt-1 w-44 rounded-sm border border-gray-300 bg-yellow-50 py-1 shadow-md"
        >
            {actions.map((action) => (
                <button
                    key={action.key}
                    type="button"
                    onClick={() => {
                        onClose();
                        onAction(action.key, row);
                    }}
                    className="block w-full px-3 py-1 text-left text-xs text-gray-700 hover:bg-gray-100"
                >
                    {action.label}
                </button>
            ))}
        </div>
    );
};

const BudgetCustomerGrid = ({
    budgetName,
    budgetYear,
    gridRows,
    totals,
    selectedRowKey,
    disabled,
    compareBudgetOptions,
    yearOptions,
    compareBudgetId,
    comparePrevYear,
    comparePrevPrevYear,
    prevYtd,
    prevPrevYtd,
    onCompareChange,
    onPrevYtdChange,
    onPrevPrevYtdChange,
    onSelectRow,
    onTotalChange,
    onAdditionChange,
    onRowAction,
}) => {
    const [openMenuKey, setOpenMenuKey] = useState(null);

    const renderNewUntil = (row) => {
        if (!isCountedAsNewInYear(row, budgetYear)) {
            return '';
        }
        const month = Number(String(row.budgetCountAsNewUntilMonth).split('-')[1]);
        return MONTH_LABELS[month - 1]?.toLowerCase() ?? '';
    };

    const groupHeaderClass = 'px-1 pb-1 text-center text-xs font-normal tracking-wide text-gray-800';

    return (
        <div className="overflow-x-auto mt-5">
            <table className="w-full min-w-[1040px] table-fixed border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                <colgroup>
                    {columnWidths.map((width, index) => (
                        <col key={index} style={{ width }} />
                    ))}
                </colgroup>
                <thead>
                    <tr>
                        <th colSpan={3} />
                        <th colSpan={3} className={groupHeaderClass}>BUDGET</th>
                        <th colSpan={3} className={groupHeaderClass}>JÄMFÖRELSEBUDGET</th>
                        <th colSpan={3} className={groupHeaderClass}>ORDERINGÅNG ÅR</th>
                        <th colSpan={3} className={groupHeaderClass}>ORDERINGÅNG ÅR</th>
                        <th />
                    </tr>
                    <tr>
                        <th colSpan={3} />
                        <th colSpan={3} className="px-1 pb-2 text-center font-normal text-gray-700">
                            {budgetName}
                        </th>
                        <th colSpan={3} className="px-1 pb-2">
                            <div className="flex items-center justify-center">
                                <select
                                    value={compareBudgetId ?? ''}
                                    onChange={(event) => onCompareChange({
                                        compareBudgetId: event.target.value ? Number(event.target.value) : null,
                                    })}
                                    className={selectClass}
                                    disabled={disabled}
                                >
                                    <option value="" />
                                    {compareBudgetOptions.map((option) => (
                                        <option key={option.id} value={option.id}>{option.name}</option>
                                    ))}
                                </select>
                            </div>
                        </th>
                        {[
                            { value: comparePrevYear, field: 'comparePrevYear', ytd: prevYtd, onYtd: onPrevYtdChange },
                            { value: comparePrevPrevYear, field: 'comparePrevPrevYear', ytd: prevPrevYtd, onYtd: onPrevPrevYtdChange },
                        ].map((group) => (
                            <th key={group.field} colSpan={3} className="px-1 pb-2 font-normal">
                                <div className="flex items-center justify-center gap-2">
                                    <select
                                        value={group.value ?? ''}
                                        onChange={(event) => onCompareChange({
                                            [group.field]: event.target.value ? Number(event.target.value) : null,
                                        })}
                                        className={selectClass}
                                        disabled={disabled}
                                    >
                                        <option value="" />
                                        {yearOptions.map((year) => (
                                            <option key={year} value={year}>{year}</option>
                                        ))}
                                    </select>
                                    <LabeledCheckbox
                                        name={`${group.field}-ytd`}
                                        label="YTD"
                                        checked={group.ytd}
                                        onChange={group.onYtd}
                                    />
                                </div>
                            </th>
                        ))}
                        <th />
                    </tr>
                    <tr className="border-y border-gray-300 text-tiny tracking-wide text-gray-600">
                        <th className="px-1 py-1 text-left font-normal">Kund</th>
                        <th className="px-1 py-1 text-left font-normal">Ny till</th>
                        <th className="px-1 py-1 text-left font-normal">Säljare</th>
                        <th className="px-1 py-1 text-center font-normal">Budget</th>
                        <th className="px-1 py-1 text-center font-normal">Påslag</th>
                        <th className="px-1 py-1 text-center font-normal">TB</th>
                        <th className="px-1 py-1 text-center font-normal">Budget</th>
                        <th className="px-1 py-1 text-center font-normal">Påslag</th>
                        <th className="px-1 py-1 text-center font-normal">TB</th>
                        <th className="px-1 py-1 text-center font-normal">Oms</th>
                        <th className="px-1 py-1 text-center font-normal">Påslag</th>
                        <th className="px-1 py-1 text-center font-normal">TB</th>
                        <th className="px-1 py-1 text-center font-normal">Oms</th>
                        <th className="px-1 py-1 text-center font-normal">Påslag</th>
                        <th className="px-1 py-1 text-center font-normal">TB</th>
                        <th />
                    </tr>
                </thead>
                <tbody>
                    <tr className="border-b border-gray-300 bg-gray-50">
                        <td className="px-1 py-[3px] font-medium text-gray-800">TOTALT</td>
                        <td />
                        <td />
                        <td className="px-[2px] py-[3px]"><ReadOnlyNumber value={totals.current.total} /></td>
                        <td className="px-[2px] py-[3px]"><ReadOnlyNumber value={totals.current.addition} digits={1} /></td>
                        <td className="px-[2px] py-[3px]"><ReadOnlyNumber value={totals.current.tb} /></td>
                        <td className="px-[2px] py-[3px]"><ReadOnlyNumber value={totals.compare.total} /></td>
                        <td className="px-[2px] py-[3px]"><ReadOnlyNumber value={totals.compare.addition} digits={1} /></td>
                        <td className="px-[2px] py-[3px]"><ReadOnlyNumber value={totals.compare.tb} /></td>
                        <td className="px-[2px] py-[3px]"><ReadOnlyNumber value={totals.prev.total} /></td>
                        <td className="px-[2px] py-[3px]"><ReadOnlyNumber value={totals.prev.addition} digits={1} /></td>
                        <td className="px-[2px] py-[3px]"><ReadOnlyNumber value={totals.prev.tb} /></td>
                        <td className="px-[2px] py-[3px]"><ReadOnlyNumber value={totals.prevPrev.total} /></td>
                        <td className="px-[2px] py-[3px]"><ReadOnlyNumber value={totals.prevPrev.addition} digits={1} /></td>
                        <td className="px-[2px] py-[3px]"><ReadOnlyNumber value={totals.prevPrev.tb} /></td>
                        <td />
                    </tr>
                    {gridRows.map((row) => {
                        const isSelected = row.key === selectedRowKey;
                        const isBefKund = row.customerId === BEF_KUND_CUSTOMER_ID;
                        const canEditValues = row.isEditable && !disabled;
                        const hasMenu = row.customerId != null && row.customerId > 0 && !disabled;

                        return (
                            <tr
                                key={row.key}
                                className={`${isSelected ? 'bg-lime-50' : 'hover:bg-gray-50'} ${row.isEditable || isBefKund ? '' : 'text-gray-400'}`}
                            >
                                <td className="truncate px-1 py-[2px]">
                                    <button
                                        type="button"
                                        onClick={() => onSelectRow(row)}
                                        disabled={!row.isEditable}
                                        className={`w-full truncate text-left ${row.customerId == null ? 'font-semibold' : ''} ${isSelected ? 'text-red-600' : ''} ${row.isEditable ? 'hover:underline' : 'cursor-default'} ${row.customerActive ? '' : 'italic'}`}
                                        title={row.customerName}
                                    >
                                        {row.customerName}
                                    </button>
                                </td>
                                <td className="px-1 py-[2px] text-gray-600">{renderNewUntil(row)}</td>
                                <td className="truncate px-1 py-[2px] text-gray-600">{row.employeeName}</td>
                                <td className="px-[2px] py-[2px]">
                                    {canEditValues ? (
                                        <BudgetNumberInput
                                            value={row.currentTotal}
                                            onCommit={(value) => onTotalChange(row, value)}
                                        />
                                    ) : (
                                        <ReadOnlyNumber value={row.currentTotal} />
                                    )}
                                </td>
                                <td className="px-[2px] py-[2px]">
                                    {canEditValues ? (
                                        <BudgetNumberInput
                                            value={row.currentAddition}
                                            digits={1}
                                            onCommit={(value) => onAdditionChange(row, value)}
                                        />
                                    ) : (
                                        <ReadOnlyNumber value={row.currentAddition} digits={1} />
                                    )}
                                </td>
                                <td className="px-[2px] py-[2px]"><ReadOnlyNumber value={row.currentTb} /></td>
                                <td className="px-[2px] py-[2px]"><ReadOnlyNumber value={row.compareTotal} /></td>
                                <td className="px-[2px] py-[2px]"><ReadOnlyNumber value={row.compareAddition} digits={1} /></td>
                                <td className="px-[2px] py-[2px]"><ReadOnlyNumber value={row.compareTb} /></td>
                                <td className="px-[2px] py-[2px]"><ReadOnlyNumber value={row.prevTotal} /></td>
                                <td className="px-[2px] py-[2px]"><ReadOnlyNumber value={row.prevAddition} digits={1} /></td>
                                <td className="px-[2px] py-[2px]"><ReadOnlyNumber value={row.prevTb} /></td>
                                <td className="px-[2px] py-[2px]"><ReadOnlyNumber value={row.prevPrevTotal} /></td>
                                <td className="px-[2px] py-[2px]"><ReadOnlyNumber value={row.prevPrevAddition} digits={1} /></td>
                                <td className="px-[2px] py-[2px]"><ReadOnlyNumber value={row.prevPrevTb} /></td>
                                <td className="relative px-1 py-[2px] text-right">
                                    {hasMenu && (
                                        <>
                                            <button
                                                type="button"
                                                onClick={() => setOpenMenuKey((prev) => (prev === row.key ? null : row.key))}
                                                className="text-xs text-gray-500 hover:text-gray-900 hover:underline"
                                            >
                                                Mer...
                                            </button>
                                            {openMenuKey === row.key && (
                                                <RowMenu
                                                    row={row}
                                                    onAction={onRowAction}
                                                    onClose={() => setOpenMenuKey(null)}
                                                />
                                            )}
                                        </>
                                    )}
                                </td>
                            </tr>
                        );
                    })}
                    {gridRows.length === 0 && (
                        <tr>
                            <td colSpan={16} className="py-4 text-center text-xs font-light text-gray-500">
                                Inga kunder för valda säljare
                            </td>
                        </tr>
                    )}
                </tbody>
            </table>
        </div>
    );
};

export default BudgetCustomerGrid;
