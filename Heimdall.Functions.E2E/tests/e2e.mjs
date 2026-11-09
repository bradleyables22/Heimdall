import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { chromium } from 'playwright';
import fs from 'node:fs';
import crypto from 'node:crypto';

const root = fileURLToPath(new URL('..', import.meta.url));
const scriptRoot = process.env.HEIMDALL_FUNCTIONS_E2E_SCRIPT_ROOT || path.join(root, 'bin', 'Debug', 'net10.0');
const external = process.env.HEIMDALL_FUNCTIONS_E2E_URL;
const base = (external || 'http://127.0.0.1:7079').replace(/\/$/, '');
const soak = process.argv.includes('--soak');
const keyed = process.argv.includes('--keys');
const functionKey = keyed ? (external ? process.env.HEIMDALL_E2E_FUNCTION_KEY : crypto.randomBytes(32).toString('base64url')) : null;
if (keyed && !functionKey) throw new Error('Set HEIMDALL_E2E_FUNCTION_KEY for an external keyed test host.');
const api = keyed ? '/api/keyed/__heimdall/v1' : '/api/__heimdall/v1';
const keyHeaders = keyed ? { 'x-functions-key': functionKey } : {};
let secretsDirectory;
let temporaryRoot;
const soakMs = Number(process.env.HEIMDALL_FUNCTIONS_SOAK_MS || 250000);
let worker;
let output = '';
let browser;

async function waitUntilReady() {
  const deadline = Date.now() + 60000;
  while (Date.now() < deadline) {
    if (worker && worker.exitCode !== null) throw new Error(`Functions host exited: ${output}`);
    try {
      const response = await fetch(`${base}/api/demo`, { signal: AbortSignal.timeout(1500) });
      if (response.ok) return;
    } catch {}
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  throw new Error(`Functions host did not become ready: ${output}`);
}

async function csrf(user) {
  const response = await fetch(`${base}${api}/csrf`, {
    headers: { ...keyHeaders, ...(user ? { 'X-E2E-User': user } : {}) }, signal: AbortSignal.timeout(10000)
  });
  assert.equal(response.status, 200);
  return {
    ...keyHeaders,
    Cookie: response.headers.get('set-cookie').split(';')[0],
    RequestVerificationToken: (await response.json()).requestToken
  };
}

async function sseTransport() {
  const headers = await csrf();
  const minted = await fetch(`${base}${api}/bifrost/token?topic=functions-e2e`, { headers, signal: AbortSignal.timeout(10000) });
  assert.equal(minted.status, 200);
  const { token } = await minted.json();
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), soak ? soakMs + 30000 : 15000);
  let reader;
  try {
    const started = Date.now();
    const stream = await fetch(`${base}${api}/bifrost?topic=functions-e2e&st=${encodeURIComponent(token)}${keyed ? '&code=' + encodeURIComponent(functionKey) : ''}`, { signal: controller.signal });
    assert.equal(stream.status, 200);
    assert.match(stream.headers.get('content-type'), /text\/event-stream/);
    reader = stream.body.getReader();
    const decoder = new TextDecoder();
    let received = '';
    let published = false;
    while (true) {
      const chunk = await reader.read();
      assert.equal(chunk.done, false, 'SSE stream should stay open');
      received += decoder.decode(chunk.value, { stream: true });
      if (!published && received.includes('heimdall:connected') && received.includes(': ping')) {
        assert.ok(Date.now() - started < 10000, 'Initial event and heartbeat must arrive before function completion');
        const response = await fetch(`${base}${api}/content/invocations`, {
          method: 'POST', headers: { ...headers, 'Content-Type': 'application/json', 'X-Heimdall-Content-Invocation': 'functions.publish' }, body: '{}'
        });
        assert.equal(response.status, 200);
        published = true;
      }
      if (published && received.includes('Live from Functions') && (!soak || Date.now() - started >= soakMs)) break;
    }
    console.log(`ok - streamed initial event, idle heartbeat, and published HTML${soak ? `; stayed connected for ${Date.now() - started}ms` : ''}`);
  } finally {
    clearTimeout(timeout);
    controller.abort();
    await reader?.cancel().catch(() => {});
  }
}

