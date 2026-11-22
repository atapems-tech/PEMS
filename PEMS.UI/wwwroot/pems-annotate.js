// PEMS.UI file preview annotations (SCRUM-775): pins and notes on the pages of a PDF or on an image shown by <pems-file-preview>.
// <div class="pems-file-preview" data-pems-annotate='{...}'> is written by the file-preview component when it gets annotation urls; this script adds the page bar, a pin layer and the note form.
//   listUrl    GET  -> { items: [{ id, page, x, y, note }] }   (a data source of a stored definition, /d/{key}?f.fileId=...; the server checks the record permission)
//   saveUrl    POST { id?, fileId, page, x, y, note }  (JSON, antiforgery token; a stored action, /a/{action}, with the record permission and guards) -> { id } optional
//   deleteUrl  POST { id }                             (a stored action)
// Without saveUrl the pins are read only. x and y are percent from the left and the top of the page, so a pin keeps its place in RTL and LTR (the layer mirrors the value, the page does not).
// Keyboard: the annotate toggle turns the layer into a focusable area, arrow keys move the crosshair, Enter puts a pin and opens its note; pins are buttons. All markup is a registered script template (fp-*).
(function () {
    'use strict';
    if (window.PemsAnnotate) return;

    function tpl(name, values) { return window.PemsTemplate.fill(window.PemsTemplate.clone(name), values || {}); }
    function slot(el, name) { return el.matches('[data-slot="' + name + '"]') ? el : el.querySelector('[data-slot="' + name + '"]'); }
    function token() { var el = document.querySelector('input[name="__RequestVerificationToken"]'); return el ? el.value : ''; }
    function clamp(v) { return Math.max(0, Math.min(100, v)); }
    function post(url, body) {
        return fetch(url, { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'Accept': 'application/json', 'RequestVerificationToken': token() }, body: JSON.stringify(body) })
            .then(function (r) { if (!r.ok) throw new Error(String(r.status)); return r.text(); })
            .then(function (t) { try { return t ? JSON.parse(t) : {}; } catch (e) { return {}; } });
    }
    function load(url) {
        return fetch(url, { headers: { 'Accept': 'application/json' }, credentials: 'same-origin' })
            .then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); })
            .then(function (data) { var list = data && data.items ? data.items : data; return Array.isArray(list) ? list : []; });
    }

    function init(root) {
        if (root.getAttribute('data-pems-annotate-ready') !== null) return;
        var frame = root.querySelector('iframe');
        if (!frame) return;
        root.setAttribute('data-pems-annotate-ready', '');
        var cfg = JSON.parse(root.getAttribute('data-pems-annotate')), T = cfg.texts || {};
        var canEdit = !!cfg.saveUrl, base = frame.getAttribute('src') || '';
        var rtl = getComputedStyle(root).direction === 'rtl';
        var page = 1, items = [], annotating = false, ghost = { x: 50, y: 50 }, open = null;

        var bar = tpl('fp-bar', { prev: T.previous, next: T.next, pageLabel: T.page, annotate: T.annotate });
        var stage = tpl('fp-stage'), layer = slot(tpl('fp-layer'), 'layer');
        var pageInput = slot(bar, 'page'), toggle = slot(bar, 'annotate'), status = slot(bar, 'status');
        root.insertBefore(bar, frame);
        root.insertBefore(stage, frame);
        stage.appendChild(frame);
        stage.appendChild(layer);
        if (!canEdit) toggle.hidden = true;
        function say(text) { status.textContent = text || ''; }
        function place(el, x, y) { el.style.setProperty('--x', String(rtl ? 100 - x : x)); el.style.setProperty('--y', String(y)); }

        function goto(n) {
            page = Math.max(1, n); pageInput.value = String(page);
            frame.setAttribute('src', base.split('#')[0] + '#page=' + page);
            close(); draw();
        }
        function draw() {
            layer.querySelectorAll('.pems-file-preview__pin').forEach(function (p) { p.remove(); });
            items.filter(function (a) { return (+a.page || 1) === page; }).forEach(function (a, i) { layer.appendChild(pinFor(a, i + 1)); });
        }
        function pinFor(a, n) {
            var pin = tpl('fp-pin', { pin: String(n) });
            place(pin, +a.x || 0, +a.y || 0);
            pin.setAttribute('title', String(a.note || ''));
            pin.setAttribute('aria-label', (T.pin || '') + ' ' + n + (a.note ? ': ' + a.note : ''));
            pin.addEventListener('click', function (e) { e.stopPropagation(); edit(a, pin); });
            return pin;
        }
        function close() { if (open) { open.remove(); open = null; } }
        function edit(a, anchor) {
            close();
            var form = tpl('fp-note', { label: T.note, save: T.save, remove: T.remove, cancel: T.cancel });
            var text = slot(form, 'text'), del = slot(form, 'remove'), save = slot(form, 'save');
            text.value = String(a.note || '');
            text.readOnly = !canEdit; save.hidden = !canEdit;
            del.hidden = !(canEdit && a.id != null && cfg.deleteUrl);
            place(form, +a.x || 0, +a.y || 0);
            form.addEventListener('submit', function (e) {
                e.preventDefault();
                var note = text.value.trim(); if (!note) { text.focus(); return; }
                var body = { id: a.id == null ? null : a.id, fileId: cfg.fileId, page: page, x: +a.x, y: +a.y, note: note };
                save.disabled = true;
                post(cfg.saveUrl, body).then(function (res) {
                    a.note = note; a.page = page;
                    if (a.id == null && res && res.id != null) a.id = res.id;
                    if (items.indexOf(a) < 0) items.push(a);
                    close(); draw(); say('');
                    if (a.id == null) reload();   // the action answered without the new id: read the list again
                }).catch(function () { save.disabled = false; say(T.error); });
            });
            del.addEventListener('click', function () {
                post(cfg.deleteUrl, { id: a.id }).then(function () { items = items.filter(function (x) { return x !== a; }); close(); draw(); say(''); })
                    .catch(function () { say(T.error); });
            });
            function leave() { close(); draw(); if (anchor) anchor.focus(); }
            slot(form, 'cancel').addEventListener('click', leave);
            form.addEventListener('keydown', function (e) { if (e.key === 'Escape') { e.stopPropagation(); leave(); } });
            layer.appendChild(form); open = form; text.focus();
        }
        function addAt(x, y) { edit({ id: null, page: page, x: Math.round(clamp(x) * 10) / 10, y: Math.round(clamp(y) * 10) / 10, note: '' }, null); }

        toggle.addEventListener('click', function () {
            annotating = !annotating;
            toggle.setAttribute('aria-pressed', annotating ? 'true' : 'false');
            root.classList.toggle('is-annotating', annotating);
            if (annotating) { layer.setAttribute('tabindex', '0'); layer.setAttribute('aria-label', T.annotate || ''); layer.focus(); }
            else { layer.removeAttribute('tabindex'); layer.removeAttribute('aria-label'); close(); }
        });
        layer.addEventListener('click', function (e) {
            if (!annotating || e.target !== layer) return;
            var r = layer.getBoundingClientRect(); if (!r.width || !r.height) return;
            addAt((e.clientX - r.left) / r.width * 100, (e.clientY - r.top) / r.height * 100);
        });
        layer.addEventListener('keydown', function (e) {
            if (!annotating || e.target !== layer) return;
            var step = e.shiftKey ? 1 : 5, dx = { ArrowLeft: -step, ArrowRight: step }[e.key] || 0, dy = { ArrowUp: -step, ArrowDown: step }[e.key] || 0;
            if (dx || dy) { e.preventDefault(); ghost.x = clamp(ghost.x + dx); ghost.y = clamp(ghost.y + dy); say(Math.round(ghost.x) + '%, ' + Math.round(ghost.y) + '%'); }
            else if (e.key === 'Enter') { e.preventDefault(); addAt(ghost.x, ghost.y); }
        });
        slot(bar, 'prev').addEventListener('click', function () { goto(page - 1); });
        slot(bar, 'next').addEventListener('click', function () { goto(page + 1); });
        pageInput.addEventListener('change', function () { goto(parseInt(pageInput.value, 10) || 1); });
        pageInput.value = '1';

        function reload() {
            if (!cfg.listUrl) return;
            say(T.loading);
            load(cfg.listUrl).then(function (list) { items = list; say(''); draw(); }).catch(function () { say(T.error); });
        }
        reload();
        root.pemsAnnotate = { reload: reload, page: function () { return page; } };
    }

    function scan(scope) { (scope || document).querySelectorAll('[data-pems-annotate]').forEach(init); }
    function boot() {
        scan(document);
        new MutationObserver(function (list) { list.forEach(function (m) { m.addedNodes.forEach(function (n) { if (n.nodeType === 1) { if (n.matches && n.matches('[data-pems-annotate]')) init(n); scan(n); } }); }); }).observe(document.body, { childList: true, subtree: true });
    }
    window.PemsAnnotate = { init: init, scan: scan };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot); else boot();
})();
