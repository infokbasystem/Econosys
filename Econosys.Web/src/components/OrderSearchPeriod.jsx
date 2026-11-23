import DateRangePicker from './DaterangePicker';

const toDateString = (date) => date
    ? `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
    : null;

const OrderSearchPeriod = ({ onChange }) => (
    <DateRangePicker
        placeholder="Välj period"
        presets={['this-month', 'last-month', 'last-3-months', 'year-to-date']}
        onApply={({ startDate, endDate }) => onChange({
            createdFrom: toDateString(startDate),
            createdTo: toDateString(endDate),
        })}
        triggerRadius="full"
        triggerClassName="h-6 border-lime-600 px-3 text-xs text-gray-700 focus:border-lime-700"
    />
);

export default OrderSearchPeriod;
