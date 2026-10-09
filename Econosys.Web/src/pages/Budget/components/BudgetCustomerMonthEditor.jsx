import { formatNumber } from '../../../helpers/numberUtils';
import {
    MONTH_LABELS,
    getMonthTbs,
    getRowDistribution,
    markupFromTb,
    normalizeMonths,
} from '../budgetCalc';
import BudgetNumberInput from './BudgetNumberInput';

const sum = (values) => values.reduce((acc, value) => acc + (Number(value) || 0), 0);

const BudgetCustomerMonthEditor = ({ row, disabled, onMonthChange, onHide }) => {
    const distribution = getRowDistribution(row);
    const salesMonths = normalizeMonths(row.salesMonths);
    const additionMonths = normalizeMonths(row.additionMonths);
    const tbMonths = getMonthTbs(row);
    const salesTotal = Number(row.totalSalesBudget) || 0;
    const tbTotal = sum(tbMonths);
    const distributionTotal = sum(distribution);
    const isDistributionInvalid = Math.round(distributionTotal * 100) / 100 !== 100 && Math.round(distributionTotal * 100) / 100 !== 0;
    const isRowDisabled = disabled || !row.isEditable;

    const editorRows = [
        { key: 'distribution', title: 'Förd.', digits: 2, values: distribution, total: distributionTotal, editable: true },
        { key: 'sales', title: 'Oms.', digits: 0, values: salesMonths, total: salesTotal, editable: true },
        { key: 'tb', title: 'TB', digits: 0, values: tbMonths, total: tbTotal, editable: false },
        {
            key: 'addition',
            title: 'Påslag',
            digits: 1,
            values: additionMonths,
            total: salesTotal !== 0 ? markupFromTb(salesTotal, tbTotal) : null,
            editable: true,
        },
    ];

    return (
        <table className="w-full min-w-[1180px] table-fixed border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
            <colgroup>
                <col className="w-48" />
                {MONTH_LABELS.map((label) => (
                    <col key={label} />
                ))}
                <col />
            </colgroup>
            <thead>
                <tr className="text-gray-600">
                    <th className="px-2 py-1 text-left font-semibold text-gray-800">
                        <div className="flex items-center gap-3 whitespace-nowrap">
                            <span>{row.customerName}</span>
                            <button
                                type="button"
                                onClick={onHide}
                                className="font-normal text-gray-600 underline hover:text-gray-900"
                            >
                                Dölj
                            </button>
                        </div>
                    </th>
                    {MONTH_LABELS.map((label) => (
                        <th key={label} className="px-1 py-1 text-center font-medium">{label}</th>
                    ))}
                    <th className="px-1 py-1 text-center font-medium">TOT</th>
                </tr>
            </thead>
            <tbody>
                {editorRows.map((editorRow) => (
                    <tr key={editorRow.key}>
                        <td className="px-2 py-[2px] text-gray-700">{editorRow.title}</td>
                        {editorRow.values.map((value, monthIndex) => (
                            <td key={monthIndex} className="px-[1px] py-[0.5px]">
                                <BudgetNumberInput
                                    value={value}
                                    digits={editorRow.digits}
                                    disabled={isRowDisabled || !editorRow.editable}
                                    onCommit={(nextValue) => onMonthChange(editorRow.key, monthIndex, nextValue)}
                                />
                            </td>
                        ))}
                        <td className="px-[1px] py-[0.5px]">
                            <div
                                className={`flex h-6 items-center justify-center rounded-sm text-xs tabular-nums ${
                                    editorRow.key === 'distribution' && isDistributionInvalid
                                        ? 'bg-rose-100 font-semibold text-rose-700'
                                        : 'text-gray-800'
                                }`}
                            >
                                {formatNumber(editorRow.total, editorRow.digits)}
                            </div>
                        </td>
                    </tr>
                ))}
            </tbody>
        </table>
    );
};

export default BudgetCustomerMonthEditor;
