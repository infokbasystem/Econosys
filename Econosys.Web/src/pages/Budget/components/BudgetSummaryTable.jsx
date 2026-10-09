import { formatNumber } from '../../../helpers/numberUtils';
import { MONTH_LABELS } from '../budgetCalc';

const numberCellClass = 'px-1 py-[3px] text-right tabular-nums';

const SummaryBlock = ({ title, rows, className, hasData = true }) => (
    <tbody className={className}>
        {rows.map((row, index) => (
            <tr key={row.key} className="h-5">
                <td className="px-2 py-[3px] font-semibold text-gray-800">
                    {index === 0 ? title : ''}
                </td>
                <td className="px-2 py-[3px] text-gray-700">{row.title}</td>
                {row.months.map((value, monthIndex) => (
                    <td key={monthIndex} className={numberCellClass}>
                        {formatNumber(hasData ? value : null, row.digits)}
                    </td>
                ))}
                <td className={`${numberCellClass} font-medium`}>
                    {formatNumber(hasData ? row.total : null, row.digits)}
                </td>
            </tr>
        ))}
    </tbody>
);

const BudgetSummaryTable = ({ statBlocks, budgetRows }) => (
    <div className="overflow-x-auto mb-8">
        <table className="w-full table-fixed border-collapse text-xs" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
            <colgroup>
                <col className="w-20" />
                <col className="w-28" />
                {MONTH_LABELS.map((label) => (
                    <col key={label} />
                ))}
                <col />
            </colgroup>
            <thead>
                <tr className="h-5 bg-lime-200/50 text-gray-900">
                    <th colSpan={2} />
                    {MONTH_LABELS.map((label) => (
                        <th key={label} className="px-1 py-[3px] text-right font-normal">{label}</th>
                    ))}
                    <th className="px-1 py-[3px] text-right font-normal">TOT</th>
                </tr>
            </thead>
            {statBlocks.map((block) => (
                <SummaryBlock
                    key={block.year}
                    title={String(block.year)}
                    rows={block.rows}
                    className="bg-lime-50"
                    hasData={block.hasData}
                />
            ))}
            <SummaryBlock title="Budget" rows={budgetRows} className="bg-slate-200/70" />
        </table>
    </div>
);

export default BudgetSummaryTable;
