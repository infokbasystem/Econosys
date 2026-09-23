const SEGMENTED_FILTER_THEMES = {
    slate: {
        wrapperClassName: 'bg-slate-700',
        activeClassName: 'bg-amber-400 text-white',
        inactiveClassName: 'text-slate-300 hover:text-white',
    },
    lime: {
        wrapperClassName: 'border border-lime-600 bg-lime-50',
        activeClassName: 'bg-[var(--color-green-200)] text-gray-900',
        inactiveClassName: 'text-gray-700 hover:text-gray-900',
    },
    sky: {
        wrapperClassName: 'border border-sky-300 bg-sky-50',
        activeClassName: 'bg-[#bfdbfe] text-gray-900',
        inactiveClassName: 'text-sky-900/80 hover:text-sky-950',
    },
}

export default function SegmentedFilter({
    value,
    values,
    onChange,
    options = [],
    isMulti = false,
    theme = 'slate',
    className = '',
}) {
    const themeConfig = SEGMENTED_FILTER_THEMES[theme] ?? SEGMENTED_FILTER_THEMES.slate
    const selectedValues = Array.isArray(values) ? values : []

    function isSelected(optionValue) {
        if (isMulti) {
            return selectedValues.includes(optionValue)
        }

        return value === optionValue
    }

    function handleOptionClick(optionValue) {
        if (isMulti) {
            const nextValues = selectedValues.includes(optionValue)
                ? selectedValues.filter((entry) => entry !== optionValue)
                : [...selectedValues, optionValue]

            onChange?.(nextValues)
            return
        }

        onChange?.(optionValue)
    }

    return (
        <div className={`h-7 inline-flex items-center gap-0.5 rounded-full ${themeConfig.wrapperClassName} ${className}`.trim()}>
            {options.map((option) => (
                <button
                    key={option.value}
                    type="button"
                    onClick={() => handleOptionClick(option.value)}
                    className={`rounded-full px-3 pt-[7px] pb-[5px] text-xs font-semibold transition-colors ${
                        isSelected(option.value)
                            ? themeConfig.activeClassName
                            : themeConfig.inactiveClassName
                    }`}
                >
                    {option.label}
                </button>
            ))}
        </div>
    )
}