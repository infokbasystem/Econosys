import ReactSelect from 'react-select'

const Select = ({
    value,
    items = [],
    disabled = false,
    stylePreset = 'default',
    isSearchable = false,
    className,
    optionTextAlign = 'left',
    onChange,
    ...props
}) => {
    const isGridCell = stylePreset === 'gridCell'
    const useSearchSelect = isSearchable || stylePreset === 'search'

    if (useSearchSelect) {
        const options = items.map((item) => ({
            value: item.id,
            label: item.name,
            isDisabled: item.isActive === false,
        }))
        const isMultiSelect = Boolean(props.isMulti)
        const selectedValue = isMultiSelect
            ? options.filter((option) => Array.isArray(value) && value.some((itemValue) => String(itemValue) === String(option.value)))
            : options.find((option) => String(option.value) === String(value)) ?? null

        return (
            <ReactSelect
                {...props}
                options={options}
                value={selectedValue}
                isDisabled={disabled}
                isSearchable
                className={className}
                onChange={(selected) => {
                    if (isMultiSelect) {
                        onChange?.((selected ?? []).map((option) => option.value))
                        return
                    }

                    onChange?.(selected?.value ?? '')
                }}
                styles={{
                    control: (provided, state) => ({
                        ...provided,
                        minHeight: 28,
                        height: isMultiSelect ? 'auto' : 28,
                        borderRadius: 9999,
                        borderColor: state.isFocused ? '#4d7c0f' : '#65a30d',
                        boxShadow: 'none',
                        backgroundColor: 'white',
                        ':hover': {
                            borderColor: state.isFocused ? '#4d7c0f' : '#65a30d',
                        },
                    }),
                    valueContainer: (provided) => ({
                        ...provided,
                        height: 28,
                        padding: '0 12px',
                        fontSize: '12px',
                    }),
                    input: (provided) => ({
                        ...provided,
                        margin: 0,
                        padding: 0,
                        fontSize: '12px',
                    }),
                    indicatorsContainer: (provided) => ({
                        ...provided,
                        height: 28,
                    }),
                    clearIndicator: (provided, state) => ({
                        ...provided,
                        padding: '0 0px 0 6px',
                        color: state.isFocused ? '#d97706' : '#f59e0b',
                        cursor: 'pointer',
                        ':hover': {
                            color: '#d97706',
                        },
                    }),
                    dropdownIndicator: (provided, state) => ({
                        ...provided,
                        padding: '0 8px 0 0px',
                        color: state.isFocused ? '#6b7280' : '#9ca3af',
                        ':hover': {
                            color: '#6b7280',
                        },
                    }),
                    indicatorSeparator: () => ({
                        display: 'none',
                    }),
                    option: (provided, state) => ({
                        ...provided,
                        fontSize: '12px',
                        backgroundColor: state.isFocused ? '#f7fee7' : 'white',
                        color: '#374151',
                        textAlign: optionTextAlign,
                    }),
                    singleValue: (provided) => ({
                        ...provided,
                        fontSize: '12px',
                        color: '#374151',
                    }),
                    placeholder: (provided) => ({
                        ...provided,
                        fontSize: '12px',
                        color: '#374151',
                        fontWeight: 400,
                    }),
                    menu: (provided) => ({
                        ...provided,
                        zIndex: 60,
                    }),
                }}
            />
        )
    }

    const baseClassName = isGridCell
        ? 'block h-full min-h-[24px] w-full border border-transparent bg-transparent px-2 pt-[3px] text-xs leading-none text-gray-700 outline-none transition focus:bg-white focus:ring-0 focus:shadow-[inset_0_0_0_1px_#60a5fa]'
        : 'w-full rounded-sm border border-gray-300 bg-white px-1 py-[calc(0.27rem-1px)] text-xs leading-none focus:outline-none disabled:bg-transparent disabled:text-gray-500'

    return (
        <select
            value={value}
            disabled={disabled}
            onChange={(event) => onChange?.(event.target.value)}
            className={`${baseClassName} ${className ?? ''}`.trim()}
            {...props}
        >
            {items.map((item) => {
                const isInactive = item.isActive === false
                const isSelected = String(item.id) === String(value)

                return (
                    <option
                        key={item.id}
                        value={item.id}
                        disabled={isInactive}
                        hidden={isInactive && !isSelected}
                        style={{ textAlign: optionTextAlign }}
                    >
                        {item.name}
                    </option>
                )
            })}
        </select>
    )
}

export default Select