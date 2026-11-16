import assert from 'node:assert/strict';
import { afterEach, test } from 'node:test';
import apiClient from '../config/apiClient.js';
import { canSearchHeader, searchHeader } from './globalSearch.js';

const originalAdapter = apiClient.defaults.adapter;
afterEach(() => {
    apiClient.defaults.adapter = originalAdapter;
});

test('numeric calculation search requests an exact ID and maps the result', async () => {
    const requests = [];
    apiClient.defaults.adapter = async (config) => {
        requests.push(config);
        return {
            config, status: 200, statusText: 'OK', headers: {},
            data: { items: config.url === '/calculations/search'
                ? [{ id: 123, productName: 'Box', customerName: 'Store', supplierName: 'Mill', createdAt: '2026-01-02' }]
                : [] },
        };
    };

    const { groups, hadErrors } = await searchHeader(' 00123 ');

    assert.equal(hadErrors, false);
    assert.deepEqual(groups.find(({ key }) => key === 'calculations'), {
        key: 'calculations',
        label: 'KALKYLER',
        failed: false,
        results: [{
            id: 123, number: 123, name: 'Box', details: 'Store · Mill',
            date: '2026-01-02', path: '/order/calculations/123',
        }],
    });
    const request = requests.find(({ url }) => url === '/calculations/search');
    assert.deepEqual(JSON.parse(request.data), {
        pagination: { pageNumber: 1, pageSize: 5 },
        filter: { conditions: [{ field: 'id', operator: 'eq', value: 123 }] },
    });
});

test('short numeric IDs search calculations without querying other categories', async () => {
    const requests = [];
    apiClient.defaults.adapter = async (config) => {
        requests.push(config);
        return { config, status: 200, statusText: 'OK', headers: {}, data: { items: [] } };
    };

    for (const [term, id] of [['1', 1], ['007', 7], ['999', 999]]) {
        requests.length = 0;
        assert.equal(canSearchHeader(term), true);
        await searchHeader(term);
        assert.deepEqual(requests.map(({ url }) => url), ['/calculations/search']);
        assert.equal(JSON.parse(requests[0].data).filter.conditions[0].value, id);
    }

    assert.equal(canSearchHeader('0'), false);
    assert.equal(canSearchHeader('abc'), false);
    assert.equal(canSearchHeader('abcd'), true);
});

test('calculation search excludes nonnumeric, zero and out-of-range IDs', async () => {
    const requests = [];
    apiClient.defaults.adapter = async (config) => {
        requests.push(config);
        return { config, status: 200, statusText: 'OK', headers: {}, data: { items: [] } };
    };

    for (const term of ['box123', '0000', '2147483648', '999999999999999999999']) {
        requests.length = 0;
        const { hadErrors } = await searchHeader(term);
        assert.equal(hadErrors, false);
        assert.equal(requests.some(({ url }) => url === '/calculations/search'), false);
        assert.ok(requests.length > 0);
    }

    requests.length = 0;
    await searchHeader('2147483647');
    assert.equal(JSON.parse(requests.find(({ url }) => url === '/calculations/search').data).filter.conditions[0].value, 2147483647);
});

test('a failed calculation search is reported alongside other results', async () => {
    apiClient.defaults.adapter = async (config) => {
        if (config.url === '/calculations/search') throw new Error('Calculation search failed');
        return { config, status: 200, statusText: 'OK', headers: {}, data: { items: [] } };
    };

    const { groups, hadErrors } = await searchHeader('1234');
    assert.equal(hadErrors, true);
    assert.deepEqual(groups, []);
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