try {
  if (!external) {
    if (keyed) {
      temporaryRoot = path.join(root, 'bin', 'test-runtime', crypto.randomUUID());
      // Core Tools selects its key store under Path.GetTempPath(), overriding host options.
      secretsDirectory = path.join(temporaryRoot, 'secrets', 'functions', 'secrets');
      fs.mkdirSync(secretsDirectory, { recursive: true });
      fs.writeFileSync(path.join(secretsDirectory, 'keyedheimdall.json'), JSON.stringify({ keys: [{ name: 'default', value: functionKey, encrypted: false }] }));
    }
    const executable = path.join(root, 'node_modules', 'azure-functions-core-tools', 'bin', process.platform === 'win32' ? 'func.exe' : 'func');
    // Worker SDK can leave its generated extension-bundle host.json in the build output.
    // The fixture uses the explicit HTTP configuration from the source project.
    if (!process.env.HEIMDALL_FUNCTIONS_E2E_SCRIPT_ROOT)
      fs.copyFileSync(path.join(root, 'host.json'), path.join(scriptRoot, 'host.json'));
    worker = spawn(executable, ['start', '--script-root', scriptRoot, '--port', '7079', '--no-build', ...(keyed ? ['--enableAuth'] : [])], {
      cwd: process.env.HEIMDALL_FUNCTIONS_E2E_SCRIPT_ROOT ? scriptRoot : root, windowsHide: true,
      env: { ...process.env, FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated', AZURE_FUNCTIONS_CORE_TOOLS_TELEMETRY_OPTOUT: '1',
        ...(keyed ? { AzureWebJobsSecretStorageType: 'files', TMP: temporaryRoot, TEMP: temporaryRoot, TMPDIR: temporaryRoot, E2EFunctionKey: functionKey } : {}) }
    });
    worker.stdout.on('data', data => { output = (output + data).slice(-25000); });
    worker.stderr.on('data', data => { output = (output + data).slice(-25000); });
    worker.on('error', error => { output += error.stack; });
  }
  await waitUntilReady();
  if (keyed) {
    for (const suffix of ['/csrf', '/content/invocations', '/bifrost/token?topic=functions-e2e', '/bifrost?topic=functions-e2e&st=bad']) {
      for (const headers of [{}, { 'x-functions-key': 'wrong-key' }]) {
        const response = await fetch(`${base}${api}${suffix}`, { method: suffix.includes('content/') ? 'POST' : 'GET', headers, signal: AbortSignal.timeout(10000) });
        assert.equal(response.status, 401, `Functions host must reject missing/invalid keys for ${suffix}`);
      }
    }
    for (const suffix of ['/csrf?code=' + encodeURIComponent(functionKey), '/csrf']) {
      const response = await fetch(`${base}${api}${suffix}`, { headers: suffix.includes('?') ? {} : keyHeaders, signal: AbortSignal.timeout(10000) });
      assert.equal(response.status, 200);
    }
    assert.equal((await fetch(`${base}${api}/csrf?code=wrong-key`)).status, 401);
    console.log('ok - Functions host enforces missing, invalid, and valid function keys');
  }
  browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  page.setDefaultTimeout(15000);
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto(`${base}${keyed ? '/api/keyed/demo' : '/api/demo'}`);
  const asset = await fetch(`${base}/api/assets/site.css`);
  assert.equal(asset.status, 200);
  assert.match(asset.headers.get('content-type'), /text\/css/);
  const appRange = await fetch(`${base}/api/assets/site.css`, { headers: { Range: 'bytes=0-3' } });
  assert.equal(appRange.status, 206);
  assert.equal((await appRange.arrayBuffer()).byteLength, 4);
  const runtimeUrl = `${base}/api/_content/HeimdallFramework.Web/heimdall-bundle.min.js`;
  const runtime = await fetch(runtimeUrl, { headers: { 'Accept-Encoding': 'identity' } });
  assert.equal(runtime.status, 200);
  assert.match(await runtime.text(), /Heimdall/);
  const cached = await fetch(runtimeUrl, { headers: { 'If-None-Match': runtime.headers.get('etag'), 'Accept-Encoding': 'identity' } });
  assert.equal(cached.status, 304);
  const compressed = await fetch(runtimeUrl, { headers: { 'Accept-Encoding': 'gzip' } });
  assert.equal(compressed.status, 200);
  assert.equal(compressed.headers.get('content-encoding'), 'gzip');
  const compressedCached = await fetch(runtimeUrl, { headers: { 'If-None-Match': compressed.headers.get('etag'), 'Accept-Encoding': 'gzip' } });
  assert.equal(compressedCached.status, 304);
  const plugin = await fetch(`${base}/api/_content/Heimdall.E2E.TestPlugin/css/test-plugin.css`);
  assert.equal(plugin.status, 200);
  assert.match(plugin.headers.get('content-type'), /text\/css/);
  const pluginScript = await fetch(`${base}/api/_content/Heimdall.E2E.TestPlugin/js/test-plugin.js`);
  assert.equal(pluginScript.status, 200);
  const head = await fetch(runtimeUrl, { method: 'HEAD', headers: { 'Accept-Encoding': 'identity' } });
  assert.equal(head.status, 200);
  assert.equal(await head.text(), '');
  const range = await fetch(runtimeUrl, { headers: { Range: 'bytes=0-15', 'Accept-Encoding': 'identity' } });
  assert.equal(range.status, 206);
  assert.equal((await range.arrayBuffer()).byteLength, 16);
  if (!external) {
    const manifest = JSON.parse(fs.readFileSync(path.join(scriptRoot, 'Heimdall.Functions.E2E.staticwebassets.endpoints.json'), 'utf8'));
    const fingerprinted = manifest.Endpoints.find(endpoint =>
      endpoint.Route.startsWith('_content/HeimdallFramework.Web/heimdall-bundle.min.') &&
      endpoint.Route.endsWith('.js') && endpoint.Route !== '_content/HeimdallFramework.Web/heimdall-bundle.min.js');
    assert.ok(fingerprinted, 'SDK should generate the fingerprinted RCL bundle URL');
    const immutable = await fetch(`${base}/api/${fingerprinted.Route}`, { headers: { 'Accept-Encoding': 'identity' } });
    assert.equal(immutable.status, 200);
    assert.match(immutable.headers.get('cache-control'), /immutable/);
  }
  console.log('ok - application assets and RCL assets from Heimdall.Web and test plugin');
  await page.click('#increment');
  await page.waitForFunction(() => document.querySelector('#counter').textContent === '5');
  console.log('ok - browser JSON form invocation with automatic antiforgery');
  await page.setInputFiles('input[type=file]', { name: 'sample.txt', mimeType: 'text/plain', buffer: Buffer.from('hello') });
  await page.click('#upload-form button');
  await page.waitForFunction(() => document.querySelector('#upload-result').textContent === 'Received 5 bytes');
  console.log('ok - browser multipart upload');
  await page.click('#directives');
  await page.waitForFunction(() => document.querySelector('#counter').textContent === 'Updated' && document.querySelector('#notice').textContent === 'Notice');
  console.log('ok - main swap and out-of-band directive');
  await page.click('#mvc');
  await page.locator('#mvc-result').waitFor();
  assert.equal(await page.locator('#mvc-result').textContent(), 'MVC <Functions>');
  await page.click('#blazor');
  await page.locator('#blazor-result').waitFor();
  assert.equal(await page.locator('#blazor-result').textContent(), 'Blazor <Functions>');
  console.log('ok - compiled Razor partial and Blazor component render through the worker');
  await page.click('#publish');
  await page.waitForFunction(() => document.querySelector('#live').textContent === 'Live from Functions');
  console.log('ok - browser SSE receives published HTML');
  // End the actual worker response. The runtime must mint a fresh token and reconnect.
  let minted = 0;
  page.on('request', request => { if (request.url().includes('/bifrost/token?')) minted++; });
  await page.evaluate(() => {
    window.e2eReopened = false;
    document.addEventListener('heimdall:sse-open', () => { window.e2eReopened = true; }, { once: true });
  });
  await page.evaluate(() => Heimdall.invoke('functions.drop-stream', {}, { swap: 'none' }));
  await page.waitForFunction(() => window.e2eReopened);
  for (let i = 0; i < 100 && minted === 0; i++) await page.waitForTimeout(100);
  assert.ok(minted > 0, 'SSE reconnect must mint a fresh token');
  await page.evaluate(() => { document.querySelector('#live').textContent = 'Reconnecting'; });
  await page.click('#publish');
  await page.waitForFunction(() => document.querySelector('#live').textContent === 'Live from Functions');
  console.log('ok - browser SSE reconnects with a fresh token');
  assert.deepEqual(errors, []);
  await page.close();

  const denied = await fetch(`${base}${api}/content/invocations`, {
    method: 'POST', headers: { ...keyHeaders, 'X-Heimdall-Content-Invocation': 'functions.increment', 'Content-Type': 'application/json' }, body: '{"count":4}'
  });
  assert.equal(denied.status, 400);
  console.log('ok - worker rejects missing antiforgery token');
  for (const user of [undefined, 'alice']) {
    const tokens = await csrf(user);
    const response = await fetch(`${base}${api}/content/invocations`, {
      method: 'POST', signal: AbortSignal.timeout(10000),
      headers: { ...tokens, ...(user ? { 'X-E2E-User': user } : {}),
        'X-Heimdall-Content-Invocation': 'functions.private', 'Content-Type': 'application/json' },
      body: '{}'
    });
    assert.equal(response.status, user ? 200 : 401);
  }
  console.log('ok - worker authenticates the default scheme and enforces invocation authorization');
  await sseTransport();
  console.log('Functions E2E checks passed.');
} catch (error) {
  console.error(error);
  if (worker) console.error(output);
  process.exitCode = 1;
} finally {
  await browser?.close();
  if (worker?.pid && worker.exitCode === null) {
    if (process.platform === 'win32') spawnSync('taskkill', ['/pid', String(worker.pid), '/T', '/F'], { windowsHide: true, stdio: 'ignore' });
    else worker.kill('SIGTERM');
  }
  if (temporaryRoot) {
    const resolved = path.resolve(temporaryRoot);
    if (!resolved.startsWith(path.resolve(root, 'bin', 'test-runtime') + path.sep)) throw new Error('Unexpected test temp directory');
    fs.rmSync(resolved, { recursive: true, force: true, maxRetries: 5 });
  }
}
