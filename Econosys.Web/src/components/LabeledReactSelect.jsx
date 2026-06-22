import Select, { components } from 'react-select';


const LabeledReactSelect = ({ label, labelWidth, margintop, name, value, items, onChange, disableInactive, allowRawValueLabel = false, ...props }) => {
    const normalizedItems = Array.isArray(items) ? items : [];

    const options = normalizedItems
        .filter(i => !disableInactive || i.isActive !== false)
        .map(i => ({
            value: i.id,
            label: i.name
        }));

    const isMultiSelect = Boolean(props.isMulti);

    const selectedValue = isMultiSelect
        ? options.filter(opt => Array.isArray(value) && value.some(v => String(v) === String(opt.value)))
        : (options.find(opt => String(opt.value) === String(value))
            || (allowRawValueLabel && value ? { value, label: String(value) } : null));

    const handleChange = (selected) => {
        if (isMultiSelect) {
            onChange?.((selected || []).map(opt => opt.value));
            return;
        }

        onChange?.(selected?.value || '');
    };

    const CheckboxOption = (optionProps) => (
        <components.Option {...optionProps}>
            <div className="flex items-center gap-2">
                <input type="checkbox" checked={optionProps.isSelected} readOnly className="h-3 w-3" />
                <span>{optionProps.label}</span>
            </div>
        </components.Option>
    );

    const customStyles = {
        control: (provided, state) => ({
            ...provided,
            minHeight: 25,
            height: isMultiSelect ? 'auto' : 25,
            borderColor: state.isFocused ? '#ccc' : '#ccc',
            boxShadow: state.isFocused ? 'none' : 'none',
        }),
        valueContainer: (provided) => ({
            ...provided,
            minHeight: 25,
            padding: '0px 4px 1px 6px',
        }),
        input: (provided) => ({
            ...provided,
            margin: 0,
            padding: 0,
        }),
        indicatorsContainer: (provided) => ({
            ...provided,
            height: 25,
            
        }),
        multiValue: (provided) => ({
            ...provided,
            marginTop: 1,
            marginBottom: 1,
        }),
        clearIndicator: (provided) => ({
            ...provided,
            padding: 0,
            color: 'orange',
            cursor: 'pointer',
            '&:hover': { color: 'orange' },
        }),
        dropdownIndicator: (provided) => ({
            ...provided,
            padding: 0,
            color: 'darkgray',
            cursor: 'pointer',
            '&:hover': { color: '#000' },
        }),
        indicatorSeparator: () => ({
            display: 'none',
        }),
    };


    return (
        <div className={`flex items-center space-x-1 w-full pb-[1px] mt-${margintop}`}>
            <label className={`${labelWidth || ''} flex-none text-xs text-gray-700`}>{label}</label>
            <Select
                options={options}
                value={selectedValue}
                onChange={handleChange}
                className="text-xs w-full"
                placeholder="Välj"
                isClearable
                isSearchable
                styles={customStyles}
                closeMenuOnSelect={!isMultiSelect}
                hideSelectedOptions={false}
                components={isMultiSelect ? { Option: CheckboxOption } : undefined}
                {...props}
            />
        </div>
    );

};

export default LabeledReactSelect;