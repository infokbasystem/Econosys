import { useState } from 'react';

import LabeledDatePicker from '../../../components/LabeledDatePicker';
import LabeledSelect from '../../../components/LabeledSelect';
import BudgetModal from './BudgetModal';

const AllocateBudgetCustomerModal = ({ isOpen, customerName, employees, onClose, onConfirm }) => {
    const [employeeId, setEmployeeId] = useState('');
    const [allocateFromDate, setAllocateFromDate] = useState(null);

    const employeeItems = [
        { id: '', name: '' },
        ...employees.map((employee) => ({ id: employee.id, name: employee.name })),
    ];

    return (
        <BudgetModal
            isOpen={isOpen}
            title="ALLOKERA OM KUND"
            onClose={onClose}
            onConfirm={() => onConfirm({ employeeId: Number(employeeId), allocateFromDate })}
            confirmDisabled={!employeeId || !allocateFromDate}
        >
            <p className="mb-4 text-center font-semibold">{customerName}</p>
            <div className="space-y-2">
                <LabeledSelect
                    label="Ny säljare"
                    labelWidth="w-24"
                    value={employeeId}
                    items={employeeItems}
                    onChange={setEmployeeId}
                />
                <LabeledDatePicker
                    label="Från datum"
                    labelWidth="w-24"
                    value={allocateFromDate}
                    valueType="input"
                    onChange={setAllocateFromDate}
                />
            </div>
            <p className="mt-4 text-gray-500">
                Kunden flyttas till den nya säljaren från och med valt datum.
            </p>
        </BudgetModal>
    );
};

export default AllocateBudgetCustomerModal;
