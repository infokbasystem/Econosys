import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { Search } from 'lucide-react';
import apiClient from '../config/apiClient';

const OrderSearchModal = ({
    isOpen,
    onClose,
    onSelect }) => {

    const [searchTerm, setSearchTerm] = useState('');
    const [results, setResults] = useState([]);
    const [loading, setLoading] = useState(false);
    const [selectedIndex, setSelectedIndex] = useState(-1);

    useEffect(() => {
        if (!isOpen) {
            setSearchTerm('');
            setResults([]);
            setSelectedIndex(-1);
        }
    }, [isOpen]);

    useEffect(() => {
        const term = searchTerm.trim();

        if (!term) {
            setResults([]);
            setLoading(false);
            return;
        }

        const timer = setTimeout(async () => {
            setLoading(true);
            try {
                const res = await apiClient.post('/customerorders/search', {
                    filter: { conditions: [{ field: 'CustomerOrderNr', operator: 'startswith', value: term }] },
                    pagination: { pageNumber: 1, pageSize: 20 }
                });
                setResults(res.data?.items ?? []);
            } catch {
                setResults([]);
            } finally {
                setLoading(false);
            }
        }, 100);

        return () => clearTimeout(timer);
    }, [searchTerm]);

    if (!isOpen) return null;


    return createPortal(
        <div className="fixed inset-0 z-50">
            <div className="absolute inset-0 bg-black/50 z-40" />
            <div className="relative z-50 flex min-h-screen items-start justify-center pt-20" onClick={onClose}>
                <div
                    className="relative bg-white rounded-sm shadow-xl max-w-xl w-full mx-4 p-6"
                    style={{ background: 'rgb(255, 255, 234)' }}
                    onClick={(e) => e.stopPropagation()}
                >
                    <div className="relative flex items-center justify-center mb-4">
                        <h2 className="text-sm font-semibold">Välj ordererkännande</h2>
                        <button
                            onClick={onClose}
                            className="absolute right-0 text-gray-400 hover:text-gray-600 text-l leading-none mb-1"
                        >
                            ×
                        </button>
                    </div>
                    {/* Search Input */}
                    <div className="mx-10 mt-7 mb-4 flex justify-center">
                        <div className="relative flex items-center w-80">
                            <Search className="absolute left-2 w-4 h-4 text-gray-400" />
                            <input
                                type="text"
                                placeholder="Sök ordernummer"
                                value={searchTerm}
                                onChange={(e) => setSearchTerm(e.target.value)}
                                autoFocus
                                className="w-full pl-8 pr-4 py-2 border border-gray-300 rounded-sm text-xs focus:outline-none focus:border-blue-500 text-center bg-white"
                            />
                        </div>
                    </div>
                    {/* Results List */}
                    <div className="mx-10 mb-4 max-h-96 overflow-y-auto">

                        {loading ? (
                            <div className="p-4 text-center text-xs text-gray-500">
                                Söker...
                            </div>
                        ) : results.length === 0 ? (
                            <div className="p-4 text-center text-xs text-gray-500">
                                {searchTerm ? 'Inga order hittades' : 'Börja skriva för att söka'}
                            </div>
                        ) : (
                            <ul className="divide-y divide-gray-200">
                                {results.map((order, index) => (
                                    <li
                                        key={order.id}
                                        className={`p-3 cursor-pointer transition-colors ${selectedIndex === index
                                            ? 'bg-yellow-200'
                                            : 'hover:bg-yellow-100'
                                            }`}
                                        onClick={() => onSelect(order)}
                                        onMouseEnter={() => setSelectedIndex(index)}
                                    >
                                        <div className='flex'>
                                            <div className="w-40 text-xs font-medium text-gray-800">
                                                <span className='text-gray-500 mr-2'>Nr:</span>{order.customerOrderNr}
                                            </div>
                                            <div className="text-xs font-medium text-gray-800">
                                                skapad {order.created ? new Date(order.created).toLocaleDateString('sv-SE') : ''}
                                            </div>
                                        </div>
                                        <div className='flex'>
                                            <div className="w-40 text-xs text-gray-600 mt-1">
                                                {order.customerName}
                                            </div>
                                            <div className="text-xs text-gray-600 mt-1">
                                                {order.product}
                                            </div>
                                        </div>
                                    </li>
                                ))}
                            </ul>
                        )}
                        {/* <table className="w-full text-xs">
                            <thead>
                                <tr className="border-b border-gray-300">
                                    <th className="text-left py-1 px-2 font-normal text-gray-600 uppercase">Ordererk.nr</th>
                                    <th className="text-left py-1 px-2 font-normal text-gray-600 uppercase">Kund</th>
                                    <th className="text-left py-1 px-2 font-normal text-gray-600 uppercase">Skapad</th>
                                </tr>
                            </thead>
                            <tbody>
                                {loading && (
                                    <tr><td colSpan={3} className="py-4 text-center text-gray-400">Söker...</td></tr>
                                )}
                                {!loading && results.length === 0 && searchTerm.trim() && (
                                    <tr><td colSpan={3} className="py-4 text-center text-gray-400">Inga träffar.</td></tr>
                                )}
                                {!loading && results.map(order => (
                                    <tr
                                        key={order.id}
                                        onClick={() => onSelect(order)}
                                        className="cursor-pointer hover:bg-yellow-100"
                                    >
                                        <td className="py-1 px-2">{order.customerOrderNr}</td>
                                        <td className="py-1 px-2">{order.customerName}</td>
                                        <td className="py-1 px-2">{order.date ? new Date(order.date).toLocaleDateString('sv-SE') : ''}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table> */}
                    </div>
                    <div className="flex gap-4 mt-6 mb-3 pt-4 justify-end">
                        <button
                            type="button"
                            onClick={onClose}
                            className="shadow-md/30 text-xs text-white bg-orange-400 hover:bg-orange-600 px-10 p-[5px]"
                        >
                            AVBRYT
                        </button>
                    </div>
                </div>
            </div>

        </div>,
        document.body
    );
};

export default OrderSearchModal;
