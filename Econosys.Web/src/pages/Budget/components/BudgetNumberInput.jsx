import { useState } from 'react';

import { formatNumber, parseNullableNumber } from '../../../helpers/numberUtils';

const BudgetNumberInput = ({
    value,
    onCommit,
    digits = 0,
    disabled = false,
    className = '',
}) => {
    const [draft, setDraft] = useState(null);
    const isEditing = draft !== null;

    const commit = () => {
        if (!isEditing) {
            return;
        }

        const parsed = parseNullableNumber(draft);
        setDraft(null);

        if (parsed !== value) {
            onCommit?.(parsed);
        }
    };

    const handleKeyDown = (event) => {
        if (event.key === 'Enter') {
            event.currentTarget.blur();
        }

        if (event.key === 'Escape') {
            setDraft(null);
        }
    };

    return (
        <input
            type="text"
            inputMode="decimal"
            value={isEditing ? draft : formatNumber(value, digits)}
            disabled={disabled}
            onFocus={(event) => {
                setDraft(value == null ? '' : String(Math.round(value * 10 ** digits) / 10 ** digits).replace('.', ','));
                event.target.select();
            }}
            onChange={(event) => setDraft(event.target.value)}
            onBlur={commit}
            onKeyDown={handleKeyDown}
            className={`h-6 w-full rounded-sm border border-gray-300 px-1 text-center text-xs focus:outline-none focus:ring-1 focus:ring-lime-500 disabled:border-gray-200 disabled:bg-transparent disabled:text-gray-600 ${disabled ? '' : 'bg-white'} ${className}`}
        />
    );
};

export default BudgetNumberInput;
