import Select from 'react-select';


const LabeledReactSelect = ({ label, labelWidth, margintop, name, value, items, onChange, ...props }) => {
    const options = items.map(i => ({
        value: i.id,
        label: i.name
    }));

    const customStyles = {
        control: (provided, state) => ({
            ...provided,
            minHeight: 25,       // default is usually 38-40
            height: 25,
            borderColor: state.isFocused ? '#ccc' : '#ccc',
            boxShadow: state.isFocused ? 'none' : 'none',
            // '&:hover': { borderColor: '#2684FF' },
        }),
        valueContainer: (provided) => ({
            ...provided,
            height: 25,
            padding: '0px 0px 1px 6px',   // adjust left/right padding
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
            display: 'none', // optional: remove the thin separator
        }),
    };


    return (
        <div className={`flex items-center space-x-1 w-full pb-[1px] mt-${margintop}`}>
            <label className={`${labelWidth || ''} flex-none text-xs text-gray-700`}>{label}</label>
            <Select
                options={options}
                value={options.find(opt => opt.value === value) || null}
                onChange={opt => onChange(opt?.value || '')}
                className="text-xs w-full"
                placeholder="Välj"
                isClearable
                isSearchable
                styles={customStyles}
            />
        </div>
    );

};

export default LabeledReactSelect;