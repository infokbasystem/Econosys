const guards = new Set();

export const registerAppUpdateGuard = (guard) => {
    guards.add(guard);
    return () => guards.delete(guard);
};

export const isAppUpdateBlocked = () => {
    for (const guard of guards) {
        if (guard()) return true;
    }
    return false;
};

export const isEditingControl = (element) => Boolean(element && (
    element.isContentEditable
    || element.matches?.('input:not([readonly]):not([disabled]), textarea:not([readonly]):not([disabled]), select:not([disabled])')
));
