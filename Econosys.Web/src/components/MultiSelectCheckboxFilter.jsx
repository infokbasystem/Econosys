import { useEffect, useRef, useState } from 'react';

const SmallStyledCheckbox = ({ checked }) => {
    const indicatorStyle = checked
        ? { backgroundColor: '#14b8a6', borderColor: '#14b8a6', color: '#ffffff' }
        : { backgroundColor: '#ffffff', borderColor: '#aaaaaa', color: '#4b5563' };

    return (
        <span className="inline-flex h-4 w-4 shrink-0 items-center justify-center rounded-full border border-[#e5e7eb] bg-white p-[1px]">
            <span className="inline-flex h-full w-full items-center justify-center rounded-full border transition-all" style={indicatorStyle}>
                <svg viewBox="0 0 16 16" aria-hidden="true" className={`h-2.5 w-2.5 transition-opacity ${checked ? 'opacity-100' : 'opacity-0'}`}>
                    <path d="M3 8.5 6.3 11.6 13 4.9" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
            </span>
        </span>
    );
};

const MultiSelectCheckboxFilter = ({ options, selectedValues, onChange, label, disabled = false, menuAlign = 'left' }) => {
    const [open, setOpen] = useState(false);
    const rootRef = useRef(null);
    const allSelected = options.length > 0 && options.every((option) => selectedValues.includes(option.value));
    const selectionLabel = allSelected
        ? 'Välj alla'
        : selectedValues.length > 0
            ? `${selectedValues.length} valda`
            : '';

    useEffect(() => {
        const handleClickOutside = (event) => {
            if (rootRef.current && !rootRef.current.contains(event.target)) {
                setOpen(false);
            }
        };

        document.addEventListener('mousedown', handleClickOutside);
        return () => document.removeEventListener('mousedown', handleClickOutside);
    }, []);

    const toggleOption = (value) => {
        onChange(
            selectedValues.includes(value)
                ? selectedValues.filter((selectedValue) => selectedValue !== value)
                : [...selectedValues, value],
        );
    };

    return (
        <div className="relative mt-1" ref={rootRef} onClick={(event) => event.stopPropagation()}>
            <button
                type="button"
                onClick={() => setOpen((previous) => !previous)}
                disabled={disabled}
                aria-label={label}
                aria-expanded={open}
                className="flex h-5 w-full items-center justify-between rounded-sm border border-gray-300 bg-white px-1.5 text-left text-tiny font-normal text-gray-700 focus:border-slate-400 focus:outline-none disabled:opacity-50"
                title={selectionLabel || 'Välj'}
            >
                <span className={`truncate ${selectionLabel ? 'text-gray-700' : 'text-gray-400'}`}>{selectionLabel || 'Välj'}</span>
                <span className="text-tiny text-gray-500">▾</span>
            </button>

            {open ? (
                <div className={`absolute ${menuAlign === 'right' ? 'right-0' : 'left-0'} top-[26px] z-50 max-h-72 w-max min-w-full max-w-[calc(100vw-2rem)] overflow-y-auto rounded-sm border border-gray-300 bg-white p-1.5 shadow-lg`}>
                    <button
                        type="button"
                        role="checkbox"
                        aria-checked={allSelected}
                        className="mb-0.5 flex w-full cursor-pointer items-center gap-2 rounded-sm px-2 py-1 text-left text-tiny font-normal text-gray-700 hover:bg-gray-50"
                        onClick={() => onChange(allSelected ? [] : options.map((option) => option.value))}
                    >
                        <SmallStyledCheckbox checked={allSelected} />
                        <span>Välj alla</span>
                    </button>

                    {options.map((option) => (
                        <button
                            type="button"
                            role="checkbox"
                            aria-checked={selectedValues.includes(option.value)}
                            key={String(option.value)}
                            className="mb-0.5 flex w-full cursor-pointer items-center gap-2 rounded-sm px-2 py-0.5 text-left text-tiny font-normal text-gray-700 hover:bg-gray-50"
                            onClick={() => toggleOption(option.value)}
                        >
                            <SmallStyledCheckbox checked={selectedValues.includes(option.value)} />
                            <span className="truncate">{option.label}</span>
                        </button>
                    ))}
                </div>
            ) : null}
        </div>
    );
};

export default MultiSelectCheckboxFilter;
