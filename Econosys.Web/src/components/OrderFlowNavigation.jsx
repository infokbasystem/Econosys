import { useId, useState } from 'react';
import { Link } from 'react-router-dom';
import { getOrderFlowSections } from './orderFlowNavigationSections.js';

const FlowRow = ({ item, section, isCurrent }) => {
    const number = item.number || String(item.id);
    const text = item.type === 'product'
        ? item.name || String(item.id)
        : [item.type !== 'calculation' ? number : '', item.supplierName, item.customerName, item.displayDate]
            .filter(Boolean).join(' · ');
    const hasSupplier = ['inquiry', 'supplierOrder'].includes(item.type);
    const content = item.type === 'product' ? text : (
        <span className={`grid items-baseline gap-x-2 ${item.type === 'calculation'
            ? 'grid-cols-[minmax(0,1fr)_minmax(0,1fr)_42px]'
            : hasSupplier
                ? 'grid-cols-[44px_minmax(0,1fr)_48px_42px]'
                : 'grid-cols-[44px_minmax(0,1fr)_42px]'}`}>
            {item.type !== 'calculation' && <span className="truncate">{number}</span>}
            {(hasSupplier || item.type === 'calculation') && <span className="truncate">{item.supplierName}</span>}
            <span className="truncate">{item.customerName}</span>
            <span className="text-right tabular-nums">{item.displayDate}</span>
        </span>
    );
    const className = 'block min-w-0 truncate rounded-sm px-1 py-1 text-xs text-gray-700';

    return item.id > 0 ? (
        <Link
            to={`/order/${section.path}/${item.id}`}
            aria-current={isCurrent ? 'page' : undefined}
            aria-label={text}
            title={text}
            className={`${className} hover:bg-gray-100 focus-visible:outline-2 focus-visible:outline-lime-700`}
        >
            {content}
        </Link>
    ) : <span className={className}>{content}</span>;
};

const FlowSection = ({ section, currentEntityType, currentEntityId }) => {
    const [showAll, setShowAll] = useState(false);
    const headingId = useId();
    const listId = useId();
    const visibleItems = showAll || !section.limit ? section.items : section.items.slice(0, section.limit);

    return (
        <section aria-labelledby={headingId}>
            <h3 id={headingId} className={`px-1 pb-1 text-xs font-medium tracking-[0.12em] ${section.type === currentEntityType ? 'text-red-600' : 'text-gray-500'}`}>
                {section.label}
            </h3>
            <ul id={listId}>
                {visibleItems.map((item) => (
                    <li key={item.id}>
                        <FlowRow item={item} section={section} isCurrent={section.type === currentEntityType && item.id === currentEntityId} />
                    </li>
                ))}
            </ul>
            {section.limit && section.items.length > section.limit && (
                <button
                    type="button"
                    className="rounded-sm px-1 py-1 text-xs text-red-600 hover:underline focus-visible:outline-2 focus-visible:outline-lime-700"
                    aria-expanded={showAll}
                    aria-controls={listId}
                    onClick={() => setShowAll((value) => !value)}
                >
                    {showAll ? 'Visa färre' : 'Visa fler'}
                </button>
            )}
        </section>
    );
};

const OrderFlowNavigation = ({ items, currentEntityType, currentEntityId }) => (
    <nav aria-label="Orderflöde" className="space-y-4">
        {getOrderFlowSections(items, currentEntityType).map((section) => (
            <FlowSection
                key={`${currentEntityType}-${currentEntityId}-${section.type}`}
                section={section}
                currentEntityType={currentEntityType}
                currentEntityId={currentEntityId}
            />
        ))}
    </nav>
);

export default OrderFlowNavigation;
