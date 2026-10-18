// PEMS UI audit crawler. Read-only: only GETs sidebar pages and opens (never submits) dropdowns, modals and offcanvases.
//   node tools/UiAudit/audit.mjs
// Env: BASE (http://localhost:5199)  CHROME (path)  OUT (result dir)  USERS (comma list)  CREDS (file: "user<TAB>password" lines)
//      VIEWPORTS (e.g. 1366x768,600x800,375x800)  ONLY (comma list of paths)  LOCALE (en|fa, default en)  MAX_PAGES (per user)  PORT (debugging port)
// Use ONLY against a clone/test database with test accounts. Passwords are read from CREDS and never printed.
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { homedir } from 'node:os';
import { launchBrowser } from './cdp.mjs';
import { PAGE_METRICS, SAMPLE_VISIBLE, CLICK_NTH, ACTIVE_INSIDE, EXISTS, SIDEBAR_LINKS } from './probes.mjs';

const BASE = process.env.BASE || 'http://localhost:5199';
const CHROME = process.env.CHROME; if (!CHROME) throw new Error('set CHROME to a Chromium/Chrome executable');
const OUT = process.env.OUT || join(process.cwd(), 'ui-audit-results'); mkdirSync(OUT, { recursive: true });
const CREDS = process.env.CREDS || join(homedir(), '.pems-test', 'pcat-test-accounts-PEMSDB_PcatClone.txt');
const LOCALE = process.env.LOCALE || 'en';
const MAX_PAGES = +(process.env.MAX_PAGES || 200);
const PORT = +(process.env.PORT || 9440);
const ONLY = (process.env.ONLY || '').split(',').map(x => x.trim()).filter(Boolean);   // optional: only audit these paths (e.g. /Asset,/Instrument)
const wanted = (process.env.USERS || 'test-pcat-dm').split(',').map(s => s.trim()).filter(Boolean);

const creds = {};
for (const l of readFileSync(CREDS, 'utf8').split(/\r?\n/)) { const [u, p] = l.replace(/^﻿/, '').split('\t'); if (u && p && !u.startsWith('#')) creds[u] = p; }
const sleep = ms => new Promise(r => setTimeout(r, ms));
const findings = [];
const add = (user, vp, path, kind, detail) => findings.push({ user, vp, path, kind, detail });

async function popupChecks(b, user, vp, path, m, limit) {
  // --- Bootstrap dropdowns in PAGE CONTENT: first toggle, plus the LAST one (worst case: bottom row / near the footer)
  const dd = '.pems-content [data-bs-toggle="dropdown"]';
  const idxs = m.dropdownToggles ? [...new Set([0, 1, m.dropdownToggles - 1].filter(i => i < m.dropdownToggles))].slice(0, limit) : [];
  for (const i of idxs) {
    if (!(await b.ev(CLICK_NTH(dd, i)))) break;
    await sleep(350);
    const s = await b.ev(SAMPLE_VISIBLE('.dropdown-menu.show'));
    if (s.found) {
      if (s.obscured.length || (s.clippingAncestors.length && s.hit < s.onScreen)) add(user, vp, path, 'dropdown-clipped-or-obscured', { index: i, of: m.dropdownToggles, rect: s.rect, vw: s.vw, vh: s.vh, hit: `${s.hit}/${s.onScreen}`, obscured: s.obscured, clippedBy: s.clippingAncestors });
      else if (s.offscreenRatio > 0.3) add(user, vp, path, 'dropdown-offscreen', { index: i, rect: s.rect, vw: s.vw, vh: s.vh, offscreenRatio: s.offscreenRatio });
    }
    await b.key('Escape'); await sleep(150);
    if (await b.ev(EXISTS('.dropdown-menu.show'))) { add(user, vp, path, 'dropdown-escape-does-not-close', { index: i }); await b.ev(`document.body.click()`); }
  }
  // --- Bootstrap modals + offcanvas (open only)
  for (const [kind, trig, openSel, contentSel] of [['modal', '[data-bs-toggle="modal"]:not([type=submit])', '.modal.show', '.modal.show .modal-content'], ['offcanvas', '[data-bs-toggle="offcanvas"]:not([type=submit])', '.offcanvas.show', '.offcanvas.show']]) {
    const count = kind === 'modal' ? m.modalTriggers : m.offcanvasTriggers;
    for (let i = 0; i < Math.min(count, Math.max(1, limit - 1)); i++) {
      if (!(await b.ev(CLICK_NTH(trig, i)))) break;
      if (!(await b.waitFor(EXISTS(openSel), 3000))) { add(user, vp, path, `${kind}-did-not-open`, { index: i }); continue; }
      await b.waitFor(ACTIVE_INSIDE(openSel), 1500);            // Bootstrap moves focus after the fade transition
      const s = await b.ev(SAMPLE_VISIBLE(contentSel));
      if (s.found && (s.obscured.length || s.hit < s.onScreen)) add(user, vp, path, `${kind}-obscured`, { index: i, hit: `${s.hit}/${s.onScreen}`, obscured: s.obscured });
      if (!(await b.ev(ACTIVE_INSIDE(openSel)))) add(user, vp, path, `${kind}-focus-not-moved-inside`, { index: i });
      await b.key('Escape');
      if (!(await b.waitFor(`!${EXISTS(openSel)}`, 2000))) {
        add(user, vp, path, `${kind}-escape-does-not-close`, { index: i, note: 'may be intentional (static backdrop)' });
        await b.ev(`(document.querySelector('${openSel} [data-bs-dismiss]')||{click(){}}).click()`); await b.waitFor(`!${EXISTS(openSel)}`, 2000);
      }
    }
  }
}

