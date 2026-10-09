import LabeledCheckbox from '../../../components/LabeledCheckbox';
import LabeledInput from '../../../components/LabeledInput';
import LabeledSwitch from '../../../components/LabeledSwitch';
import { parseNullableInt } from '../../../helpers/numberUtils';

const BudgetSidebar = ({
    header,
    onHeaderChange,
    employees,
    selectedEmployeeIds,
    onToggleEmployee,
    onSelectOnlyEmployee,
    onSelectAllEmployees,
    onSelectNoEmployees,
    disabled,
    lockDisabled,
}) => {
    const selected = new Set(selectedEmployeeIds);

    return (
        <div className="w-60 shrink-0 space-y-5">
            <div className="space-y-1">
                <LabeledInput
                    label="Namn"
                    labelWidth="w-14"
                    value={header.name ?? ''}
                    maxLength={50}
                    disabled={disabled}
                    onChange={(value) => onHeaderChange({ name: value })}
                />
                <LabeledInput
                    label="År"
                    labelWidth="w-14"
                    value={header.year != null ? String(header.year) : ''}
                    maxLength={4}
                    disabled={disabled}
                    onChange={(value) => onHeaderChange({ year: parseNullableInt(value.replace(/\D/g, '')) })}
                />
                <LabeledSwitch
                    name="budgetIsActive"
                    label="Aktiv"
                    labelWidth="w-14"
                    value={Boolean(header.isActive)}
                    disabled={disabled}
                    onChange={(rowId, field, checked) => onHeaderChange({ isActive: checked })}
                />
                <LabeledSwitch
                    name="budgetIsLocked"
                    label="Låst"
                    labelWidth="w-14"
                    value={Boolean(header.isLocked)}
                    disabled={lockDisabled}
                    onChange={(rowId, field, checked) => onHeaderChange({ isLocked: checked })}
                />
            </div>

            <div>
                <div className="flex items-center gap-3 border-b border-gray-300 pb-1 text-xs text-gray-600">
                    <span>Välj:</span>
                    <button
                        type="button"
                        onClick={onSelectNoEmployees}
                        className="tracking-wide hover:text-gray-900 hover:underline"
                    >
                        INGEN
                    </button>
                    <button
                        type="button"
                        onClick={onSelectAllEmployees}
                        className="tracking-wide hover:text-gray-900 hover:underline"
                    >
                        ALLA
                    </button>
                </div>
                <div className="mt-1 space-y-0.5">
                    {employees.map((employee) => (
                        <div key={employee.id} className="flex items-center gap-2">
                            <LabeledCheckbox
                                name={`budget-employee-${employee.id}`}
                                checked={selected.has(employee.id)}
                                onChange={() => onToggleEmployee(employee.id)}
                                ariaLabel={`Visa ${employee.name}`}
                            />
                            <button
                                type="button"
                                onClick={() => onSelectOnlyEmployee(employee.id)}
                                className="truncate text-left text-xs text-gray-700 hover:text-gray-900 hover:underline"
                                title="Visa endast denna säljare"
                            >
                                {employee.name}
                            </button>
                        </div>
                    ))}
                    {employees.length === 0 && (
                        <p className="text-xs font-light text-gray-500">Inga säljare i budget</p>
                    )}
                </div>
            </div>
        </div>
    );
};

export default BudgetSidebar;
