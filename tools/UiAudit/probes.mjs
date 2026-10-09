// In-page probes (strings evaluated in the browser). They measure what the USER sees, not what the CSS claims:
// popups are judged with document.elementFromPoint (real hit-testing), so clipping, stacking and overlap are all caught the same way.

export const PAGE_METRICS = `(() => {
  const vis = e => { const r = e.getBoundingClientRect(); const s = getComputedStyle(e); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.display !== 'none'; };
  const text = e => (e.innerText || '').replace(/\\s+/g, ' ').trim();
  const h1s = [...document.querySelectorAll('h1')].filter(vis).map(text);
  const main = document.querySelector('main, .pems-content, .content-wrapper') || document.body;
  const dupHeading = h1s.length > 0 && [...main.querySelectorAll('h2,h3,h4,h5,h6')].filter(vis).some(h => text(h) === h1s[0]);
  const ids = {}; document.querySelectorAll('[id]').forEach(e => { ids[e.id] = (ids[e.id] || 0) + 1; });
  const dupIds = Object.entries(ids).filter(([, n]) => n > 1).map(([k]) => k);
  const inputs = [...document.querySelectorAll('input:not([type=hidden]):not([type=submit]):not([type=button]):not([type=checkbox]):not([type=radio]), select, textarea')].filter(vis);
  const unlabeled = inputs.filter(i => !(i.labels && i.labels.length) && !i.getAttribute('aria-label') && !i.getAttribute('aria-labelledby') && !i.title && !i.getAttribute('placeholder')).map(i => (i.name || i.id || i.tagName).slice(0, 40));
  const wide = [...document.querySelectorAll('body *')].filter(e => vis(e) && e.getBoundingClientRect().right > innerWidth + 1 && !e.closest('.table-responsive, .overflow-auto, .pems-sidebar, .modal, .offcanvas, [style*="overflow"]'))
    .slice(0, 3).map(e => e.tagName.toLowerCase() + (e.className && typeof e.className === 'string' ? '.' + e.className.split(' ').filter(Boolean).slice(0, 2).join('.') : ''));
  return {
    title: document.title, h1: h1s, dupHeading, dupIds: dupIds.slice(0, 6), unlabeled: unlabeled.slice(0, 6), unlabeledCount: unlabeled.length,
    pageHScroll: document.documentElement.scrollWidth > innerWidth + 1, wideElements: wide,
    dropdownToggles: [...document.querySelectorAll('.pems-content [data-bs-toggle="dropdown"]')].filter(vis).length,
    shellDropdownToggles: [...document.querySelectorAll('[data-bs-toggle="dropdown"]')].filter(e => vis(e) && !e.closest('.pems-content')).length,
    modalTriggers: [...document.querySelectorAll('[data-bs-toggle="modal"]')].filter(vis).length,
    offcanvasTriggers: [...document.querySelectorAll('[data-bs-toggle="offcanvas"]')].filter(vis).length,
    nativeSelects: [...document.querySelectorAll('select')].filter(vis).length,
    customCombos: [...document.querySelectorAll('[role=combobox], .pc-lookup, [data-pems-combo]')].filter(vis).length,
    tablesInResponsive: [...document.querySelectorAll('.table-responsive table')].length,
    path: location.pathname
  };
})()`;

// Sampling grid over an element's rect; returns {points, hit, obscured:[{x,y,topmost}], offscreen}
export const SAMPLE_VISIBLE = (selector) => `(() => {
  const el = document.querySelector(${JSON.stringify(selector)});
  if (!el) return { found: false };
  const r = el.getBoundingClientRect();
  const pts = []; for (const fx of [0.08, 0.5, 0.92]) for (const fy of [0.08, 0.5, 0.92]) pts.push([r.left + r.width * fx, r.top + r.height * fy]);
  let onScreen = 0, hit = 0; const obscured = [];
  for (const [x, y] of pts) {
    if (x < 0 || y < 0 || x > innerWidth || y > innerHeight) continue;
    onScreen++;
    const top = document.elementFromPoint(x, y);
    if (top && (el === top || el.contains(top))) hit++;
    else obscured.push({ x: Math.round(x), y: Math.round(y), topmost: top ? top.tagName.toLowerCase() + (top.className && typeof top.className === 'string' ? '.' + top.className.split(' ').filter(Boolean).slice(0, 2).join('.') : '') : 'null' });
  }
  return { found: true, rect: { l: Math.round(r.left), t: Math.round(r.top), w: Math.round(r.width), h: Math.round(r.height) }, vw: innerWidth, vh: innerHeight,
           samples: pts.length, onScreen, hit, offscreenRatio: +(1 - onScreen / pts.length).toFixed(2), obscured: obscured.slice(0, 3),
           clippingAncestors: (() => { const out = []; if (getComputedStyle(el).position === 'fixed') return out;   /* fixed elements are not clipped by ancestors */ let p = el.parentElement; while (p && p !== document.body) { const s = getComputedStyle(p); const clip = ['overflow','overflowX','overflowY'].some(k => ['hidden','auto','scroll','clip'].includes(s[k])); if (clip) { const pr = p.getBoundingClientRect(); if (r.bottom > pr.bottom + 1 || r.right > pr.right + 1 || r.left < pr.left - 1 || r.top < pr.top - 1) out.push(p.tagName.toLowerCase() + (p.className && typeof p.className === 'string' ? '.' + p.className.split(' ').filter(Boolean).slice(0, 2).join('.') : '')); } p = p.parentElement; } return out.slice(0, 3); })() };
})()`;

export const CLICK_NTH = (selector, n) => `(() => { const els = [...document.querySelectorAll(${JSON.stringify(selector)})].filter(e => { const r = e.getBoundingClientRect(); const s = getComputedStyle(e); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.display !== 'none'; });
  const e = els[${n}]; if (!e) return false; e.scrollIntoView({ block: 'center' }); e.click(); return true; })()`;

export const ACTIVE_INSIDE = (selector) => `(() => { const m = document.querySelector(${JSON.stringify(selector)}); return !!m && m.contains(document.activeElement); })()`;
export const EXISTS = (selector) => `!!document.querySelector(${JSON.stringify(selector)})`;

// All distinct same-origin GET links in the sidebar (the app's own navigation = the page inventory for the signed-in role)
export const SIDEBAR_LINKS = `(() => [...new Set([...document.querySelectorAll('#pemsSidebar a[href]')].map(a => a.getAttribute('href')).filter(h => h && h.startsWith('/') && !/logout|signout/i.test(h)))])()`;
