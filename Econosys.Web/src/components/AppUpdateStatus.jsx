/* global __APP_BUILD_ID__ */
import { useEffect, useState } from 'react';
import { startAppUpdateChecker } from '../helpers/appUpdateChecker';
import { isAppUpdateBlocked, isEditingControl } from '../helpers/appUpdateGuards';
import './AppUpdateStatus.css';

const buildId = __APP_BUILD_ID__;

export default function AppUpdateStatus() {
    const [status, setStatus] = useState('current');

    useEffect(() => {
        if (!import.meta.env.PROD || import.meta.env.MODE !== 'production') return;
        return startAppUpdateChecker({
            buildId,
            versionUrl: `${import.meta.env.BASE_URL}version.json`,
            isBlocked: () => isAppUpdateBlocked() || isEditingControl(document.activeElement),
            onStatus: setStatus,
        });
    }, []);

    return (
        <div className="app-update-status">
            {status === 'pending' && (
                <span role="status" className="app-update-message">
                    Ny version finns. Uppdateras automatiskt när du har sparat och stängt öppna dialoger.
                </span>
            )}
            {status === 'reload-blocked' && (
                <span role="status" className="app-update-message">
                    Ny version kunde inte laddas automatiskt. Spara ditt arbete och ladda om sidan.
                </span>
            )}
            <span title={`Build ID: ${buildId}${status === 'error' ? ' — Uppdateringskontrollen misslyckades. Försöker igen automatiskt.' : ''}`}>
                {buildId}
            </span>
        </div>
    );
}
