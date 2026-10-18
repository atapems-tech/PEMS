// Minimal, dependency-free Chrome DevTools Protocol client (Node >= 22: global WebSocket + fetch). Works with the Chromium that
// Playwright installs (~/AppData/Local/ms-playwright/chromium-*/chrome-win64/chrome.exe) or any Chrome/Edge. No npm install needed.
import { spawn } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const sleep = ms => new Promise(r => setTimeout(r, ms));

export async function launchBrowser({ chrome, port, width = 1366, height = 768, locale, extraArgs = [] }) {
  const profile = mkdtempSync(join(tmpdir(), 'uiaudit-'));
  const proc = spawn(chrome, ['--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`, '--no-first-run', '--disable-gpu',
    '--hide-scrollbars=false', ...(locale ? [`--lang=${locale}`] : []), ...extraArgs, 'about:blank'], { stdio: 'ignore' });
  for (let i = 0; i < 60; i++) { try { await (await fetch(`http://127.0.0.1:${port}/json/version`)).json(); break; } catch { await sleep(250); } }
  const target = await (await fetch(`http://127.0.0.1:${port}/json/new?about:blank`, { method: 'PUT' })).json();
  const ws = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise(r => ws.addEventListener('open', r));
  let id = 0; const pending = new Map(); const listeners = [];
  ws.addEventListener('message', e => {
    const m = JSON.parse(e.data);
    if (m.id && pending.has(m.id)) { pending.get(m.id)(m); pending.delete(m.id); return; }
    for (const l of listeners) l(m);
  });
  const send = (method, params = {}) => new Promise(res => { const i = ++id; pending.set(i, res); ws.send(JSON.stringify({ id: i, method, params })); });
  const ev = async expr => {
    const r = await send('Runtime.evaluate', { expression: expr, returnByValue: true, awaitPromise: true });
    if (r.result?.exceptionDetails) throw new Error('eval failed: ' + (r.result.exceptionDetails.exception?.description || r.result.exceptionDetails.text));
    return r.result?.result?.value;
  };
  const events = { console: [], exceptions: [], failed: [], http: [] };
  listeners.push(m => {
    if (m.method === 'Runtime.consoleAPICalled' && (m.params.type === 'error' || m.params.type === 'assert'))
      events.console.push(m.params.args.map(a => a.value ?? a.description ?? '').join(' ').slice(0, 300));
    else if (m.method === 'Runtime.exceptionThrown') events.exceptions.push((m.params.exceptionDetails.exception?.description || m.params.exceptionDetails.text || '').split('\n')[0].slice(0, 300));
    else if (m.method === 'Log.entryAdded' && m.params.entry.level === 'error') events.console.push(`[${m.params.entry.source}] ${m.params.entry.text} ${m.params.entry.url ?? ''}`.slice(0, 300));
    else if (m.method === 'Network.loadingFailed' && !m.params.canceled) events.failed.push(`${m.params.errorText} ${m.params.type ?? ''}`);
    else if (m.method === 'Network.responseReceived' && m.params.response.status >= 400) events.http.push(`${m.params.response.status} ${m.params.response.url.replace(/^https?:\/\/[^/]+/, '')}`.slice(0, 200));
  });
  await send('Page.enable'); await send('Runtime.enable'); await send('Log.enable'); await send('Network.enable');
  const setViewport = (w, h, mobile = w < 600) => send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile });
  await setViewport(width, height);
  const settle = async (ms = 700) => { for (let i = 0; i < 60 && (await ev('document.readyState')) !== 'complete'; i++) await sleep(200); await sleep(ms); };
  const goto = async url => { await send('Page.navigate', { url }); await settle(); return new URL(await ev('location.href')); };
  // poll an expression until truthy (or timeout): robust against slow machines; never use fixed sleeps to judge UI state
  const waitFor = async (expr, ms = 3000) => { const end = Date.now() + ms; while (Date.now() < end) { if (await ev(expr)) return true; await sleep(100); } return !!(await ev(expr)); };
  const key = (k) => send('Input.dispatchKeyEvent', { type: 'keyDown', key: k, code: k }).then(() => send('Input.dispatchKeyEvent', { type: 'keyUp', key: k, code: k }));
  const shot = async () => (await send('Page.captureScreenshot', { format: 'png' })).result.data;
  const drain = () => { const e = { console: [...events.console], exceptions: [...events.exceptions], failed: [...events.failed], http: [...events.http] }; for (const k in events) events[k].length = 0; return e; };
  const setCookie = (name, value, url) => send('Network.setCookie', { name, value, url });
  const close = async () => { try { ws.close(); } catch { } proc.kill(); await sleep(300); try { rmSync(profile, { recursive: true, force: true }); } catch { } };
  return { send, ev, goto, settle, waitFor, key, shot, drain, setViewport, setCookie, close, sleep };
}
