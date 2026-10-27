// List pages with the correspondence look (SCRUM-408): search opens over the toolbar title, and an optional KPI row
// that is hidden by default and remembered per browser. Markup contract (one per page):
//   [data-lp-toolbar]  [data-lp-search-open]  form[data-lp-search] (hidden) with input + [data-lp-search-close]
//   [data-lp-kpi-toggle] (aria-controls = id of the KPI row, data-storage-key = localStorage key)
(function () {
    'use strict';

    function initSearch(toolbar) {
        var openBtn = toolbar.querySelector('[data-lp-search-open]');
        var form = toolbar.querySelector('form[data-lp-search]');
        if (!openBtn || !form) return;
        var input = form.querySelector('input[type="search"], input[type="text"]');
        var closeBtn = form.querySelector('[data-lp-search-close]');

        function open() {
            form.hidden = false;
            toolbar.classList.add('lp-searching');
            openBtn.setAttribute('aria-expanded', 'true');
            if (input) { input.focus(); input.select(); }
        }
        function close(returnFocus) {
            form.hidden = true;
            toolbar.classList.remove('lp-searching');
            openBtn.setAttribute('aria-expanded', 'false');
            if (returnFocus) openBtn.focus();
        }

        openBtn.addEventListener('click', function () { form.hidden ? open() : close(true); });
        if (closeBtn) closeBtn.addEventListener('click', function () { close(true); });
        if (input) {
            input.addEventListener('keydown', function (e) {
                if (e.key === 'Escape') { e.stopPropagation(); close(true); }
            });
            // an active search term keeps the field open so the user sees what is filtered
            if (input.value) open();
        }
    }

    function initKpis(btn) {
        var row = document.getElementById(btn.getAttribute('aria-controls'));
        if (!row) return;
        var key = btn.getAttribute('data-storage-key');

        function apply(show) {
            row.hidden = !show;
            btn.setAttribute('aria-expanded', show ? 'true' : 'false');
            btn.classList.toggle('on', show);
        }

        var stored = null;
        try { stored = key ? window.localStorage.getItem(key) : null; } catch (e) { /* storage blocked: default stays hidden */ }
        apply(stored === '1');

        btn.addEventListener('click', function () {
            var show = row.hidden;
            apply(show);
            try { if (key) window.localStorage.setItem(key, show ? '1' : '0'); } catch (e) { /* not remembered */ }
        });
    }

    function boot() {
        document.querySelectorAll('[data-lp-toolbar]').forEach(initSearch);
        document.querySelectorAll('[data-lp-kpi-toggle]').forEach(initKpis);
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
    else boot();
})();
