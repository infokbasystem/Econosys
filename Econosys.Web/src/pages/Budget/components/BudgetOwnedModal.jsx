import { formatDateTime } from '../../../helpers/dateUtils';
import BudgetModal from './BudgetModal';

const BudgetOwnedModal = ({ isOpen, ownership, onTakeControl, onOpenReadOnly }) => (
    <BudgetModal
        isOpen={isOpen}
        title="BUDGET REDAN ÖPPEN"
        onClose={onOpenReadOnly}
        onConfirm={onTakeControl}
        confirmText="Ta kontroll"
        cancelText="Skrivskyddad"
    >
        <div className="space-y-1 text-center">
            <p className="text-gray-600">Användare</p>
            <p className="font-semibold">{ownership?.ownedByUserName || '-'}</p>
            <p>{ownership?.ownedDateTime ? formatDateTime(ownership.ownedDateTime) : '-'}</p>
        </div>
    </BudgetModal>
);

export default BudgetOwnedModal;
