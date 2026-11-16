import { useEffect, useState } from 'react';
import { Link, Outlet, useLocation } from 'react-router-dom';
import { getItemTabLock, startItemTabLock } from './itemTabLock';

const LockedItemOutlet = ({ item }) => {
    const { lockName } = item;
    const [status, setStatus] = useState('checking');

    useEffect(() => {
        const reportError = (error) => {
            console.error('Failed to check whether item is open in another tab:', error);
            setStatus('error');
        };
        return startItemTabLock(lockName, globalThis.navigator?.locks, setStatus, reportError);
    }, [lockName]);

    if (status === 'acquired') return <Outlet />;

    return (
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
    );
};

const ItemTabGuard = () => {
    const { pathname } = useLocation();
    const item = getItemTabLock(pathname);
    return item ? <LockedItemOutlet key={item.lockName} item={item} /> : <Outlet />;
};

export default ItemTabGuard;
