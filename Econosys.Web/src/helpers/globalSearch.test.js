import assert from 'node:assert/strict';
import { afterEach, test } from 'node:test';
import apiClient from '../config/apiClient.js';
import { searchHeader } from './globalSearch.js';

const originalAdapter = apiClient.defaults.adapter;
afterEach(() => {
    apiClient.defaults.adapter = originalAdapter;
});

for (const term of ['ABC-50', 'ABC-50-2', '44775']) {
    test(`customer order search supports ${term} and keeps database IDs in links`, async () => {
        const requests = [];
        apiClient.defaults.adapter = async (config) => {
            requests.push(config);
            return {
                config, status: 200, statusText: 'OK', headers: {},
                data: { items: config.url === '/customerorders/search'
                    ? [{ id: 123, customerOrderNr: term, customerName: 'Customer' }] : [] },
            };
        };

        const { groups, hadErrors } = await searchHeader(term);

        assert.equal(hadErrors, false);
        const customerOrders = groups.find(({ key }) => key === 'customerOrders');
        assert.equal(customerOrders.failed, false);
        assert.equal(customerOrders.results[0].number, term);
        assert.equal(customerOrders.results[0].path, '/order/customerorders/123');
        const request = requests.find(({ url }) => url === '/customerorders/search');
        assert.equal(JSON.parse(request.data).searchTerm, term);
    });
}
