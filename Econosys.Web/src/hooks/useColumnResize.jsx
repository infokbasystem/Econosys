import { useRef, useState } from 'react';
import { MIN_COLUMN_WIDTH, resizeColumn } from '../helpers/columnResize';

const useColumnResize = () => {
    const tableRef = useRef(null);
    const resizeRef = useRef(null);
    const [widths, setWidths] = useState(null);

    const getWidths = () => (
        widths ?? Array.from(tableRef.current.querySelectorAll('thead th'), (header) => header.getBoundingClientRect().width)
    );

    const handleStart = (event, index) => {
        event.stopPropagation();
        if (event.button !== 0) return;
        event.preventDefault();
        const initialWidths = getWidths();
        resizeRef.current = { index, startX: event.clientX, widths: initialWidths, pointerId: event.pointerId };
        setWidths(initialWidths);
        event.currentTarget.setPointerCapture(event.pointerId);
    };

    const handleMove = (event) => {
        const resize = resizeRef.current;
        if (!resize || resize.pointerId !== event.pointerId) return;
        setWidths(resizeColumn(resize.widths, resize.index, resize.widths[resize.index] + event.clientX - resize.startX));
    };

    const handleEnd = (event) => {
        if (resizeRef.current?.pointerId !== event.pointerId) return;
        resizeRef.current = null;
        if (event.currentTarget.hasPointerCapture(event.pointerId)) {
            event.currentTarget.releasePointerCapture(event.pointerId);
        }
    };

    const handleKeyDown = (event, index) => {
        if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
        event.preventDefault();
        event.stopPropagation();
        const adjustment = event.key === 'ArrowRight' ? 10 : -10;
        const currentWidths = getWidths();
        setWidths(resizeColumn(currentWidths, index, currentWidths[index] + adjustment));
    };

    const handle = (index, label) => (
        <span
            role="separator"
            aria-orientation="vertical"
            aria-label={`Ändra kolumnbredd: ${label}`}
            aria-valuemin={Math.min(MIN_COLUMN_WIDTH, widths?.[index] ?? MIN_COLUMN_WIDTH)}
            aria-valuenow={widths ? Math.round(widths[index]) : undefined}
            tabIndex={0}
            title="Dra för att ändra kolumnbredd. Använd vänster/höger piltangent."
            className="absolute right-0 top-1 bottom-2 z-10 w-2 cursor-col-resize touch-none select-none border-r border-gray-300 hover:border-lime-600 hover:bg-lime-100 focus:bg-lime-100 focus:outline-none"
            onClick={(event) => event.stopPropagation()}
            onPointerDown={(event) => handleStart(event, index)}
            onPointerMove={handleMove}
            onPointerUp={handleEnd}
            onPointerCancel={handleEnd}
            onLostPointerCapture={() => { resizeRef.current = null; }}
            onKeyDown={(event) => handleKeyDown(event, index)}
        />
    );

    return {
        tableRef,
        widths,
        tableWidth: widths?.reduce((total, width) => total + width, 0),
        handle,
    };
};

export default useColumnResize;
