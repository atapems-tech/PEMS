// PEMS.UI chat panel (SCRUM-778, component "chat-panel"). <div data-pems-chat data-config="{...}"> is written by the component; this script reads the history of the user from a data source
// (GET cfg.history -> { items }), posts a question to a stored action (POST cfg.ask { <askField>: text }, JSON, antiforgery token) and shows the history again, because the action stores the answer.
// Clear posts to cfg.clear. Enter sends, Shift+Enter is a new line. Messages are text, never markup. All markup is a registered script template (chat-*). Logical properties only: RTL and LTR.
(function () {
    'use strict';

    function tpl(name, values) { return window.PemsTemplate.fill(window.PemsTemplate.clone(name), values || {}); }
    function slot(el, name) { return el.matches('[data-slot="' + name + '"]') ? el : el.querySelector('[data-slot="' + name + '"]'); }
    function token() { var el = document.querySelector('input[name="__RequestVerificationToken"]'); return el ? el.value : ''; }
    function post(url, body) {
        return fetch(url, { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'Accept': 'application/json', 'RequestVerificationToken': token() }, body: JSON.stringify(body) })
            .then(function (r) { if (!r.ok) throw new Error(String(r.status)); });
    }

    function init(root) {
        if (root.getAttribute('data-pems-chat-ready') !== null) return;
        root.setAttribute('data-pems-chat-ready', '');
        var cfg = JSON.parse(root.getAttribute('data-config')), T = cfg.texts, F = cfg.fields;
        var shell = tpl('chat-root', { label: T.label, send: T.send, clear: T.clear });
        var log = slot(shell, 'log'), form = slot(shell, 'form'), input = slot(shell, 'text'), sendBtn = slot(shell, 'send'), clearBtn = slot(shell, 'clear'), status = slot(shell, 'status');
        root.appendChild(shell);
        log.style.maxBlockSize = cfg.height + 'px';
        input.setAttribute('placeholder', T.placeholder);
        input.setAttribute('maxlength', String(cfg.maxLength));
        if (!cfg.clear) clearBtn.hidden = true;
        var busy = false, generation = 0;

        function say(text, bad) { status.textContent = text || ''; status.classList.toggle('is-error', !!bad); }
        function isUser(role) { return String(role || '').toLowerCase() === String(cfg.userRole || '').toLowerCase(); }
        function bubble(who, text, user, time, pending) {
            var m = tpl('chat-msg', { who: who, text: text, time: time || '' });
            m.classList.add(user ? 'is-user' : 'is-assistant');
            if (pending) { m.classList.add('is-pending'); m.setAttribute('aria-busy', 'true'); }
            if (!time) slot(m, 'time').hidden = true;
            return m;
        }
        function stamp(v) { return v == null ? '' : String(v).replace('T', ' ').slice(0, 16); }
        function show(items) {
            log.textContent = '';
            var rows = (items || []).slice();
            rows.sort(function (a, b) { return String(a[F.time] || '') < String(b[F.time] || '') ? -1 : (String(a[F.time] || '') > String(b[F.time] || '') ? 1 : 0); });
            if (!rows.length) { say(T.empty); return; }
            say('');
            rows.forEach(function (it) {
                var user = isUser(it[F.role]);
                log.appendChild(bubble(user ? T.you : T.assistant, String(it[F.content] == null ? '' : it[F.content]), user, stamp(it[F.time]), false));
            });
            log.scrollTop = log.scrollHeight;
        }
        function load() {
            var mine = ++generation; say(T.loading);
            return fetch(cfg.history + (cfg.history.indexOf('?') < 0 ? '?' : '&') + 'page=1&pageSize=500', { headers: { 'Accept': 'application/json' }, credentials: 'same-origin' })
                .then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); })
                .then(function (data) { if (mine === generation) show(data.items || []); })
                .catch(function () { if (mine === generation) say(T.error, true); });
        }
        function setBusy(v) { busy = v; sendBtn.disabled = v; clearBtn.disabled = v; input.readOnly = v; root.classList.toggle('is-busy', v); }

        form.addEventListener('submit', function (e) {
            e.preventDefault();
            var text = input.value.trim();
            if (busy || !text) { if (!text) input.focus(); return; }
            setBusy(true); say('');
            var mine = bubble(T.you, text, true, '', false), wait = bubble(T.assistant, T.thinking, false, '', true);
            if (log.querySelector('.pems-chat__empty')) log.textContent = '';
            log.appendChild(mine); log.appendChild(wait); log.scrollTop = log.scrollHeight;
            var body = {}; body[cfg.askField] = text;
            post(cfg.ask, body).then(function () { input.value = ''; return load(); })
                .catch(function () { wait.remove(); mine.remove(); say(T.error, true); })
                .then(function () { setBusy(false); input.focus(); });
        });
        input.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) { e.preventDefault(); if (form.requestSubmit) form.requestSubmit(); else sendBtn.click(); }
        });
        clearBtn.addEventListener('click', function () {
            if (busy || !cfg.clear) return;
            setBusy(true);
            post(cfg.clear, {}).then(load).catch(function () { say(T.error, true); }).then(function () { setBusy(false); });
        });

        load();
        root.pemsChat = { reload: load };
    }

    function scan(scope) { (scope || document).querySelectorAll('[data-pems-chat]').forEach(init); }
    function boot() {
        scan(document);
        new MutationObserver(function (list) { list.forEach(function (m) { m.addedNodes.forEach(function (n) { if (n.nodeType === 1) { if (n.matches && n.matches('[data-pems-chat]')) init(n); scan(n); } }); }); }).observe(document.body, { childList: true, subtree: true });
    }
    window.PemsChat = { init: init, scan: scan };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot); else boot();
})();
