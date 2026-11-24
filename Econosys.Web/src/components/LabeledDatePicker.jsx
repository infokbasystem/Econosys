import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { CalendarDays, X } from 'lucide-react';
import { DayPicker } from '@daypicker/react';
import { sv } from 'date-fns/locale';

import { formatDateShort, fromDateInputToSwedishIso, toSwedishDateInputValue } from '../helpers/dateUtils';

const parsePickerDate = (value) => {
    const datePart = toSwedishDateInputValue(value);
    if (!datePart) {
        return undefined;
    }

    const [year, month, day] = datePart.split('-').map(Number);
    if (!year || !month || !day) {
        return undefined;
    }

    return new Date(year, month - 1, day);
};

const toWeekLabelValue = (value) => {
    const date = parsePickerDate(value);
    if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
        return '';
    }

    const utcDate = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
    utcDate.setUTCDate(utcDate.getUTCDate() + 4 - (utcDate.getUTCDay() || 7));
    const weekYear = utcDate.getUTCFullYear();
    const yearStart = new Date(Date.UTC(weekYear, 0, 1));
    const week = Math.ceil((((utcDate - yearStart) / 86400000) + 1) / 7);
    return `v. ${String(week).padStart(2, '0')} ${weekYear}`;
};

const formatOutputValue = (date, valueType) => {
    if (!date) {
        return null;
    }

    const datePart = toSwedishDateInputValue(date);
    if (!datePart) {
        return null;
    }

    if (valueType === 'date') {
        return date;
    }

    if (valueType === 'input') {
        return datePart;
    }

    return fromDateInputToSwedishIso(datePart);
};

