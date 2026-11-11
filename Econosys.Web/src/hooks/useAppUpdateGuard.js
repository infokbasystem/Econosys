import { useLayoutEffect } from 'react';
import { registerAppUpdateGuard } from '../helpers/appUpdateGuards';

export default function useAppUpdateGuard(blocked) {
    useLayoutEffect(() => registerAppUpdateGuard(
        typeof blocked === 'function' ? blocked : () => blocked,
    ), [blocked]);
}
