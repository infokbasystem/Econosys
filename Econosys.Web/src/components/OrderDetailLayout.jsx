import { PanelLeftClose, PanelLeftOpen } from 'lucide-react';

const EMPTY_MESSAGES = [];

const OrderDetailLayout = ({ title, messages = EMPTY_MESSAGES, isInfoPanelExpanded, isInfoPanelUsingResponsiveDefault = false, onToggleInfoPanel, infoContent, infoFooter, mainMaxWidthClass = 'max-w-350', children }) => {
    const gridColumnsClass = isInfoPanelUsingResponsiveDefault
        ? 'grid-cols-[0_minmax(0,1fr)] 2xl:grid-cols-[320px_minmax(0,1fr)]'
        : isInfoPanelExpanded
            ? 'grid-cols-[320px_minmax(0,1fr)]'
            : 'grid-cols-[0_minmax(0,1fr)]';
    const infoPanelVisibilityClass = isInfoPanelUsingResponsiveDefault
        ? 'pointer-events-none overflow-hidden mr-0 opacity-0 2xl:pointer-events-auto 2xl:relative 2xl:overflow-visible 2xl:opacity-100 2xl:after:absolute 2xl:after:top-8 2xl:after:right-0 2xl:after:bottom-0 2xl:after:border-r 2xl:after:border-gray-300'
        : isInfoPanelExpanded
            ? 'relative overflow-visible mr-0 opacity-100 after:absolute after:top-8 after:right-0 after:bottom-0 after:border-r after:border-gray-300'
            : 'pointer-events-none overflow-hidden mr-0 opacity-0';

    return (
        <div className="relative flex min-h-full flex-col">
            <div className={`grid min-h-full items-stretch transition-[grid-template-columns] duration-300 ease-in-out ${gridColumnsClass}`}>
                <div
                    id="order-detail-info-panel"
                    className={`min-w-0 transition-[opacity,margin] duration-300 ease-in-out ${infoPanelVisibilityClass}`}
                    aria-hidden={!isInfoPanelExpanded}
                >
                    <aside className="sticky top-[calc(66px+1rem)] z-10 max-h-[calc(100vh-66px-1rem)] overflow-y-auto px-2 py-6">
                        {infoContent}
                        <hr className="mt-5 border-gray-300" />
                        <h2 className="mt-5 text-center text-sm text-gray-700">Meddelanden</h2>
                        {messages.length === 0 ? (
                            <p className="mt-4 text-center text-xs font-light">Inga meddelanden</p>
                        ) : (
                            <ul className="mt-2 space-y-2">
                                {messages.map((message, index) => (
                                    <li
                                        key={`${message.type ?? 'info'}-${index}`}
                                        className={`rounded border border-gray-200 p-2 text-center text-xs ${message.type === 'error'
                                            ? 'bg-red-100 text-red-700'
                                            : message.type === 'warning'
                                                ? 'bg-yellow-100 text-yellow-800'
                                                : 'bg-green-100 text-green-700'
                                            }`}
                                    >
                                        {String(message.text ?? '')}
                                    </li>
                                ))}
                            </ul>
                        )}
                        {infoFooter}
                    </aside>
                </div>

                <main className={`min-w-0 flex-grow ps-10 pe-10 py-2 ${mainMaxWidthClass}`}>
                    <div className="pb-1 flex items-center gap-3">
                        <button
                            type="button"
                            onClick={onToggleInfoPanel}
                            className="inline-flex h-8 w-8 items-center justify-center text-gray-600 hover:bg-gray-50"
                            title={isInfoPanelExpanded ? 'Dolj informationspanel' : 'Visa informationspanel'}
                            aria-label={isInfoPanelExpanded ? 'Dolj informationspanel' : 'Visa informationspanel'}
                            aria-expanded={isInfoPanelExpanded}
                            aria-controls="order-detail-info-panel"
                        >
                            {isInfoPanelExpanded ? <PanelLeftClose size={16} /> : <PanelLeftOpen size={16} />}
                        </button>
                        <h2 className="text-sm text-gray-500 tracking-[0.10em] font-semibold uppercase">
                            {title}
                        </h2>
                    </div>
                    {children}
                </main>
            </div>
        </div>
    );
};

export default OrderDetailLayout;