const LabeledDatePicker = ({
    label,
    labelWidth,
    inputWidth,
    inputAriaLabel,
    portal = false,
    margintop,
    name,
    value,
    onChange,
    disabled = false,
    placeholder = 'Välj datum',
    valueType = 'iso',
    weekMode = false,
    onWeekModeChange,
    clearable = true,
    enableWeekSelect = false,
    showWeekNumber = true,
    ISOWeek = true,
    weekStartsOn = 1,
    theme = 'default',
    ...props
}) => {
    const wrapperRef = useRef(null);
    const inputRef = useRef(null);
    const popupRef = useRef(null);
    const [isOpen, setIsOpen] = useState(false);
    const isFilterTheme = theme === 'filter';

    const selectedDate = useMemo(() => parsePickerDate(value), [value]);
    const hiddenValue = useMemo(() => {
        const nextValue = formatOutputValue(selectedDate, valueType);
        return typeof nextValue === 'string' ? nextValue : '';
    }, [selectedDate, valueType]);

    const dayPickerComponents = useMemo(
        () => ({
            WeekNumber: ({ children }) => (
                <span className="mt-3 mr-2 inline-flex h-[18px] min-w-[40px] items-center justify-center rounded-md px-1 text-xs leading-none text-gray-500">
                    {`v. ${children}`}
                </span>
            ),
            ...(props.components || {}),
        }),
        [props.components],
    );

    useEffect(() => {
        const handleOutside = (event) => {
            if (wrapperRef.current && !wrapperRef.current.contains(event.target) && !popupRef.current?.contains(event.target)) {
                setIsOpen(false);
            }
        };

        document.addEventListener('mousedown', handleOutside);
        return () => document.removeEventListener('mousedown', handleOutside);
    }, []);

    useLayoutEffect(() => {
        if (!portal || !isOpen || disabled) return;

        const anchor = inputRef.current;
        const popup = popupRef.current;
        if (!anchor || !popup) return;

        const updatePosition = () => {
            const rect = anchor.getBoundingClientRect();
            const margin = 8;
            const gap = 4;
            const popupRect = popup.getBoundingClientRect();
            const viewportWidth = document.documentElement.clientWidth;
            const viewportHeight = window.innerHeight;
            const left = Math.max(margin, Math.min(rect.left, viewportWidth - popupRect.width - margin));
            const preferredTop = rect.bottom + gap + popupRect.height <= viewportHeight - margin
                ? rect.bottom + gap
                : rect.top - gap - popupRect.height;
            const top = Math.max(margin, Math.min(preferredTop, viewportHeight - popupRect.height - margin));

            Object.assign(popup.style, { left: `${left}px`, top: `${top}px`, visibility: 'visible' });
        };

        updatePosition();
        window.addEventListener('resize', updatePosition);
        window.addEventListener('scroll', updatePosition, true);
        const observer = new ResizeObserver(updatePosition);
        observer.observe(anchor);
        observer.observe(popup);

        return () => {
            window.removeEventListener('resize', updatePosition);
            window.removeEventListener('scroll', updatePosition, true);
            observer.disconnect();
        };
    }, [portal, isOpen, disabled]);

    const handleSelect = (date) => {
        onWeekModeChange?.(false);
        onChange?.(formatOutputValue(date, valueType));
        setIsOpen(false);
    };

    const handleWeekSelect = (week) => {
        const weekDate = Array.isArray(week?.days)
            ? week.days.map((day) => day?.date).find((date) => date instanceof Date)
            : undefined;

        if (!weekDate) {
            return;
        }

        onWeekModeChange?.(true);
        onChange?.(formatOutputValue(weekDate, valueType));
        setIsOpen(false);
    };

    const handleClear = (event) => {
        event.stopPropagation();
        onWeekModeChange?.(false);
        onChange?.(null);
        setIsOpen(false);
    };

    const selectedLabel = weekMode
        ? toWeekLabelValue(selectedDate)
        : formatDateShort(selectedDate);

    const popup = (
        <div
            ref={popupRef}
            className={`daypicker ${portal ? 'fixed z-50 w-max max-h-[calc(100vh-16px)] max-w-[calc(100vw-16px)] overflow-auto' : 'absolute left-0 top-full z-30 mt-1'} rounded-sm border border-gray-300 bg-white p-2 px-5 shadow-lg`}
            style={portal ? { visibility: 'hidden' } : undefined}
        >
            <DayPicker
                animate
                mode="single"
                locale={sv}
                selected={selectedDate}
                onSelect={handleSelect}
                showWeekNumber={showWeekNumber}
                ISOWeek={ISOWeek}
                weekStartsOn={weekStartsOn}
                className="text-xs"
                components={{
                    ...dayPickerComponents,
                    WeekNumber: ({ week, children }) => (
                        enableWeekSelect ? (
                            <button
                                type="button"
                                className="mt-3 mr-2 inline-flex h-[18px] min-w-[40px] items-center justify-center rounded-md px-1 text-xs leading-none text-blue-700 hover:bg-blue-50"
                                title={`Välj vecka ${week?.weekNumber ?? ''}`}
                                onClick={(event) => {
                                    event.preventDefault();
                                    event.stopPropagation();
                                    handleWeekSelect(week);
                                }}
                            >
                                <span>{`v. ${children}`}</span>
                            </button>
                        ) : (
                            <span className="mt-3 mr-2 inline-flex h-[18px] min-w-[40px] items-center justify-center rounded-md px-1 text-xs leading-none text-gray-500">
                                {`v. ${children}`}
                            </span>
                        )
                    ),
                }}
                {...props}
            />
        </div>
    );

    return (
        <div className={`flex items-center w-full pb-[1px] mt-${margintop}`} ref={wrapperRef}>
            {label && <label className={`${labelWidth || ''} flex-none text-xs text-gray-700 pt-0.5`}>{label}</label>}

            <div className={`relative ${inputWidth || 'w-full'}`} ref={inputRef}>
                {name && <input type="hidden" name={name} value={hiddenValue} />}

                <button
                    type="button"
                    onClick={() => !disabled && setIsOpen((current) => !current)}
                    disabled={disabled}
                    className={[
                        'flex w-full items-center gap-2 border text-left text-xs focus:outline-none',
                        isFilterTheme
                            ? 'h-7 rounded-full border-lime-600 px-3 text-gray-700 hover:border-lime-700 focus:border-lime-700'
                            : 'h-6 rounded-sm border-gray-300 px-2 pt-0.5 text-gray-700',
                        disabled ? 'bg-transparent text-gray-500' : 'bg-white',
                    ].join(' ')}
                    aria-label={inputAriaLabel}
                    aria-expanded={isOpen}
                >
                    <CalendarDays size={14} className={`shrink-0 text-gray-500 ${isFilterTheme ? '' : 'pb-0.5'}`} />
                    <span className={`flex-1 truncate ${selectedDate ? 'text-gray-800' : 'text-gray-400'}`}>
                        {selectedDate ? selectedLabel : placeholder}
                    </span>
                    {clearable && selectedDate && !disabled && (
                        <span
                            role="button"
                            tabIndex={-1}
                            onClick={handleClear}
                            className="flex h-4 w-4 items-center justify-center rounded-full text-gray-400 hover:bg-gray-100 hover:text-gray-600"
                        >
                            <X size={12} />
                        </span>
                    )}
                </button>

                {isOpen && !disabled && (portal ? createPortal(popup, document.body) : popup)}
            </div>
        </div>
    );
};

export default LabeledDatePicker;