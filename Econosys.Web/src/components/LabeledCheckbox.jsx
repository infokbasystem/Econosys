import React from 'react';

const COLOR_VARIANTS = {
    teal: { checkedBg: '#14b8a6', checkedBorder: '#14b8a6', checkedText: '#ffffff' },
    red: { checkedBg: '#e11d48', checkedBorder: '#e11d48', checkedText: '#ffffff' },
    cyan: { checkedBg: '#06b6d4', checkedBorder: '#06b6d4', checkedText: '#ffffff' },
    green: { checkedBg: '#22c55e', checkedBorder: '#22c55e', checkedText: '#ffffff' },
    yellow: { checkedBg: '#e5e700', checkedBorder: '#e5e700', checkedText: '#ffffff' },
    orange: { checkedBg: '#fb923c', checkedBorder: '#fb923c', checkedText: '#ffffff' },
    black: { checkedBg: '#000000', checkedBorder: '#000000', checkedText: '#ffffff' },
    gray: { checkedBg: '#9ca3af', checkedBorder: '#9ca3af', checkedText: '#ffffff' },
};

const LabeledCheckbox = ({
    id,
    name,
    inputType = 'checkbox',
    label,
    labelWidth,
    checked = false,
    onChange,
    disabled = false,
    labelPosition = 'right',
    text = '',
    color = 'teal',
    checkedColor,
    checkedBorderColor,
    checkedTextColor,
    uncheckedColor = '#ffffff',
    uncheckedBorderColor = '#aaaaaa',
    uncheckedTextColor = '#4b5563',
    className = '',
    labelClassName = '',
    checkboxClassName = '',
    ariaLabel,
}) => {
    const resolvedId = id || name || `labeled-checkbox-${Math.random().toString(36).slice(2)}`;
    const isRadio = inputType === 'radio';
    const hasCustomText = String(text ?? '').trim().length > 0;
    const palette = COLOR_VARIANTS[color] || COLOR_VARIANTS.teal;

    const resolvedCheckedBg = checkedColor || palette.checkedBg;
    const resolvedCheckedBorder = checkedBorderColor || palette.checkedBorder;
    const resolvedCheckedText = checkedTextColor || palette.checkedText;

    const indicatorStyle = checked
        ? {
            backgroundColor: resolvedCheckedBg,
            borderColor: resolvedCheckedBorder,
            color: resolvedCheckedText,
        }
        : {
            backgroundColor: uncheckedColor,
            borderColor: uncheckedBorderColor,
            color: uncheckedTextColor,
        };

    const handleToggle = () => {
        if (disabled) {
            return;
        }

        if (isRadio) {
            if (!checked) {
                onChange?.(true);
            }
            return;
        }

        onChange?.(!checked);
    };

    const handleKeyDown = (event) => {
        if (disabled) {
            return;
        }
        if (event.key === ' ' || event.key === 'Enter') {
            event.preventDefault();
            handleToggle();
        }
    };

    const checkboxElement = (
        <button
            id={resolvedId}
            name={name}
            type="button"
            role={isRadio ? 'radio' : 'checkbox'}
            aria-checked={checked}
            aria-label={ariaLabel || label}
            disabled={disabled}
            onClick={handleToggle}
            onKeyDown={handleKeyDown}
            className={`inline-flex h-6.5 w-6.5 shrink-0 items-center justify-center rounded-full border border-[#e5e7eb] bg-white p-[2px] align-middle text-xs leading-none transition focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-lime-700 ${disabled ? 'cursor-not-allowed opacity-60' : 'cursor-pointer'} ${checkboxClassName}`}
        >
            <span
                className="inline-flex h-full w-full items-center justify-center rounded-full border text-xs font-semibold leading-none transition-all"
                style={indicatorStyle}
            >
                {hasCustomText ? (
                    text
                ) : (
                    <svg viewBox="0 0 16 16" aria-hidden="true" className={`h-3 w-3 transition-opacity ${checked ? 'opacity-100' : 'opacity-0'}`}>
                        <path d="M3 8.5 6.3 11.6 13 4.9" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                )}
            </span>
        </button>
    );

    if (!label) {
        return <div className={className}>{checkboxElement}</div>;
    }

    return (
        <label
            htmlFor={resolvedId}
            className={`inline-flex items-center gap-2 text-xs text-gray-700 ${disabled ? 'cursor-not-allowed opacity-60' : 'cursor-pointer'} ${className}`}
        >
            {labelPosition === 'left' && <span className={`${labelWidth || ''} ${labelClassName}`.trim()}>{label}</span>}
            {checkboxElement}
            {labelPosition !== 'left' && <span className={`${labelWidth || ''} ${labelClassName}`.trim()}>{label}</span>}
        </label>
    );
};

export default LabeledCheckbox;
