import assert from 'node:assert/strict';
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawn, spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';

const root = fileURLToPath(new URL('..', import.meta.url));
const oidc = process.argv.includes('--oidc');
const manifestPath = path.resolve(root, '..', 'artifacts', 'functions', 'starters.json');
const manifest = fs.existsSync(manifestPath) ? JSON.parse(fs.readFileSync(manifestPath, 'utf8').replace(/^\uFEFF/, '')) : null;
const project = process.env.HEIMDALL_FUNCTIONS_STARTER_PROJECT || manifest?.[oidc ? 'Oidc' : 'NoAuth'] || path.resolve(root, '..', 'tmp', 'functions-starters', oidc ? 'Oidc' : 'NoAuth');
const scratch = path.resolve(root, '..', 'tmp', 'functions-starter-e2e', crypto.randomUUID());
fs.mkdirSync(scratch, { recursive: true });
const pfx = path.join(scratch, 'localhost.pfx');
const password = crypto.randomBytes(24).toString('base64url');
const certificateScript = `
$rsa = [System.Security.Cryptography.RSA]::Create(2048)
$request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=localhost', $rsa, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
$cert = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddDays(-1), [DateTimeOffset]::UtcNow.AddDays(2))
[System.IO.File]::WriteAllBytes($env:HEIMDALL_TEST_PFX, $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $env:HEIMDALL_TEST_PFX_PASSWORD))
$cert.Dispose()
$rsa.Dispose()
`;
const certificate = spawnSync('pwsh', ['-NoProfile', '-NonInteractive', '-Command', certificateScript], {
  windowsHide: true, encoding: 'utf8', env: { ...process.env, HEIMDALL_TEST_PFX: pfx, HEIMDALL_TEST_PFX_PASSWORD: password }
});
assert.equal(certificate.status, 0, certificate.stderr);

const origin = 'https://127.0.0.1:7081';
const issuer = 'http://127.0.0.1:7082';
const { publicKey, privateKey } = crypto.generateKeyPairSync('rsa', { modulusLength: 2048 });
const jwk = { ...publicKey.export({ format: 'jwk' }), kid: 'local-test', use: 'sig', alg: 'RS256' };
const codes = new Map();
let exchanged = false;
let loggedOut = false;
let providerError;
const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
const escape = value => String(value).replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;');
const provider = http.createServer(async (request, response) => {
  try {
    const url = new URL(request.url, issuer);
    const json = value => { response.setHeader('Content-Type', 'application/json'); response.end(JSON.stringify(value)); };
    if (url.pathname === '/.well-known/openid-configuration') return json({
      issuer, authorization_endpoint: issuer + '/authorize', token_endpoint: issuer + '/token',
      jwks_uri: issuer + '/jwks', end_session_endpoint: issuer + '/logout',
      response_types_supported: ['code'], subject_types_supported: ['public'],
      id_token_signing_alg_values_supported: ['RS256'], token_endpoint_auth_methods_supported: ['client_secret_post', 'client_secret_basic']
    });
    if (url.pathname === '/jwks') return json({ keys: [jwk] });
    if (url.pathname === '/authorize') {
      assert.equal(url.searchParams.get('client_id'), 'functions-test-client');
      assert.equal(url.searchParams.get('redirect_uri'), origin + '/auth/callback');
      assert.equal(url.searchParams.get('code_challenge_method'), 'S256');
      const code = crypto.randomBytes(20).toString('base64url');
      codes.set(code, Object.fromEntries(url.searchParams));
      if (url.searchParams.get('response_mode') === 'form_post') {
        response.setHeader('Content-Type', 'text/html');
        response.end(`<form method="post" action="${origin}/auth/callback"><input name="code" value="${code}"><input name="state" value="${escape(url.searchParams.get('state'))}"></form><script>document.forms[0].submit()</script>`);
      } else {
        response.writeHead(302, { Location: origin + '/auth/callback?code=' + code + '&state=' + encodeURIComponent(url.searchParams.get('state')) }); response.end();
      }
      return;
    }
    if (url.pathname === '/token') {
      let body = ''; for await (const chunk of request) body += chunk;
      const form = new URLSearchParams(body);
      const authorization = codes.get(form.get('code'));
      assert.ok(authorization, 'Code must have been issued by this provider');
      codes.delete(form.get('code'));
      assert.equal(crypto.createHash('sha256').update(form.get('code_verifier')).digest('base64url'), authorization.code_challenge);
      exchanged = true;
      const now = Math.floor(Date.now() / 1000);
      const unsigned = encode({ alg: 'RS256', kid: jwk.kid, typ: 'JWT' }) + '.' + encode({ iss: issuer, aud: 'functions-test-client', sub: 'alice', name: 'Alice', iat: now, exp: now + 300, nonce: authorization.nonce });
      const idToken = unsigned + '.' + crypto.sign('RSA-SHA256', Buffer.from(unsigned), privateKey).toString('base64url');
      return json({ access_token: 'local-test-access-token', token_type: 'Bearer', expires_in: 300, id_token: idToken });
    }
    if (url.pathname === '/logout') {
      assert.ok(url.searchParams.get('id_token_hint'), 'Provider logout must receive the saved ID token');
      assert.equal(url.searchParams.get('post_logout_redirect_uri'), origin + '/auth/signed-out');
      loggedOut = true;
      response.writeHead(302, { Location: origin + '/auth/signed-out?state=' + encodeURIComponent(url.searchParams.get('state')) }); response.end(); return;
    }
    response.writeHead(404); response.end();
  } catch (error) { providerError = error; response.writeHead(500); response.end('Local test provider failure'); }
});

