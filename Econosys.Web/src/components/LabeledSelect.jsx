import { ChevronDown } from 'lucide-react';

const LabeledSelect = ({
  label,
  labelWidth,
  inputWidth,
  margintop,
  name,
  value,
  items,
  onChange,
  disabled,
  ...props }) => {
  return (
    <div className={`flex items-center w-full pb-[1px] mt-${margintop}`}>
      <label className={`${labelWidth} flex-none text-xs text-gray-700`}>{label}</label>
      <div className={`group relative flex min-w-0 items-center ${inputWidth || 'w-full'}`}>
        <select
          name={name}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className="h-6.25 w-full appearance-none rounded-sm border border-gray-300 bg-white px-1 py-1 pr-6 text-xs focus:outline-none disabled:bg-transparent disabled:text-gray-500"
          disabled={disabled}
          {...props}
        >
          {items.map((item) => {
            const isInactive = item.isActive === false;
            const isSelected = String(item.id) === String(value);
            return (
              <option
                key={item.id}
                value={item.id}
                disabled={isInactive}
                hidden={isInactive && !isSelected}
              >
                {item.name}
              </option>
            );
          })}
        </select>
        <ChevronDown
          aria-hidden="true"
          className={`pointer-events-none absolute right-1 h-4 w-4 transition-colors ${disabled ? 'text-gray-400' : 'text-[#a9a9a9] group-hover:text-black'}`}
        />
      </div>
    </div>
  );
};

export default LabeledSelect;