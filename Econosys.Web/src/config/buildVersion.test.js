import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import viteConfig from '../../vite.config.js';

test('each production build emits a unique version matching the compiled build ID', () => {
    const first = viteConfig({ command: 'build', mode: 'production' });
    const second = viteConfig({ command: 'build', mode: 'production' });
    assert.notEqual(first.define.__APP_BUILD_ID__, second.define.__APP_BUILD_ID__);
    let emitted;
    first.plugins.find((plugin) => plugin.name === 'app-build-version').generateBundle.call({
        emitFile: (file) => { emitted = file; },
    });
    assert.equal(emitted.fileName, 'version.json');
    const { buildId } = JSON.parse(emitted.source);
    assert.match(buildId, /^\d{14}-[a-f0-9]{8}$/);
    assert.equal(JSON.parse(first.define.__APP_BUILD_ID__), buildId);
    assert.equal(first.server.https, undefined);
});

test('HTML and version checks are uncached while hashed assets retain long-lived caching', () => {
    const config = JSON.parse(readFileSync(new URL('../../public/staticwebapp.config.json', import.meta.url), 'utf8'));
    assert.match(config.globalHeaders['Cache-Control'], /no-store/);
    assert.ok(config.navigationFallback.exclude.includes('/version.json'));
    const version = config.routes.find((route) => route.route === '/version.json');
    assert.match(version.headers['Cache-Control'], /no-store/);
    const assets = config.routes.find((route) => route.route === '/assets/*');
    assert.equal(assets.headers['Cache-Control'], 'public, max-age=31536000, immutable');
});
