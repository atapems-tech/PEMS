// PEMS.UI skeleton (SCRUM-337): the grey placeholder a script-filled panel shows while its data is on the way.
// The same markup as the <pems-skeleton> tag helper (PemsSkeletonMarkup). Vanilla, offline, no inline handlers.
//   PemsSkeleton.html(kind, opts)        -> markup string; kind: text | title | card | kpi | table | form
//                                           opts: { lines: 3, rows: 5, cols: 4, label: "Loading..." }
//   PemsSkeleton.show(element, kind, opts) puts it into element (replaces its content)
// The screen-reader label comes from opts.label or <body data-pems-skeleton-label="...">, set by the layout in the
// current language.
(function () {
    'use strict';

    var KINDS = ['text', 'title', 'card', 'kpi', 'table', 'form'];

    function clamp(n, lo, hi, d) { n = parseInt(n, 10); if (isNaN(n)) n = d; return Math.max(lo, Math.min(hi, n)); }
    function esc(s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;'); }
    function bar(extra) { return '<span class="pems-skeleton__line' + (extra || '') + '" aria-hidden="true"></span>'; }

    function defaultLabel() {
        var b = document.body;
        return b ? (b.getAttribute('data-pems-skeleton-label') || '') : '';
    }

    function html(kind, opts) {
        opts = opts || {};
        var k = KINDS.indexOf(kind) >= 0 ? kind : 'text';
        var lines = clamp(opts.lines, 1, 20, 3), rows = clamp(opts.rows, 1, 30, 5), cols = clamp(opts.cols, 1, 12, 4);
        var label = opts.label != null ? opts.label : defaultLabel();
        var out = '<div class="pems-skeleton pems-skeleton--' + k + '" role="status" aria-busy="true" aria-live="polite">';
        if (label) out += '<span class="visually-hidden">' + esc(label) + '</span>';
        var i, r, c;
        switch (k) {
            case 'title':
                out += bar(' pems-skeleton__line--title');
                break;
            case 'card':
                out += bar(' pems-skeleton__line--title');
                for (i = 0; i < 3; i++) out += bar(i === 2 ? ' pems-skeleton__line--short' : '');
                break;
            case 'kpi':
                out += bar(' pems-skeleton__line--square') + '<span class="pems-skeleton__stack">' + bar(' pems-skeleton__line--value') + bar(' pems-skeleton__line--short') + '</span>';
                break;
            case 'table':
                for (r = 0; r < rows; r++) {
                    out += '<span class="pems-skeleton__row">';
                    for (c = 0; c < cols; c++) out += bar();
                    out += '</span>';
                }
                break;
            case 'form':
                for (r = 0; r < rows; r++) out += bar(' pems-skeleton__line--label') + bar(' pems-skeleton__line--field');
                break;
            default:
                for (i = 0; i < lines; i++) out += bar(i === lines - 1 && lines > 1 ? ' pems-skeleton__line--short' : '');
        }
        return out + '</div>';
    }

    function show(element, kind, opts) {
        if (!element) return;
        element.innerHTML = html(kind, opts);
    }

    window.PemsSkeleton = { html: html, show: show, kinds: KINDS.slice() };
})();