let worker;
let browser;
let output = '';
let page;
try {
  if (oidc) await new Promise(resolve => provider.listen(7082, '127.0.0.1', resolve));
  const executable = path.join(root, 'node_modules', 'azure-functions-core-tools', 'bin', process.platform === 'win32' ? 'func.exe' : 'func');
  fs.copyFileSync(path.join(project, 'host.json'), path.join(project, 'bin', 'Debug', 'net10.0', 'host.json'));
  worker = spawn(executable, ['start', '--script-root', path.join(project, 'bin', 'Debug', 'net10.0'), '--port', '7081', '--no-build', '--useHttps', '--cert', pfx, '--password', password], {
    cwd: project, windowsHide: true, env: { ...process.env, FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated', AZURE_FUNCTIONS_CORE_TOOLS_TELEMETRY_OPTOUT: '1',
      ...(oidc ? { Authentication__Oidc__Authority: issuer, Authentication__Oidc__ClientId: 'functions-test-client', Authentication__Oidc__ClientSecret: 'local-test-secret', Authentication__Oidc__RequireHttpsMetadata: 'false' } : {}) }
  });
  worker.stdout.on('data', data => { output = (output + data).slice(-25000); });
  worker.stderr.on('data', data => { output = (output + data).slice(-25000); });
  browser = await chromium.launch();
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  let ready = false;
  for (let i = 0; i < 120; i++) {
    if (worker.exitCode !== null) throw new Error('Worker exited during startup');
    try {
      const response = await context.request.get(origin + '/', { maxRedirects: 0, timeout: 1000 });
      if ([200, 302].includes(response.status())) { ready = true; break; }
    } catch {}
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  assert.ok(ready, 'Generated Functions app should start');
  if (oidc) {
    const csrf = await context.request.get(origin + '/__heimdall/v1/csrf', { maxRedirects: 0 });
    assert.equal(csrf.status(), 401, 'The generated protocol trigger must require sign-in');
  }
  page = await context.newPage();
  page.setDefaultTimeout(15000);
  await page.addInitScript(() => { window.e2eOpened = false; document.addEventListener('heimdall:sse-open', () => { window.e2eOpened = true; }); });
  await page.goto(origin + '/');
  await page.locator('#increment').waitFor();
  assert.equal((await context.request.get(origin + '/home', { maxRedirects: 0 })).status(), 200);
  const greeting = await context.request.get(origin + '/hello/Ada', { maxRedirects: 0 });
  assert.equal(greeting.status(), 200);
  assert.match(await greeting.text(), /Hello, Ada/);
  assert.equal((await context.request.get(origin + '/hello/123', { maxRedirects: 0 })).status(), 404);
  assert.equal((await context.request.get(origin + '/missing-page', { maxRedirects: 0 })).status(), 404);
  if (providerError) throw providerError;
  if (oidc) { assert.ok(exchanged); assert.match(await page.locator('main').textContent(), /Alice/); }
  await page.click('#increment');
  await page.waitForFunction(() => document.querySelector('#count').textContent === '1');
  await page.click('#increment');
  await page.waitForFunction(() => document.querySelector('#count').textContent === '2');
  await page.waitForFunction(() => window.e2eOpened);
  await page.click('#publish');
  await page.waitForFunction(() => document.querySelector('#live').textContent === 'Live from Functions');
  assert.equal((await context.request.get(origin + '/assets/site.css')).status(), 200);
  assert.equal((await context.request.get(origin + '/_content/HeimdallFramework.Web/heimdall-bundle.min.js')).status(), 200);
  console.log(`ok - generated ${oidc ? 'OIDC' : 'no-auth'} starter renders, increments, serves assets, and receives SSE`);
  if (oidc) {
    const rejected = await context.request.post(origin + '/account/sign-out', { maxRedirects: 0 });
    assert.equal(rejected.status(), 400);
    assert.equal((await context.request.get(origin + '/', { maxRedirects: 0 })).status(), 200);
    await page.click('form[action="/account/sign-out"] button');
    await page.waitForURL(origin + '/account/signed-out');
    assert.ok(loggedOut);
    assert.equal((await context.request.get(origin + '/', { maxRedirects: 0 })).status(), 302);
    console.log('ok - generated OIDC starter completes code/PKCE login and CSRF-protected provider logout over HTTPS');
  }
  if (providerError) throw providerError;
} catch (error) {
  console.error(error);
  if (providerError) console.error(providerError);
  if (page) console.error('Last browser path:', new URL(page.url()).pathname, (await page.locator('body').textContent().catch(() => '')).slice(0, 200));
  console.error(output.replaceAll(password, '[test password]'));
  process.exitCode = 1;
} finally {
  await browser?.close();
  if (worker?.pid && worker.exitCode === null) {
    if (process.platform === 'win32') spawnSync('taskkill', ['/pid', String(worker.pid), '/T', '/F'], { windowsHide: true, stdio: 'ignore' });
    else worker.kill();
  }
  if (oidc) await new Promise(resolve => provider.close(resolve));
  const allowed = path.resolve(root, '..', 'tmp', 'functions-starter-e2e') + path.sep;
  if (!scratch.startsWith(allowed)) throw new Error('Unexpected test scratch path');
  fs.rmSync(scratch, { recursive: true, force: true, maxRetries: 5 });
}
