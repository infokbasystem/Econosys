import { useState } from 'react';

import LabeledInput from '../../../components/LabeledInput';
import { formatNumber } from '../../../helpers/numberUtils';
import { MONTH_LABELS, normalizeMonths } from '../budgetCalc';
import BudgetModal from './BudgetModal';

const MONTH_NAMES = [
    'Januari', 'Februari', 'Mars', 'April', 'Maj', 'Juni',
    'Juli', 'Augusti', 'September', 'Oktober', 'November', 'December',
];

const BudgetMonthDistributionModal = ({ isOpen, distributionMonths, onClose, onConfirm }) => {
    const [values, setValues] = useState(() => normalizeMonths(distributionMonths));

    const total = values.reduce((acc, value) => acc + (Number(value) || 0), 0);
    const isValid = Math.abs(total - 100) < 0.000001;

    return (
        <BudgetModal
            isOpen={isOpen}
            title="MÅNADSFÖRDELNING"
            onClose={onClose}
            onConfirm={() => onConfirm(values)}
            confirmDisabled={!isValid}
        >
            <p className="mb-4 text-center text-gray-600">
                Fördelningen appliceras på alla kunders budget och påslag.
            </p>
            <div className="grid grid-cols-2 gap-x-8 gap-y-1">
                {MONTH_LABELS.map((label, index) => (
                    <LabeledInput
                        key={label}
                        label={MONTH_NAMES[index]}
                        labelWidth="w-24"
                        type="number"
                        value={values[index]}
                        onChange={(value) => setValues((prev) => prev.map((item, itemIndex) => (
                            itemIndex === index ? value : item
                        )))}
                    />
                ))}
            </div>
            <div className={`mt-4 text-right font-semibold ${isValid ? 'text-gray-700' : 'text-rose-700'}`}>
                Summa: {formatNumber(total, 2)} %
                {!isValid && <span className="ml-2 font-normal">(måste vara 100)</span>}
            </div>
        </BudgetModal>
    );
};

export default BudgetMonthDistributionModal;
