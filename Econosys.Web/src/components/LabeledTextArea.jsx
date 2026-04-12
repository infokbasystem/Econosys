import React, { useState, useEffect, useRef } from 'react';
import { ArrowDownToLine } from 'lucide-react';

const LabeledTextArea = ({
    label,
    labelWidth,
    inputWidth,
    margintop,
    height,
    name,
    type = 'text',
    decimals = 2,
    integerOnly = false,
    value,
    disabled = false,
    onChange,
    popupItems,
    popupOnSelect,
    popupLabelField = 'name',
    ...props
}) => {
    const [displayValue, setDisplayValue] = useState(
        type === 'number' ? formatNumberDisplay(value) : value || ''
    );
    const [showPopup, setShowPopup] = useState(false);
    const [filter, setFilter] = useState('');
    const wrapperRef = useRef(null);

    useEffect(() => {
        setDisplayValue(type === 'number' ? formatNumberDisplay(value) : value || '');
    }, [value, type]);

    useEffect(() => {
        function handleOutside(e) {
            if (wrapperRef.current && !wrapperRef.current.contains(e.target)) {
                setShowPopup(false);
            }
        }
        document.addEventListener('mousedown', handleOutside);
        return () => document.removeEventListener('mousedown', handleOutside);
    }, []);

    const handleChange = (e) => {
        const input = e.target.value;

        if (type === 'number') {
            if (/^[0-9\s,]*$/.test(input) || input === '') {
                setDisplayValue(input);
                const numericValue = parseNumberInput(input, integerOnly);
                onChange && onChange(numericValue);
            }
        } else {
            setDisplayValue(input);
            onChange && onChange(input);
        }
    };

    const handleBlur = () => {
        if (type === 'number') {
            const numericValue = parseNumberInput(displayValue, integerOnly);
            setDisplayValue(formatNumberDisplay(numericValue, integerOnly));
        }
    };

    const handleSelect = (item) => {
        if (typeof popupOnSelect === 'function') {
            popupOnSelect(item);
        } else {
            const text = item?.[popupLabelField] ?? '';
            setDisplayValue(text);
            onChange && onChange(text);
        }
        setShowPopup(false);
    };

    function formatNumberDisplay(value, integerOnly = false) {
        if (value === null || value === undefined) return '';
        if (integerOnly) {
            return Math.round(value).toLocaleString('sv-SE');
        }
        return value.toLocaleString('sv-SE', { minimumFractionDigits: 0, maximumFractionDigits: 5 });
    }

    function parseNumberInput(str, integerOnly = false) {
        if (!str) return null;
        const normalized = str.replace(/\s/g, '').replace(',', '.');
        let num = parseFloat(normalized);
        if (isNaN(num)) return null;
        if (integerOnly) num = Math.round(num);
        return num;
    }

    return (
        <div className={`flex items-center space-x-1 w-full pb-[1px] mt-${margintop}`} ref={wrapperRef}>
            <div className={`relative flex items-center flex-none justify-between`}>
                <label className={`text-xs text-gray-700 ${labelWidth || ''}`}>{label}</label>
                {popupItems && Array.isArray(popupItems) && (
                    <button
                        type="button"
                        onClick={() => { setShowPopup(s => !s); setFilter(''); }}
                        className="ml-2 text-xs text-gray-600 px-1 rounded hover:bg-gray-100 text-right"
                        aria-expanded={showPopup}
                    >
                        <ArrowDownToLine size={12} className='text-red-700 hover:text-red scale-110' />
                    </button>
                )}

                {showPopup && popupItems && (
                    <div className="absolute left-0 top-full z-30 mt-1 w-72 bg-yellow-50 border border-gray-400 rounded shadow-md p-2">
                        <input
                            className="w-full border border-gray-400 bg-white rounded px-2 py-1 text-xs mb-2"
                            placeholder="Sök..."
                            value={filter}
                            onChange={(e) => setFilter(e.target.value)}
                        />
                        <div className="max-h-40 overflow-auto space-y-1">
                            {popupItems
                                .filter(it => {
                                    if (!filter) return true;
                                    const txt = String(it?.[popupLabelField] ?? '').toLowerCase();
                                    return txt.includes(filter.toLowerCase());
                                })
                                .map(it => (
                                    <button
                                        key={it.id ?? (it[popupLabelField] ?? Math.random())}
                                        type="button"
                                        className="w-full text-left text-xs px-2 py-1 hover:bg-gray-100 rounded"
                                        onClick={() => handleSelect(it)}
                                    >
                                        {it[popupLabelField] ?? ''}
                                    </button>
                                ))}
                        </div>
                    </div>
                )}
            </div>

            <textarea
                name={name}
                value={displayValue}
                disabled={disabled}
                onChange={handleChange}
                onBlur={handleBlur}
                className={`text-xs ${inputWidth || 'w-full'} ${height} border border-gray-300 rounded-sm px-2 py-1 focus:outline-none ${!disabled ? 'bg-white' : ''}`}
                {...props}
            />
        </div>
    );
};

export default LabeledTextArea;