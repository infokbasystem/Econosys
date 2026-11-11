import EntityOrderFlowNavigation from '../../components/EntityOrderFlowNavigation.jsx';

const CalculationNavigation = ({ calculation }) => {
    const id = calculation.id;
    const draftItems = [
        ...(calculation.product ? [{ type: 'product', ...calculation.product }] : []),
        { type: 'calculation', id: 0, supplierName: 'Ny kalkyl' },
    ];

    return (
        <EntityOrderFlowNavigation
            entityType="calculation"
            entityId={id}
            refreshKey={calculation}
            draftItems={draftItems}
        />
    );
};

export default CalculationNavigation;
