import { useEffect, useState } from 'react';
import { Link, Outlet, useLocation } from 'react-router-dom';
import { getItemTabLock, startItemTabLock } from './itemTabLock';

const LockedItemOutlet = ({ item, newlyCreatedInvoiceId }) => {
    const lockName = item?.lockName ?? null;
    const [lockState, setLockState] = useState({
        lockName,
        status: 'checking',
        preserveOutlet: lockName === `econosys:item:finance:invoice:${newlyCreatedInvoiceId}`,
    });

    if (lockState.lockName !== lockName) {
        setLockState({
            lockName,
            status: 'checking',
            preserveOutlet: lockName === `econosys:item:finance:invoice:${newlyCreatedInvoiceId}`,
        });
    }

    const { status, preserveOutlet } = lockState;

    useEffect(() => {
        if (!lockName) return;

        const reportStatus = (nextStatus) => {
            setLockState((current) => ({ ...current, status: nextStatus }));
        };
        const reportError = (error) => {
            console.error('Failed to check whether item is open in another tab:', error);
            reportStatus('error');
        };
        return startItemTabLock(lockName, globalThis.navigator?.locks, reportStatus, reportError);
    }, [lockName]);

    const showOutlet = !item || status === 'acquired' || (preserveOutlet && status === 'checking');

    return (
        <>
            <div className="contents" inert={Boolean(item && status !== 'acquired')}>
                {showOutlet && <Outlet />}
            </div>
            {!showOutlet && (
                <main className="flex min-h-[50vh] flex-col items-center justify-center px-6 text-center">
                    {status === 'duplicate' ? (
                        <>
                            <h1 className="text-lg font-semibold text-gray-800">Posten är redan öppen i en annan flik</h1>
                            <p className="mt-2 max-w-lg text-sm text-gray-600">
                                För att undvika att ändringar skrivs över kan posten bara redigeras i en flik åt gången.
                                Gå till den andra fliken eller stäng den innan du försöker igen.
                            </p>
                            <Link to={item.listPath} className="mt-4 text-sm text-sky-700 hover:underline">
                                Tillbaka till listan
                            </Link>
                        </>
                    ) : status === 'error' || status === 'unsupported' ? (
                        <p role="alert" className="max-w-lg text-sm text-red-700">
                            {status === 'unsupported'
                                ? 'Din webbläsare kan inte kontrollera om posten är öppen i en annan flik. Använd en webbläsare som stöder fliklåsning.'
                                : 'Det gick inte att kontrollera om posten är öppen i en annan flik. Ladda om sidan och försök igen.'}
                        </p>
                    ) : (
                        <p role="status" className="text-sm text-gray-600">Kontrollerar om posten är öppen i en annan flik...</p>
                    )}
                </main>
            )}
        </>
    );
};

const ItemTabGuard = () => {
    const { pathname, state } = useLocation();
    const item = getItemTabLock(pathname);
    return <LockedItemOutlet item={item} newlyCreatedInvoiceId={state?.newlyCreatedInvoiceId} />;
};

export default ItemTabGuard;
