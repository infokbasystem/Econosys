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
  const selectedItem = items?.find(it => String(it.id) === String(value));

  return (
    <div className={`flex items-center w-full pb-[1px] mt-${margintop}`}>
      <label className={`${labelWidth} flex-none text-xs text-gray-700`}>{label}</label>
      <select
        name={name}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className={`text-xs ${inputWidth || 'w-full'} border border-gray-300 rounded-sm px-1 py-1 focus:outline-none bg-white disabled:bg-transparent disabled:text-gray-500`} 
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
    </div>
  );
};

export default LabeledSelect;