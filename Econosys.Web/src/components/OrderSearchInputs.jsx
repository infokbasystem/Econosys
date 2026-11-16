import { Search } from 'lucide-react';

const fields = [
    { key: 'searchTerm', label: 'Sök' },
    { key: 'product', label: 'Produkt' },
    { key: 'supplierName', label: 'Leverantör' },
    { key: 'customerName', label: 'Kund' },
];

const OrderSearchInputs = ({ values, onChange, extraFields = [] }) => (
    <div className="ml-16 flex shrink-0 items-center gap-4">
        {fields.concat(extraFields).map(({ key, label }) => (
            <div key={key} className="relative w-44">
                <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
                <input
                    type="text"
                    aria-label={label}
                    value={values[key]}
                    onChange={(event) => onChange((prev) => ({ ...prev, [key]: event.target.value }))}
                    placeholder={label}
                    className="h-6 w-full rounded-full border border-lime-600 bg-white pl-8 pr-4 text-xs text-gray-700 outline-none transition placeholder:text-gray-500 focus:border-lime-700"
                />
            </div>
        ))}
    </div>
);

export default OrderSearchInputs;