async function auditUser(user) {
  const b = await launchBrowser({ chrome: CHROME, port: PORT + wanted.indexOf(user), locale: LOCALE });
  try {
    if (LOCALE === 'fa') await b.setCookie('.AspNetCore.Culture', 'c%3Dfa%7Cuic%3Dfa', BASE);
    await b.goto(BASE + '/Account/Login');
    await b.ev(`(()=>{document.querySelector('#UserName').value=${JSON.stringify(user)};document.querySelector('#Password').value=${JSON.stringify(creds[user])};document.querySelector('#loginSubmitBtn').click()})()`);
    await b.settle(1200);
    const landed = new URL(await b.ev('location.href'));
    if (landed.pathname.startsWith('/Account/')) { add(user, '-', landed.pathname, 'login-failed', {}); return { user, pages: 0 }; }
    const links = (await b.ev(SIDEBAR_LINKS)).slice(0, MAX_PAGES);
    const pages = ONLY.length ? ONLY.map(href => ({ href })) : [{ href: '/' }, ...links.filter(h => h !== '/').map(href => ({ href }))];
    console.log(`[${user}] ${pages.length} pages from the role's sidebar`);
    const visited = [];
    const VIEWPORTS = (process.env.VIEWPORTS || '1366x768,375x800').split(',').map((v, i) => { const [w, h] = v.trim().split('x').map(Number); return { name: w >= 1200 ? 'desktop' : w < 600 ? 'mobile' : `tablet${w}`, w, h, limit: w >= 1200 ? 4 : 2 }; });
    for (const vp of VIEWPORTS) {
      await b.setViewport(vp.w, vp.h);
      for (const p of pages) {
        b.drain();
        let u; try { u = await b.goto(BASE + p.href); } catch (e) { add(user, vp.name, p.href, 'navigation-error', String(e).slice(0, 160)); continue; }
        if (u.pathname.startsWith('/Account/')) { if (vp.w >= 1200) visited.push({ href: p.href, result: 'denied' }); continue; }
        const m = await b.ev(PAGE_METRICS);
        const ev = b.drain();
        if (vp.name === 'desktop') visited.push({ href: p.href, path: u.pathname, h1: m.h1, dropdowns: m.dropdownToggles, modals: m.modalTriggers, selects: m.nativeSelects, combos: m.customCombos });
        if (ev.exceptions.length) add(user, vp.name, u.pathname, 'js-exception', ev.exceptions.slice(0, 3));
        if (ev.console.length) add(user, vp.name, u.pathname, 'console-error', [...new Set(ev.console)].slice(0, 3));
        if (ev.failed.length) add(user, vp.name, u.pathname, 'request-failed', [...new Set(ev.failed)].slice(0, 3));
        if (ev.http.length) add(user, vp.name, u.pathname, 'http-4xx-5xx-subresource', [...new Set(ev.http)].slice(0, 4));
        if (m.pageHScroll) add(user, vp.name, u.pathname, 'page-horizontal-scroll', m.wideElements);
        if (vp.w >= 1200) {
          if (m.h1.length !== 1) add(user, vp.name, u.pathname, 'h1-count', m.h1);
          if (m.dupHeading) add(user, vp.name, u.pathname, 'duplicate-heading', m.h1);
          if (m.dupIds.length) add(user, vp.name, u.pathname, 'duplicate-ids', m.dupIds);
          if (m.unlabeledCount) add(user, vp.name, u.pathname, 'unlabeled-inputs', { count: m.unlabeledCount, examples: m.unlabeled });
        }
        await popupChecks(b, user, vp.name, u.pathname, m, vp.limit);
        if (p.href === '/' && m.shellDropdownToggles) {
          for (let i = 0; i < m.shellDropdownToggles; i++) {
            if (!(await b.ev(CLICK_NTH('[data-bs-toggle="dropdown"]:not(.pems-content *)', i)))) break;
            await sleep(350);
            const s = await b.ev(SAMPLE_VISIBLE('.dropdown-menu.show'));
            if (s.found && (s.obscured.length || s.offscreenRatio > 0.3)) add(user, vp.name, '(topbar)', 'shell-dropdown-obscured-or-offscreen', { index: i, rect: s.rect, vw: s.vw, hit: `${s.hit}/${s.onScreen}`, obscured: s.obscured, offscreenRatio: s.offscreenRatio });
            await b.key('Escape'); await sleep(150);
          }
        }
        const after = b.drain();
        if (after.exceptions.length) add(user, vp.name, u.pathname, 'js-exception-after-interaction', after.exceptions.slice(0, 3));
      }
    }
    return { user, pages: pages.length, visited };
  } finally { await b.close(); }
}

const summaries = [];
for (const u of wanted) { if (!creds[u]) { console.log(`no credential for ${u}`); continue; } summaries.push(await auditUser(u)); }
writeFileSync(join(OUT, `findings-${LOCALE}.json`), JSON.stringify({ base: BASE, locale: LOCALE, at: new Date().toISOString(), summaries, findings }, null, 1));
const byKind = {}; for (const f of findings) byKind[f.kind] = (byKind[f.kind] || 0) + 1;
console.log('\n=== findings by kind ===\n' + Object.entries(byKind).sort((a, b) => b[1] - a[1]).map(([k, n]) => `${String(n).padStart(4)}  ${k}`).join('\n'));
console.log(`\nresults: ${join(OUT, `findings-${LOCALE}.json`)}`);
process.exit(0);
